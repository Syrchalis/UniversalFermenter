using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalFermenter
{

	public class CompProperties_UniversalFermenter : CompProperties
	{
		public List<UF_Process> products = new List<UF_Process>();
        public List<UF_Process> processes = new List<UF_Process>();
        public bool showProductIcon = true;
        public Vector2 barOffset = new Vector2(0f, 0.25f);

        public CompProperties_UniversalFermenter()
		{
			compClass = typeof(CompUniversalFermenter);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			for (int i = 0; i < this.processes.Count; i++)
			{				
				this.processes[i].ResolveReferences();
			}
		}
	}
}
