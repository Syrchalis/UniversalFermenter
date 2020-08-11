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
                listing_Standard.CheckboxLabeled("UF_SingleItemIcon".Translate(), ref UF_Settings.singleItemIcon, "UF_SingleItemIconTooltip".Translate());
                listing_Standard.Gap(12);
                listing_Standard.CheckboxLabeled("UF_SortAlphabetically".Translate(), ref UF_Settings.sortAlphabetically, "UF_SortAlphabeticallyTooltip".Translate());
                listing_Standard.GapLine(30);
                listing_Standard.CheckboxLabeled("UF_ShowCurrentQualityIcon".Translate(), ref UF_Settings.showCurrentQualityIcon, "UF_ShowCurrentQualityIconTooltip".Translate());
                listing_Standard.Gap(12);
                listing_Standard.CheckboxLabeled("UF_ShowTargetQualityIcon".Translate(), ref UF_Settings.showTargetQualityIcon, "UF_ShowTargetQualityTooltip".Translate());
                listing_Standard.GapLine(30);
                Rect rectReplaceBarrels = listing_Standard.GetRect(30f);
                TooltipHandler.TipRegion(rectReplaceBarrels, "UF_ReplaceVanillaBarrelsTooltip".Translate());
                if (Widgets.ButtonText(rectReplaceBarrels, "UF_ReplaceVanillaBarrels".Translate(), true, true, true))
                {
                    ReplaceVanillaBarrels();
                }
                listing_Standard.GapLine(30);
                Rect rectDefaultSettings = listing_Standard.GetRect(30f);
                TooltipHandler.TipRegion(rectDefaultSettings, "UF_DefaultSettingsTooltip".Translate());
                if (Widgets.ButtonText(rectDefaultSettings, "UF_DefaultSettings".Translate(), true, true, true))
                {
                    UF_Settings.showProcessIconGlobal = true;
                    UF_Settings.processIconSize = 0.6f;
                    UF_Settings.singleItemIcon = true;
                    UF_Settings.sortAlphabetically = false;
                    UF_Settings.showCurrentQualityIcon = true;
                    UF_Settings.showTargetQualityIcon = false;
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

        public void ReplaceVanillaBarrels()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.FermentingBarrel).ToList())
                {
                    bool inUse = false;
                    float progress = 0;
                    int fillCount = 0;
                    IntVec3 position = thing.Position;
                    ThingDef stuff;
                    if (thing.Stuff != null)
                    {
                         stuff = thing.Stuff;
                    }
                    else
                    {
                        stuff = ThingDefOf.WoodLog;
                    }
                    if (thing is Building_FermentingBarrel oldBarrel)
                    {
                        inUse = oldBarrel.SpaceLeftForWort < 25;
                        if (inUse)
                        {
                            progress = oldBarrel.Progress;
                            fillCount = 25 - oldBarrel.SpaceLeftForWort;
                        }
                    }
                    Thing newBarrel = ThingMaker.MakeThing(UF_DefOf.UniversalFermenter, stuff);
                    GenSpawn.Spawn(newBarrel, position, map);
                    if (inUse)
                    {
                        CompUniversalFermenter compUF = newBarrel.TryGetComp<CompUniversalFermenter>();
                        compUF.CurrentProcess = compUF.Props.processes.First(p => p.thingDef == ThingDefOf.Beer);
                        Thing wort = ThingMaker.MakeThing(ThingDefOf.Wort, null);
                        wort.stackCount = fillCount;
                        compUF.AddIngredient(wort);
                        compUF.ProgressTicks = (int)(360000 * progress);
                    }
                }
                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.MinifiedThing).Where(t => t.GetInnerIfMinified().def == ThingDefOf.FermentingBarrel))
                {
                    MinifiedThing minifiedThing = thing as MinifiedThing;
                    ThingDef stuff;
                    if (minifiedThing.InnerThing.Stuff != null)
                    {
                        stuff = minifiedThing.InnerThing.Stuff;
                    }
                    else
                    {
                        stuff = ThingDefOf.WoodLog;
                    }
                    minifiedThing.InnerThing = null;
                    Thing newBarrel = ThingMaker.MakeThing(UF_DefOf.UniversalFermenter, stuff);
                    minifiedThing.InnerThing = newBarrel;
                    cachedGraphic.SetValue(minifiedThing, null);
                }
            }
        }
        public static FieldInfo cachedGraphic = typeof(MinifiedThing).GetField("cachedGraphic", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public class UF_Settings : ModSettings
    {
        public static bool showProcessIconGlobal = true;
        public static float processIconSize = 0.6f;
        public static bool showCurrentQualityIcon = true;
        public static bool showTargetQualityIcon = false;
        public static bool singleItemIcon = true;
        public static bool sortAlphabetically = false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref showProcessIconGlobal, "UF_showProcessIconGlobal", true, true);
            Scribe_Values.Look<float>(ref processIconSize, "UF_processIconSize", 0.6f, true);
            Scribe_Values.Look<bool>(ref showCurrentQualityIcon, "UF_showCurrentQualityIcon", true, true);
            Scribe_Values.Look<bool>(ref showTargetQualityIcon, "UF_showTargetQualityIcon", false, true);
            Scribe_Values.Look<bool>(ref singleItemIcon, "UF_singleItemIcon", true, true);
            Scribe_Values.Look<bool>(ref sortAlphabetically, "UF_sortAlphabetically", false, true);
        }
    }
}
