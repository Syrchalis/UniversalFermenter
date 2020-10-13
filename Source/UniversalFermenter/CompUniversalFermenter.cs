// Notes:
//   * parent.Map is null when the building (parent) is minified (uninstalled).

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenter
{
    public class CompUniversalFermenter : ThingComp, IThingHolder
    {
        private readonly Cacheable<string> compInspectStringExtra;
        private readonly Cacheable<Dictionary<Thing, Tuple<string, string>>> ingredientProductLabels;
        private readonly CacheableDict<ThingDef, int> spaceLeftFor;

        /// <summary>Possible flickable component on the fermenter.</summary>
        public CompFlickable? flickComp;

        /// <summary>Is a graphics change request queued?</summary>
        public bool graphicChangeQueued;

        public ThingOwner<Thing> innerContainer = null!;

        public ThingFilter ParentIngredientFilter { get; } = new ThingFilter();

        /// <summary>Possible power trader component on the fermenter.</summary>
        public CompPowerTrader? powerTradeComp;

        private Dictionary<ThingDef?, UF_Process> processesByProduct = null!;

        public List<UF_Progress> progresses = new List<UF_Progress>();

        /// <summary>Possible refuelable component on the fermenter.</summary>
        public CompRefuelable? refuelComp;

        /// <summary>Selected target quality for fermentation.</summary>
        public QualityCategory targetQuality = QualityCategory.Normal;

        /// <summary>Current filter for products the fermenter can make.</summary>
        public ThingFilter ProductFilter = new ThingFilter();

        /// <summary>Parent filter for products (includes all possible products).</summary>
        public ThingFilter ParentProductFilter = new ThingFilter();

        /// <summary>Current filter for ingredients the fermenter can use.</summary>
        public ThingFilter IngredientFilter;

        /// <summary>Filter for ingredients which combines IngredientFilter and ParentIngredientFilter.</summary>
        public Cacheable<ThingFilter> CombinedIngredientFilter;

        private BackwardsCompatibilityData? backwardsCompatibilityData;

        private int tickCounter;

        public CompUniversalFermenter()
        {
            IngredientCount = new Cacheable<int>(() => progresses.Sum(p => p.IngredientCount));
            SpaceLeft = new Cacheable<int>(() => Mathf.Max(0, MaxCapacity - IngredientCount));
            spaceLeftFor = new CacheableDict<ThingDef, int>(SpaceLeftForInternal);
            RoofCoverage = new Cacheable<float>(CalcRoofCoverage);
            compInspectStringExtra = new Cacheable<string>(GetCompInspectStringExtra);
            ingredientProductLabels = new Cacheable<Dictionary<Thing, Tuple<string, string>>>(() => innerContainer.ToDictionary(t => t, GetIngredientProductLabel));
            IngredientFilter = new ThingFilter(IngredientFilterChanged);
            CombinedIngredientFilter = new Cacheable<ThingFilter>(GetCombinedIngredientFilter);
        }

        /// <summary>Gets the component properties for this component.</summary>
        public CompProperties_UniversalFermenter Props => (CompProperties_UniversalFermenter) props;

        /// <summary>Gets whether the fermenter is empty.</summary>
        public bool Empty => progresses.Count == 0;

        /// <summary>The maximum number of ingredients that the fermenter can currently hold.</summary>
        public int MaxCapacity
        {
            get
            {
                if (progresses.Count > 0)
                {
                    return progresses[0].Process.processType != ProcessType.MultipleMixed
                        ? progresses[0].Process.maxCapacity
                        : Processes.Where(p => p.processType == ProcessType.MultipleMixed).Max(p => p.maxCapacity);
                }

                return Processes.Max(p => p.maxCapacity);
            }
        }

        /// <summary>Gets the amount of space left in the fermenter for more ingredients.</summary>
        public Cacheable<int> SpaceLeft { get; }

        /// <summary>The total number of ingredients in this fermenter.</summary>
        public Cacheable<int> IngredientCount { get; }

        public string Label => parent.Label;

        private List<UF_Process> Processes => Props.processes;

        public UF_Process? SingleProductDef
        {
            get
            {
                if (progresses.Count > 0 && progresses[0].Process.processType != ProcessType.MultipleMixed)
                    return progresses[0].Process;
                return Processes.Count == 1 && Processes[0].processType != ProcessType.MultipleMixed ? Processes[0] : null;
            }
        }

        /// <summary>How much of the building is under a roof.</summary>
        public Cacheable<float> RoofCoverage { get; }

        public IEnumerable<UF_Process> EnabledProcesses
        {
            get
            {
                foreach (ThingDef? product in ProductFilter.AllowedThingDefs)
                    yield return processesByProduct[product];
            }
        }

        public IEnumerable<ThingDef> AcceptedThings
        {
            get
            {
                foreach (UF_Process process in EnabledProcesses)
                foreach (ThingDef? ingredient in process.ingredientFilter.AllowedThingDefs)
                    yield return ingredient;
            }
        }

        public bool Fueled => refuelComp == null || refuelComp.HasFuel;

        public bool Powered => powerTradeComp == null || powerTradeComp.PowerOn;

        public bool FlickedOn => flickComp == null || flickComp.SwitchIsOn;

        public bool TemperatureOk
        {
            get
            {
                float temp = parent.AmbientTemperature;
                foreach (var process in EnabledProcesses)
                {
                    if (temp >= process.temperatureSafe.min - 2 || temp <= process.temperatureSafe.max + 2)
                        return true;
                }

                return false;
            }
        }

        public bool AnyFinished => progresses.Any(p => p.Finished);

        public bool AnyRuined => progresses.Any(p => p.Ruined);

        public bool AnyIngredientsOnMap => parent.Map.listerThings.ThingsMatching(IngredientFilter).Any();

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        private void ProductFilterChanged()
        {
            // Reset parent ingredient filter
            ParentIngredientFilter.SetDisallowAll();
            foreach (ThingDef def in AcceptedThings)
            {
                ParentIngredientFilter.SetAllow(def, true);
            }
        }

        private void IngredientFilterChanged()
        {
            CombinedIngredientFilter.Invalidate();
        }

        private ThingFilter GetCombinedIngredientFilter()
        {
            ThingFilter filter = new ThingFilter();
            filter.CopyAllowancesFrom(IngredientFilter);

            foreach (ThingDef def in filter.AllowedThingDefs.ToList())
            {
                if (!ParentIngredientFilter.Allows(def))
                    filter.SetAllow(def, false);
            }

            return filter;
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);

            processesByProduct = Processes.ToDictionary(x => x.thingDef, x => x);
            refuelComp = parent.GetComp<CompRefuelable>();
            powerTradeComp = parent.GetComp<CompPowerTrader>();
            flickComp = parent.GetComp<CompFlickable>();
            innerContainer = new ThingOwner<Thing>(this);
            ProductFilter = new ThingFilter(ProductFilterChanged);
            ProductFilter.CopyAllowancesFrom(Props.defaultFilter);

            parent.def.inspectorTabsResolved ??= new List<InspectTabBase>();

            if (!parent.def.inspectorTabsResolved.Any(t => t is ITab_UFContents))
            {
                if (Props.processes.Count > 1)
                    parent.def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_UFProductFilter)));
                parent.def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_UFIngredientFilter)));
                parent.def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_UFContents)));
            }

            // Defaults for filters
            foreach(UF_Process process in Props.processes)
            {
                ParentProductFilter.SetAllow(process.thingDef, true);
            }

            ProductFilterChanged();

            foreach (ThingDef def in Props.processes.SelectMany(x => x.ingredientFilter.AllowedThingDefs))
            {
                IngredientFilter.SetAllow(def, true);
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            parent.Map.GetComponent<MapComponent_UF>().Register(parent);

            if (!Empty)
                graphicChangeQueued = true;
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            map.GetComponent<MapComponent_UF>().Deregister(parent);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            //Dev options			
            if (Prefs.DevMode)
                yield return UF_Utility.DebugGizmo();

            //Default buttons
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (Processes.Any(process => process.usesQuality))
                yield return UF_Utility.qualityGizmos[targetQuality];

            foreach (var gizmo in UF_Clipboard.CopyPasteGizmosFor(this))
                yield return gizmo;
        }

        public override void PostDraw()
        {
            base.PostDraw();

            if (!Empty)
            {
                if (graphicChangeQueued)
                {
                    GraphicChange(false);
                    graphicChangeQueued = false;
                }

                bool showCurrentQuality = progresses[0].Process.processType == ProcessType.Single && progresses[0].Process.usesQuality && UF_Settings.showCurrentQualityIcon;
                Vector3 drawPos = parent.DrawPos;
                drawPos.x += Props.barOffset.x - (showCurrentQuality ? 0.1f : 0f);
                drawPos.y += 0.05f;
                drawPos.z += Props.barOffset.y;

                Vector2 size = Static_Bar.Size * Props.barScale;

                // Border
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(size.x + 0.1f, 1, size.y + 0.1f)), Static_Bar.UnfilledMat, 0);

                float xPosAccum = 0;
                for (int i = 0; i < progresses.Count; i++)
                {
                    UF_Progress? progress = progresses[i];
                    float width = size.x * ((float) progress.IngredientCount / MaxCapacity);
                    float xPos = (drawPos.x - (size.x / 2.0f)) + (width / 2.0f) + xPosAccum;
                    xPosAccum += width;
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(xPos, drawPos.y + 0.01f, drawPos.z), Quaternion.identity, new Vector3(width, 1, size.y)), progress.ProgressColorMaterial, 0);
                }

                if (showCurrentQuality) // show small icon for current quality over bar
                {
                    drawPos.y += 0.02f;
                    drawPos.x += 0.45f * Props.barScale.x;
                    Matrix4x4 matrix2 = default;
                    matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.2f * Props.barScale.x, 1f, 0.2f * Props.barScale.y));
                    Graphics.DrawMesh(MeshPool.plane10, matrix2, UF_Utility.qualityMaterials[progresses[0].CurrentQuality], 0);
                }
            }

            UF_Process? singleProduct = SingleProductDef;
            if (UF_Settings.showProcessIconGlobal && Props.showProductIcon && singleProduct != null)
            {
                Vector3 drawPos = parent.DrawPos;
                float sizeX = UF_Settings.processIconSize * Props.productIconSize.x;
                float sizeZ = UF_Settings.processIconSize * Props.productIconSize.y;
                if (Processes.Count == 1 && !Empty && singleProduct.usesQuality) // show larger, centered quality icon if object has only one process
                {
                    drawPos.y += 0.02f;
                    drawPos.z += 0.05f;
                    Matrix4x4 matrix = default;
                    matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(0.6f * sizeX, 1f, 0.6f * sizeZ));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, UF_Utility.qualityMaterials[progresses[0].TargetQuality], 0);
                }
                else if (Processes.Count > 1 && !Empty) // show process icon if object has more than one process
                {
                    drawPos.y += 0.02f;
                    drawPos.z += 0.05f;
                    Matrix4x4 matrix = default;
                    matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(sizeX, 1f, sizeZ));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, UF_Utility.processMaterials[singleProduct], 0);
                    if (progresses.Count > 0 && singleProduct.usesQuality && UF_Settings.showTargetQualityIcon) // show small offset quality icon if object also uses quality
                    {
                        drawPos.y += 0.01f;
                        drawPos.x += 0.25f * sizeX;
                        drawPos.z -= 0.35f * sizeZ;
                        Matrix4x4 matrix2 = default;
                        matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.4f * sizeX, 1f, 0.4f * sizeZ));
                        Graphics.DrawMesh(MeshPool.plane10, matrix2, UF_Utility.qualityMaterials[progresses[0].TargetQuality], 0);
                    }
                }
            }
        }

        // Inspector string eats max. 5 lines - there is room for one more
        public override string CompInspectStringExtra()
        {
            return compInspectStringExtra;
        }

        private string GetCompInspectStringExtra()
        {
            // Perf: Only recalculate this inspect string periodically
            if (progresses.Count == 0)
                return "UF_NoIngredient".TranslateSimple();

            StringBuilder str = new StringBuilder();

            // Line 1. Show the current number of items in the fermenter
            UF_Process? singleDef = SingleProductDef;
            if (singleDef != null)
            {
                if (singleDef.processType == ProcessType.Single && progresses.Count == 1 && singleDef.usesQuality && progresses[0].ProgressDays >= singleDef.qualityDays.awful)
                {
                    UF_Progress progress = progresses[0];
                    str.AppendTagged("UF_ContainsProduct".Translate(MaxCapacity - SpaceLeft, MaxCapacity, singleDef.thingDef.Named("PRODUCT"), progress.CurrentQuality.GetLabel().ToLower().Named("QUALITY")));
                }
                else
                {
                    // Usually this will only be one def label shown
                    string ingredientLabels = singleDef.ingredientFilter.AllowedThingDefs.Select(d => d.label).Join();
                    str.AppendTagged("UF_ContainsIngredient".Translate(MaxCapacity - SpaceLeft, MaxCapacity, ingredientLabels.Named("INGREDIENTS")));
                }
            }
            else
            {
                str.AppendTagged("UF_ContainsIngredientsGeneric".Translate(MaxCapacity - SpaceLeft, MaxCapacity));
            }

            str.AppendLine();

            // Line 2. Show how many processes are running, or the current status of the process
            if (singleDef == null || singleDef.processType == ProcessType.Multiple)
            {
                int running = progresses.Count(p => p.Running);
                str.AppendTagged("UF_NumProcessing".Translate(running, running == 1
                    ? "UF_RunningStacksNoun".Translate().Named("STACKS")
                    : Find.ActiveLanguageWorker.Pluralize("UF_RunningStacksNoun".Translate(), running).Named("STACKS")));

                int slow = progresses.Count(p => p.CurrentSpeedFactor < UF_Progress.SlowAtSpeedFactor);
                if (slow > 0)
                    str.AppendTagged("UF_RunningCountSlow".Translate(slow));

                int finished = progresses.Count(p => p.Finished);
                if (finished > 0)
                    str.AppendTagged("UF_RunningCountFinished".Translate(finished));

                int ruined = progresses.Count(p => p.Ruined);
                if (ruined > 0)
                    str.AppendTagged("UF_RunningCountRuined".Translate(ruined));
            }
            else
            {
                if (progresses[0].Finished)
                    str.AppendTagged("UF_Finished".Translate());
                else if (progresses[0].Ruined)
                    str.AppendTagged("UF_Ruined".Translate());
                else if (progresses[0].CurrentSpeedFactor < UF_Progress.SlowAtSpeedFactor)
                    str.AppendTagged("UF_RunningSlow".Translate(progresses[0].CurrentSpeedFactor.ToStringPercent(), progresses[0].ProgressPercentFlooredString.Value));
                else
                    str.AppendTagged("UF_RunningInfo".Translate(progresses[0].ProgressPercentFlooredString.Value));
            }

            str.AppendLine();

            if (progresses.Any(p => p.Process.usesTemperature))
            {
                // Line 3. Show the ambient temperature, and if overheating/freezing
                float ambientTemperature = parent.AmbientTemperature;
                str.AppendFormat("{0}: {1}", "Temperature".TranslateSimple(), ambientTemperature.ToStringTemperature("F0"));

                if (singleDef != null)
                {
                    if (singleDef.temperatureSafe.Includes(ambientTemperature))
                    {
                        str.AppendFormat(" ({0})", singleDef.temperatureIdeal.Includes(ambientTemperature) ? "UF_Ideal".TranslateSimple() : "UF_Safe".TranslateSimple());
                    }
                    else if (!Empty)
                    {
                        bool overheating = ambientTemperature < singleDef.temperatureSafe.TrueMin; 
                        str.AppendFormat(" ({0}{1})".Colorize(overheating ? ColoredText.RedReadable : Color.blue),
                            overheating ? "Freezing".TranslateSimple() : "Overheating".TranslateSimple(),
                            progresses.Count == 1 && progresses[0].Process.processType == ProcessType.Single ? $" {progresses[0].ruinedPercent.ToStringPercent()}" : "");
                    }
                }
                else if (progresses.Count > 0)
                {
                    bool abort = false;
                    foreach (UF_Progress progress in progresses)
                    {
                        if (ambientTemperature > progress.Process.temperatureSafe.TrueMax)
                        {
                            str.AppendFormat(" ({0})", "Freezing".TranslateSimple());
                            abort = true;
                            break;
                        }

                        if (ambientTemperature < progress.Process.temperatureSafe.TrueMin)
                        {
                            str.AppendFormat(" ({0})", "Overheating".TranslateSimple());
                            abort = true;
                            break;
                        }
                    }

                    if (!abort)
                    {
                        foreach (UF_Progress progress in progresses)
                        {
                            if (progress.Process.temperatureIdeal.Includes(ambientTemperature))
                            {
                                str.AppendFormat(" ({0})", "UF_Safe".TranslateSimple());
                                abort = true;
                                break;
                            }
                        }
                    }

                    if (!abort)
                    {
                        str.AppendFormat(" ({0})", "UF_Idea".TranslateSimple());
                    }
                }

                str.AppendLine();

                // Line 4. Ideal temp range
                if (singleDef != null && singleDef.usesTemperature)
                {
                    str.AppendFormat("{0}: {1}~{2} ({3}~{4})", "UF_IdealSafeProductionTemperature".TranslateSimple(),
                        singleDef.temperatureIdeal.min.ToStringTemperature("F0"),
                        singleDef.temperatureIdeal.max.ToStringTemperature("F0"),
                        singleDef.temperatureSafe.min.ToStringTemperature("F0"),
                        singleDef.temperatureSafe.max.ToStringTemperature("F0"));
                }
            }

            return str.ToString().TrimEndNewlines();
        }

        private float CalcRoofCoverage()
        {
            if (parent.Map == null) return 0f;

            int allTiles = 0;
            int roofedTiles = 0;
            foreach (IntVec3 current in parent.OccupiedRect())
            {
                allTiles++;
                if (parent.Map.roofGrid.Roofed(current))
                    roofedTiles++;
            }

            return roofedTiles / (float) allTiles;
        }

        public override void CompTick()
        {
            DoTicks(1);
        }

        public override void CompTickRare()
        {
            DoTicks(GenTicks.TickRareInterval);
        }

        public override void CompTickLong()
        {
            DoTicks(GenTicks.TickLongInterval);
        }

        public Tuple<string, string> GetIngredientProductLabels(Thing thing)
        {
            return ingredientProductLabels.Value.TryGetValue(thing, out Tuple<string, string> labels) ? labels : Tuple.Create("Unknown", "Unknown");
        }

        public void DoTicks(int ticks)
        {
            tickCounter += ticks;

            if (backwardsCompatibilityData is not null)
                ApplyBackwardsCompatibility();

            if (!Fueled || !Powered || !FlickedOn)
                return;

            foreach (UF_Progress progress in progresses)
                progress.DoTicks(ticks);

            if (refuelComp?.Props.consumeFuelOnlyWhenUsed == true && !Empty)
                refuelComp.ConsumeFuel((refuelComp.Props.fuelConsumptionRate / GenDate.TicksPerDay) * ticks);

            if (tickCounter >= GenTicks.TickRareInterval)
            {
                while(tickCounter >= GenTicks.TickRareInterval)
                    tickCounter -= GenTicks.TickRareInterval;

                CachesInvalid(true);

                foreach(UF_Progress progress in progresses)
                {
                    progress.TickRare();
                }
            }
        }

        public UF_Process? GetProcess(ThingDef ingredient)
        {
            foreach (UF_Process? process in EnabledProcesses)
            {
                if (process.ingredientFilter.Allows(ingredient))
                    return process;
            }

            return null;
        }

        public void AddIngredient(Thing ingredient)
        {
            try
            {
                UF_Process? process = GetProcess(ingredient.def);
                if (process == null)
                    throw new UFException($"Tried to add {ingredient} to {Label}, but no process accepts that as an ingredient.");

                if (ingredient.stackCount > SpaceLeft)
                    throw new UFException($"Tried to add {ingredient} ×{ingredient.stackCount} to {Label}, but fermenter only accepts {SpaceLeft} more ingredients.");

                UF_Progress? existingProgress = progresses.Find(p => p.Process == process);
                bool wasEmpty = Empty;

                if (existingProgress == null)
                    TryAddIngredientNewProgress(ingredient, process);
                else
                    TryAddIngredientExistingProcess(ingredient, process, existingProgress);

                if (wasEmpty && !Empty)
                    GraphicChange(false);

                CachesInvalid();
            }
            catch (UFException ex)
            {
                Log.Warning(ex.Message);
                ingredient.Destroy();
            }
        }

        public void CachesInvalid(bool rareTick = false)
        {
            if (rareTick)
            {
                // Check periodically
                RoofCoverage.Invalidate();
            }
            else
            {
                // Only update when contents have changed
                spaceLeftFor.Invalidate();
                ingredientProductLabels.Invalidate();
                IngredientCount.Invalidate();
                SpaceLeft.Invalidate();
            }

            compInspectStringExtra.Invalidate();
        }

        private void TryAddIngredientNewProgress(Thing ingredient, UF_Process process)
        {
            if (progresses.Count > 0 && progresses[0].Process.processType == ProcessType.Single)
                throw new UFException($"Tried to add non-compatible ingredient {ingredient} to single-process of {Label} which creates {progresses[0].Process.thingDef}.");

            if (progresses.Count > 0 && process.processType != ProcessType.MultipleMixed && progresses.Any(p => p.Process.processType != ProcessType.MultipleMixed))
                throw new UFException($"Tried to add ingredient {ingredient} to {Label} for new process making {process}, but there are already running processes without process type MultipleMixed.");

            if (progresses.Count > 0 && process.processType == ProcessType.Multiple && progresses.Any(p => p.Process != process))
                throw new UFException($"Tried to add ingredient {ingredient} to {Label} for new process making {process}, but there are existing processes that do not make {process}.");

            AddIngredientNewProgress(ingredient, process);
        }

        private void TryAddIngredientExistingProcess(Thing ingredient, UF_Process process, UF_Progress existingProgress)
        {
            if (existingProgress.Process != process || process.ingredientFilter.Allows(ingredient) == false)
                throw new UFException($"Tried to add ingredient {ingredient} to {Label} for existing process creating {existingProgress.Process} - invalid configuration.");

            if (process.processType == ProcessType.Single && existingProgress.Finished)
                throw new UFException($"Tried to add ingredient {ingredient} to {Label} to Single process creating {existingProgress.Process}, but existing progress is already finished.");

            if (process.processType == ProcessType.Single && existingProgress.IngredientCount == process.maxCapacity)
                throw new UFException($"Tried to add ingredient {ingredient} to {Label} to single process creating {existingProgress.Process}, but existing progress is already full.");

            if (process.processType == ProcessType.Single)
                AddIngredientExistingProcess(ingredient, existingProgress);
            else
                AddIngredientNewProgress(ingredient, process);
        }

        private void AddIngredientNewProgress(Thing ingredient, UF_Process process)
        {
            AcceptIngredientThing(ingredient);
            progresses.Add(new UF_Progress(this)
            {
                ProcessIndex = Processes.IndexOf(process),
                TargetQuality = targetQuality,
                storedThings = new List<Thing> { ingredient }
            });
        }

        private void AddIngredientExistingProcess(Thing ingredient, UF_Progress progress)
        {
            AcceptIngredientThing(ingredient);
            progress.AddThing(ingredient);
            progress.progressTicks = Mathf.RoundToInt(GenMath.WeightedAverage(0f, ingredient.stackCount, progress.progressTicks, progress.IngredientCount));
        }

        private void AcceptIngredientThing(Thing? ingredient)
        {
            if (ingredient == null)
                throw new UFException($"Tried to add null ingredient to innerContainer of {Label}.");

            bool added = ingredient.holdingOwner?.TryTransferToContainer(ingredient, innerContainer, false)
                         ?? innerContainer.TryAdd(ingredient, false);

            if (!added)
                throw new UFException($"Tried to add ingredient {ingredient} to innerContainer of {Label} but it did not accept the item.");
        }

        private Tuple<string, string> GetIngredientProductLabel(Thing? ingredient)
        {
            // Perf: Only calculate these strings once
            if (ingredient is null)
                return Tuple.Create("Unknown", "Unknown");

            UF_Progress? progress = progresses.Find(p => p.storedThings.Contains(ingredient));

            if (progress is null)
                return Tuple.Create("Unknown", "Unknown");

            return Tuple.Create(
                ingredient.LabelCap,
                GenLabel.ThingLabel(progress.Process.thingDef, null, Mathf.RoundToInt(ingredient.stackCount * progress.Process.efficiency)).CapitalizeFirst());
        }

        public Thing? TakeOutProduct(UF_Progress progress)
        {
            try
            {
                if (!progresses.Contains(progress) || progress.ProcessIndex > Processes.Count - 1)
                    throw new UFException("Cannot take product from this fermenter - progress is invalid.");

                UF_Process process = progress.Process;

                if (!progress.Finished && !process.usesQuality && !progress.Ruined)
                    throw new UFException($"Tried to get product {process} from {Label}, but it is not done fermenting yet ({progress.ProgressPercent.ToStringPercent()}).");

                if (process.usesQuality && !progress.Ruined && progress.CurrentQuality < progress.TargetQuality)
                    throw new UFException($"Tried to get product {process} from {Label}, but it has not reached the target quality yet (is {progress.CurrentQuality}, wants {progress.TargetQuality}");

                Thing? thing = null;
                if (!progress.Ruined)
                {
                    thing = ThingMaker.MakeThing(process.thingDef);

                    CompIngredients compIngredients = thing.TryGetComp<CompIngredients>();
                    if (compIngredients != null && !progress.Ingredients.Any())
                        compIngredients.ingredients.AddRange(progress.Ingredients);

                    if (process.usesQuality)
                    {
                        CompQuality compQuality = thing.TryGetComp<CompQuality>();
                        compQuality?.SetQuality(progress.CurrentQuality, ArtGenerationContext.Colony);
                    }

                    thing.stackCount = Mathf.RoundToInt(progress.IngredientCount * process.efficiency);

                    if (thing.stackCount == 0)
                        throw new UFException($"Tried to get product {process} from {Label}, but stack count ended up as 0.");
                }

                foreach (var ingredient in progress.storedThings)
                {
                    innerContainer.Remove(ingredient);
                    ingredient.Destroy();
                }

                progresses.Remove(progress);

                if (Empty)
                    GraphicChange(true);

                CachesInvalid();

                return thing;
            }
            catch (UFException ex)
            {
                Log.Warning(ex.Message);
                return null;
            }
        }

        public int SpaceLeftFor(ThingDef def)
        {
            return spaceLeftFor.Get(def);
        }

        public int SpaceLeftFor(Thing thing)
        {
            int spaceLeft = SpaceLeftFor(thing.def);
            return spaceLeft == 0 || CombinedIngredientFilter.Value.Allows(thing) == false ? 0 : spaceLeft;
        }

        private int SpaceLeftForInternal(ThingDef def)
        {
            UF_Process? process = GetProcess(def);
            if (SpaceLeft == 0 || process == null)
                return 0;

            if (progresses.Count > 0 && (progresses[0].Process.processType != ProcessType.MultipleMixed || process.processType != ProcessType.MultipleMixed) && process != progresses[0].Process)
                return 0; // Has Single or Multiple of different type, no space for this
            
            return Mathf.Max(0, process.maxCapacity - IngredientCount);
        }

        public void Reset()
        {
            progresses.Clear();

            // Drop all droppable ingredients
            foreach (UF_Process? process in Processes)
            {
                if (!process.incompleteProductsCanBeRetrieved)
                    continue;
                foreach (ThingDef? ingredient in process.ingredientFilter.AllowedThingDefs)
                foreach (Thing? thing in innerContainer.InnerListForReading.ToList())
                {
                    if (thing.def != ingredient)
                        continue;
                    innerContainer.TryDrop(thing, parent.Position, parent.Map, ThingPlaceMode.Near, thing.stackCount, out _);
                }
            }

            innerContainer.ClearAndDestroyContents();

            GraphicChange(true);
            CachesInvalid();
        }

        public void GraphicChange(bool toEmpty)
        {
            if (Processes.All(p => p.graphicSuffix == null))
                return;

            string? texPath = parent.def.graphicData.texPath;
            string? suffix = progresses.FirstOrDefault()?.Process.graphicSuffix;

            if (!toEmpty && suffix != null)
                texPath += suffix;

            parent.ReloadGraphic(texPath);
        }

        public UF_Progress? GetProgress(Thing thing)
        {
            foreach (var progress in progresses)
            {
                foreach (var progressThing in progress.storedThings)
                {
                    if (progressThing == thing)
                        return progress;
                }
            }

            return null;
        }

#pragma warning disable 618
        public override void PostExposeData()
        {
            try
            {
                Scribe_Values.Look(ref targetQuality, "targetQuality", QualityCategory.Normal);

                Scribe_Deep.Look(ref innerContainer, "UF_innerContainer", this);
                Scribe_Collections.Look(ref progresses, "UF_progresses", LookMode.Deep, this);
                Scribe_Deep.Look(ref ProductFilter, "UF_productFilter");
                Scribe_Deep.Look(ref IngredientFilter, "UF_ingredientFilter");

                BackwardsCompatibilityUpdate();

                ProductFilterChanged();
            }
            catch (UFException ex)
            {
                Log.Warning(ex.Message);
            }
        }

        class BackwardsCompatibilityData
        {
            public float ruinedPercent;
            public int ingredientCount;
            public int progressTicks;
            public int currentProcessIndex;
            public int queuedProcessIndex;
        }

        private void BackwardsCompatibilityUpdate()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (progresses != null)
                return;

            BackwardsCompatibilityData data = new BackwardsCompatibilityData();

            Scribe_Values.Look(ref data.ruinedPercent, "ruinedPercent");
            Scribe_Values.Look(ref data.ingredientCount, "UF_UniversalFermenter_IngredientCount");
            Scribe_Values.Look(ref data.progressTicks, "UF_progressTicks");
            Scribe_Values.Look(ref data.currentProcessIndex, "UF_currentResourceInd");
            Scribe_Values.Look(ref data.queuedProcessIndex, "UF_queuedProcessIndex");

            backwardsCompatibilityData = data;

            progresses = new List<UF_Progress>();
            innerContainer = new ThingOwner<Thing>(this);
            ProductFilter = new ThingFilter(ProductFilterChanged);
            ProductFilter.CopyAllowancesFrom(Props.defaultFilter);

            IngredientFilter = new ThingFilter(IngredientFilterChanged);
            foreach (ThingDef def in Props.processes.SelectMany(x => x.ingredientFilter.AllowedThingDefs))
            {
                IngredientFilter.SetAllow(def, true);
            }
        }

        private void ApplyBackwardsCompatibility()
        {
            if (backwardsCompatibilityData == null)
                return;

            try
            {
                BackwardsCompatibilityData data = backwardsCompatibilityData;

                if (progresses.Count > 0 || data.ingredientCount == 0)
                    return;

                UF_Process? process = Processes[data.currentProcessIndex];
                if (process == null)
                    throw new UFException($"Tried to upgrade fermenter {Label} but the current process index was invalid.");

                Thing? ingredients = ThingMaker.MakeThing(process.ingredientFilter.AllowedThingDefs.FirstOrDefault());

                if (ingredients == null)
                    throw new UFException($"Tried to upgrade fermenter {Label} creating {process}, but could not find any ingredients.");

                ingredients.stackCount = data.ingredientCount;

                AcceptIngredientThing(ingredients);

                progresses.Add(new UF_Progress(this)
                {
                    ruinedPercent = data.ruinedPercent,
                    ProcessIndex = data.currentProcessIndex,
                    TargetQuality = targetQuality,
                    storedThings = { ingredients },
                    ProgressTicks = data.progressTicks
                });

                GraphicChange(false);
            }
            catch (UFException ex)
            {
                Log.Warning(ex.Message);
            }
            finally
            {
                backwardsCompatibilityData = null;
            }

        }
#pragma warning restore 618
    }
}
