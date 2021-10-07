#nullable enable
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public class ITab_UFIngredientFilter : ITab
    {
        private Vector2 scrollPosition;
        private readonly Rect area;
        private readonly Rect configRect;

        public ITab_UFIngredientFilter()
        {
            labelKey = "UF_TabIngredientFilter";
            size = new Vector2(300, 400);
            area = new Rect(0.0f, 0.0f, size.x, size.y).ContractedBy(10f);
            configRect = new Rect(0.0f, 20, area.width, area.height);
        }

        public override bool IsVisible => SelObject is Thing thing && thing.Faction == Faction.OfPlayerSilentFail && thing.TryGetComp<CompUniversalFermenter>() != null;

        protected override void FillTab()
        {
            CompUniversalFermenter? fermenter = SelObject is ThingWithComps selObj ? selObj.GetComp<CompUniversalFermenter>() : null;
            if (fermenter is null)
                return;

            GUI.BeginGroup(area);

            fermenter.ParentProductFilter.allowedHitPointsConfigurable = false;
            fermenter.ParentProductFilter.allowedQualitiesConfigurable = false;

            ThingFilterUI.DoThingFilterConfigWindow(configRect, ref scrollPosition, fermenter.IngredientFilter, fermenter.ParentIngredientFilter, 8);

            GUI.EndGroup();
        }
    }
}
