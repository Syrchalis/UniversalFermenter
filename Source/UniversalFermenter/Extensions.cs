using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

#nullable enable

namespace UniversalFermenter
{
    public static class Extensions
    {
        private static readonly AccessTools.FieldRef<Thing, Graphic> Get_graphicInt = AccessTools.FieldRefAccess<Thing, Graphic>("graphicInt");

        public static readonly List<Pair<float, Color>> GreenToYellowToRed = new List<Pair<float, Color>>
        {
            new Pair<float, Color>(0, ColoredText.RedReadable),
            new Pair<float, Color>(0.5f, Color.yellow),
            new Pair<float, Color>(1, Color.green)
        };

        /// <summary>Converts a float percentage into a string, colored according to the percentage. Default from green to yellow to red.</summary>
        public static string ToStringPercentColored(this float val, List<Pair<float, Color>>? colors = null)
        {
            colors ??= GreenToYellowToRed;
            return val.ToStringPercent().Colorize(GenUI.LerpColor(colors, val));
        }

        public static void ReloadGraphic(this Thing thing, string texPath)
        {
            ref Graphic graphicRef = ref Get_graphicInt(thing);

            graphicRef = GraphicDatabase.Get(
                thing.def.graphicData.graphicClass,
                texPath,
                ShaderDatabase.LoadShader(thing.def.graphicData.shaderType.shaderPath),
                thing.def.graphicData.drawSize,
                thing.DrawColor,
                thing.DrawColorTwo);

            thing.Map?.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlag.Things);
        }

        public static IEnumerable<Thing> ThingsMatching(this ListerThings listerThings, ThingFilter filter)
        {
            foreach (ThingDef def in filter.AllowedThingDefs)
            {
                foreach (Thing thing in listerThings.ThingsOfDef(def))
                {
                    if (filter.Allows(thing))
                        yield return thing;
                }
            }
        }
    }
}
