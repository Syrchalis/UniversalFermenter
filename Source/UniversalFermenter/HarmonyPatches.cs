using HarmonyLib;
using System.Reflection;
using RimWorld;
using Verse;

namespace UniversalFermenter
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("Syrchalis.Rimworld.UniversalFermenter");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(Building_FermentingBarrel), nameof(Building_FermentingBarrel.GetInspectString))]
    public static class OldBarrel_GetInspectStringPatch
    {
        [HarmonyPrefix]
        public static bool OldBarrel_GetInspectString_Postfix(ref string __result)
        {
            __result = "UF_OldBarrelInspectString".Translate();
            return false;
        }
    }
}