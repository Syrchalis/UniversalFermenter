#nullable enable
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public class UF_Core : Mod
    {
        public static UF_Settings settings = null!;

        public static FieldInfo cachedGraphic = AccessTools.Field(typeof(MinifiedThing), "cachedGraphic");

        public UF_Core(ModContentPack content) : base(content)
        {
            settings = GetSettings<UF_Settings>();
        }

        public override string SettingsCategory()
        {
            return "UF_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            checked
            {
                Listing_Standard listing_Standard = new Listing_Standard();
                listing_Standard.Begin(inRect);
                listing_Standard.CheckboxLabeled("UF_ShowProcessIcon".Translate(), ref UF_Settings.showProcessIconGlobal, "UF_ShowProcessIconTooltip".Translate());
                listing_Standard.Gap();
                listing_Standard.Label("UF_ProcessIconSize".Translate() + ": " + UF_Settings.processIconSize.ToStringByStyle(ToStringStyle.PercentZero), -1, "UF_ProcessIconSizeTooltip".Translate());
                UF_Settings.processIconSize = listing_Standard.Slider(GenMath.RoundTo(UF_Settings.processIconSize, 0.05f), 0.2f, 1f);
                listing_Standard.CheckboxLabeled("UF_SingleItemIcon".Translate(), ref UF_Settings.singleItemIcon, "UF_SingleItemIconTooltip".Translate());
                listing_Standard.Gap();
                listing_Standard.CheckboxLabeled("UF_SortAlphabetically".Translate(), ref UF_Settings.sortAlphabetically, "UF_SortAlphabeticallyTooltip".Translate());
                listing_Standard.GapLine(30);
                listing_Standard.CheckboxLabeled("UF_ShowCurrentQualityIcon".Translate(), ref UF_Settings.showCurrentQualityIcon, "UF_ShowCurrentQualityIconTooltip".Translate());
                listing_Standard.Gap();
                listing_Standard.CheckboxLabeled("UF_ShowTargetQualityIcon".Translate(), ref UF_Settings.showTargetQualityIcon, "UF_ShowTargetQualityTooltip".Translate());
                listing_Standard.GapLine(30);
                Rect rectReplaceBarrels = listing_Standard.GetRect(30f);
                TooltipHandler.TipRegion(rectReplaceBarrels, "UF_ReplaceVanillaBarrelsTooltip".Translate());
                if (Widgets.ButtonText(rectReplaceBarrels, "UF_ReplaceVanillaBarrels".Translate()))
                {
                    ReplaceVanillaBarrels();
                }

                listing_Standard.GapLine(30);
                Rect rectDefaultSettings = listing_Standard.GetRect(30f);
                TooltipHandler.TipRegion(rectDefaultSettings, "UF_DefaultSettingsTooltip".Translate());
                if (Widgets.ButtonText(rectDefaultSettings, "UF_DefaultSettings".Translate()))
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

        public static void ReplaceVanillaBarrels()
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
                    ThingDef stuff = thing.Stuff ?? ThingDefOf.WoodLog;
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
                        Thing wort = ThingMaker.MakeThing(ThingDefOf.Wort);
                        wort.stackCount = fillCount;
                        compUF.AddIngredient(wort);
                        compUF.progresses.First().progressTicks = (int) (360000 * progress);
                    }
                }

                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.MinifiedThing).Where(t => t.GetInnerIfMinified().def == ThingDefOf.FermentingBarrel))
                {
                    MinifiedThing minifiedThing = (MinifiedThing) thing;
                    ThingDef stuff = minifiedThing.InnerThing.Stuff ?? ThingDefOf.WoodLog;
                    minifiedThing.InnerThing = null;
                    Thing newBarrel = ThingMaker.MakeThing(UF_DefOf.UniversalFermenter, stuff);
                    minifiedThing.InnerThing = newBarrel;
                    cachedGraphic.SetValue(minifiedThing, null);
                }
            }
        }
    }

    public class UF_Settings : ModSettings
    {
        public static bool showProcessIconGlobal = true;
        public static float processIconSize = 0.6f;
        public static bool showCurrentQualityIcon = true;
        public static bool showTargetQualityIcon;
        public static bool singleItemIcon = true;
        public static bool sortAlphabetically;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showProcessIconGlobal, "UF_showProcessIconGlobal", true, true);
            Scribe_Values.Look(ref processIconSize, "UF_processIconSize", 0.6f, true);
            Scribe_Values.Look(ref showCurrentQualityIcon, "UF_showCurrentQualityIcon", true, true);
            Scribe_Values.Look(ref showTargetQualityIcon, "UF_showTargetQualityIcon", false, true);
            Scribe_Values.Look(ref singleItemIcon, "UF_singleItemIcon", true, true);
            Scribe_Values.Look(ref sortAlphabetically, "UF_sortAlphabetically", false, true);
        }
    }
}
