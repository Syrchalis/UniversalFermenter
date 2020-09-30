#nullable enable
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public enum ProcessType
    {
        /// <summary>The process contains a single "stack" of items which can be added to. Valid for things like beer fermenting.</summary>
        Single,

        /// <summary>The process contains multiple stacks which each ferment individually. Valid for things like drying lumber.</summary>
        Multiple,

        /// <summary>Same as multiple, but multiple processes of type MultipleMixed can be ran at once. Valid for things like ovens.</summary>
        MultipleMixed
    }

    /// <summary>A single process the fermenter can execute, turning one item into another item after a set amount of time.</summary>
    public class UF_Process
    {
        /// <summary>A unique ID for the fermentation process, mainly for multiplayer.</summary>
        public int uniqueID;

        /// <summary>The thing that is created at the end of the fermentation process.</summary>
        public ThingDef? thingDef;

        /// <summary>The ingredients that are allowed to put in to make the target thing.</summary>
        public ThingFilter ingredientFilter = new ThingFilter();

        /// <summary>Whether the speed of the fermentation process is affected by the temperature. The process can still be ruined by bad temperatures.</summary>
        public bool usesTemperature = true;

        /// <summary>The safe range of temperatures for this process. Outside the safe range, the product will start to spoil/degrade.</summary>
        public FloatRange temperatureSafe = new FloatRange(-1f, 32f);

        /// <summary>The ideal range of temperatures for this process. Outside the ideal range, the process will slow down.</summary>
        public FloatRange temperatureIdeal = new FloatRange(7f, 32f);

        /// <summary>If outside the safe temperature range, the product gets ruined this percentage, per degree non-ideal, per hour.</summary>
        public float ruinedPerDegreePerHour = 2.5f;

        /// <summary>The speed of the fermentation process below the minimum safe temperature.</summary>
        public float speedBelowSafe = 0.1f;

        /// <summary>The speed of the fermentatino process above the maximum safe temperature.</summary>
        public float speedAboveSafe = 1f;

        /// <summary>The total number of days for the fermentation process to complete, assuming perfect conditions.</summary>
        public float processDays = 6f;

        /// <summary>The max capacity of items for this process.</summary>
        public int maxCapacity = 25;

        /// <summary>The number of resulting products is equal to the number of input things multiplied by this efficiency.</summary>
        public float efficiency = 1f;

        /// <summary>The speed multipliers at the minimum and maximum possible sun amounts.</summary>
        public FloatRange sunFactor = new FloatRange(1f, 1f);

        /// <summary>The speed multipliers at the minimum and maximum possible rain amounts.</summary>
        public FloatRange rainFactor = new FloatRange(1f, 1f);

        /// <summary>The speed multipliers at the minimum and maximum possible snow amounts.</summary>
        public FloatRange snowFactor = new FloatRange(1f, 1f);

        /// <summary>The speed multipliers at the minimum and maximum possible wind speeds.</summary>
        public FloatRange windFactor = new FloatRange(1f, 1f);

        /// <summary>When there are items in the fermenter for this process, this suffix will be added to the fermenter's graphic, to load a "filled" graphic.</summary>
        public string? graphicSuffix = null;

        /// <summary>Whether the fermentation process results in different quality levels of product depending on how long it has fermented.</summary>
        public bool usesQuality = false;

        /// <summary>The number of days for each quality level of product, from awful to legendary.</summary>
        public QualityDays qualityDays = new QualityDays(1, 0, 0, 0, 0, 0, 0);

        /// <summary>Whether the process has a color-coded overlay.</summary>
        public bool colorCoded = false;

        /// <summary>Color to apply to the texture.</summary>
        public Color color = new Color(1.0f, 1.0f, 1.0f);

        /// <summary>Custom label to give finished products.</summary>
        public string customLabel = "";

        /// <summary>The type of process being executed, Single, Multiple, or MultipleMixed.</summary>
        public ProcessType processType = ProcessType.Single;

        public void ResolveReferences()
        {
            ingredientFilter.ResolveReferences();
        }
    }
}