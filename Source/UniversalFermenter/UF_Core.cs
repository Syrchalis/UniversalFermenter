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
                listing_Standard.CheckboxLabeled("UF_ShowProductIcon".Translate(), ref UF_Settings.showProductIconGlobal, "UF_ShowProductIconTooltip".Translate());
                listing_Standard.End();
                settings.Write();
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }

    public class UF_Settings : ModSettings
    {
        public static bool showProductIconGlobal = true;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref showProductIconGlobal, "UF_showProductIconGlobal", true, true);
        }
    }
}
