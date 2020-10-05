#nullable enable
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public class ITab_UFProductFilter : ITab_Storage
    {
        private Vector2 scrollPosition;

        public ITab_UFProductFilter()
        {
            labelKey = "UF_TabProductFilter";
            size = new Vector2(300, 400);
        }

        protected override bool IsPrioritySettingVisible => false;

        protected override void FillTab()
        {
            Rect area = new Rect(0.0f, 0.0f, size.x, size.y).ContractedBy(10f);
            GUI.BeginGroup(area);

            ThingFilter? parentFilter = SelStoreSettingsParent.GetParentStoreSettings()?.filter;
            if (parentFilter != null)
            {
                parentFilter.allowedHitPointsConfigurable = false;
                parentFilter.allowedQualitiesConfigurable = false;
            }

            ThingFilterUI.DoThingFilterConfigWindow(
                new Rect(0.0f, 20, area.width, area.height),
                ref scrollPosition,
                SelStoreSettingsParent.GetStoreSettings().filter,
                parentFilter,
                8,
                forceHiddenFilters: DefDatabase<SpecialThingFilterDef>.AllDefsListForReading,
                forceHideHitPointsConfig: true);

            GUI.EndGroup();
        }
    }
}
