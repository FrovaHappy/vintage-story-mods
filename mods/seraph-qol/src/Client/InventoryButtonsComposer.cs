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
    private bool _badgesLoaded;

    public InventoryButtonsComposer(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
    {
        _capi = capi;
        _modSystem = modSystem;
        _favoriteBadgeTex = new LoadedTexture(capi);
        _lockBadgeTex = new LoadedTexture(capi);
        _matchingHighlightTex = new LoadedTexture(capi);
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
                ColorUtil.ColorFromRgba(255, 210, 75, 255),
                _modSystem.Favorites.IsFavoriteModeActive,
                SeraphQoLLang.Get("tooltip-favorite-mode"),
                cursorX, y,
                active =>
                {
                    _modSystem.Favorites.IsFavoriteModeActive = active;
                    if (active) _modSystem.LockedSlots.IsLockModeActive = false;
                });
            cursorX -= Spacing;

            AddToggleButton(composer, titleBar, addedButtons, "seraphqol-inv-lock", "lock-slot",
                ColorUtil.ColorFromRgba(235, 70, 70, 255),
                _modSystem.LockedSlots.IsLockModeActive,
                SeraphQoLLang.Get("tooltip-lock-slot-mode"),
                cursorX, y,
                active =>
                {
                    _modSystem.LockedSlots.IsLockModeActive = active;
                    if (active) _modSystem.Favorites.IsFavoriteModeActive = false;
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
        _capi.Render.GLDisableDepthTest();
        if (drawFavorite && _favoriteBadgeTex.TextureId > 0)
        {
            _capi.Render.Render2DTexturePremultipliedAlpha(
                _favoriteBadgeTex.TextureId,
                (float)(slotBounds.renderX + GuiElement.scaled(2.0)),
                (float)(slotBounds.renderY + GuiElement.scaled(2.0)),
                (float)iconSize,
                (float)iconSize,
                260f);
        }

        if (drawLocked && _lockBadgeTex.TextureId > 0)
        {
            _capi.Render.Render2DTexturePremultipliedAlpha(
                _lockBadgeTex.TextureId,
                (float)(slotBounds.renderX + slotBounds.InnerWidth - iconSize - GuiElement.scaled(2.0)),
                (float)(slotBounds.renderY + GuiElement.scaled(2.0)),
                (float)iconSize,
                (float)iconSize,
                260f);
        }
        _capi.Render.GLEnableDepthTest();
    }

    public void DrawMatchingHighlight(ElementBounds slotBounds)
    {
        EnsureBadgeTextures();
        if (_matchingHighlightTex.TextureId <= 0) return;

        _capi.Render.GLDisableDepthTest();
        _capi.Render.Render2DTexturePremultipliedAlpha(
            _matchingHighlightTex.TextureId,
            (float)slotBounds.renderX,
            (float)slotBounds.renderY,
            (float)slotBounds.InnerWidth,
            (float)slotBounds.InnerHeight,
            256f);
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
            ColorUtil.ColorFromRgba(255, 210, 75, 240),
            ref _favoriteBadgeTex);
        SvgTextureHelper.LoadOrReplace(
            _capi,
            new AssetLocation(SeraphQoLLang.Domain, "textures/icons/lock-slot.svg"),
            size,
            ColorUtil.ColorFromRgba(235, 65, 65, 245),
            ref _lockBadgeTex);

        EnsureMatchingHighlightTexture();
    }

    private void EnsureMatchingHighlightTexture()
    {
        const int texSize = 48;
        using var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, texSize, texSize);
        using var ctx = new Cairo.Context(surface);
        ctx.Operator = Cairo.Operator.Clear;
        ctx.Paint();
        ctx.Operator = Cairo.Operator.Over;

        // Resaltado marron claro sutil en el centro y borde marcado para no tapar indicadores de estado
        ctx.SetSourceRGBA(0.80, 0.58, 0.36, 0.20);
        ctx.Rectangle(2, 2, texSize - 4, texSize - 4);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0.92, 0.72, 0.48, 0.98);
        ctx.LineWidth = 3.2;
        ctx.Stroke();

        _capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: true, ref _matchingHighlightTex);
    }

    public void Dispose()
    {
        _favoriteBadgeTex.Dispose();
        _lockBadgeTex.Dispose();
        _matchingHighlightTex.Dispose();
    }
}
