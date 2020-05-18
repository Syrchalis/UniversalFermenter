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
        public int progressTicks;
        public float ruinedPercent;
        private Material barFilledCachedMat;
        public int currentProcessIndex;
        public int queuedProcessIndex;
        public QualityCategory targetQuality = QualityCategory.Normal;

        private int ingredientCount;
        private List<string> ingredientLabels = new List<string>();
        public List<ThingDef> inputIngredients = new List<ThingDef>();
        public bool graphicChangeQueued = false;

        public CompRefuelable refuelComp;
        public CompPowerTrader powerTradeComp;
        public CompFlickable flickComp;

        //----------------------------------------------------------------------------------------------------
        // Properties
        public CompProperties_UniversalFermenter Props => (CompProperties_UniversalFermenter)props;

        public bool Ruined => ruinedPercent >= 1f;
        public bool Empty => ingredientCount <= 0;
        public bool Finished => !Empty && ProgressPercent >= 1f;
        public int SpaceLeftForIngredient => Finished ? 0 : CurrentProcess.maxCapacity - ingredientCount;
        public int ProgressTicks
        {
            get => progressTicks;
            set
            {
                if (value == progressTicks)
                {
                    return;
                }
                progressTicks = value;
                barFilledCachedMat = null;
            }
        }
        public float ProgressDays => (float)ProgressTicks / GenDate.TicksPerDay;
        public float ProgressPercent
        {
            get
            {
                if (CurrentProcess.usesQuality)
                {
                    return ProgressDays / DaysToReachTargetQuality;
                }
                return ProgressDays / CurrentProcess.processDays;
            }
        }
        public UF_Process CurrentProcess
        {
            get => Props.processes[currentProcessIndex];
            set
            {
                if (Props.processes.Contains(value))
                {
                    if (Empty)
                    {
                        currentProcessIndex = Props.processes.IndexOf(value);
                        if (!value.usesQuality)
                        {
                            TargetQuality = QualityCategory.Normal;
                        }
                    }
                    queuedProcessIndex = Props.processes.IndexOf(value);
                }
            }
        }

        public int EstimatedTicksLeft
        {
            get
            {
                if (CurrentSpeedFactor <= 0)
                {
                    return -1;
                }
                else if (CurrentProcess.usesQuality)
                {
                    return Mathf.Max(Mathf.RoundToInt((DaysToReachTargetQuality * GenDate.TicksPerDay) - ProgressTicks), 0);
                }
                else
                {
                    return Mathf.Max(Mathf.RoundToInt((CurrentProcess.processDays * GenDate.TicksPerDay) - ProgressTicks), 0);
                }
            }
        }

        public QualityCategory TargetQuality
        {
            get => targetQuality;
            set
            {
                if (value == targetQuality)
                {
                    return;
                }
                targetQuality = value;
                barFilledCachedMat = null;
            }
        }

        public QualityCategory CurrentQuality
        {
            get
            {
                if (ProgressDays < CurrentProcess.qualityDays.poor)
                    return QualityCategory.Awful;
                if (ProgressDays < CurrentProcess.qualityDays.normal)
                    return QualityCategory.Poor;
                if (ProgressDays < CurrentProcess.qualityDays.good)
                    return QualityCategory.Normal;
                if (ProgressDays < CurrentProcess.qualityDays.excellent)
                    return QualityCategory.Good;
                if (ProgressDays < CurrentProcess.qualityDays.masterwork)
                    return QualityCategory.Excellent;
                if (ProgressDays < CurrentProcess.qualityDays.legendary)
                    return QualityCategory.Masterwork;
                if (ProgressDays >= CurrentProcess.qualityDays.legendary)
                    return QualityCategory.Legendary;
                return QualityCategory.Normal;
            }
        }

        public float DaysToReachTargetQuality
        {
            get
            {
                switch (targetQuality)
                {
                    case QualityCategory.Awful:
                        return CurrentProcess.qualityDays.awful;
                    case QualityCategory.Poor:
                        return CurrentProcess.qualityDays.poor;
                    case QualityCategory.Normal:
                        return CurrentProcess.qualityDays.normal;
                    case QualityCategory.Good:
                        return CurrentProcess.qualityDays.good;
                    case QualityCategory.Excellent:
                        return CurrentProcess.qualityDays.excellent;
                    case QualityCategory.Masterwork:
                        return CurrentProcess.qualityDays.masterwork;
                    case QualityCategory.Legendary:
                        return CurrentProcess.qualityDays.legendary;
                    default:
                        return CurrentProcess.qualityDays.normal;
                }
            }
        }

        private Material BarFilledMat
        {
            get
            {
                if (barFilledCachedMat == null)
                {
                    barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(Static_Bar.ZeroProgressColor, Static_Bar.FermentedColor, ProgressPercent), false);
                }
                return barFilledCachedMat;
            }
        }

        public float CurrentSpeedFactor
        {
            get
            {
                return Mathf.Max(CurrentTemperatureFactor * CurrentSunFactor * CurrentRainFactor * CurrentSnowFactor * CurrentWindFactor, 0f);
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
        
        public string SummaryAddedIngredients
        {
            get
            {
                string summary = "";
                if (ingredientLabels.Count > 0)
                {
                    for (int i = 0; i < ingredientLabels.Count; i++)
                    {
                        if (i == 0)
                        {
                            summary += ingredientLabels[i];
                        }
                        else
                        {
                            summary += ", " + ingredientLabels[i];
                        }
                    }
                }
                else
                {
                    summary += CurrentProcess.ingredientFilter.Summary;
                }
                int substractLength;
                int maxSummaryLength;
                int lineLength = 60;
                substractLength = ("Contains " + CurrentProcess.maxCapacity.ToString() + "/" + CurrentProcess.maxCapacity.ToString() + " ").Length;
                maxSummaryLength = lineLength - substractLength;
                return UF_Utility.VowelTrim(summary, maxSummaryLength);
            }
        }

        public bool Fueled => refuelComp == null || refuelComp.HasFuel;
        public bool Powered => powerTradeComp == null || powerTradeComp.PowerOn;
        public bool FlickedOn => flickComp == null || flickComp.SwitchIsOn;

        //----------------------------------------------------------------------------------------------------
        // Overrides

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            refuelComp = parent.GetComp<CompRefuelable>();
            powerTradeComp = parent.GetComp<CompPowerTrader>();
            flickComp = parent.GetComp<CompFlickable>();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            UF_Utility.comps.Add(this);
            if (!Empty)
            {
                graphicChangeQueued = true;
            }
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            UF_Utility.comps.Remove(this);
        }
        
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref ruinedPercent, "ruinedPercent", 0f);
            Scribe_Values.Look(ref ingredientCount, "UF_UniversalFermenter_IngredientCount", 0);
            Scribe_Values.Look(ref progressTicks, "UF_progressTicks", 0);
            Scribe_Values.Look(ref currentProcessIndex, "UF_currentResourceInd", 0);
            Scribe_Values.Look(ref queuedProcessIndex, "UF_queuedProcessIndex", 0);
            Scribe_Values.Look(ref targetQuality, "targetQuality", QualityCategory.Normal);
            Scribe_Collections.Look(ref ingredientLabels, "UF_ingredientLabels");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            //Dev options			
            if (Prefs.DevMode)
            {
                yield return UF_Utility.DebugGizmo();
            }
            //Default buttons
            foreach (Gizmo c in base.CompGetGizmosExtra())
            {
                yield return c;
            }
            //Switching products button (no button if only 1 resource)
            if (Props.processes.Count > 1)
            {
                yield return UF_Utility.processGizmos[CurrentProcess];
            }
            if (CurrentProcess.usesQuality)
            {
                yield return UF_Utility.qualityGizmos[TargetQuality];
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!Empty)
            {
                if (graphicChangeQueued)
                {
                    GraphicChange(false);
                    graphicChangeQueued = false;
                }
                bool showCurrentQuality = CurrentProcess.usesQuality && UF_Settings.showCurrentQualityIcon;
                Vector3 drawPos = parent.DrawPos;
                drawPos.x += Props.barOffset.x - (showCurrentQuality ? 0.1f : 0f);
                drawPos.y += 0.05f;
                drawPos.z += Props.barOffset.y;
                GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
                {
                    center = drawPos,
                    size = Static_Bar.Size * Props.barScale,
                    fillPercent = ingredientCount / (float)CurrentProcess.maxCapacity,
                    filledMat = BarFilledMat,
                    unfilledMat = Static_Bar.UnfilledMat,
                    margin = 0.1f,
                    rotation = Rot4.North
                });
                if (showCurrentQuality) // show small icon for current quality over bar
                {
                    drawPos.y += 0.02f;
                    drawPos.x += 0.45f * Props.barScale.x;
                    Matrix4x4 matrix2 = default(Matrix4x4);
                    matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.2f * Props.barScale.x, 1f, 0.2f * Props.barScale.y));
                    Graphics.DrawMesh(MeshPool.plane10, matrix2, UF_Utility.qualityMaterials[CurrentQuality], 0);
                }
            }
            if (CurrentProcess != null && UF_Settings.showProcessIconGlobal && Props.showProductIcon)
            {
                Vector3 drawPos = parent.DrawPos;
                float sizeX = UF_Settings.processIconSize * Props.productIconSize.x;
                float sizeZ = UF_Settings.processIconSize * Props.productIconSize.y;
                if (Props.processes.Count == 1 && CurrentProcess.usesQuality) // show larger, centered quality icon if object has only one process
                {
                    drawPos.y += 0.02f;
                    drawPos.z += 0.05f;
                    Matrix4x4 matrix = default(Matrix4x4);
                    matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(0.6f * sizeX, 1f, 0.6f * sizeZ));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, UF_Utility.qualityMaterials[TargetQuality], 0);
                }
                else if (Props.processes.Count > 1) // show process icon if object has more than one process
                {
                    drawPos.y += 0.02f;
                    drawPos.z += 0.05f;
                    Matrix4x4 matrix = default(Matrix4x4);
                    matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(sizeX, 1f, sizeZ));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, UF_Utility.processMaterials[CurrentProcess], 0);
                    if (CurrentProcess.usesQuality && UF_Settings.showTargetQualityIcon) // show small offset quality icon if object also uses quality
                    {
                        drawPos.y += 0.01f;
                        drawPos.x += 0.25f * sizeX;
                        drawPos.z -= 0.35f * sizeZ;
                        Matrix4x4 matrix2 = default(Matrix4x4);
                        matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.4f * sizeX, 1f, 0.4f * sizeZ));
                        Graphics.DrawMesh(MeshPool.plane10, matrix2, UF_Utility.qualityMaterials[TargetQuality], 0);
                    }
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
            if (CurrentProcess.usesTemperature)
            {
                stringBuilder.AppendLine(StatusInfo());
            }

            // 2nd line: "Contains xx/xx ingredient (product)"
            if (!Ruined)
            {
                if (CurrentProcess.usesQuality && ProgressDays >= CurrentProcess.qualityDays.awful)
                {
                    stringBuilder.AppendLine("UF_ContainsProduct".Translate(ingredientCount, CurrentProcess.maxCapacity, CurrentProcess.thingDef.label) + " (" + CurrentQuality.GetLabel().ToLower() + ")");
                }
                else if (Finished)
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
                if (Finished)
                {
                    stringBuilder.AppendLine("UF_Finished".Translate());
                }
                else if (parent.Map != null) // parent.Map is null when minified
                {
                    stringBuilder.AppendLine("UF_Progress".Translate(ProgressPercent.ToStringPercent(), TimeLeft()));
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
            if (CurrentProcess.usesTemperature)
            {
                stringBuilder.AppendLine(string.Concat(new string[]
                {
                    "UF_IdealSafeProductionTemperature".Translate(),": ",
                    CurrentProcess.temperatureIdeal.min.ToStringTemperature("F0"),"~",
                    CurrentProcess.temperatureIdeal.max.ToStringTemperature("F0"), " (",
                    CurrentProcess.temperatureSafe.min.ToStringTemperature("F0"), "~",
                    CurrentProcess.temperatureSafe.max.ToStringTemperature("F0"), ")"
                }));
            }

            return stringBuilder.ToString().TrimEndNewlines();
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

        //----------------------------------------------------------------------------------------------------
        // Functional Methods

        public void DoTicks(int ticks)
        {
            if (!Empty && Fueled && Powered && FlickedOn)
            {
                ProgressTicks += Mathf.RoundToInt(ticks * CurrentSpeedFactor);
            }
            if (!Ruined)
            {
                if (!Empty)
                {
                    // 2500 ticks per hour, 100 percent = divide by 250000
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

        public bool AddIngredient(Thing ingredient)
        {
            if (ingredient == null || !CurrentProcess.ingredientFilter.Allows(ingredient))
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
                inputIngredients.AddRange(comp.ingredients);
            }
            AddIngredient(ingredient.stackCount);
            ingredient.Destroy(DestroyMode.Vanish);
            return true;
        }

        public void AddIngredient(int count)
        {
            ruinedPercent = 0f;
            if (Finished)
            {
                Log.Warning("Universal Fermenter:: Tried to add ingredient to a fermenter full of product. Colonists should take the product first.");
                return;
            }
            int num = Mathf.Min(count, CurrentProcess.maxCapacity - ingredientCount);
            if (num <= 0)
            {
                return;
            }
            ProgressTicks = Mathf.RoundToInt(GenMath.WeightedAverage(0f, num, ProgressTicks, ingredientCount));
            if (Empty)
            {
                GraphicChange(false);
            }
            ingredientCount += num;
        }

        public Thing TakeOutProduct()
        {
            if (!Finished && !CurrentProcess.usesQuality)
            {
                Log.Warning("Universal Fermenter: Tried to get product but it's not yet fermented.");
                return null;
            }
            else if (CurrentProcess.usesQuality && CurrentQuality < TargetQuality)
            {
                Log.Warning("Universal Fermenter: Tried to get product but it has not reached target quality.");
                return null;
            }
            Thing thing = ThingMaker.MakeThing(CurrentProcess.thingDef, null);
            CompIngredients compIngredients = thing.TryGetComp<CompIngredients>();
            if (compIngredients != null && !inputIngredients.NullOrEmpty())
            {
                compIngredients.ingredients.AddRange(inputIngredients);
            }
            if (CurrentProcess.usesQuality)
            {
                CompQuality compQuality = thing.TryGetComp<CompQuality>();
                if (compQuality != null)
                {
                    compQuality.SetQuality(CurrentQuality, ArtGenerationContext.Colony);
                }
            }
            thing.stackCount = Mathf.RoundToInt(ingredientCount * CurrentProcess.efficiency);
            Reset();
            return thing;
        }

        public void Reset()
        {
            ingredientCount = 0;	
            ProgressTicks = 0;
            inputIngredients.Clear();
            ingredientLabels.Clear();
            GraphicChange(true);
            CurrentProcess = Props.processes[queuedProcessIndex];
        }

        public void GraphicChange(bool toEmpty)
        {
            string suffix = CurrentProcess.graphicSuffix;
            if (suffix != null)
            {
                string texPath = parent.def.graphicData.texPath;
                if (!toEmpty)
                {
                    texPath += CurrentProcess.graphicSuffix;
                }
                TexReloader.Reload(parent, texPath);
            }
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
