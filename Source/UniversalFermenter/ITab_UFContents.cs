#nullable enable
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public class ITab_UFContents : ITab
    {
        private const float H = 28;
        private const float Icon = 24;
        private const float Pad = 4;
        private const float PaddedIcon = Pad + Icon + Pad;
        private const float ThingWidth = 230;

        public static readonly List<Pair<float, Color>> WhiteToYellowToRed = new List<Pair<float, Color>>
        {
            new Pair<float, Color>(0, ColoredText.RedReadable),
            new Pair<float, Color>(0.5f, Color.yellow),
            new Pair<float, Color>(1, ITab_Pawn_Gear.ThingLabelColor)
        };

        private float lastDrawnHeight;
        private Vector2 scrollPosition;

        public ITab_UFContents()
        {
            labelKey = "UF_TabContents";
            size = new Vector2(600f, 450f);
        }

        protected override void FillTab()
        {
            CompUniversalFermenter? fermenter = SelThing.TryGetComp<CompUniversalFermenter>();
            if (fermenter == null)
                return;

            Rect outRect = new Rect(default, size).ContractedBy(10f);
            outRect.yMin += 20f;
            Rect rect = new Rect(0f, 0f, outRect.width, Mathf.Max(lastDrawnHeight, outRect.height));
            Text.Font = GameFont.Small;
            Widgets.BeginScrollView(outRect, ref scrollPosition, rect);
            float num = 0f;
            DoItemsLists(rect, ref num, fermenter);
            lastDrawnHeight = num;
            Widgets.EndScrollView();
        }

        protected void DoItemsLists(Rect inRect, ref float curY, CompUniversalFermenter fermenter)
        {
            GUI.BeginGroup(inRect);

            GUI.color = Widgets.SeparatorLabelColor;
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.Label(new Rect(ThingWidth, curY + 3, ThingWidth, 30), "UF_Product".Translate());
            Widgets.Label(new Rect(ThingWidth + ThingWidth, curY + 3, inRect.width - ThingWidth - ThingWidth, 30), "UF_TargetQuality".Translate());
            Widgets.ListSeparator(ref curY, inRect.width, "UF_FermentingItems".Translate());
            bool flag = false;

            for (int i = 0; i < fermenter.innerContainer.Count; i++)
            {
                Thing t = fermenter.innerContainer[i];
                if (t != null)
                {
                    flag = true;
                    DoThingRow(t.def, t.stackCount, t, inRect.width, ref curY, fermenter);
                }
            }

            if (!flag)
            {
                Widgets.NoneLabel(ref curY, inRect.width);
            }

            GUI.EndGroup();
        }

        protected void DoThingRow(ThingDef thingDef, int count, Thing thing, float width, ref float y, CompUniversalFermenter fermenter)
        {
            UF_Progress? progress = fermenter.GetProgress(thing);
            if (progress == null)
                return;

            // Areas
            Rect ingredientArea = new Rect(0, y, ThingWidth, H);
            Rect productArea = new Rect(ingredientArea.xMax, y, ThingWidth, H);

            Rect qualityArea = new Rect(productArea.xMax, y, 50, H);
            Rect percentAndSpeed = new Rect(width - 70, y, 75, H);

            if (Mouse.IsOver(new Rect(0, y, width, H)))
                TargetHighlighter.Highlight(thing);

            // Hover over percentage or speed icon
            if (Mouse.IsOver(percentAndSpeed))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(percentAndSpeed, TexUI.HighlightTex);
            }

            // Draw ✓, ~, or X based on the current speed
            Texture2D runningTexture = progress.CurrentSpeedFactor <= 0 ? Widgets.CheckboxOffTex
                : progress.CurrentSpeedFactor <= UF_Progress.SlowAtSpeedFactor ? Widgets.CheckboxPartialTex
                : Widgets.CheckboxOnTex;

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect((percentAndSpeed.xMax - PaddedIcon) + Pad, y, Icon, Icon), runningTexture);

            // Progress %
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(percentAndSpeed.x, y, percentAndSpeed.xMax - percentAndSpeed.x - PaddedIcon, H), Mathf.Floor(progress.ProgressPercent * 100).ToString("0") + "%");

            // Hover over progress %
            StringBuilder progressTip = new StringBuilder();
            progressTip.AppendTagged("UF_SpeedTooltip1".Translate(progress.ProgressPercent.ToStringPercent().Named("COMPLETEPERCENT"), progress.CurrentSpeedFactor.ToStringPercentColored().Named("SPEED")));
            progressTip.AppendTagged("UF_SpeedTooltip2".Translate(
                progress.CurrentTemperatureFactor.ToStringPercentColored().Named("TEMPERATURE"),
                progress.CurrentWindFactor.ToStringPercentColored().Named("WIND"),
                progress.CurrentRainFactor.ToStringPercentColored().Named("RAIN"),
                progress.CurrentSnowFactor.ToStringPercentColored().Named("SNOW"),
                progress.CurrentSunFactor.ToStringPercentColored().Named("SUN")));
            progressTip.AppendTagged("UF_SpeedTooltip3".Translate(progress.EstimatedTicksLeft.ToStringTicksToPeriod(canUseDecimals: false).Named("ESTIMATED")));
            TooltipHandler.TipRegion(percentAndSpeed, () => progressTip.ToString(), 23492376);

            // Quality label or dropdown to change the quality
            StringBuilder qualityTip = new StringBuilder();
            if (progress.Process.usesQuality)
            {
                Widgets.Dropdown(qualityArea, progress, p => p?.TargetQuality ?? QualityCategory.Normal, GetProgressQualityDropdowns,
                    progress.TargetQuality.GetLabel().CapitalizeFirst(),
                    (Texture2D) UF_Utility.qualityMaterials[progress.TargetQuality].mainTexture);

                qualityTip.AppendTagged("UF_QualityTooltip1".Translate(
                    progress.ProgressDays < progress.Process.qualityDays.awful
                        ? "UF_None".TranslateSimple().Named("CURRENT")
                        : progress.CurrentQuality.GetLabel().Named("CURRENT"),
                    progress.TargetQuality.GetLabel().Named("TARGET")));

                qualityTip.AppendTagged("UF_QualityTooltip2".Translate(
                    Mathf.RoundToInt(progress.Process.qualityDays.awful * 60000).ToStringTicksToPeriod(canUseDecimals: false).Named("AWFUL"),
                    Mathf.RoundToInt(progress.Process.qualityDays.poor * 60000).ToStringTicksToPeriod(canUseDecimals: false).Named("POOR"),
                    Mathf.RoundToInt(progress.Process.qualityDays.normal * 60000).ToStringTicksToPeriod(canUseDecimals: false).Named("NORMAL"),
                    Mathf.RoundToInt(progress.Process.qualityDays.good * 60000).ToStringTicksToPeriod(canUseDecimals: false).Named("GOOD"),
                    Mathf.RoundToInt(progress.Process.qualityDays.excellent * 60000).ToStringTicksToPeriod(canUseDecimals: false).Named("EXCELLENT"),
                    Mathf.RoundToInt(progress.Process.qualityDays.masterwork * 60000).ToStringTicksToPeriod(canUseDecimals: false).Named("MASTERWORK"),
                    Mathf.RoundToInt(progress.Process.qualityDays.legendary * 60000).ToStringTicksToPeriod(canUseDecimals: false).Named("LEGENDARY")
                ));
            }
            else
            {
                if (Mouse.IsOver(qualityArea))
                {
                    GUI.color = ITab_Pawn_Gear.HighlightColor;
                    GUI.DrawTexture(qualityArea, TexUI.HighlightTex);
                }

                GUI.color = ITab_Pawn_Gear.ThingLabelColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(qualityArea, "UF_NA".Translate());

                qualityTip.AppendTagged("UF_QualityTooltipNA".Translate(progress.Process.thingDef.Named("PRODUCT")).CapitalizeFirst());
            }

            TooltipHandler.TipRegion(qualityArea, () => qualityTip.ToString(), "UF_QualityTooltip1".GetHashCode());

            GUI.color = Color.white;

            // Info buttons
            Widgets.InfoCardButton(ingredientArea.xMax - Pad - Icon, y, thing);
            Widgets.InfoCardButton(productArea.xMax - Pad - Icon, y, progress.Process.thingDef);

            // Hover over product or ingredient
            Rect ingredientHover = new Rect(ingredientArea.x, y, ingredientArea.width - PaddedIcon, H);
            if (Mouse.IsOver(ingredientHover))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(ingredientHover, TexUI.HighlightTex);
            }

            Rect productHover = new Rect(productArea.x, y, productArea.width - PaddedIcon, H);
            if (Mouse.IsOver(productHover))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(productHover, TexUI.HighlightTex);
            }

            GUI.color = Color.white;

            // Icons for product and ingredient
            if (thingDef.DrawMatSingle?.mainTexture != null)
                Widgets.ThingIcon(new Rect(ingredientArea.x, y, PaddedIcon, H), thing);

            if (progress.Process.thingDef?.DrawMatSingle?.mainTexture != null)
                Widgets.ThingIcon(new Rect(productArea.x, y, PaddedIcon, H), progress.Process.thingDef);

            // Product and ingredient labels
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = GenUI.LerpColor(WhiteToYellowToRed, 1.0f - progress.ruinedPercent);

            string ingredientLabel = count == thing.stackCount ? thing.LabelCap : GenLabel.ThingLabel(thingDef, null, count).CapitalizeFirst();
            string? productLabel = GenLabel.ThingLabel(progress.Process.thingDef, null, Mathf.RoundToInt(count * progress.Process.efficiency)).CapitalizeFirst();

            Text.WordWrap = false;
            Widgets.Label(new Rect(ingredientArea.x + PaddedIcon, y, 200 - PaddedIcon, H), ingredientLabel.Truncate(ingredientArea.width));
            Widgets.Label(new Rect(productArea.x + PaddedIcon, y, 200 - PaddedIcon, H), productLabel?.Truncate(productArea.width));

            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            string qualityStr = progress.Process.usesQuality ? $" ({progress.TargetQuality.GetLabel().CapitalizeFirst()})" : "";

            StringBuilder creatingTip = new StringBuilder();

            creatingTip.AppendTagged("UF_CreatingTooltip1".Translate(productLabel.Named("PRODUCT"), ingredientLabel.Named("INGREDIENT"), qualityStr.Named("QUALITY")));
            creatingTip.AppendTagged(progress.Process.usesQuality
                ? "UF_CreatingTooltip2_Quality".Translate(Mathf.RoundToInt(progress.Process.qualityDays.awful * 60000).ToStringTicksToPeriod().Named("TOAWFUL"))
                : "UF_CreatingTooltip2_NoQuality".Translate(Mathf.RoundToInt(progress.Process.processDays * 60000).ToStringTicksToPeriod().Named("TIME")));

            if (progress.Process.usesTemperature)
            {
                creatingTip.AppendTagged("UF_CreatingTooltip3".Translate(
                    progress.Process.temperatureIdeal.min.ToStringTemperature().Named("MIN"),
                    progress.Process.temperatureIdeal.max.ToStringTemperature().Named("MAX")));
                creatingTip.AppendTagged("UF_CreatingTooltip4".Translate(
                    progress.Process.temperatureSafe.min.ToStringTemperature().Named("MIN"),
                    progress.Process.temperatureSafe.max.ToStringTemperature().Named("MAX"),
                    (progress.Process.ruinedPerDegreePerHour / 100f).ToStringPercent().Named("PERHOUR")
                ));
            }

            if (progress.ruinedPercent > 0.05f)
            {
                creatingTip.AppendTagged("UF_CreatingTooltip5".Translate(progress.ruinedPercent.ToStringPercent().Colorize(ColoredText.RedReadable)));
            }

            if (!progress.Process.temperatureSafe.Includes(fermenter.parent.AmbientTemperature))
            {
                creatingTip.Append("UF_CreatingTooltip6".Translate(fermenter.parent.AmbientTemperature.ToStringTemperature()).Resolve().Colorize(ColoredText.RedReadable));
            }

            TooltipHandler.TipRegion(new Rect(productArea.x, y, productArea.width - PaddedIcon, H), () => creatingTip.ToString(), "UF_CreatingTooltip1".GetHashCode());
            TooltipHandler.TipRegion(new Rect(ingredientArea.x, y, productArea.width - PaddedIcon, H), () => creatingTip.ToString(), "UF_CreatingTooltip2".GetHashCode());

            y += H;
        }

        private IEnumerable<Widgets.DropdownMenuElement<QualityCategory>> GetProgressQualityDropdowns(UF_Progress? progress)
        {
            if (progress == null)
                yield break;

            foreach (QualityCategory quality in QualityUtility.AllQualityCategories)
            {
                Material iconMaterial = UF_Utility.qualityMaterials[quality];
                yield return new Widgets.DropdownMenuElement<QualityCategory>
                {
                    option = new FloatMenuOption(
                        quality.GetLabel().CapitalizeFirst(),
                        () => progress.TargetQuality = quality,
                        (Texture2D) iconMaterial.mainTexture,
                        iconMaterial.color),
                    payload = quality
                };
            }
        }
    }
}
