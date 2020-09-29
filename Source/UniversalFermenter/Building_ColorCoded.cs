using UnityEngine;
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
                return comp != null && comp.CurrentProcess.colorCoded ? comp.CurrentProcess.color : DrawColor;
            }
        }
    }
}
