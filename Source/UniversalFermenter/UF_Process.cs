using System;
using System.Globalization;
using System.Xml;
using UnityEngine;
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
        public bool usesQuality = false;
        public QualityDays qualityDays = new QualityDays(1, 0, 0, 0, 0, 0, 0);
        public bool colorCoded = false;
        public Color color = new Color(1.0f, 1.0f, 1.0f);

		public void ResolveReferences()
		{			
			this.ingredientFilter.ResolveReferences();			
		}
	}

    public class QualityDays
    {
        public QualityDays()
        {
        }

        public QualityDays(float awful, float poor, float normal, float good, float excellent, float masterwork, float legendary)
        {
            this.awful = awful;
            this.poor = poor;
            this.normal = normal;
            this.good = good;
            this.excellent = excellent;
            this.masterwork = masterwork;
            this.legendary = legendary;
        }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (xmlRoot.ChildNodes.Count != 1) Log.Error("UF: QualityDays configured incorrectly");
            else
            {
                string str = xmlRoot.FirstChild.Value;
                str = str.TrimStart(new char[]
                {
                    '('
                });
                str = str.TrimEnd(new char[]
                {
                    ')'
                });
                string[] array = str.Split(new char[]
                {
                    ','
                });
                CultureInfo invariantCulture = CultureInfo.InvariantCulture;
                awful = Convert.ToSingle(array[0], invariantCulture);
                poor = Convert.ToSingle(array[1], invariantCulture);
                normal = Convert.ToSingle(array[2], invariantCulture);
                good = Convert.ToSingle(array[3], invariantCulture);
                excellent = Convert.ToSingle(array[4], invariantCulture);
                masterwork = Convert.ToSingle(array[5], invariantCulture);
                legendary = Convert.ToSingle(array[6], invariantCulture);
            }
        }
        public float awful;
        public float poor;
        public float normal;
        public float good;
        public float excellent;
        public float masterwork;
        public float legendary;
    }
}