using System.Collections.Generic;

using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalFermenter
{

	public class JobDriver_FillUF : JobDriver
	{

		private const TargetIndex FermenterInd = TargetIndex.A;
		private const TargetIndex IngredientInd = TargetIndex.B;
		private const int Duration = 200;

		protected Thing Fermenter
		{
			get
			{
				return this.job.GetTarget(TargetIndex.A).Thing;
			}
		}

		protected Thing Ingredient
		{
			get
			{
				return this.job.GetTarget(TargetIndex.B).Thing;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
			return this.pawn.Reserve(this.Fermenter, this.job, 1, -1, null, errorOnFailed) && this.pawn.Reserve(this.Ingredient, this.job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			CompUniversalFermenter comp = Fermenter.TryGetComp<CompUniversalFermenter>();
						
			// Verify fermenter and ingredient validity
			this.FailOnDespawnedNullOrForbidden(FermenterInd);
			this.FailOnBurningImmobile(FermenterInd);
            base.AddEndCondition(delegate
            {
                if (comp.SpaceLeftForIngredient > 0)
                {
                    return JobCondition.Ongoing;
                }
                return JobCondition.Succeeded;
            });
            yield return Toils_General.DoAtomic(delegate
            {
                job.count = comp.SpaceLeftForIngredient;
            });

            // Creating the toil before yielding allows for CheckForGetOpportunityDuplicate
            Toil reserveIngredient = Toils_Reserve.Reserve(IngredientInd);
			yield return reserveIngredient;

			yield return Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(IngredientInd).FailOnSomeonePhysicallyInteracting(IngredientInd);

			yield return Toils_Haul.StartCarryThing(IngredientInd, false, true, false).FailOnDestroyedNullOrForbidden(IngredientInd);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveIngredient, IngredientInd, TargetIndex.None, true);

            // Carry ingredients to the fermenter
            yield return Toils_Goto.GotoThing(FermenterInd, PathEndMode.Touch);

            // Add delay for adding ingredients to the fermenter
            yield return Toils_General.Wait(Duration, FermenterInd).FailOnDestroyedNullOrForbidden(IngredientInd).FailOnDestroyedNullOrForbidden(FermenterInd)
                .FailOnCannotTouch(FermenterInd, PathEndMode.Touch).WithProgressBarToilDelay(FermenterInd, false, -0.5f);

            // Use ingredients
            // The UniversalFermenter automatically destroys held ingredients
            yield return new Toil
            {
                initAction = delegate ()
                {
                    comp.AddIngredient(Ingredient);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
		}
	}
}
