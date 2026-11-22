using System.Reflection;
using HarmonyLib;
using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SeraphQoL.Client;

/// <summary>
/// Inyecta botones de accion en el inventario del jugador y en ventanas de contenedores abiertos,
/// ademas de dibujar insignias de favoritos y slots bloqueados.
/// </summary>
public sealed class InventoryButtonsComposer : IDisposable
{
    private const double ButtonSize = 16.0;
    private const double Spacing = 24.0;
    private const double TitleBarRightReserve = 80.0;

    private static readonly FieldInfo? ComposerInteractiveElementsField =
        AccessTools.Field(typeof(GuiComposer), "interactiveElements");

    private readonly ICoreClientAPI _capi;
    private readonly SeraphQoLModSystem _modSystem;

    private LoadedTexture _favoriteBadgeTex;
    private LoadedTexture _lockBadgeTex;
    private LoadedTexture _matchingHighlightTex;
    private LoadedTexture _inactiveSlotHatchTex;
    private bool _badgesLoaded;

    public InventoryButtonsComposer(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
        _favoriteBadgeTex = new LoadedTexture(capi);
        _lockBadgeTex = new LoadedTexture(capi);
        _matchingHighlightTex = new LoadedTexture(capi);
        _inactiveSlotHatchTex = new LoadedTexture(capi);
    }

    public void ComposePlayerInventoryButtons(GuiComposer composer, IInventory backpackInventory)
    {
        if (!_modSystem.EffectivePolicy.EnableInventoryManagement) return;

        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        (GuiElementDialogTitleBar? titleBar, ElementBounds hostBounds, double cursorX, double y) = ResolveHeaderLayout(composer);
        var addedButtons = new List<SvgToolbarButton>();

        composer.BeginChildElements(hostBounds);
        try
        {
            if (!cfg.HideSortButton)
            {
                string sortTip = SeraphQoLLang.Get("tooltip-sort", SeraphQoLLang.GetSortOrderName(cfg.SortOrder));
                AddIconButton(composer, titleBar, addedButtons, "seraphqol-inv-sort", "sort", sortTip, cursorX, y,
                    () => { _modSystem.RequestSortFromClient(backpackInventory.InventoryID); return true; },
                    delta => _modSystem.CycleSortOrderFromClient(delta));
                cursorX -= Spacing;
            }

            if (!cfg.HideStoreNearbyButton && _modSystem.EffectivePolicy.EnableStoreNearby)
            {
                AddIconButton(composer, titleBar, addedButtons, "seraphqol-inv-store-nearby", "store-nearby",
                    SeraphQoLLang.Get("tooltip-store-nearby"), cursorX, y,
                    () => { _modSystem.RequestStoreNearbyFromClient(); return true; });
                cursorX -= Spacing;
            }

            AddToggleButton(composer, titleBar, addedButtons, "seraphqol-inv-favorite", "favorite",
                ColorUtil.ColorFromRgba(84, 185, 222, 255),
                _modSystem.Favorites.IsFavoriteModeActive,
                SeraphQoLLang.Get("tooltip-favorite-mode"),
                cursorX, y,
                active =>
                {
                    _modSystem.Favorites.IsFavoriteModeActive = active;
                    if (active)
                    {
                        _modSystem.LockedSlots.IsLockModeActive = false;
                        if (composer.GetElement("seraphqol-inv-lock") is SvgIconToggleButton lockBtn)
                        {
                            lockBtn.IsActive = false;
                        }
                    }
                });
            cursorX -= Spacing;

            AddToggleButton(composer, titleBar, addedButtons, "seraphqol-inv-lock", "lock-slot",
                ColorUtil.ColorFromRgba(224, 198, 111, 255),
                _modSystem.LockedSlots.IsLockModeActive,
                SeraphQoLLang.Get("tooltip-lock-slot-mode"),
                cursorX, y,
                active =>
                {
                    _modSystem.LockedSlots.IsLockModeActive = active;
                    if (active)
                    {
                        _modSystem.Favorites.IsFavoriteModeActive = false;
                        if (composer.GetElement("seraphqol-inv-favorite") is SvgIconToggleButton favBtn)
                        {
                            favBtn.IsActive = false;
                        }
                    }
                });
            cursorX -= Spacing;

            if (!cfg.HideStackPerishablesButton)
            {
                AddToggleButton(composer, titleBar, addedButtons, "seraphqol-inv-stack-perishables", "stack-perishables",
                    ColorUtil.ColorFromRgba(125, 230, 130, 255),
                    cfg.StackPerishables,
                    SeraphQoLLang.Get("tooltip-stack-perishables"),
                    cursorX, y,
                    active => _modSystem.SetStackPerishablesFromClient(active));
            }

            if (addedButtons.Count > 0 && composer.GetElement("seraphqol-inv-tooltip-layer") == null)
            {
                composer.AddInteractiveElement(
                    new SvgTooltipLayerElement(_capi, addedButtons, ElementBounds.Fixed(0, 0, 1, 1)),
                    "seraphqol-inv-tooltip-layer");
            }
        }
        finally
        {
            composer.EndChildElements();
        }

        if (addedButtons.Count > 0)
        {
            composer.ReCompose();
        }
    }

    public void ComposeContainerButtons(GuiComposer composer, IInventory containerInventory)
    {
        if (!_modSystem.EffectivePolicy.EnableInventoryManagement) return;

        SeraphQoLClientConfig cfg = _modSystem.ClientConfig;
        (GuiElementDialogTitleBar? titleBar, ElementBounds hostBounds, double cursorX, double y) = ResolveHeaderLayout(composer);
        var addedButtons = new List<SvgToolbarButton>();
        bool titleShortened = false;

        if (titleBar != null)
        {
            string shortened = ContainerTitleFormatter.ShortenTitle(titleBar.Text);
            if (!string.IsNullOrEmpty(shortened) && !string.Equals(shortened, titleBar.Text, StringComparison.Ordinal))
            {
                titleBar.Text = shortened;
                titleShortened = true;
            }
        }

        composer.BeginChildElements(hostBounds);
        try
        {
            if (!cfg.HideSortButton)
            {
                string sortTip = SeraphQoLLang.Get("tooltip-sort", SeraphQoLLang.GetSortOrderName(cfg.SortOrder));
                AddIconButton(composer, titleBar, addedButtons, "seraphqol-cont-sort", "sort", sortTip, cursorX, y,
                    () => { _modSystem.RequestSortFromClient(containerInventory.InventoryID); return true; },
                    delta => _modSystem.CycleSortOrderFromClient(delta));
                cursorX -= Spacing;
            }

            if (!cfg.HideQuickStoreButton)
            {
                AddIconButton(composer, titleBar, addedButtons, "seraphqol-cont-quick-store", "quick-store",
                    SeraphQoLLang.Get("tooltip-quick-store"), cursorX, y,
                    () => { _modSystem.RequestQuickStoreFromClient(containerInventory.InventoryID); return true; });
            }

            if (addedButtons.Count > 0 && composer.GetElement("seraphqol-cont-tooltip-layer") == null)
            {
                composer.AddInteractiveElement(
                    new SvgTooltipLayerElement(_capi, addedButtons, ElementBounds.Fixed(0, 0, 1, 1)),
                    "seraphqol-cont-tooltip-layer");
            }
        }
        finally
        {
            composer.EndChildElements();
        }

        if (addedButtons.Count > 0 || titleShortened)
        {
            composer.ReCompose();
        }
    }

    public void DrawSlotBadge(ElementBounds slotBounds, bool drawFavorite, bool drawLocked)
    {
        if (!drawFavorite && !drawLocked) return;
        EnsureBadgeTextures();

        double iconSize = GuiElement.scaled(11.0);
        double pad = GuiElement.scaled(2.5);
        _capi.Render.GLDisableDepthTest();

        // Estrella (favorito): esquina superior derecha (top-right)
        if (drawFavorite && _favoriteBadgeTex.TextureId > 0)
        {
            _capi.Render.Render2DTexturePremultipliedAlpha(
                _favoriteBadgeTex.TextureId,
                (float)(slotBounds.renderX + slotBounds.InnerWidth - iconSize - pad),
                (float)(slotBounds.renderY + pad),
                (float)iconSize,
                (float)iconSize,
                260f);
        }

        // Candado (bloqueado): esquina inferior izquierda (bottom-left)
        if (drawLocked && _lockBadgeTex.TextureId > 0)
        {
            _capi.Render.Render2DTexturePremultipliedAlpha(
                _lockBadgeTex.TextureId,
                (float)(slotBounds.renderX + pad),
                (float)(slotBounds.renderY + slotBounds.InnerHeight - iconSize - pad),
                (float)iconSize,
                (float)iconSize,
                260f);
        }
        _capi.Render.GLEnableDepthTest();
    }

    public void DrawMatchingHighlight(ElementBounds slotBounds) =>
        DrawSlotOverlay(_matchingHighlightTex, slotBounds, 256f);

    public void DrawInactiveSlotHatch(ElementBounds slotBounds) =>
        DrawSlotOverlay(_inactiveSlotHatchTex, slotBounds, 252f);

    private void DrawSlotOverlay(LoadedTexture texture, ElementBounds slotBounds, float z)
    {
        EnsureBadgeTextures();
        if (texture.TextureId <= 0) return;

        _capi.Render.GLDisableDepthTest();
        _capi.Render.Render2DTexturePremultipliedAlpha(
            texture.TextureId,
            (float)slotBounds.renderX,
            (float)slotBounds.renderY,
            (float)slotBounds.InnerWidth,
            (float)slotBounds.InnerHeight,
            z);
        _capi.Render.GLEnableDepthTest();
    }

    private (GuiElementDialogTitleBar? TitleBar, ElementBounds HostBounds, double StartX, double Y) ResolveHeaderLayout(GuiComposer composer)
    {
        GuiElementDialogTitleBar? titleBar = FindTitleBar(composer);
        ElementBounds hostBounds = titleBar?.Bounds ?? composer.Bounds;
        double scale = Math.Max(0.25, GuiElement.scaled(1.0));
        double hostWidth = hostBounds.InnerWidth / scale;
        double hostHeight = hostBounds.InnerHeight / scale;
        double startX = Math.Max(48.0, hostWidth - TitleBarRightReserve);
        double y = titleBar != null ? Math.Max(3.0, (hostHeight - ButtonSize) * 0.5) : 4.0;
        return (titleBar, hostBounds, startX, y);
    }

    private static GuiElementDialogTitleBar? FindTitleBar(GuiComposer composer)
    {
        if (ComposerInteractiveElementsField?.GetValue(composer) is Dictionary<string, GuiElement> elements)
        {
            return elements.Values.OfType<GuiElementDialogTitleBar>().FirstOrDefault();
        }
        return null;
    }

    private void AddIconButton(
        GuiComposer composer,
        GuiElementDialogTitleBar? ownerTitleBar,
        ICollection<SvgToolbarButton> addedButtons,
        string key,
        string iconName,
        string tooltip,
        double x,
        double y,
        ActionConsumable onClick,
        Action<int>? onWheel = null)
    {
        if (composer.GetElement(key) != null) return;

        ElementBounds bounds = ElementBounds.Fixed(x, y, ButtonSize, ButtonSize);
        var asset = new AssetLocation(SeraphQoLLang.Domain, $"textures/icons/{iconName}.svg");
        var btn = new SvgIconButton(_capi, asset, tooltip, onClick, bounds, onWheel, ownerTitleBar);
        composer.AddInteractiveElement(btn, key);
        addedButtons.Add(btn);
    }

    private void AddToggleButton(
        GuiComposer composer,
        GuiElementDialogTitleBar? ownerTitleBar,
        ICollection<SvgToolbarButton> addedButtons,
        string key,
        string iconName,
        int activeColor,
        bool initialState,
        string tooltip,
        double x,
        double y,
        Action<bool> onToggle)
    {
        if (composer.GetElement(key) != null) return;

        ElementBounds bounds = ElementBounds.Fixed(x, y, ButtonSize, ButtonSize);
        var asset = new AssetLocation(SeraphQoLLang.Domain, $"textures/icons/{iconName}.svg");
        var btn = new SvgIconToggleButton(_capi, asset, activeColor, initialState, tooltip, onToggle, bounds, ownerTitleBar);
        composer.AddInteractiveElement(btn, key);
        addedButtons.Add(btn);
    }

    private void EnsureBadgeTextures()
    {
        if (_badgesLoaded) return;
        _badgesLoaded = true;

        int size = Math.Max(8, (int)GuiElement.scaled(12.0));
        SvgTextureHelper.LoadOrReplace(
            _capi,
            new AssetLocation(SeraphQoLLang.Domain, "textures/icons/favorite.svg"),
            size,
            null,
            ref _favoriteBadgeTex);
        SvgTextureHelper.LoadOrReplace(
            _capi,
            new AssetLocation(SeraphQoLLang.Domain, "textures/icons/lock-slot.svg"),
            size,
            null,
            ref _lockBadgeTex);

        EnsureMatchingHighlightTexture();
        EnsureInactiveSlotHatchTexture();
    }

    private void EnsureMatchingHighlightTexture()
    {
        int texSize = Math.Max(24, (int)GuiElement.scaled(48.0));
        SvgTextureHelper.LoadOrReplace(
            _capi,
            new AssetLocation(SeraphQoLLang.Domain, "textures/icons/slot-selection.svg"),
            texSize,
            null,
            ref _matchingHighlightTex);

        if (_matchingHighlightTex.TextureId > 0) return;

        using var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, texSize, texSize);
        using var ctx = new Cairo.Context(surface);
        ctx.Operator = Cairo.Operator.Clear;
        ctx.Paint();
        ctx.Operator = Cairo.Operator.Over;

        // Fallback procedural: Resaltado marron sutil (#9F6833) si el SVG no estuviese disponible
        ctx.SetSourceRGBA(0.80, 0.58, 0.36, 0.20);
        ctx.Rectangle(2, 2, texSize - 4, texSize - 4);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.62, 0.41, 0.20, 0.98);
        ctx.LineWidth = 3.2;
        ctx.Stroke();

        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: true, ref _matchingHighlightTex);
    }

    private void EnsureInactiveSlotHatchTexture()
    {
        const int texSize = 96;
        const double inset = 4.0;
        const double stripePeriod = 24.0;
        const double stripeWidth = 8.5;

        using var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, texSize, texSize);
        using var ctx = new Cairo.Context(surface);
        ctx.Operator = Cairo.Operator.Clear;
        ctx.Paint();
        ctx.Operator = Cairo.Operator.Over;

        // Lavado marron oscuro suave + franjas diagonales bronce apagado en paleta SeraphQoL
        ctx.Rectangle(inset, inset, texSize - (inset * 2.0), texSize - (inset * 2.0));
        ctx.SetSourceRGBA(0.09, 0.07, 0.05, 0.28);
        ctx.FillPreserve();

        ctx.Save();
        ctx.Clip();
        ctx.SetSourceRGBA(0.62, 0.50, 0.36, 0.34);
        ctx.LineWidth = stripeWidth;
        for (double offset = -texSize; offset <= texSize; offset += stripePeriod)
        {
            ctx.MoveTo(offset, 0);
            ctx.LineTo(offset + texSize, texSize);
        }
        ctx.Stroke();
        ctx.Restore();

        ctx.Rectangle(inset, inset, texSize - (inset * 2.0), texSize - (inset * 2.0));
        ctx.SetSourceRGBA(0.62, 0.50, 0.36, 0.42);
        ctx.LineWidth = 2.0;
        ctx.Stroke();

        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: true, ref _inactiveSlotHatchTex);
    }

    public void Dispose()
    {
        _favoriteBadgeTex.Dispose();
        _lockBadgeTex.Dispose();
        _matchingHighlightTex.Dispose();
        _inactiveSlotHatchTex.Dispose();
    }
}
