using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;

namespace UniversalFermenter
{
    public class UF_Core : Mod
    {
        public static UF_Settings settings;
        public UF_Core(ModContentPack content) : base(content)
        {
            settings = GetSettings<UF_Settings>();
        }
        public override string SettingsCategory() => "UF_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            checked
            {
                Listing_Standard listing_Standard = new Listing_Standard();
                listing_Standard.Begin(inRect);
                listing_Standard.CheckboxLabeled("UF_ShowProcessIcon".Translate(), ref UF_Settings.showProcessIconGlobal, "UF_ShowProcessIconTooltip".Translate());
                listing_Standard.Gap(12);
                listing_Standard.Label("UF_ProcessIconSize".Translate() +  ": " + UF_Settings.processIconSize.ToStringByStyle(ToStringStyle.PercentZero), -1, "UF_ProcessIconSizeTooltip".Translate());
                UF_Settings.processIconSize = listing_Standard.Slider(GenMath.RoundTo(UF_Settings.processIconSize, 0.05f), 0.2f, 1f);
                listing_Standard.Gap(12);
                listing_Standard.CheckboxLabeled("UF_SingleItemIcon".Translate(), ref UF_Settings.singleItemIcon, "UF_SingleItemIconTooltip".Translate());
                listing_Standard.Gap(12);
                listing_Standard.CheckboxLabeled("UF_SortAlphabetically".Translate(), ref UF_Settings.sortAlphabetically, "UF_SortAlphabeticallyTooltip".Translate());
                listing_Standard.Gap(24);
                if (listing_Standard.ButtonText("UF_DefaultSettings".Translate(), "UF_DefaultSettingsTooltip".Translate()))
                {
                    UF_Settings.showProcessIconGlobal = true;
                    UF_Settings.processIconSize = 0.6f;
                    UF_Settings.singleItemIcon = true;
                    UF_Settings.sortAlphabetically = false;
    }
                listing_Standard.End();
                settings.Write();
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            UF_Utility.RecacheAll();
        }
    }

    public class UF_Settings : ModSettings
    {
        public static bool showProcessIconGlobal = true;
        public static float processIconSize = 0.6f;
        public static bool singleItemIcon = true;
        public static bool sortAlphabetically = false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref showProcessIconGlobal, "UF_showProcessIconGlobal", true, true);
            Scribe_Values.Look<float>(ref processIconSize, "UF_processIconSize", 0.6f, true);
            Scribe_Values.Look<bool>(ref singleItemIcon, "UF_singleItemIcon", true, true);
            Scribe_Values.Look<bool>(ref sortAlphabetically, "UF_sortAlphabetically", false, true);
        }
    }
}
