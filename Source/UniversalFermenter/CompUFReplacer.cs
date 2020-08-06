using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;
using System.Reflection;

namespace UniversalFermenter
{
    //Allows dynamic replacing of vanilla barrels
    public class CompUFReplacer : ThingComp
    {
        public override void CompTick()
        {
            base.CompTick();
            ageTicks += 1;
            if (ageTicks > 3 && ageTicks < 30)
            {
                Building_FermentingBarrel oldBarrel = parent as Building_FermentingBarrel;
                IntVec3 position = parent.Position;
                Map map = parent.Map;
                bool inUse = oldBarrel.SpaceLeftForWort < 25;
                float progress = 0;
                int fillCount = 0;
                if (inUse)
                {
                    progress = oldBarrel.Progress;
                    fillCount = 25 - oldBarrel.SpaceLeftForWort;
                }
                Thing newBarrel = ThingMaker.MakeThing(UF_DefOf.UniversalFermenter, ThingDefOf.WoodLog);
                GenSpawn.Spawn(newBarrel, position, map);
                CompUniversalFermenter compUF = newBarrel.TryGetComp<CompUniversalFermenter>();
                if (inUse)
                {
                    Thing wort = ThingMaker.MakeThing(ThingDefOf.Wort, null);
                    wort.stackCount = fillCount;
                    compUF.AddIngredient(wort);
                    compUF.ProgressTicks = (int)(360000 * progress);
                }
            }
            if (ageTicks > 30)
            {
                Log.Warning("Vanilla Barrel " + parent + " still ticking.");
            }
        }
        public int ageTicks = 0;
    }
}
