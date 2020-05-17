using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;

namespace UniversalFermenter
{
    [StaticConstructorOnStartup]
    public static class UF_Utility
    {
        public static List<UF_Process> allUFProcesses = new List<UF_Process>();
        public static Dictionary<UF_Process, Command_Action> processGizmos = new Dictionary<UF_Process, Command_Action>();
        public static Dictionary<UF_Process, Material> processMaterials = new Dictionary<UF_Process, Material>();
        public static List<CompUniversalFermenter> comps = new List<CompUniversalFermenter>();
        public static Dictionary<QualityCategory, Command_Action> qualityGizmos = new Dictionary<QualityCategory, Command_Action>();
        public static UF_Process currentGizmoProcess = null;
        public static QualityCategory currentQualityCategory = QualityCategory.Normal;

        static UF_Utility()
        {
            CheckForErrors();
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
                    if (!compUF.products.NullOrEmpty()) //if anyone uses the outdated "products" field we log a warning and copy the list to processes
                    {
                        stringBuilder.AppendLine("Universal Fermenter: ThingDef '" + thingDef.defName + "' uses outdated field 'products', please rename to 'processes'.");
                        compUF.processes.AddRange(compUF.products);
                        sendWarning = true;
                    }
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
            RecacheProcessGizmos();
            RecacheProcessMaterials();
            RecacheQualityGizmos();
        }

        public static void RecacheProcessGizmos()
        {
            allUFProcesses.Clear();
            processGizmos.Clear();
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompUniversalFermenter)))) //we grab every thingDef that has the UF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompUniversalFermenter)) is CompProperties_UniversalFermenter compUF)
                {
                    allUFProcesses.AddRange(compUF.processes); //adds the processes to a list so we have a full list of all processes
                    List<FloatMenuOption> floatMenuOptions = new List<FloatMenuOption>();
                    foreach (UF_Process process in compUF.processes) //we grab every process from the current thingDef and make a float menu option for it
                    {
                        floatMenuOptions.Add(new FloatMenuOption(process.thingDef.LabelCap, delegate () //the action switches all selected comps to the process
                        {
                            foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>().Where(t => t.def == thingDef)) //we only want things that are of the current thingDef, otherwise other things with this comp try to switch their process
                            {
                                CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                                if (comp != null && comp.CurrentProcess == currentGizmoProcess)
                                {
                                    comp.CurrentProcess = process;
                                    thing.Notify_ColorChanged();
                                }
                            }
                            currentGizmoProcess = null;
                        }, GetIcon(process.thingDef, UF_Settings.singleItemIcon), Color.white, MenuOptionPriority.Default, null, null, 0f, null, null));
                    }
                    if (UF_Settings.sortAlphabetically)
                    {
                        floatMenuOptions.SortBy(fmo => fmo.Label);
                    }
                    foreach (UF_Process process in compUF.processes) //we loop again to make a gizmo for each process, now that we have a complete FloatMenuOption list
                    {
                        processGizmos.Add(process, new Command_Action
                        {
                            defaultLabel = process.thingDef.label,
                            defaultDesc = "UF_NextDesc".Translate(process.thingDef.label, IngredientFilterSummary(process.ingredientFilter)),
                            activateSound = SoundDefOf.Tick_Tiny,
                            icon = GetIcon(process.thingDef, UF_Settings.singleItemIcon),
                            action = () =>
                            {
                                FloatMenu floatMenu = new FloatMenu(floatMenuOptions)
                                {
                                    vanishIfMouseDistant = true,
                                    onCloseCallback = () =>
                                    {
                                        currentGizmoProcess = null;
                                    }
                                };
                                Find.WindowStack.Add(floatMenu);
                                currentGizmoProcess = process;
                            },
                        });
                    }
                }
            }
        }

        public static void RecacheProcessMaterials()
        {
            processMaterials.Clear();
            foreach (UF_Process process in allUFProcesses)
            {
                Texture2D icon = GetIcon(process.thingDef, UF_Settings.singleItemIcon);
                Material mat = MaterialPool.MatFrom(icon);
                processMaterials.Add(process, mat);
            }
        }

        public static void RecacheQualityGizmos()
        {
            qualityGizmos.Clear();
            List<FloatMenuOption> qualityfloatMenuOptions = new List<FloatMenuOption>();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                qualityfloatMenuOptions.Add(new FloatMenuOption(quality.GetLabel(), delegate ()
                {
                    foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                    {
                        CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                        if (comp != null && comp.CurrentProcess.usesQuality && comp.targetQuality == currentQualityCategory)
                        {
                            comp.TargetQuality = quality;
                        }
                    }
                    currentQualityCategory = QualityCategory.Normal;
                }));
            }
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                qualityGizmos.Add(quality, new Command_Action
                {
                    defaultLabel = quality.GetLabel().CapitalizeFirst(),
                    defaultDesc = "UF_SetQualityDesc".Translate(),
                    activateSound = SoundDefOf.Tick_Tiny,
                    icon = ContentFinder<Texture2D>.Get("UI/QualityIcons/" + quality.ToString()),
                    action = () =>
                    {
                        FloatMenu floatMenu = new FloatMenu(qualityfloatMenuOptions)
                        {
                            vanishIfMouseDistant = true,
                            onCloseCallback = () =>
                            {
                                currentQualityCategory = QualityCategory.Normal;
                            }
                        };
                        Find.WindowStack.Add(floatMenu);
                        currentQualityCategory = quality;
                    }
                });
            }
        }

        public static Command_Action DispSpeeds = new Command_Action()
        {
            defaultLabel = "DEBUG: Display Speed Factors",
            defaultDesc = "Display the current sun, rain, snow and wind speed factors and how much of the building is covered by roof.",
            activateSound = SoundDefOf.Tick_Tiny,
            action = () => 
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        Log.Message(comp.parent.ToString() + ": " +
                              "sun: " + comp.CurrentSunFactor.ToString("0.00") +
                              "| rain: " + comp.CurrentRainFactor.ToString("0.00") +
                              "| snow: " + comp.CurrentSnowFactor.ToString("0.00") +
                              "| wind: " + comp.CurrentWindFactor.ToString("0.00") +
                              "| roofed: " + comp.RoofCoverage.ToString("0.00"));
                    }
                }
            }
        };
        public static Command_Action DevFinish = new Command_Action()
        {
            defaultLabel = "DEBUG: Finish",
            activateSound = SoundDefOf.Tick_Tiny,
            action = () => 
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        if (comp.CurrentProcess.usesQuality)
                        {
                            comp.progressTicks = Mathf.RoundToInt(comp.DaysToReachTargetQuality * GenDate.TicksPerDay);
                        }
                        else
                        {
                            comp.progressTicks = Mathf.RoundToInt(comp.CurrentProcess.processDays * GenDate.TicksPerDay);
                        }
                    }
                }
            },
        };

        public static Command_Action AgeOneDay = new Command_Action()
        {
            defaultLabel = "DEBUG: Age One Day",
            activateSound = SoundDefOf.Tick_Tiny,
            action = () =>
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        comp.DoTicks(GenDate.TicksPerDay);
                    }
                }
            },
        };

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

                if (IsVowel(str[i]))
                {
                    if (str[i - 1] == ' ')
                    {
                        continue;
                    }
                    else
                    {
                        str = str.Remove(i, 1);
                        vowelsToRemove--;
                    }
                }
            }

            if (str.Length > limit)
            {
                str = str.Remove(limit - 2) + "..";
            }

            return str;
        }

        public static bool IsVowel(char c)
        {
            var vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u' };
            return vowels.Contains(c);
        }

        // Try to get a texture of a thingDef; If not found, use LaunchReport icon
        public static Texture2D GetIcon(ThingDef thingDef, bool singleStack = true)
        {
            Texture2D icon = ContentFinder<Texture2D>.Get(thingDef.graphicData.texPath, false);
            if (icon == null)
            {
                // Use the first texture in the folder
                icon = singleStack ? ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).FirstOrDefault() : ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).LastOrDefault();
                if (icon == null)
                {
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport", true);
                    Log.Warning("Universal Fermenter:: No texture at " + thingDef.graphicData.texPath + ".");
                }
            }
            return icon;
        }
    }
}
