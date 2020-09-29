using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace UniversalFermenter
{
    public class Command_Process : Command_Action
    {
        public UF_Process processToTarget;
        public List<UF_Process> processOptions = new List<UF_Process>();

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                List<FloatMenuOption> floatMenuOptions = new List<FloatMenuOption>();
                foreach (UF_Process process in processOptions)
                {
                    floatMenuOptions.Add(
                        new FloatMenuOption(
                            process.customLabel != "" ? process.customLabel : process.thingDef.label.CapitalizeFirst(),
                            () => ChangeProcess(processToTarget, process),
                            UF_Utility.GetIcon(process.thingDef, UF_Settings.singleItemIcon),
                            Color.white
                        )
                    );
                }
                if (UF_Settings.sortAlphabetically)
                {
                    floatMenuOptions.SortBy(fmo => fmo.Label);
                }
                return floatMenuOptions;
            }
        }

        internal static void ChangeProcess(UF_Process processToTarget, UF_Process process)
        {
            foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>()) {
                CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                if (comp != null && comp.CurrentProcess == processToTarget) {
                    comp.CurrentProcess = process;
                }
            }
        }
    }

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
                            () => ChangeQuality(qualityToTarget,quality),
                            (Texture2D)UF_Utility.qualityMaterials[quality].mainTexture,
                            Color.white
                        )
                    );
                }
                return qualityfloatMenuOptions;
            }
        }

        internal static void ChangeQuality(QualityCategory qualityToTarget, QualityCategory quality)
        {
            foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>()) {
                CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                if (comp != null && comp.CurrentProcess.usesQuality && comp.TargetQuality == qualityToTarget) {
                    comp.TargetQuality = quality;
                }
            }
        }
    }
}
