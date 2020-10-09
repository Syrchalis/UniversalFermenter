#nullable enable
using System.Collections.Generic;
using System.Linq;
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

        protected Thing Fermenter => job.GetTarget(FermenterInd).Thing;

        protected Thing Product => job.GetTarget(ProductToHaulInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Fermenter, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompUniversalFermenter comp = Fermenter.TryGetComp<CompUniversalFermenter>();
            // Verify fermenter validity
            this.FailOn(() => comp.Empty || !(comp.AnyFinished || comp.AnyRuined));
            this.FailOnDestroyedNullOrForbidden(FermenterInd);

            // Reserve fermenter
            yield return Toils_Reserve.Reserve(FermenterInd);

            // Go to the fermenter
            yield return Toils_Goto.GotoThing(FermenterInd, PathEndMode.ClosestTouch);

            // Add delay for collecting product from fermenter, if it is ready
            yield return Toils_General.Wait(Duration)
                .FailOnDestroyedNullOrForbidden(FermenterInd)
                .WithProgressBarToilDelay(FermenterInd);

            // Collect products
            Toil collect = new Toil
            {
                initAction = () =>
                {
                    UF_Progress? progress = comp.progresses.First(x => x.Finished || x.Ruined);
                    Thing? product = comp.TakeOutProduct(progress);

                    // Remove a ruined product
                    if (product == null)
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }

                    GenPlace.TryPlaceThing(product, pawn.Position, Map, ThingPlaceMode.Near);
                    StoragePriority storagePriority = StoreUtility.CurrentStoragePriorityOf(product);

                    // Try to find a suitable storage spot for the product
                    if (StoreUtility.TryFindBestBetterStoreCellFor(product, pawn, Map, storagePriority, pawn.Faction, out IntVec3 c))
                    {
                        job.SetTarget(ProductToHaulInd, product);
                        job.count = product.stackCount;
                        job.SetTarget(StorageCellInd, c);
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
