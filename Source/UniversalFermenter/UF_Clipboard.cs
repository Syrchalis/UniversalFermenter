using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace UniversalFermenter
{
    public static class UF_Clipboard
    {
        private static readonly Dictionary<ThingDef, Tuple<ThingFilter, ThingFilter>> Copies = new Dictionary<ThingDef, Tuple<ThingFilter, ThingFilter>>();

        public static bool HasCopiedSettings(CompUniversalFermenter fermenter)
        {
            return Copies.ContainsKey(fermenter.parent.def);
        }

        public static void Copy(CompUniversalFermenter fermenter)
        {
            ThingFilter productFilter = new ThingFilter();
            productFilter.CopyAllowancesFrom(fermenter.ProductFilter);

            ThingFilter ingredientFilter = new ThingFilter();
            ingredientFilter.CopyAllowancesFrom(fermenter.IngredientFilter);

            Copies[fermenter.parent.def] = Tuple.Create(productFilter, ingredientFilter);
        }

        private static void PasteInto(CompUniversalFermenter fermenter)
        {
            if (!Copies.TryGetValue(fermenter.parent.def, out var filters))
                return;
            fermenter.ProductFilter.CopyAllowancesFrom(filters.Item1);
            fermenter.IngredientFilter.CopyAllowancesFrom(filters.Item2);
        }

        public static IEnumerable<Gizmo> CopyPasteGizmosFor(CompUniversalFermenter fermenter)
        {
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CopySettings"),
                defaultLabel = "CommandCopyZoneSettingsLabel".Translate(),
                defaultDesc = "CommandCopyZoneSettingsDesc".Translate(),
                action = () =>
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Copy(fermenter);
                },
                hotKey = KeyBindingDefOf.Misc4
            };

            Command_Action paste = new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings"),
                defaultLabel = "CommandPasteZoneSettingsLabel".Translate(),
                defaultDesc = "CommandPasteZoneSettingsDesc".Translate(),
                action = () =>
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    PasteInto(fermenter);
                },
                hotKey = KeyBindingDefOf.Misc5
            };

            if (!HasCopiedSettings(fermenter))
                paste.Disable();

            yield return paste;
        }
    }
}
