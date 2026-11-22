using HarmonyLib;
using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace SeraphQoL.Handbook;

/// <summary>
/// Componente de texto enriquecido del Handbook que dibuja un botón (+ / *) junto a recetas
/// de grilla para auto-rellenar ingredientes desde inventario, cofres abiertos y cofres cercanos.
/// </summary>
public sealed class HandbookFillButtonComponent : RichTextComponentBase
{
    private const double UnscaledButtonSize = 24.0;
    private const double VerticalButtonSpacing = 26.0;
    private const double UnscaledLeftOverlap = 3.0;
    private const double UnscaledRightGapBeforeEquals = 4.0;
    private const double TooltipHoverDelaySeconds = 1.0;

    private static readonly double[] MissingButtonTint = [1.0, 0.45, 0.45, 1.0];

    private readonly HandbookRecipeService _service;
    private readonly GridRecipe[] _recipes;
    private readonly bool _fillMax;
    private readonly int _buttonIndex;
    private readonly double _offsetX;
    private readonly double _offsetY;
    private readonly FixedAbsoluteBounds _bounds;
    private readonly string _label;
    private readonly string _baseTooltip;

    private GuiElementTextButton _button;
    private GuiElementHoverText _hoverText;
    private bool? _lastMissingState;
    private double _hoverElapsed;

    public HandbookFillButtonComponent(
        ICoreClientAPI capi,
        HandbookRecipeService service,
        int buttonIndex,
        bool fillMax,
        GridRecipe[] recipes)
        : base(capi)
    {
        _service = service;
        _buttonIndex = buttonIndex;
        _fillMax = fillMax;
        _recipes = recipes;
        _offsetX = -1.0;
        _offsetY = fillMax ? -7.5 : -10.5;
        _label = fillMax ? "*" : "+";
        _baseTooltip = SeraphQoLLang.Get(fillMax ? "handbook-tooltip-addmax" : "handbook-tooltip-addone");

        Float = EnumFloat.Inline;
        VerticalAlign = EnumVerticalAlign.FixedOffset;

        double size = Math.Ceiling(GuiElement.scaled(UnscaledButtonSize));
        _bounds = new FixedAbsoluteBounds(0, 0, size, size);

        _button = CreateButton(capi, _label, _bounds, textColor: null);
        _hoverText = CreateHoverText(capi, _baseTooltip, _bounds);
    }

    private bool IsVisible =>
        _service.IsAutoFillEnabled && HandbookRecipeService.HasSurvivalCraftingGridOpened(api);

    public override EnumCalcBoundsResult CalcBounds(
        TextFlowPath[] flowPath,
        double currentLineHeight,
        double offsetX,
        double lineY,
        out double nextOffsetX)
    {
        double x = offsetX - GuiElement.scaled(UnscaledLeftOverlap);
        double y = lineY + GuiElement.scaled(102.0 - (_buttonIndex * VerticalButtonSpacing));
        double size = GuiElement.scaled(UnscaledButtonSize);

        BoundsPerLine = [new LineRectangled(x, y, size, size)];
        _bounds.fixedWidth = size;
        _bounds.fixedHeight = size;

        nextOffsetX = ComputeNextOffsetX(
            offsetX,
            x,
            size,
            GuiElement.scaled(UnscaledRightGapBeforeEquals),
            advanceCursor: _service.IsAutoFillEnabled && _buttonIndex == 1);
        return EnumCalcBoundsResult.Continue;
    }

    internal static double ComputeNextOffsetX(
        double offsetX,
        double buttonX,
        double buttonWidth,
        double rightGap,
        bool advanceCursor) =>
        advanceCursor ? buttonX + buttonWidth + rightGap : offsetX;

    public override void RenderInteractiveElements(float deltaTime, double renderX, double renderY, double renderZ)
    {
        if (!IsVisible) return;

        bool isMissing = _service.AreAllRecipesMissing(_recipes);
        if (_lastMissingState != isMissing)
        {
            _lastMissingState = isMissing;
            UpdateMissingAppearance(isMissing);
        }

        SyncBounds(renderX, renderY);
        _button.RenderInteractiveElements(deltaTime);

        bool hovered = _bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        _hoverElapsed = hovered ? _hoverElapsed + deltaTime : 0.0;
        _hoverText.SetVisible(_hoverElapsed > TooltipHoverDelaySeconds);
        _hoverText.RenderInteractiveElements(deltaTime);
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (!IsVisible) return;
        SyncBounds(0, 0);
        _button.OnMouseDown(api, args);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        if (!IsVisible) return;
        SyncBounds(0, 0);
        _button.OnMouseUp(api, args);
    }

    public override void OnMouseMove(MouseEvent args)
    {
        if (!IsVisible) return;
        _button.PlaySound = true;
        _button.OnMouseMove(api, args);
        _button.PlaySound = false;
    }

    public override void Dispose()
    {
        _button.Dispose();
        _hoverText.Dispose();
        base.Dispose();
    }

    private void UpdateMissingAppearance(bool isMissing)
    {
        _button.Dispose();
        _button = CreateButton(api, _label, _bounds, isMissing ? MissingButtonTint : null);

        string tip = isMissing
            ? $"{_baseTooltip}\n{SeraphQoLLang.Get("handbook-tooltip-missing-ingredients")}"
            : _baseTooltip;
        _hoverText.SetNewText(tip);
    }

    private GuiElementTextButton CreateButton(
        ICoreClientAPI capi,
        string label,
        ElementBounds bounds,
        double[]? textColor)
    {
        CairoFont normal = CairoFont.ButtonText();
        CairoFont pressed = CairoFont.ButtonPressedText();
        if (textColor != null)
        {
            normal = normal.WithColor(textColor);
            pressed = pressed.WithColor(textColor);
        }

        double fontSize = GuiElement.scaled(32.0);
        normal.UnscaledFontsize = fontSize;
        pressed.UnscaledFontsize = fontSize;

        var button = new GuiElementTextButton(
            capi,
            label,
            normal,
            pressed,
            HandleClick,
            bounds,
            EnumButtonStyle.Small)
        {
            PlaySound = false
        };

        Traverse tr = Traverse.Create(button);
        AdjustTextOffset(tr.Field<GuiElementStaticText>("normalText").Value);
        AdjustTextOffset(tr.Field<GuiElementStaticText>("pressedText").Value);
        button.ComposeElements(null, null);
        return button;
    }

    private static GuiElementHoverText CreateHoverText(ICoreClientAPI capi, string text, ElementBounds bounds)
    {
        var hover = new GuiElementHoverText(capi, text, CairoFont.WhiteSmallText(), 220, bounds);
        hover.SetAutoDisplay(false);
        return hover;
    }

    private void AdjustTextOffset(GuiElementStaticText? textElem)
    {
        if (textElem == null) return;
        textElem.offsetX = GuiElement.scaled(_offsetX);
        textElem.offsetY = GuiElement.scaled(_offsetY);
    }

    private bool HandleClick()
    {
        bool filled = _service.TryFillGrid(_recipes, _fillMax, out bool handledAsyncByServer);
        if (!handledAsyncByServer)
        {
            api.Gui.PlaySound(filled ? "menubutton_press" : "menubutton_wood", false, 1f);
        }
        return true;
    }

    private void SyncBounds(double renderX, double renderY)
    {
        if (BoundsPerLine == null || BoundsPerLine.Length == 0) return;
        LineRectangled rect = BoundsPerLine[0];
        _bounds.absInnerWidth = rect.Width;
        _bounds.absInnerHeight = rect.Height;
        _bounds.absFixedX = renderX + rect.X;
        _bounds.absFixedY = renderY + rect.Y;
    }

    private sealed class FixedAbsoluteBounds : ElementBounds
    {
        public FixedAbsoluteBounds(double x, double y, double width, double height)
        {
            absFixedX = x;
            absFixedY = y;
            fixedWidth = width;
            absInnerWidth = width;
            fixedHeight = height;
            absInnerHeight = height;
            BothSizing = ElementSizing.Fixed;
            ParentBounds = new ElementBounds();
        }

        public override double bgDrawX => absFixedX;
        public override double bgDrawY => absFixedY;
        public override double renderX => absFixedX + renderOffsetX;
        public override double renderY => absFixedY + renderOffsetY;
        public override double absX => absFixedX;
        public override double absY => absFixedY;
    }
}
