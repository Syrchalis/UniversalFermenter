using HarmonyLib;
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
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("Syrchalis.Rimworld.UniversalFermenter");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
    [HarmonyPatch(typeof(Building_FermentingBarrel), nameof(Building_FermentingBarrel.GetInspectString))]
    public class OldBarrel_GetInspectStringPatch
    {
        [HarmonyPrefix]
        public static bool OldBarrel_GetInspectString_Postfix(ref string __result)
        {
            __result = "UF_OldBarrelInspectString".Translate();
            return false;
        }
    }
}
