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
                if (comp != null && comp.CurrentProcess.colorCoded)
                {
                    return comp.CurrentProcess.color;
                }
                return DrawColor;
            }
        }
    }
}
