using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

#nullable enable
namespace UniversalFermenter
{
    public class UF_Progress : IExposable
    {
        /// <summary>If the speed is below this percentage, then the process is considered to be running slowly.</summary>
        public static readonly float SlowAtSpeedFactor = 0.75f;

        private readonly Cacheable<float> currentSpeedFactor;

        private readonly CompUniversalFermenter fermenter;

        /// <summary>Index of the process that this progress is for.</summary>
        private int processIndex;

        /// <summary>Material for the progress bar being filled.</summary>
        [Unsaved]
        private Material? progressColorMaterial;

        /// <summary>Number of ticks this progress has elapsed since being inserted.</summary>
        public long progressTicks;

        /// <summary>Percentage that this process is ruined.</summary>
        public float ruinedPercent;

        /// <summary>The Things stored in the fermenter that are fermenting.</summary>
        public List<Thing> storedThings = new List<Thing>();

        /// <summary>The target quality for this progress. Different progresses can have different qualities.</summary>
        private QualityCategory targetQuality;

        public UF_Progress(CompUniversalFermenter parent)
        {
            fermenter = parent;
            ProgressPercentFlooredString = new Cacheable<string>(() => $"{Mathf.Floor(ProgressPercent * 100):0}%");
            currentSpeedFactor = new Cacheable<float>(CalcCurrentSpeedFactor);
            IngredientCount = new Cacheable<int>(() => storedThings.Sum(x => x.stackCount));
        }

        public UF_Process Process { get; private set; } = null!;

        public Map? Map => fermenter.parent.Map;

        /// <summary>Gets whether the current process has finished.</summary>
        public bool Finished => ProgressPercent >= 1f;

        /// <summary>Gets whether the current process has been ruined.</summary>
        public bool Ruined => ruinedPercent >= 1f;

        /// <summary>The total ingredient count for this progress.</summary>
        public Cacheable<int> IngredientCount { get; }

        public float CurrentSpeedFactor => currentSpeedFactor;

        /// <summary>Gets or sets the number of progress ticks that have elapsed for the fermenter.</summary>
        public long ProgressTicks
        {
            get => progressTicks;
            set
            {
                if (value == progressTicks)
                    return;

                progressTicks = value;
                progressColorMaterial = null;
            }
        }

        public float CurrentSunFactor
        {
            get
            {
                if (Map == null)
                    return 0f;

                if (Process.sunFactor.Span == 0)
                    return 1f;

                float skyGlow = Map.skyManager.CurSkyGlow * (1 - fermenter.RoofCoverage);
                return GenMath.LerpDouble(Static_Weather.SunGlowRange.TrueMin, Static_Weather.SunGlowRange.TrueMax,
                    Process.sunFactor.min, Process.sunFactor.max,
                    skyGlow);
            }
        }

        public float CurrentTemperatureFactor
        {
            get
            {
                if (!Process.usesTemperature)
                    return 1f;

                float ambientTemperature = fermenter.parent.AmbientTemperature;
                // Temperature out of a safe range
                if (ambientTemperature < Process.temperatureSafe.min)
                    return Process.speedBelowSafe;

                if (ambientTemperature > Process.temperatureSafe.max)
                    return Process.speedAboveSafe;

                // Temperature out of an ideal range but still within a safe range
                if (ambientTemperature < Process.temperatureIdeal.min)
                    return GenMath.LerpDouble(Process.temperatureSafe.min, Process.temperatureIdeal.min, Process.speedBelowSafe, 1f, ambientTemperature);

                if (ambientTemperature > Process.temperatureIdeal.max)
                    return GenMath.LerpDouble(Process.temperatureIdeal.max, Process.temperatureSafe.max, 1f, Process.speedAboveSafe, ambientTemperature);

                // Temperature within an ideal range
                return 1f;
            }
        }

        public float CurrentRainFactor
        {
            get
            {
                if (Map == null)
                    return 0f;

                if (Process.rainFactor.Span == 0)
                    return 1f;

                // When snowing, the game also increases RainRate.
                // Therefore, non-zero SnowRate puts RainRespect to a state as if it was not raining.
                if (Map.weatherManager.SnowRate != 0)
                    return Process.rainFactor.min;

                float rainRate = Map.weatherManager.RainRate * (1 - fermenter.RoofCoverage);
                return GenMath.LerpDoubleClamped(Static_Weather.RainRateRange.TrueMin, Static_Weather.RainRateRange.TrueMax,
                    Process.rainFactor.min, Process.rainFactor.max,
                    rainRate);
            }
        }

        public float CurrentSnowFactor
        {
            get
            {
                if (Map == null)
                    return 0f;

                if (Process.snowFactor.Span == 0)
                    return 1f;

                float snowRate = Map.weatherManager.SnowRate * (1 - fermenter.RoofCoverage);
                return GenMath.LerpDoubleClamped(Static_Weather.SnowRateRange.TrueMin, Static_Weather.SnowRateRange.TrueMax,
                    Process.snowFactor.min, Process.snowFactor.max,
                    snowRate);
            }
        }

        public float CurrentWindFactor
        {
            get
            {
                if (Map == null)
                    return 0f;

                if (Process.windFactor.Span == 0)
                    return 1f;

                if (fermenter.RoofCoverage != 0)
                    return Process.windFactor.min;

                return GenMath.LerpDoubleClamped(Static_Weather.WindSpeedRange.TrueMin, Static_Weather.WindSpeedRange.TrueMax,
                    Process.windFactor.min, Process.windFactor.max,
                    Map.windManager.WindSpeed);
            }
        }

        /// <summary>Gets the number of days the current process has fermented.</summary>
        public float ProgressDays => (float) ProgressTicks / GenDate.TicksPerDay;

        /// <summary>Gets the percentage the current process has finished to completion.</summary>
        public float ProgressPercent => Mathf.Clamp01(ProgressDays / (Process.usesQuality ? DaysToReachTargetQuality : Process.processDays));

        /// <summary>Progress percentage without decimal, e.g. 23%</summary>
        public Cacheable<string> ProgressPercentFlooredString { get; }

        /// <summary>Gets the number of ticks estimated to be remaining for the current fermentation process to finish, based on current speed.</summary>
        public int EstimatedTicksLeft =>
            CurrentSpeedFactor <= 0
                ? -1
                : Mathf.Max(Process.usesQuality
                        ? Mathf.RoundToInt((DaysToReachTargetQuality * GenDate.TicksPerDay) - ProgressTicks)
                        : Mathf.RoundToInt((Process.processDays * GenDate.TicksPerDay) - ProgressTicks),
                    0);

        /// <summary>Gets the number of hours estimated to be remaining for the current fermentation process to finish, based on the current speed.</summary>
        public float EstimatedHoursLeft => EstimatedTicksLeft / 2500f;

        /// <summary>Gets the number of days estimated to be remaining for the current fermentation process to finish, based on the current speed.</summary>
        public float EstimatedDaysLeft => (float) EstimatedTicksLeft / GenDate.TicksPerDay;

        /// <summary>Gets the current quality of fermentation.</summary>
        public QualityCategory CurrentQuality
        {
            get
            {
                if (!Process.usesQuality)
                    return QualityCategory.Normal;

                return ProgressDays switch
                {
                    { } d when d < Process.qualityDays.poor => QualityCategory.Awful,
                    { } d when d < Process.qualityDays.normal => QualityCategory.Poor,
                    { } d when d < Process.qualityDays.good => QualityCategory.Normal,
                    { } d when d < Process.qualityDays.excellent => QualityCategory.Good,
                    { } d when d < Process.qualityDays.masterwork => QualityCategory.Excellent,
                    { } d when d < Process.qualityDays.legendary => QualityCategory.Legendary,
                    { } d when d >= Process.qualityDays.legendary => QualityCategory.Legendary,
                    _ => QualityCategory.Normal
                };
            }
        }

        /// <summary>Gets the number of days required to reach the current selected target quality.</summary>
        public float DaysToReachTargetQuality
        {
            get
            {
                if (!Process.usesQuality)
                    return Process.processDays;

                return TargetQuality switch
                {
                    QualityCategory.Awful => Process.qualityDays.awful,
                    QualityCategory.Poor => Process.qualityDays.poor,
                    QualityCategory.Normal => Process.qualityDays.normal,
                    QualityCategory.Good => Process.qualityDays.good,
                    QualityCategory.Excellent => Process.qualityDays.excellent,
                    QualityCategory.Masterwork => Process.qualityDays.masterwork,
                    QualityCategory.Legendary => Process.qualityDays.legendary,
                    _ => Process.qualityDays.normal
                };
            }
        }

        /// <summary>Gets or sets the quality to target for the fermentation process.</summary>
        public QualityCategory TargetQuality
        {
            get => Process.usesQuality ? targetQuality : QualityCategory.Normal;
            set
            {
                if (value == targetQuality || !Process.usesQuality)
                    return;

                targetQuality = value;
                progressColorMaterial = null;
            }
        }

        /// <summary>Gets all ingredients this process is using. If any thing use CompIngredients, those ingredients will be separately.</summary>
        public IEnumerable<ThingDef> Ingredients
        {
            get
            {
                foreach (var thing in storedThings)
                {
                    CompIngredients? ingredients = thing.TryGetComp<CompIngredients>();
                    if (ingredients != null)
                    {
                        foreach (var ingredient in ingredients.ingredients)
                        {
                            yield return ingredient;
                        }
                    }
                    else
                    {
                        yield return thing.def;
                    }
                }
            }
        }

        /// <summary>Gets the material for the progress bar.</summary>
        public Material ProgressColorMaterial
        {
            get
            {
                progressColorMaterial ??= SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(Static_Bar.ZeroProgressColor, Static_Bar.FermentedColor, ProgressPercent));
                return progressColorMaterial;
            }
        }

        /// <summary>If this process is currently running and will complete.</summary>
        public bool Running => !Ruined && !Finished && CurrentSpeedFactor > 0;

        public string ProgressTooltip
        {
            get
            {
                StringBuilder progressTip = new StringBuilder();
                progressTip.AppendTagged("UF_SpeedTooltip1".Translate(ProgressPercent.ToStringPercent().Named("COMPLETEPERCENT"), CurrentSpeedFactor.ToStringPercentColored().Named("SPEED")));
                progressTip.AppendTagged("UF_SpeedTooltip2".Translate(
                    CurrentTemperatureFactor.ToStringPercentColored().Named("TEMPERATURE"),
                    CurrentWindFactor.ToStringPercentColored().Named("WIND"),
                    CurrentRainFactor.ToStringPercentColored().Named("RAIN"),
                    CurrentSnowFactor.ToStringPercentColored().Named("SNOW"),
                    CurrentSunFactor.ToStringPercentColored().Named("SUN")));

                if (!Finished)
                    progressTip.AppendTagged("UF_SpeedTooltip3".Translate(EstimatedTicksLeft.ToStringTicksToPeriod(canUseDecimals: false).Named("ESTIMATED")));

                return progressTip.ToString();
            }
        }

        public string QualityTooltip
        {
            get
            {
                if (!Process.usesQuality)
                    return "UF_QualityTooltipNA".Translate(Process.thingDef.Named("PRODUCT")).CapitalizeFirst();

                StringBuilder qualityTip = new StringBuilder();

                qualityTip.AppendTagged("UF_QualityTooltip1".Translate(
                    ProgressDays < Process.qualityDays.awful
                        ? "UF_None".TranslateSimple().Named("CURRENT")
                        : CurrentQuality.GetLabel().Named("CURRENT"),
                    TargetQuality.GetLabel().Named("TARGET")));

                qualityTip.AppendTagged("UF_QualityTooltip2".Translate(
                    Mathf.RoundToInt(Process.qualityDays.awful * GenDate.TicksPerDay).ToStringTicksToPeriod(canUseDecimals: false).Named("AWFUL"),
                    Mathf.RoundToInt(Process.qualityDays.poor * GenDate.TicksPerDay).ToStringTicksToPeriod(canUseDecimals: false).Named("POOR"),
                    Mathf.RoundToInt(Process.qualityDays.normal * GenDate.TicksPerDay).ToStringTicksToPeriod(canUseDecimals: false).Named("NORMAL"),
                    Mathf.RoundToInt(Process.qualityDays.good * GenDate.TicksPerDay).ToStringTicksToPeriod(canUseDecimals: false).Named("GOOD"),
                    Mathf.RoundToInt(Process.qualityDays.excellent * GenDate.TicksPerDay).ToStringTicksToPeriod(canUseDecimals: false).Named("EXCELLENT"),
                    Mathf.RoundToInt(Process.qualityDays.masterwork * GenDate.TicksPerDay).ToStringTicksToPeriod(canUseDecimals: false).Named("MASTERWORK"),
                    Mathf.RoundToInt(Process.qualityDays.legendary * GenDate.TicksPerDay).ToStringTicksToPeriod(canUseDecimals: false).Named("LEGENDARY")
                ));

                return qualityTip.ToString();
            }
        }

        /// <summary>Index of the process that this progress is for.</summary>
        public int ProcessIndex
        {
            get => processIndex;
            set
            {
                processIndex = value;
                Process = fermenter.Props.processes[ProcessIndex];
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ruinedPercent, "ruinedPercent");
            Scribe_Values.Look(ref progressTicks, "progressTicks");
            Scribe_Values.Look(ref processIndex, "processIndex");
            Scribe_Collections.Look(ref storedThings, "storedThings", LookMode.Reference);
            Scribe_Values.Look(ref targetQuality, "targetQuality");

            IngredientCount.Invalidate();
            Process = fermenter.Props.processes[ProcessIndex];
        }

        public void DoTicks(int ticks)
        {
            ProgressTicks += Mathf.RoundToInt(ticks * CurrentSpeedFactor);

            if (Ruined || !Process.usesTemperature)
                return;

            // 2500 ticks per hour, 100 percent = divide by 250000
            float ambientTemperature = fermenter.parent.AmbientTemperature;
            if (ambientTemperature > Process.temperatureSafe.max) 
                ruinedPercent += (ambientTemperature - Process.temperatureSafe.max) * (Process.ruinedPerDegreePerHour / GenDate.TicksPerHour / 100f) * ticks;
            else if (ambientTemperature < Process.temperatureSafe.min) ruinedPercent -= (ambientTemperature - Process.temperatureSafe.min) * (Process.ruinedPerDegreePerHour / GenDate.TicksPerHour / 100f) * ticks;

            if (ruinedPercent >= 1f)
            {
                ruinedPercent = 1f;
                fermenter.parent.BroadcastCompSignal("RuinedByTemperature");
            }
            else if (ruinedPercent < 0f)
            {
                ruinedPercent = 0f;
            }
        }

        public void TickRare()
        {
            // Performance - only calculate these every so often
            currentSpeedFactor.Invalidate();
            ProgressPercentFlooredString.Invalidate();
        }

        private float CalcCurrentSpeedFactor()
        {
            return Mathf.Max(CurrentTemperatureFactor * CurrentSunFactor * CurrentRainFactor * CurrentSnowFactor * CurrentWindFactor, 0f);
        }

        public bool Accepts(ThingDef def)
        {
            return Process.processType == ProcessType.Single && Process.ingredientFilter.Allows(def) && IngredientCount < Process.maxCapacity;
        }

        public string ProcessTooltip(string ingredientLabel, string? productLabel)
        {
            StringBuilder creatingTip = new StringBuilder();

            string qualityStr = Process.usesQuality ? $" ({TargetQuality.GetLabel().CapitalizeFirst()})" : "";

            creatingTip.AppendTagged("UF_CreatingTooltip1".Translate(productLabel.Named("PRODUCT"), ingredientLabel.Named("INGREDIENT"), qualityStr.Named("QUALITY")));
            creatingTip.AppendTagged(Process.usesQuality
                ? "UF_CreatingTooltip2_Quality".Translate(Mathf.RoundToInt(Process.qualityDays.awful * GenDate.TicksPerDay).ToStringTicksToPeriod().Named("TOAWFUL"))
                : "UF_CreatingTooltip2_NoQuality".Translate(Mathf.RoundToInt(Process.processDays * GenDate.TicksPerDay).ToStringTicksToPeriod().Named("TIME")));

            if (Process.usesTemperature)
            {
                creatingTip.AppendTagged("UF_CreatingTooltip3".Translate(
                    Process.temperatureIdeal.min.ToStringTemperature().Named("MIN"),
                    Process.temperatureIdeal.max.ToStringTemperature().Named("MAX")));
                creatingTip.AppendTagged("UF_CreatingTooltip4".Translate(
                    Process.temperatureSafe.min.ToStringTemperature().Named("MIN"),
                    Process.temperatureSafe.max.ToStringTemperature().Named("MAX"),
                    (Process.ruinedPerDegreePerHour / 100f).ToStringPercent().Named("PERHOUR")
                ));
            }

            if (ruinedPercent > 0.05f)
            {
                creatingTip.AppendTagged("UF_CreatingTooltip5".Translate(ruinedPercent.ToStringPercent().Colorize(ColoredText.RedReadable)));
            }

            if (!Process.temperatureSafe.Includes(fermenter.parent.AmbientTemperature) && !Ruined)
            {
                creatingTip.Append("UF_CreatingTooltip6".Translate(fermenter.parent.AmbientTemperature.ToStringTemperature()).Resolve().Colorize(ColoredText.RedReadable));
            }

            return creatingTip.ToString();
        }

        public void AddThing(Thing thing)
        {
            storedThings.Add(thing);
            IngredientCount.Invalidate();
        }

        public void RemoveThing(Thing thing)
        {
            storedThings.Remove(thing);
            IngredientCount.Invalidate();
        }
    }
}
