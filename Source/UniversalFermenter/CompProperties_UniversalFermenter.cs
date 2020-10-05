#nullable enable
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public class CompProperties_UniversalFermenter : CompProperties
    {
        /// <summary>Offset for the fermentation progress bar overlay.</summary>
        public Vector2 barOffset = new Vector2(0f, 0.25f);

        /// <summary>Scale for the fermentation process bar overlay.</summary>
        public Vector2 barScale = new Vector2(1f, 1f);

        /// <summary>The defaults products that are set to be created at the fermenter. Defaults to all products from all processes.</summary>
        public ThingFilter? defaultFilter;

        /// <summary>The processes that this fermenter can execute.</summary>
        public List<UF_Process> processes = new List<UF_Process>();

        /// <summary>Scale for the current product overlay.</summary>
        public Vector2 productIconSize = new Vector2(1f, 1f);

        /// <summary>Deprecated</summary>
        [Obsolete("Use processes")]
        public List<UF_Process> products = new List<UF_Process>();

        /// <summary>Show the current product as an overlay on the fermenter?</summary>
        public bool showProductIcon = true;

        public CompProperties_UniversalFermenter()
        {
            compClass = typeof(CompUniversalFermenter);
        }

        /// <summary>The storage settings for the products that fermenter can create.</summary>
        public StorageSettings FixedStorageSettings { get; private set; } = null!;

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            foreach (UF_Process process in processes)
            {
                process.ResolveReferences();
            }

            FixedStorageSettings = new StorageSettings();

            foreach (var process in processes)
            {
                if (process.thingDef != null)
                {
                    FixedStorageSettings.filter.SetAllow(process.thingDef, true);
                }
            }

            if (defaultFilter == null)
            {
                defaultFilter = new ThingFilter();
                defaultFilter.CopyAllowancesFrom(FixedStorageSettings.filter);
            }
        }
    }
}
