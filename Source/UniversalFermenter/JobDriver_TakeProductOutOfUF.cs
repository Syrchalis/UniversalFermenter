using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalFermenter
{
    public class JobDriver_TakeProductOutOfUF : JobDriver
	{
        private const TargetIndex FermenterInd = TargetIndex.A;
		private const TargetIndex ProductToHaulInd = TargetIndex.B;
		private const TargetIndex StorageCellInd = TargetIndex.C;
		private const int Duration = 200;

        //return CurJob.GetTarget(TargetIndex.A).Thing;
		protected Thing Fermenter => job.GetTarget(TargetIndex.A).Thing;

        //return CurJob.GetTarget(TargetIndex.B).Thing;
        protected Thing Product => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
			return pawn.Reserve(Fermenter, job);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			CompUniversalFermenter comp = Fermenter.TryGetComp<CompUniversalFermenter>();
			// Verify fermenter validity
			this.FailOn(() => !comp.Finished);
			this.FailOnDestroyedNullOrForbidden(FermenterInd);

			// Reserve fermenter
			yield return Toils_Reserve.Reserve(FermenterInd);

			// Go to the fermenter
			yield return Toils_Goto.GotoThing(FermenterInd, PathEndMode.ClosestTouch);

			// Add delay for collecting product from fermenter, if it is ready
			yield return Toils_General.Wait(Duration).FailOnDestroyedNullOrForbidden(FermenterInd).WithProgressBarToilDelay(FermenterInd);

			// Collect product
            Toil collect = new Toil
            {
                initAction = () =>
                {
                    Thing product = comp.TakeOutProduct();
                    GenPlace.TryPlaceThing(product, pawn.Position, Map, ThingPlaceMode.Near);
                    StoragePriority storagePriority = StoreUtility.CurrentStoragePriorityOf(product);

                    // Try to find a suitable storage spot for the product
                    if (StoreUtility.TryFindBestBetterStoreCellFor(product, pawn, Map, storagePriority, pawn.Faction, out IntVec3 c))
                    {
                        job.SetTarget(TargetIndex.B, product);
                        job.count = product.stackCount;
                        job.SetTarget(TargetIndex.C, c);
                    }
                    // If there is no spot to store the product, end this job
                    else
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return collect;

			// Reserve the product
			yield return Toils_Reserve.Reserve(ProductToHaulInd);

			// Reserve the storage cell
			yield return Toils_Reserve.Reserve(StorageCellInd);

			// Go to the product
			yield return Toils_Goto.GotoThing(ProductToHaulInd, PathEndMode.ClosestTouch);

			// Pick up the product
			yield return Toils_Haul.StartCarryThing(ProductToHaulInd);

			// Carry the product to the storage cell, then place it down
			Toil carry = Toils_Haul.CarryHauledThingToCell(StorageCellInd);
			yield return carry;
			yield return Toils_Haul.PlaceHauledThingInCell(StorageCellInd, carry, true);
        }
	}
}
