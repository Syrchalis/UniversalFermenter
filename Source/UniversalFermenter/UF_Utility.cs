#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace UniversalFermenter
{
    [StaticConstructorOnStartup]
    public static class UF_Utility
    {
        public static List<UF_Process> allUFProcesses = new List<UF_Process>();

        public static Dictionary<UF_Process, Command_Action> processGizmos = new Dictionary<UF_Process, Command_Action>();
        public static Dictionary<QualityCategory, Command_Action> qualityGizmos = new Dictionary<QualityCategory, Command_Action>();

        public static Dictionary<UF_Process, Material> processMaterials = new Dictionary<UF_Process, Material>();
        public static Dictionary<QualityCategory, Material> qualityMaterials = new Dictionary<QualityCategory, Material>();

        private static int gooseAngle = Rand.Range(0, 360);
        private static readonly HashSet<char> Vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u' };

        static UF_Utility()
        {
            CheckForErrors();
            CacheAllProcesses();
            RecacheAll();
        }

        public static void CheckForErrors()
        {
            bool sendWarning = false;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<-- Universal Fermenter Errors -->");
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompUniversalFermenter)))) //we grab every thingDef that has the UF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompUniversalFermenter)) is CompProperties_UniversalFermenter compUF)
                {
#pragma warning disable 618
                    if (!compUF.products.NullOrEmpty()) //if anyone uses the outdated "products" field we log a warning and copy the list to processes
                    {
                        stringBuilder.AppendLine("Universal Fermenter: ThingDef '" + thingDef.defName + "' uses outdated field 'products', please rename to 'processes'.");
                        compUF.processes.AddRange(compUF.products);
                        sendWarning = true;
                    }
#pragma warning restore 618
                    if (compUF.processes.Any(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty()))
                    {
                        stringBuilder.AppendLine("ThingDef '" + thingDef.defName + "' has processes with no product or no filter. These fields are required.");
                        compUF.processes.RemoveAll(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty());
                        sendWarning = true;
                    }
                }
            }

            if (sendWarning)
            {
                Log.Warning(stringBuilder.ToString().TrimEndNewlines());
            }
        }

        public static void RecacheAll() //Gets called in constructor and in writeSettings
        {
            RecacheProcessMaterials();
            RecacheQualityGizmos();
        }

        private static void CacheAllProcesses()
        {
            List<UF_Process> tempProcessList = new List<UF_Process>();
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompUniversalFermenter))))
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompUniversalFermenter)) is CompProperties_UniversalFermenter compUF)
                {
                    tempProcessList.AddRange(compUF.processes);
                }
            }

            for (int i = 0; i < tempProcessList.Count; i++)
            {
                tempProcessList[i].uniqueID = i;
                allUFProcesses.Add(tempProcessList[i]);
            }
        }

        public static void RecacheProcessMaterials()
        {
            processMaterials.Clear();
            foreach (UF_Process process in allUFProcesses)
            {
                Texture2D? icon = GetIcon(process.thingDef, UF_Settings.singleItemIcon);
                Material mat = MaterialPool.MatFrom(icon);
                processMaterials.Add(process, mat);
            }

            qualityMaterials.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Texture2D icon = ContentFinder<Texture2D>.Get("UI/QualityIcons/" + quality);
                Material mat = MaterialPool.MatFrom(icon);
                qualityMaterials.Add(quality, mat);
            }
        }

        public static void RecacheQualityGizmos()
        {
            qualityGizmos.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Command_Quality command_Quality = new Command_Quality
                {
                    defaultLabel = quality.GetLabel().CapitalizeFirst(),
                    defaultDesc = "UF_SetQualityDesc".Translate(),
                    //activateSound = SoundDefOf.Tick_Tiny,
                    icon = (Texture2D) qualityMaterials[quality].mainTexture,
                    qualityToTarget = quality
                };
                command_Quality.action = () =>
                {
                    FloatMenu floatMenu = new FloatMenu(command_Quality.RightClickFloatMenuOptions.ToList())
                    {
                        vanishIfMouseDistant = true
                    };
                    Find.WindowStack.Add(floatMenu);
                };
                qualityGizmos.Add(quality, command_Quality);
            }
        }

        public static Command_Action DebugGizmo()
        {
            Command_Action gizmo = new Command_Action
            {
                defaultLabel = "Debug: Options",
                defaultDesc = "Opens a float menu with debug options.",
                icon = ContentFinder<Texture2D>.Get("UI/DebugGoose"),
                iconAngle = gooseAngle,
                iconDrawScale = 1.25f,
                action = () =>
                {
                    FloatMenu floatMenu = new FloatMenu(DebugOptions()) { vanishIfMouseDistant = true };
                    Find.WindowStack.Add(floatMenu);
                }
            };
            return gizmo;
        }

        public static List<FloatMenuOption> DebugOptions()
        {
            List<FloatMenuOption> floatMenuOptions = new List<FloatMenuOption>();
            IEnumerable<ThingWithComps> things = Find.Selector.SelectedObjects.OfType<ThingWithComps>().Where(t => t.GetComp<CompUniversalFermenter>() != null);
            IEnumerable<CompUniversalFermenter> comps = things.Select(t => t.TryGetComp<CompUniversalFermenter>()).ToList();

            if (comps.Any(c => !c.Empty && !c.AnyFinished))
            {
                floatMenuOptions.Add(new FloatMenuOption("Finish process", () => FinishProcess(comps)));
                floatMenuOptions.Add(new FloatMenuOption("Progress one day", () => ProgressOneDay(comps)));
                floatMenuOptions.Add(new FloatMenuOption("Progress half quadrum", () => ProgressHalfQuadrum(comps)));
            }

            if (comps.Any(c => c.AnyFinished))
            {
                floatMenuOptions.Add(new FloatMenuOption("Empty object", () => EmptyObject(comps)));
            }

            floatMenuOptions.Add(new FloatMenuOption("Log speed factors", LogSpeedFactors));

            return floatMenuOptions;
        }

        internal static void FinishProcess(IEnumerable<CompUniversalFermenter> comps)
        {
            foreach (CompUniversalFermenter comp in comps)
            {
                foreach (var progress in comp.progresses)
                {
                    progress.progressTicks = progress.Process.usesQuality
                        ? Mathf.RoundToInt(progress.DaysToReachTargetQuality * GenDate.TicksPerDay)
                        : Mathf.RoundToInt(progress.Process.processDays * GenDate.TicksPerDay);
                }
                comp.CachesInvalid();
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        internal static void ProgressOneDay(IEnumerable<CompUniversalFermenter> comps)
        {
            foreach (CompUniversalFermenter comp in comps)
            {
                foreach (var progress in comp.progresses)
                {
                    progress.progressTicks += GenDate.TicksPerDay;
                }
                comp.CachesInvalid();
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        internal static void ProgressHalfQuadrum(IEnumerable<CompUniversalFermenter> comps)
        {
            foreach (CompUniversalFermenter comp in comps)
            {
                foreach (var progress in comp.progresses)
                {
                    progress.progressTicks += GenDate.TicksPerQuadrum / 2;
                }
                comp.CachesInvalid();
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        internal static void EmptyObject(IEnumerable<CompUniversalFermenter> comps)
        {
            foreach (CompUniversalFermenter comp in comps)
            {
                foreach (var progress in comp.progresses)
                {
                    if (progress.Finished)
                    {
                        Thing? product = comp.TakeOutProduct(progress);
                        GenPlace.TryPlaceThing(product, comp.parent.Position, comp.parent.Map, ThingPlaceMode.Near);
                    }
                }
                comp.CachesInvalid();
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        internal static void LogSpeedFactors()
        {
            foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
            {
                CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                if (comp != null)
                {
                    foreach (var progress in comp.progresses)
                    {
                        Log.Message(comp.parent + ": " +
                                    "sun: " + progress.CurrentSunFactor.ToStringPercent() +
                                    "| rain: " + progress.CurrentRainFactor.ToStringPercent() +
                                    "| snow: " + progress.CurrentSnowFactor.ToStringPercent() +
                                    "| wind: " + progress.CurrentWindFactor.ToStringPercent() +
                                    "| roofed: " + comp.RoofCoverage.Value.ToStringPercent());
                    }
                }
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        public static string IngredientFilterSummary(ThingFilter thingFilter)
        {
            return thingFilter.Summary;
        }

        public static string VowelTrim(string str, int limit)
        {
            int vowelsToRemove = str.Length - limit;
            for (int i = str.Length - 1; i > 0; i--)
            {
                if (vowelsToRemove <= 0)
                    break;

                if (!IsVowel(str[i]) || str[i - 1] == ' ')
                    continue;

                str = str.Remove(i, 1);
                vowelsToRemove--;
            }

            if (str.Length > limit)
            {
                str = str.Remove(limit - 2) + "..";
            }

            return str;
        }

        public static bool IsVowel(char c)
        {
            return Vowels.Contains(c);
        }

        // Try to get a texture of a thingDef; If not found, use LaunchReport icon
        public static Texture2D? GetIcon(ThingDef? thingDef, bool singleStack = true)
        {
            if (thingDef == null)
                return null;

            Texture2D? icon = ContentFinder<Texture2D>.Get(thingDef.graphicData.texPath, false);
            if (icon != null)
                return icon;

            // Use the first texture in the folder
            icon = singleStack ? ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).FirstOrDefault() : ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).LastOrDefault();
            if (icon != null)
                return icon;

            icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport");
            Log.Warning("Universal Fermenter:: No texture at " + thingDef.graphicData.texPath + ".");
            return icon;
        }
    }
}
