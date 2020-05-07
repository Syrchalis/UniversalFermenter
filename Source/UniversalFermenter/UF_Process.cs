using Verse;

namespace UniversalFermenter
{
	public class UF_Process
	{
		public ThingDef thingDef;
		public ThingFilter ingredientFilter = new ThingFilter();

        public bool usesTemperature = true;
		public FloatRange temperatureSafe = new FloatRange(-1f, 32f);
		public FloatRange temperatureIdeal = new FloatRange(7f, 32f);
		public float ruinedPerDegreePerHour = 2.5f;
        public float speedBelowSafe = 0.1f;
        public float speedAboveSafe = 1f;
        public float processDays = 6f;
		public int maxCapacity = 25;
		public float efficiency = 1f;
		public FloatRange sunFactor = new FloatRange(1f, 1f);
		public FloatRange rainFactor = new FloatRange(1f, 1f);
		public FloatRange snowFactor = new FloatRange(1f, 1f);
		public FloatRange windFactor = new FloatRange(1f, 1f);
		public string graphicSuffix = null;

		public void ResolveReferences()
		{			
			this.ingredientFilter.ResolveReferences();			
		}
	}
}