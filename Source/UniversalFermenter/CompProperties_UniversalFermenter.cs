using System;
using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalFermenter
{
    public class CompProperties_UniversalFermenter : CompProperties
    {
        /// <summary>Deprecated</summary>
        [Obsolete("Use processes")]
        public List<UF_Process> products = new List<UF_Process>();

        /// <summary>The processes that this fermenter can execute.</summary>
        public List<UF_Process> processes = new List<UF_Process>();

        /// <summary>Show the current product as an overlay on the fermenter?</summary>
        public bool showProductIcon = true;

        /// <summary>Offset for the fermentation progress bar overlay.</summary>
        public Vector2 barOffset = new Vector2(0f, 0.25f);

        /// <summary>Scale for the fermentation process bar overlay.</summary>
        public Vector2 barScale = new Vector2(1f, 1f);

        /// <summary>Scale for the current product overlay.</summary>
        public Vector2 productIconSize = new Vector2(1f, 1f);

        public CompProperties_UniversalFermenter()
        {
            compClass = typeof(CompUniversalFermenter);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            foreach (UF_Process process in processes)
            {
                process.ResolveReferences();
            }
        }
    }
}