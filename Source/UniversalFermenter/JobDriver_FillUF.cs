using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace UniversalFermenter
{
    public class JobDriver_FillUF : JobDriver
    {
        private const TargetIndex FermenterInd = TargetIndex.A;
        private const TargetIndex IngredientInd = TargetIndex.B;
        private const int Duration = 200;

        protected Thing Fermenter => job.GetTarget(TargetIndex.A).Thing;

        protected Thing Ingredient => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Fermenter, job, 1, -1, null, errorOnFailed)
                   && pawn.Reserve(Ingredient, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompUniversalFermenter comp = Fermenter.TryGetComp<CompUniversalFermenter>();

            // Verify fermenter and ingredient validity
            this.FailOnDespawnedNullOrForbidden(FermenterInd);
            this.FailOnBurningImmobile(FermenterInd);
            AddEndCondition(() => comp.SpaceLeftForIngredient > 0 ? JobCondition.Ongoing : JobCondition.Succeeded);
            yield return Toils_General.DoAtomic(() => job.count = comp.SpaceLeftForIngredient);

            // Creating the toil before yielding allows for CheckForGetOpportunityDuplicate
            Toil reserveIngredient = Toils_Reserve.Reserve(IngredientInd);
            yield return reserveIngredient;

            yield return Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(IngredientInd)
                .FailOnSomeonePhysicallyInteracting(IngredientInd);

            yield return Toils_Haul.StartCarryThing(IngredientInd, false, true)
                .FailOnDestroyedNullOrForbidden(IngredientInd);

            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveIngredient, IngredientInd, TargetIndex.None, true);

            // Carry ingredients to the fermenter
            yield return Toils_Goto.GotoThing(FermenterInd, PathEndMode.Touch);

            // Add delay for adding ingredients to the fermenter
            yield return Toils_General.Wait(Duration, FermenterInd)
                .FailOnDestroyedNullOrForbidden(IngredientInd)
                .FailOnDestroyedNullOrForbidden(FermenterInd)
                .FailOnCannotTouch(FermenterInd, PathEndMode.Touch)
                .WithProgressBarToilDelay(FermenterInd);

            // Use ingredients
            // The UniversalFermenter automatically destroys held ingredients
            yield return new Toil
            {
                initAction = () => comp.AddIngredient(Ingredient),
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}