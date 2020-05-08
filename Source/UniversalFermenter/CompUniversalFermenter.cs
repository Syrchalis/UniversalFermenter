// Notes:
//   * parent.Map is null when the building (parent) is minified (uninstalled).

using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace UniversalFermenter
{
    public class CompUniversalFermenter : ThingComp
    {
        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            UF_Utility.comps.Remove(this);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            UF_Utility.comps.Add(this);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // Add a dev button for finishing the fermenting			
            if (Prefs.DevMode && !Empty)
            {
                yield return UF_Utility.DevFinish;
            }

            // Dev button for printing speed factors (speed factors: sun, rain, snow, wind, roofed)
            if (Prefs.DevMode)
            {
                yield return UF_Utility.DispSpeeds;
            }
            // Default buttons
            foreach (Gizmo c in base.CompGetGizmosExtra())
            {
                yield return c;
            }
            // Switching products button (no button if only 1 resource)
            if (ProcessListSize > 1)
            {
                yield return UF_Utility.productGizmos[CurrentProcess];
            }
        }

        private int ingredientCount;
        private float progressInt;
        private Material barFilledCachedMat;
        public int nextResourceInd;
        public int currentResourceInd;
        private List<string> ingredientLabels = new List<string>();
        public List<ThingDef> fermenterIngredients = new List<ThingDef>();

        protected float ruinedPercent;

        public string defaultTexPath;
        public CompRefuelable refuelComp;
        public CompPowerTrader powerTradeComp;
        public CompFlickable flickComp;

        // Properties

        public CompProperties_UniversalFermenter Props
        {
            get { return (CompProperties_UniversalFermenter)props; }
        }

        public int ProcessListSize
        {
            get
            {
                return Props.processes.Count;
            }
        }

        public UF_Process CurrentProcess
        {
            get
            {
                return Props.processes[currentResourceInd];
            }
        }

        public UF_Process NextProcess
        {
            get
            {
                return Props.processes[nextResourceInd];
            }
        }

        public bool Ruined
        {
            get
            {
                return ruinedPercent >= 1f;
            }
        }

        public string SummaryAddedIngredients
        {
            get
            {
                int substractLength;
                int maxSummaryLength;
                int lineLength = 60;
                string summary = "";
                for (int i = 0; i < ingredientLabels.Count; i++)
                {
                    if (i == 0)
                        summary += ingredientLabels[i];
                    else
                        summary += ", " + ingredientLabels[i];
                }

                substractLength = ("Contains " + CurrentProcess.maxCapacity.ToString() + "/" + CurrentProcess.maxCapacity.ToString() + " ").Length;
                maxSummaryLength = lineLength - substractLength;
                return UF_Utility.VowelTrim(summary, maxSummaryLength);
            }
        }

        public string SummaryNextIngredientFilter
        {
            get
            {
                return UF_Utility.IngredientFilterSummary(NextProcess.ingredientFilter);
            }
        }

        public float Progress
        {
            get
            {
                return progressInt;
            }
            set
            {
                if (value == progressInt)
                {
                    return;
                }
                progressInt = value;
                barFilledCachedMat = null;
            }
        }

        private Material BarFilledMat
        {
            get
            {
                if (barFilledCachedMat == null)
                {
                    barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(Static_Bar.ZeroProgressColor, Static_Bar.FermentedColor, Progress), false);
                }
                return barFilledCachedMat;
            }
        }

        public bool Empty
        {
            get
            {
                return ingredientCount <= 0;
            }
        }

        public bool Fermented
        {
            get
            {
                return !Empty && Progress >= 1f;
            }
        }

        public int SpaceLeftForIngredient
        {
            get
            {
                if (Fermented)
                {
                    return 0;
                }
                return CurrentProcess.maxCapacity - ingredientCount;
            }
        }

        private void NextResource()
        {
            nextResourceInd++;
            if (nextResourceInd >= ProcessListSize)
                nextResourceInd = 0;
            if (Empty)
            {
                currentResourceInd = nextResourceInd;
            }
        }



        private float CurrentTemperatureFactor
        {
            get
            {
                if (!CurrentProcess.usesTemperature)
                {
                    return 1f;
                }
                float ambientTemperature = parent.AmbientTemperature;
                // Temperature out of a safe range
                if (ambientTemperature < CurrentProcess.temperatureSafe.min)
                {
                    return CurrentProcess.speedBelowSafe;
                }
                else if (ambientTemperature > CurrentProcess.temperatureSafe.max)
                {
                    return CurrentProcess.speedAboveSafe;
                }
                // Temperature out of an ideal range but still within a safe range
                if (ambientTemperature < CurrentProcess.temperatureIdeal.min)
                {
                    return GenMath.LerpDouble(CurrentProcess.temperatureSafe.min, CurrentProcess.temperatureIdeal.min, CurrentProcess.speedBelowSafe, 1f, ambientTemperature);
                }
                else if (ambientTemperature > CurrentProcess.temperatureIdeal.max)
                {
                    return GenMath.LerpDouble(CurrentProcess.temperatureIdeal.max, CurrentProcess.temperatureSafe.max, 1f, CurrentProcess.speedAboveSafe, ambientTemperature);
                }
                // Temperature within an ideal range
                return 1f;
            }
        }

        public float CurrentSunFactor
        {
            get
            {
                if (parent.Map == null)
                {
                    return 0f;
                }
                if (CurrentProcess.sunFactor.Span == 0)
                {
                    return 1f;
                }
                float skyGlow = parent.Map.skyManager.CurSkyGlow * (1 - RoofCoverage);
                return GenMath.LerpDouble(Static_Weather.SunGlowRange.TrueMin, Static_Weather.SunGlowRange.TrueMax,
                                          CurrentProcess.sunFactor.min, CurrentProcess.sunFactor.max,
                                          skyGlow);
            }
        }

        public float CurrentRainFactor
        {
            get
            {
                if (parent.Map == null)
                {
                    return 0f;
                }
                if (CurrentProcess.rainFactor.Span == 0)
                {
                    return 1f;
                }
                // When snowing, the game also increases RainRate.
                // Therefore, non-zero SnowRate puts RainRespect to a state as if it was not raining.
                if (parent.Map.weatherManager.SnowRate != 0)
                {
                    return CurrentProcess.rainFactor.min;
                }
                float rainRate = parent.Map.weatherManager.RainRate * (1 - RoofCoverage);
                return GenMath.LerpDouble(Static_Weather.RainRateRange.TrueMin, Static_Weather.RainRateRange.TrueMax,
                                          CurrentProcess.rainFactor.min, CurrentProcess.rainFactor.max,
                                          rainRate);
            }
        }

        public float CurrentSnowFactor
        {
            get
            {
                if (parent.Map == null)
                {
                    return 0f;
                }
                if (CurrentProcess.snowFactor.Span == 0)
                {
                    return 1f;
                }
                float snowRate = parent.Map.weatherManager.SnowRate * (1 - RoofCoverage);
                return GenMath.LerpDouble(Static_Weather.SnowRateRange.TrueMin, Static_Weather.SnowRateRange.TrueMax,
                                          CurrentProcess.snowFactor.min, CurrentProcess.snowFactor.max,
                                          snowRate);
            }
        }

        public float CurrentWindFactor
        {
            get
            {
                if (parent.Map == null)
                {
                    return 0f;
                }
                if (CurrentProcess.windFactor.Span == 0)
                {
                    return 1f;
                }
                if (RoofCoverage != 0)
                {
                    return CurrentProcess.windFactor.min;
                }
                return GenMath.LerpDouble(Static_Weather.WindSpeedRange.TrueMin, Static_Weather.WindSpeedRange.TrueMax,
                                          CurrentProcess.windFactor.min, CurrentProcess.windFactor.max,
                                          parent.Map.windManager.WindSpeed);
            }
        }

        public float RoofCoverage  // How much of the building is under a roof
        {
            get
            {
                if (parent.Map == null)
                {
                    return 0f;
                }
                int allTiles = 0;
                int roofedTiles = 0;
                foreach (IntVec3 current in parent.OccupiedRect())
                {
                    allTiles++;
                    if (parent.Map.roofGrid.Roofed(current))
                    {
                        roofedTiles++;
                    }
                }
                return (float)roofedTiles / (float)allTiles;
            }
        }

        public float CurrentSpeedFactor
        {
            get
            {
                return Mathf.Max(CurrentTemperatureFactor * CurrentSunFactor * CurrentRainFactor * CurrentSnowFactor * CurrentWindFactor, 0f);
            }
        }

        private float CurrentProgressPerTick
        {
            get
            {
                //sanity check if someone entered the time in ticks instead of days
                if (CurrentProcess.processDays > 1000)
                {
                    return 1f / CurrentProcess.processDays * CurrentSpeedFactor;
                }
                return 1f / (CurrentProcess.processDays * 60000f) * CurrentSpeedFactor;
            }
        }

        private int EstimatedTicksLeft
        {
            get
            {
                if (CurrentProgressPerTick == 0)
                {
                    return -1;
                }
                else
                {
                    return Mathf.Max(Mathf.RoundToInt((1f - Progress) / CurrentProgressPerTick), 0);
                }
            }
        }

        public bool Fueled
        {
            get
            {
                return (refuelComp == null || refuelComp.HasFuel);
            }
        }

        public bool Powered
        {
            get
            {
                return (powerTradeComp == null || powerTradeComp.PowerOn);
            }
        }

        public bool FlickedOn
        {
            get
            {
                return (flickComp == null || flickComp.SwitchIsOn);
            }
        }

        // Methods

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            refuelComp = parent.GetComp<CompRefuelable>();
            powerTradeComp = parent.GetComp<CompPowerTrader>();
            flickComp = parent.GetComp<CompFlickable>();
            defaultTexPath = parent.def.graphicData.texPath;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref ruinedPercent, "ruinedPercent", 0f, false);
            Scribe_Values.Look(ref ingredientCount, "UF_UniversalFermenter_IngredientCount", 0);
            Scribe_Values.Look(ref progressInt, "UF_UniversalFermenter_Progress", 0f);
            Scribe_Values.Look(ref nextResourceInd, "UF_nextResourceInd", 0);
            Scribe_Values.Look(ref currentResourceInd, "UF_currentResourceInd", 0);
            Scribe_Values.Look(ref defaultTexPath, "defaultTexPath");
            Scribe_Collections.Look(ref ingredientLabels, "UF_ingredientLabels");
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!Empty)
            {
                Vector3 drawPos = parent.DrawPos;
                drawPos.y += 0.0483870953f;
                drawPos.z += 0.25f;
                GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
                {
                    center = drawPos,
                    size = Static_Bar.Size,
                    fillPercent = ingredientCount / (float)CurrentProcess.maxCapacity,
                    filledMat = BarFilledMat,
                    unfilledMat = Static_Bar.UnfilledMat,
                    margin = 0.1f,
                    rotation = Rot4.North
                });
            }
            if (CurrentProcess != null && UF_Settings.showProductIconGlobal && Props.showProductIcon && ProcessListSize > 1)
            {
                Vector3 drawPos = parent.DrawPos;
                drawPos.y += 0.02f;
                drawPos.z += 0.05f;
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(0.75f, 1f, 0.75f));
                Graphics.DrawMesh(MeshPool.plane10, matrix, UF_Utility.productMaterials[CurrentProcess], 0);
            }
        }
        
        public bool AddIngredient(Thing ingredient)
        {
            if (!CurrentProcess.ingredientFilter.Allows(ingredient))
            {
                return false;
            }
            if (!ingredientLabels.Contains(ingredient.def.label))
            {
                ingredientLabels.Add(ingredient.def.label);
            }
            CompIngredients comp = ingredient.TryGetComp<CompIngredients>();
            if (comp != null)
            {
                fermenterIngredients.AddRange(comp.ingredients);
            }
            AddIngredient(ingredient.stackCount);
            ingredient.Destroy(DestroyMode.Vanish);
            return true;
        }

        public void AddIngredient(int count)
        {
            ruinedPercent = 0f;
            if (Fermented)
            {
                Log.Warning("Universal Fermenter:: Tried to add ingredient to a fermenter full of product. Colonists should take the product first.");
                return;
            }
            int num = Mathf.Min(count, CurrentProcess.maxCapacity - ingredientCount);
            if (num <= 0)
            {
                return;
            }
            Progress = GenMath.WeightedAverage(0f, num, Progress, ingredientCount);
            if (Empty)
            {
                GraphicChange(false);
            }
            ingredientCount += num;
        }

        public Thing TakeOutProduct()
        {
            if (!Fermented)
            {
                Log.Warning("Universal Fermenter:: Tried to get product but it's not yet fermented.");
                return null;
            }
            Thing thing = ThingMaker.MakeThing(CurrentProcess.thingDef, null);
            CompIngredients comp = thing.TryGetComp<CompIngredients>();
            if (comp != null && !fermenterIngredients.NullOrEmpty())
            {
                comp.ingredients.AddRange(fermenterIngredients);
                fermenterIngredients.Clear();
            }
            thing.stackCount = Mathf.RoundToInt(ingredientCount * CurrentProcess.efficiency);
            Reset();
            return thing;
        }

        public void Reset()
        {
            ingredientCount = 0;
            //ruinedPercent = 0f;			
            Progress = 0f;
            currentResourceInd = nextResourceInd;
            ingredientLabels.Clear();
            GraphicChange(true);
        }

        public void GraphicChange(bool toEmpty)
        {
            string suffix = CurrentProcess.graphicSuffix;
            if (suffix != null)
            {
                string texPath = defaultTexPath;
                if (!toEmpty)
                {
                    texPath += CurrentProcess.graphicSuffix;
                }
                TexReloader.Reload(parent, texPath);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            DoTicks(1);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            DoTicks(250);
        }

        private void DoTicks(int ticks)
        {
            if (!Empty && Fueled && Powered && FlickedOn)
            {
                Progress = Mathf.Min(Progress + (float)ticks * CurrentProgressPerTick, 1f);
            }
            if (!Ruined)
            {
                if (!Empty)
                {
                    float ambientTemperature = parent.AmbientTemperature;
                    if (ambientTemperature > CurrentProcess.temperatureSafe.max)
                    {
                        ruinedPercent += (ambientTemperature - CurrentProcess.temperatureSafe.max) * (CurrentProcess.ruinedPerDegreePerHour / 250000f) * (float)ticks;
                    }
                    else if (ambientTemperature < CurrentProcess.temperatureSafe.min)
                    {
                        ruinedPercent -= (ambientTemperature - CurrentProcess.temperatureSafe.min) * (CurrentProcess.ruinedPerDegreePerHour / 250000f) * (float)ticks;
                    }
                }
                if (ruinedPercent >= 1f)
                {
                    ruinedPercent = 1f;
                    parent.BroadcastCompSignal("RuinedByTemperature");
                    Reset();
                }
                else if (ruinedPercent < 0f)
                {
                    ruinedPercent = 0f;
                }
            }
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            float t = (float)count / (float)(parent.stackCount + count);
            CompUniversalFermenter comp = ((ThingWithComps)otherStack).GetComp<CompUniversalFermenter>();
            ruinedPercent = Mathf.Lerp(ruinedPercent, comp.ruinedPercent, t);
        }

        public override bool AllowStackWith(Thing other)
        {
            CompUniversalFermenter comp = ((ThingWithComps)other).GetComp<CompUniversalFermenter>();
            return Ruined == comp.Ruined;
        }

        public override void PostSplitOff(Thing piece)
        {
            CompUniversalFermenter comp = ((ThingWithComps)piece).GetComp<CompUniversalFermenter>();
            comp.ruinedPercent = ruinedPercent;
        }

        // Inspector string eats max. 5 lines - there is room for one more
        public override string CompInspectStringExtra()
        {
            StringBuilder stringBuilder = new StringBuilder();

            // 1st line: "Temperature: xx C (Overheating/Freezing/Ideal/Safe)" or "Ruined by temperature"
            stringBuilder.AppendLine(StatusInfo());

            // 2nd line: "Contains xx/xx ingredient (product)"
            if (!Empty && !Ruined)
            {
                if (Fermented)
                {
                    stringBuilder.AppendLine("UF_ContainsProduct".Translate(ingredientCount, CurrentProcess.maxCapacity, CurrentProcess.thingDef.label));
                }
                else
                {
                    stringBuilder.AppendLine("UF_ContainsIngredient".Translate(ingredientCount, CurrentProcess.maxCapacity, SummaryAddedIngredients));
                }
            }

            // 3rd line: "Finished" or "Progress: xx %" 
            // 4th line: "Non-ideal temp, sun, ... . Ferm. speed: xx %"
            if (!Empty)
            {
                if (Fermented)
                {
                    stringBuilder.AppendLine("UF_Finished".Translate());
                }
                else if (parent.Map != null) // parent.Map is null when minified
                {
                    stringBuilder.AppendLine("UF_Progress".Translate(Progress.ToStringPercent(),TimeLeft()));
                    if (CurrentSpeedFactor != 1f)
                    {
                        // Should be max. 59 chars in the English translation
                        if (CurrentSpeedFactor < 1f)
                        {
                            stringBuilder.Append("UF_NonIdealInfluences".Translate(WhatsWrong())).Append(" ").AppendLine("UF_NonIdealSpeedFactor".Translate(CurrentSpeedFactor.ToStringPercent()));
                        }
                        else
                        {
                            stringBuilder.AppendLine("UF_NonIdealSpeedFactor".Translate(CurrentSpeedFactor.ToStringPercent()));
                        }
                    }
                }
            }

            // 5th line: "Ideal/safe temperature range"
            stringBuilder.AppendLine(string.Concat(new string[]
            {
                "UF_IdealSafeProductionTemperature".Translate(),
                ": ",
                CurrentProcess.temperatureIdeal.min.ToStringTemperature("F0"),
                "~",
                CurrentProcess.temperatureIdeal.max.ToStringTemperature("F0"),
                " (",
                CurrentProcess.temperatureSafe.min.ToStringTemperature("F0"),
                "~",
                CurrentProcess.temperatureSafe.max.ToStringTemperature("F0"),
                ")"
            }));

            return stringBuilder.ToString().TrimEndNewlines();
        }

        public string TimeLeft()
        {
            if (EstimatedTicksLeft >= 0)
            {
                return EstimatedTicksLeft.ToStringTicksToPeriod() + " left";
            }
            else
            {
                return "stopped";
            }
        }

        public string WhatsWrong()
        {
            if (CurrentSpeedFactor < 1f)
            {
                List<string> wrong = new List<string>();
                if (CurrentTemperatureFactor < 1f)
                {
                    wrong.Add("UF_WeatherTemperature".Translate());
                }
                if (CurrentSunFactor < 1f)
                {
                    wrong.Add("UF_WeatherSunshine".Translate());
                }
                if (CurrentRainFactor < 1f)
                {
                    wrong.Add("UF_WeatherRain".Translate());
                }
                if (CurrentSnowFactor < 1f)
                {
                    wrong.Add("UF_WeatherSnow".Translate());
                }
                if (CurrentWindFactor < 1f)
                {
                    wrong.Add("UF_WeatherWind".Translate());
                }
                return string.Join(", ", wrong.ToArray());
            }
            else
            {
                return "nothing";
            }
        }

        public string StatusInfo()
        {
            if (Ruined)
            {
                return "RuinedByTemperature".Translate();
            }

            float ambientTemperature = parent.AmbientTemperature;
            string str = null;
            string tempStr = "Temperature".Translate() + ": " + ambientTemperature.ToStringTemperature("F0");

            if (!Empty)
            {
                if (CurrentProcess.temperatureSafe.Includes(ambientTemperature))
                {
                    if (CurrentProcess.temperatureIdeal.Includes(ambientTemperature))
                    {
                        str = "UF_Ideal".Translate();
                    }
                    else
                    {
                        str = "UF_Safe".Translate();
                    }
                }
                else
                {
                    if (ruinedPercent > 0f)
                    {
                        if (ambientTemperature < CurrentProcess.temperatureSafe.min)
                        {
                            str = "Freezing".Translate();
                        }
                        else
                        {
                            str = "Overheating".Translate();
                        }
                        str = str + " " + ruinedPercent.ToStringPercent();
                    }
                }
            }

            if (str == null)
            {
                return tempStr;
            }
            else
            {
                return tempStr + " (" + str + ")";
            }
        }
    }
}
