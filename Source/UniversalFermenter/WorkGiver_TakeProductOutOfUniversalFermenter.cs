using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalProcessors
{
    public class WorkGiver_TakeProductOutOfUniversalFermenter : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return CompUniversalFermenter.comps.Where(x => x.parent.Map == pawn.Map).Select(x => x.parent);
        }


        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var comp = t.TryGetComp<CompUniversalFermenter>();
            return comp != null && comp.Fermented && !t.IsBurning() && !t.IsForbidden(pawn) &&
                   pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced);
        }


        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return new Job(DefDatabase<JobDef>.GetNamed("TakeProductOutOfUniversalFermenter"), t);
        }
    }
}