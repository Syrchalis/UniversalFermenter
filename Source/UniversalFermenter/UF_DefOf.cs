using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace UniversalFermenter
{
    [DefOf]
    public static class UF_DefOf
    {
        static UF_DefOf()
        {
        }
        public static JobDef FillUniversalFermenter;
        public static JobDef TakeProductOutOfUniversalFermenter;

        public static SoundDef UF_Honk;

        public static ThingDef UniversalFermenter;
    }
}
