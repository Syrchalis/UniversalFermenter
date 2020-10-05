#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public class Command_Quality : Command_Action
    {
        public QualityCategory qualityToTarget;

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                List<FloatMenuOption> qualityfloatMenuOptions = new List<FloatMenuOption>();
                foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
                {
                    qualityfloatMenuOptions.Add(
                        new FloatMenuOption(
                            quality.GetLabel(),
                            () => ChangeQuality(qualityToTarget, quality),
                            (Texture2D) UF_Utility.qualityMaterials[quality].mainTexture,
                            Color.white
                        )
                    );
                }

                return qualityfloatMenuOptions;
            }
        }

        internal static void ChangeQuality(QualityCategory qualityToTarget, QualityCategory quality)
        {
            foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
            {
                CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                if (comp != null && comp.Props.processes.Any(p => p.usesQuality) && comp.targetQuality == qualityToTarget)
                {
                    comp.targetQuality = quality;
                }
            }
        }
    }
}
