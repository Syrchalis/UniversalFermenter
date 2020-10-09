#nullable enable
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalFermenter
{
    public class WorkGiver_FillUF : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool Prioritized => true;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.GetComponent<MapComponent_UF>().thingsWithUFComp.Any();
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.GetComponent<MapComponent_UF>().thingsWithUFComp;
        }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            CompUniversalFermenter comp = t.Thing.TryGetComp<CompUniversalFermenter>();
            if (comp is null)
                return 0;
            return 1f / comp.SpaceLeft;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompUniversalFermenter comp = t.TryGetComp<CompUniversalFermenter>();
            if (comp == null || comp.SpaceLeft == 0)
                return false;

            if (!comp.TemperatureOk)
            {
                JobFailReason.Is("BadTemperature".Translate().ToLower());
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null
                || t.IsForbidden(pawn)
                || !comp.AnyIngredientsOnMap
                || !pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced)
                || t.IsBurning())
                return false;

            if (FindIngredient(pawn, t) == null)
            {
                JobFailReason.Is("UF_NoIngredient".Translate());
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Thing? t2 = FindIngredient(pawn, t);
            return new Job(UF_DefOf.FillUniversalFermenter, t, t2);
        }

        private static Thing? FindIngredient(Pawn pawn, Thing fermenter)
        {
            CompUniversalFermenter? comp = fermenter.TryGetComp<CompUniversalFermenter>();
            if (comp is null)
                return null;

            ThingFilter filter = comp.CombinedIngredientFilter.Value;

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                filter.BestThingRequest,
                PathEndMode.ClosestTouch, 
                TraverseParms.For(pawn), 
                9999f, 
                x => !x.IsForbidden(pawn) && pawn.CanReserve(x) && filter.Allows(x));
        }
    }
}
