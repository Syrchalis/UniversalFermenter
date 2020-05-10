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
    public class Building_ColorCoded : Building
    {
        public override Color DrawColorTwo
        {
            get
            {
                CompUniversalFermenter comp = this.TryGetComp<CompUniversalFermenter>();
                if (comp != null && comp.CurrentProcess.colorCoded)
                {
                    return comp.CurrentProcess.color;
                }
                return DrawColor;
            }
        }
    }
}
