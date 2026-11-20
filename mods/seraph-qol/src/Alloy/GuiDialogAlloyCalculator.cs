using SeraphQoL.Config;
using SystemUI;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SeraphQoL.Alloy;

/// <summary>
/// Ventana acoplada a la fogata para planificar aleaciones/metales sin desperdicio y depositar crisol y combustible.
/// </summary>
public sealed class GuiDialogAlloyCalculator : UiDialogBase
{
    private static readonly int[] StepMultipliers = [1, 5, 10, 100];

    private readonly SeraphQoLModSystem _modSystem;
    private readonly AlloyDepositService _depositService;

    private GuiDialogBlockEntityFirepit? _firepitDialog;
    private IReadOnlyList<AlloyRecipeSpec> _recipes = Array.Empty<AlloyRecipeSpec>();
    private int _recipeIndex;
    private double[] _percentages = Array.Empty<double>();
    private int _targetUnits = 100;
    private int _stepMultiplierIndex;
    private int _fuelIndex;

    public GuiDialogAlloyCalculator(ICoreClientAPI capi, SeraphQoLModSystem modSystem, AlloyDepositService depositService)
        : base(capi)
    {
        _modSystem = modSystem;
        _depositService = depositService;
    }

    protected override string ComposerKey => "seraphqol-alloy-calculator";
    protected override string DialogTitle => SeraphQoLLang.Get("alloy-ui-title");
    protected override double DialogWidth => 352;
    protected override double DialogHeight => 378;
    public override bool PrefersUngrabbedMouse => false;
    public override double DrawOrder => 0.25;

    public void AttachToFirepit(GuiDialogBlockEntityFirepit firepitDialog)
    {
        if (!_modSystem.ClientConfig.EnableAlloyCalculator) return;

        _firepitDialog = firepitDialog;
        if (_recipes.Count == 0)
        {
            _recipes = _depositService.LoadRecipes();
            SelectRecipe(0);
        }

        FirepitContextSnapshot snapshot = _depositService.InspectFirepit(firepitDialog);
        if (!snapshot.HasCrucible)
        {
            if (IsOpened()) TryClose();
            return;
        }

        RequestScanIfNearbyEnabled();

        if (!IsOpened())
        {
            TryOpen();
        }
        else
        {
            RefreshView();
        }
    }

    public void DetachFromFirepit(GuiDialogBlockEntityFirepit firepitDialog)
    {
        if (ReferenceEquals(_firepitDialog, firepitDialog))
        {
            _firepitDialog = null;
            if (IsOpened()) TryClose();
        }
    }

    public void RefreshIfOpen(GuiDialogBlockEntityFirepit firepitDialog)
    {
        if (!_modSystem.ClientConfig.EnableAlloyCalculator) return;

        if (ReferenceEquals(_firepitDialog, firepitDialog) || _firepitDialog == null)
        {
            _firepitDialog = firepitDialog;
            FirepitContextSnapshot snap = _depositService.InspectFirepit(firepitDialog);
            if (!snap.HasCrucible)
            {
                if (IsOpened()) TryClose();
                return;
            }

            if (!IsOpened()) TryOpen();
            else RefreshView();
        }
    }

    public void RefreshForFirepitPos(BlockPos pos)
    {
        if (_firepitDialog?.BlockEntityPosition != null && _firepitDialog.BlockEntityPosition.Equals(pos))
        {
            RefreshView();
        }
    }

    protected override ElementBounds CreateDialogBounds()
    {
        ElementBounds? fb = _firepitDialog?.SingleComposer?.Bounds;
        if (fb == null || fb.OuterWidth <= 0)
        {
            return ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-24, 0);
        }

        double scale = Math.Max(0.25, GuiElement.scaled(1.0));
        double screenW = capi.Render.FrameWidth / scale;
        double screenH = capi.Render.FrameHeight / scale;

        double firepitLeft = fb.absX / scale;
        double firepitTop = fb.absY / scale;
        double firepitWidth = fb.OuterWidth / scale;

        double outerW = DialogWidth + (Theme.DialogPadding * 2);
        double outerH = DialogHeight + GuiStyle.TitleBarHeight + Theme.SectionGap + (Theme.DialogPadding * 2);

        double dockX = firepitLeft + firepitWidth + 12;
        if (dockX + outerW + 12 > screenW)
        {
            dockX = firepitLeft - outerW - 12;
        }

        dockX = Math.Clamp(dockX, 12, Math.Max(12, screenW - outerW - 12));
        double dockY = Math.Clamp(firepitTop, 12, Math.Max(12, screenH - outerH - 12));

        return ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedAlignmentOffset(dockX, dockY);
    }

    protected override void ComposeContent(GuiComposer composer, ElementBounds contentBounds)
    {
        if (_recipes.Count == 0)
        {
            _recipes = _depositService.LoadRecipes();
        }
        if (_recipes.Count == 0) return;

        int selectedIdx = Math.Clamp(_recipeIndex, 0, _recipes.Count - 1);
        AlloyRecipeSpec recipe = _recipes[selectedIdx];
        if (_percentages.Length != recipe.Ingredients.Count)
        {
            _percentages = AlloyCalculatorLogic.BuildDefaultPercentages(recipe);
        }

        FirepitContextSnapshot snapshot = _firepitDialog != null
            ? _depositService.InspectFirepit(_firepitDialog)
            : new FirepitContextSnapshot(false, 20, Array.Empty<CrucibleMetalSnapshot>(), Array.Empty<SmeltableMetalItem>(), Array.Empty<FuelOptionSpec>());

        AlloyBatchPlan plan = AlloyCalculatorLogic.CalculateBatchPlan(
            recipe,
            _percentages,
            _targetUnits,
            snapshot.CrucibleContents,
            snapshot.AvailableItems,
            _modSystem.ClientConfig.AlloyUnitsPerNugget);

        double w = DialogWidth;
        double y = contentBounds.fixedY;

        // Lista select de materiales / aleaciones
        var values = new string[_recipes.Count];
        var names = new string[_recipes.Count];
        for (int i = 0; i < _recipes.Count; i++)
        {
            values[i] = i.ToString();
            names[i] = _recipes[i].OutputName;
        }

        composer.AddDropDown(
            values,
            names,
            selectedIdx,
            (code, selected) =>
            {
                if (selected && int.TryParse(code, out int nextIdx) && nextIdx != _recipeIndex)
                {
                    SelectRecipe(nextIdx);
                    capi.Event.EnqueueMainThreadTask(() => RefreshView(), "seraphqol-alloy-select");
                }
            },
            ElementBounds.Fixed(0, y, w, 28),
            Theme.BodyFont(),
            "ddlRecipe");
        y += 34;

        // Target units (+/- sin desperdicio con multiplicador de paso x1 / x5 / x10 / x100)
        string ingotInfo = $"{plan.TargetOutputUnits}u ({plan.TargetOutputUnits / 100.0:0.##}x)";
        composer.AddActionButton("-", () =>
        {
            int currentStep = StepMultipliers[Math.Clamp(_stepMultiplierIndex, 0, StepMultipliers.Length - 1)];
            _targetUnits = AlloyCalculatorLogic.FindNextWasteFreeUnits(recipe, _percentages, _targetUnits, -1, _modSystem.ClientConfig.AlloyUnitsPerNugget, currentStep);
            capi.Gui.PlaySound("tick");
            RefreshView();
            return true;
        }, ElementBounds.Fixed(0, y, 30, 24), "btnTargetDown");
        composer.AddDynamicText($"{SeraphQoLLang.Get("alloy-target-label")}: {ingotInfo}",
            Theme.BodyFont().WithOrientation(EnumTextOrientation.Center),
            ElementBounds.Fixed(36, y + 2, w - 72, 22), "txtTargetUnits");
        composer.AddActionButton("+", () =>
        {
            int currentStep = StepMultipliers[Math.Clamp(_stepMultiplierIndex, 0, StepMultipliers.Length - 1)];
            _targetUnits = AlloyCalculatorLogic.FindNextWasteFreeUnits(recipe, _percentages, _targetUnits, 1, _modSystem.ClientConfig.AlloyUnitsPerNugget, currentStep);
            capi.Gui.PlaySound("tick");
            RefreshView();
            return true;
        }, ElementBounds.Fixed(w - 30, y, 30, 24), "btnTargetUp");
        y += 28;

        double gap = 6;
        double multW = (w - (gap * (StepMultipliers.Length - 1))) / StepMultipliers.Length;
        var multLabels = new[] { "x1", "x5", "x10", "x100" };
        var multBounds = new ElementBounds[StepMultipliers.Length];
        for (int i = 0; i < StepMultipliers.Length; i++)
        {
            multBounds[i] = ElementBounds.Fixed(i * (multW + gap), y, multW, 24);
        }

        int activeMultIdx = Math.Clamp(_stepMultiplierIndex, 0, StepMultipliers.Length - 1);
        composer.AddTextToggleButtons(
            multLabels,
            Theme.BodyFont().WithFontSize(12),
            idx =>
            {
                _stepMultiplierIndex = Math.Clamp(idx, 0, StepMultipliers.Length - 1);
                capi.Gui.PlaySound("tick");
            },
            multBounds,
            "btnStepMult");
        composer.ToggleButtonsSetValue("btnStepMult", activeMultIdx);
        y += 30;

        composer.AddPanelCard(0, y, w, 160, Theme);
        double cardY = y + 8;

        for (int i = 0; i < plan.Needs.Count && i < 4; i++)
        {
            int idx = i;
            AlloyIngredientNeed need = plan.Needs[i];
            int pctRounded = (int)Math.Round(need.Ratio * 100.0);
            string line = $"{need.Label}: {pctRounded}% | +{need.ItemsToDeposit}/{need.ItemsNeededTotal} ({need.AvailableItemCount})";

            CairoFont ingFont = need.MissingUnitsAfterDeposit == 0
                ? Theme.PositiveFont(detail: false).WithFontSize(13)
                : Theme.Font([0.96, 0.95, 0.92, 1.0]).WithFontSize(13);

            composer.AddStaticText(line, ingFont,
                ElementBounds.Fixed(10, cardY + 2, w - 78, 22), $"txtIng{i}");

            if (plan.Needs.Count > 1)
            {
                composer.AddActionButton("-", () =>
                {
                    _percentages = AlloyCalculatorLogic.AdjustIngredientPercentage(recipe, _percentages, idx, _percentages[idx] - 0.02);
                    capi.Gui.PlaySound("tick");
                    RefreshView();
                    return true;
                }, ElementBounds.Fixed(w - 64, cardY, 24, 22), $"btnIngMinus{i}");

                composer.AddActionButton("+", () =>
                {
                    _percentages = AlloyCalculatorLogic.AdjustIngredientPercentage(recipe, _percentages, idx, _percentages[idx] + 0.02);
                    capi.Gui.PlaySound("tick");
                    RefreshView();
                    return true;
                }, ElementBounds.Fixed(w - 36, cardY, 24, 22), $"btnIngPlus{i}");
            }

            cardY += 28;
        }

        string statusText = plan.CanFulfillFromInventory
            ? SeraphQoLLang.Get("alloy-status-ready", plan.RequiredMeltTempC)
            : SeraphQoLLang.Get("alloy-status-missing", plan.RequiredMeltTempC);
        CairoFont statusFont = (plan.CanFulfillFromInventory ? Theme.PositiveFont(detail: false, bold: true) : Theme.WarningFont(detail: false, bold: true))
            .WithFontSize(13);
        composer.AddStaticText(statusText, statusFont,
            ElementBounds.Fixed(10, y + 130, w - 20, 22), "txtAlloyStatus");

        y += 168;

        // Lista select de combustibles disponibles y cantidad requerida
        if (snapshot.AvailableFuels.Count > 0)
        {
            int selectedFuelIdx = Math.Clamp(_fuelIndex, 0, snapshot.AvailableFuels.Count - 1);
            FuelOptionSpec fuel = snapshot.AvailableFuels[selectedFuelIdx];
            double stackFactor = Math.Max(0.5, plan.TargetOutputUnits / 200.0);
            FuelRequirementResult fuelReq = AlloyCalculatorLogic.CalculateFuelRequirement(
                fuel,
                snapshot.FurnaceTempC,
                plan.RequiredMeltTempC,
                stackFactor);

            var fuelValues = new string[snapshot.AvailableFuels.Count];
            var fuelNames = new string[snapshot.AvailableFuels.Count];
            for (int i = 0; i < snapshot.AvailableFuels.Count; i++)
            {
                FuelOptionSpec opt = snapshot.AvailableFuels[i];
                FuelRequirementResult optReq = AlloyCalculatorLogic.CalculateFuelRequirement(
                    opt,
                    snapshot.FurnaceTempC,
                    plan.RequiredMeltTempC,
                    stackFactor);
                fuelValues[i] = i.ToString();
                fuelNames[i] = optReq.ReachesRequiredTemperature
                    ? $"{opt.DisplayName} ({opt.BurnTemperatureC}C): x{optReq.PiecesNeeded}/{opt.AvailableCount}"
                    : $"{opt.DisplayName} ({opt.BurnTemperatureC}C < {plan.RequiredMeltTempC}C): x{optReq.PiecesNeeded}/{opt.AvailableCount}";
            }

            composer.AddDropDown(
                fuelValues,
                fuelNames,
                selectedFuelIdx,
                (code, selected) =>
                {
                    if (selected && int.TryParse(code, out int nextFuel) && nextFuel != _fuelIndex)
                    {
                        _fuelIndex = nextFuel;
                        capi.Event.EnqueueMainThreadTask(() => RefreshView(), "seraphqol-alloy-fuel-select");
                    }
                },
                ElementBounds.Fixed(0, y, w, 28),
                Theme.BodyFont(),
                "ddlFuel");

            y += 34;

            composer.AddActionButton(SeraphQoLLang.Get("alloy-btn-deposit-items"), () =>
            {
                if (_firepitDialog != null)
                {
                    _depositService.RequestDeposit(_firepitDialog, plan, fuel, 0, depositItems: true, depositFuel: false);
                    capi.Gui.PlaySound("tick");
                }
                return true;
            }, ElementBounds.Fixed(0, y, (w - 8) * 0.5, 28), "btnDepositItems");

            composer.AddActionButton(SeraphQoLLang.Get("alloy-btn-deposit-fuel"), () =>
            {
                if (_firepitDialog != null && fuelReq.PiecesNeeded > 0)
                {
                    _depositService.RequestDeposit(_firepitDialog, plan, fuel, fuelReq.PiecesNeeded, depositItems: false, depositFuel: true);
                    capi.Gui.PlaySound("tick");
                }
                return true;
            }, ElementBounds.Fixed((w - 8) * 0.5 + 8, y, (w - 8) * 0.5, 28), "btnDepositFuel");

            y += 34;

            composer.AddActionButton(SeraphQoLLang.Get("alloy-btn-deposit-all"), () =>
            {
                if (_firepitDialog != null)
                {
                    _depositService.RequestDeposit(_firepitDialog, plan, fuel, fuelReq.PiecesNeeded, depositItems: true, depositFuel: fuelReq.PiecesNeeded > 0);
                    capi.Gui.PlaySound("tick");
                }
                return true;
            }, ElementBounds.Fixed(0, y, w, 30), "btnDepositAll");
        }
        else
        {
            composer.AddStaticText(SeraphQoLLang.Get("alloy-no-fuel"), Theme.WarningFont(detail: false).WithFontSize(13),
                ElementBounds.Fixed(0, y, w, 24), "txtNoFuel");
            y += 30;
            composer.AddActionButton(SeraphQoLLang.Get("alloy-btn-deposit-items"), () =>
            {
                if (_firepitDialog != null)
                {
                    _depositService.RequestDeposit(_firepitDialog, plan, null, 0, depositItems: true, depositFuel: false);
                    capi.Gui.PlaySound("tick");
                }
                return true;
            }, ElementBounds.Fixed(0, y, w, 28), "btnDepositItemsOnly");
        }
    }

    private void RequestScanIfNearbyEnabled()
    {
        if (!_modSystem.ClientConfig.AlloySearchNearbyContainers || _firepitDialog?.BlockEntityPosition == null)
        {
            return;
        }

        int radius = Math.Clamp(
            _modSystem.ClientConfig.AlloySearchRadius,
            SeraphQoLClientConfig.MinAlloySearchRadius,
            SeraphQoLClientConfig.MaxAlloySearchRadius);
        _modSystem.RequestAlloyScanFromClient(_firepitDialog.BlockEntityPosition, radius);
    }

    private void SelectRecipe(int index)
    {
        if (_recipes.Count == 0) return;
        _recipeIndex = ((index % _recipes.Count) + _recipes.Count) % _recipes.Count;
        _percentages = AlloyCalculatorLogic.BuildDefaultPercentages(_recipes[_recipeIndex]);
    }
}
