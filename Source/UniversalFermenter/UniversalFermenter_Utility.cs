using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;

namespace UniversalProcessors
{
    [StaticConstructorOnStartup]
    public static class UniversalFermenter_Utility
    {
        static UniversalFermenter_Utility()
        {
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompUniversalFermenter))))
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompUniversalFermenter)) is CompProperties_UniversalFermenter compUF)
                {
                    allUFProducts.AddRange(compUF.products);
                }
            }
            foreach (UniversalFermenterProduct product in allUFProducts)
            {
                productGizmos.Add(product, new Command_Action
                {
                    defaultLabel = product.thingDef.label,
                    defaultDesc = "UF_NextDesc".Translate(product.thingDef.label, IngredientFilterSummary(product.ingredientFilter)),
                    activateSound = SoundDef.Named("Click"),
                    icon = GetIcon(product.thingDef),
                    action = () =>
                    {
                        foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                        {
                            CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                            if (comp != null && comp.Product == product)
                            {
                                NextResource(comp);
                            }
                        }
                    },
                });
                Texture2D icon = GetIcon(product.thingDef);
                Material mat = MaterialPool.MatFrom(icon);
                productMaterials.Add(product, mat);
            }
        }

        public static void NextResource(CompUniversalFermenter comp)
        {
            comp.nextResourceInd++;
            if (comp.nextResourceInd >= comp.ResourceListSize)
            {
                comp.nextResourceInd = 0;
            }
            if (comp.Empty)
            {
                comp.currentResourceInd = comp.nextResourceInd;
            }
        }
        public static List<UniversalFermenterProduct> allUFProducts = new List<UniversalFermenterProduct>();
        public static Dictionary<UniversalFermenterProduct, Command_Action> productGizmos = new Dictionary<UniversalFermenterProduct, Command_Action>();
        public static Dictionary<UniversalFermenterProduct, Material> productMaterials = new Dictionary<UniversalFermenterProduct, Material>();
        
        public static Command_Action DispSpeeds = new Command_Action()
        {
            defaultLabel = "DEBUG: Display Speed Factors",
            defaultDesc = "Display the current sun, rain, snow and wind speed factors and how much of the building is covered by roof.",
            activateSound = SoundDef.Named("Click"),
            action = () => 
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        Log.Message(comp.parent.ToString() + ": " +
                              "sun: " + comp.SunRespectSpeedFactor.ToString("0.00") +
                              "| rain: " + comp.RainRespectSpeedFactor.ToString("0.00") +
                              "| snow: " + comp.SnowRespectSpeedFactor.ToString("0.00") +
                              "| wind: " + comp.WindRespectSpeedFactor.ToString("0.00") +
                              "| roofed: " + comp.RoofedFactor.ToString("0.00"));
                    }
                }
            }
        };
        public static Command_Action DevFinish = new Command_Action()
        {
            defaultLabel = "DEBUG: Finish",
            activateSound = SoundDef.Named("Click"),
            action = () => 
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        comp.Progress = 1f;
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
        public static Texture2D GetIcon(ThingDef thingDef)
        {
            Texture2D icon = ContentFinder<Texture2D>.Get(thingDef.graphicData.texPath, false);
            if (icon == null)
            {
                // Use the first texture in the folder
                icon = ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).ToList()[0];
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
