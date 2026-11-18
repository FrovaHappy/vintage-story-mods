using HarmonyLib;
using SystemUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Comerciantes;

/// <summary>
/// Directorio global desplazable de comerciantes conocidos, ordenado por última visita,
/// con filtro por tipo, acceso a consulta rápida y centrado en el mapa.
/// </summary>
public sealed class GuiDialogTraderDirectory : UiDialogBase
{
    private const int SellCols = 10;
    private const int SellRows = 2;
    private const int SellSlotTotal = SellCols * SellRows;
    private const int WantedCols = 5;
    private const int WantedRows = 2;
    private const int WantedSlotTotal = WantedCols * WantedRows;

    private const double ListViewportWidth = 819;
    private const double ListViewportHeight = 460;
    private const double FullCardHeight = 214;
    private const double CompactCardHeight = 44;
    private const double CardGap = 12;

    private readonly ITraderClientGateway _mod;
    private KnownTradersSyncPacket? _snapshot;
    private ItemStack? _gearStack;
    private string _selectedArchetype = string.Empty;

    private readonly List<InventoryGeneric> _sellDisplayInvs = [];
    private readonly List<InventoryGeneric> _wantedDisplayInvs = [];
    private List<KnownTraderDto> _filteredTraders = [];

    private ElementBounds? _clipBounds;
    private ElementBounds? _scrollContainerBounds;
    private double _totalContentHeight;

    public GuiDialogTraderDirectory(ICoreClientAPI capi, ITraderClientGateway mod) : base(capi)
    {
        _mod = mod;
    }

    protected override string ComposerKey => "tradersoverhaul_global_directory";
    protected override string DialogTitle => ComerciantesLang.Get("gui-dir-title");
    protected override double DialogWidth => 848;
    protected override double DialogHeight => 510;

    public void UpdateData(KnownTradersSyncPacket packet)
    {
        _snapshot = packet;
        RefreshView();
    }

    private ItemStack? GetGearIcon()
    {
        if (_gearStack == null)
        {
            Item? gear = capi.World.GetItem(new AssetLocation("game", "gear-rusty"));
            if (gear != null) _gearStack = new ItemStack(gear, 1);
        }
        return _gearStack;
    }

    private void BuildArchetypeFilterOptions(out string[] values, out string[] names, out int selectedIndex)
    {
        List<string> archetypes = (_snapshot?.Traders ?? [])
            .Select(t => t.Archetype)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ComerciantesLang.GetArchetypeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var valList = new List<string> { string.Empty };
        var nameList = new List<string> { ComerciantesLang.Get("gui-dir-filter-all") };

        foreach (string arch in archetypes)
        {
            valList.Add(arch);
            nameList.Add(ComerciantesLang.GetArchetypeName(arch));
        }

        values = valList.ToArray();
        names = nameList.ToArray();
        selectedIndex = Math.Max(0, valList.FindIndex(v => string.Equals(v, _selectedArchetype, StringComparison.OrdinalIgnoreCase)));
        if (selectedIndex == 0) _selectedArchetype = string.Empty;
    }

    protected override void ComposeContent(GuiComposer composer, ElementBounds contentBounds)
    {
        _sellDisplayInvs.Clear();
        _wantedDisplayInvs.Clear();
        _clipBounds = null;
        _scrollContainerBounds = null;

        double topY = contentBounds.fixedY;
        bool isLive = _snapshot?.IsLiveSyncMode == true;
        bool instantTrade = _snapshot?.IsInstantTradeEnabled ?? false;
        int totalKnown = _snapshot?.Traders.Count ?? 0;

        var headerRb = new UiRichTextBuilder(capi, Theme)
            .Text(ComerciantesLang.Get("gui-dir-header-refresh"), Theme.BodyFont())
            .Bold(ComerciantesLang.Get(isLive ? "gui-dir-mode-live" : "gui-dir-mode-snapshot"), detail: false)
            .Text(ComerciantesLang.Get("gui-dir-header-known"), Theme.BodyFont())
            .Bold($"{totalKnown}", detail: false)
            .Text(ComerciantesLang.Get("gui-dir-header-instant"), Theme.BodyFont());

        if (instantTrade) headerRb.Positive(ComerciantesLang.Get("gui-dir-instant-active"), detail: false);
        else headerRb.Muted(ComerciantesLang.Get("gui-dir-instant-inactive"), detail: false);

        composer.AddRichTextBlock(headerRb.Build(), ElementBounds.Fixed(4, topY + 5, 626, 24), "dirHeaderInfo");

        BuildArchetypeFilterOptions(out string[] filterValues, out string[] filterNames, out int selectedFilterIdx);
        composer.AddDropDown(
            filterValues,
            filterNames,
            selectedFilterIdx,
            OnArchetypeFilterChanged,
            ElementBounds.Fixed(636, topY + 2, 210, 28),
            CairoFont.WhiteSmallText(),
            "filterArchetype");

        double listY = topY + 40;
        _filteredTraders = KnownTradersRegistry.FilterAndSortByLastVisited(_snapshot?.Traders ?? [], _selectedArchetype);

        if (_filteredTraders.Count == 0)
        {
            _totalContentHeight = ListViewportHeight;
            composer.AddPanelCard(0, listY, DialogWidth, 72, Theme);
            composer.AddRichTextBlock(
                new UiRichTextBuilder(capi, Theme)
                    .Warning(ComerciantesLang.Get(totalKnown == 0 ? "gui-dir-empty-title" : "gui-dir-filter-empty"))
                    .NewLine()
                    .Muted(ComerciantesLang.Get("gui-dir-empty-subtitle"))
                    .Build(),
                ElementBounds.Fixed(14, listY + 14, DialogWidth - 28, 46),
                "dirEmpty");
            return;
        }

        double currentDays = _snapshot?.CurrentTotalDays ?? 0;
        double contentY = 0;
        foreach (KnownTraderDto t in _filteredTraders)
        {
            bool updated = KnownTradersRegistry.HasUpdatedList(t.LastSeenTotalDays, t.NextRefreshTotalDays, currentDays, isLive);
            contentY += (updated ? CompactCardHeight : FullCardHeight) + CardGap;
        }
        _totalContentHeight = Math.Max(ListViewportHeight, contentY);

        ElementBounds clipBounds = ElementBounds.Fixed(0, listY, ListViewportWidth, ListViewportHeight);
        ElementBounds scrollContainerBounds = ElementBounds.Fixed(0, 0, ListViewportWidth, _totalContentHeight);
        clipBounds.WithChild(scrollContainerBounds);
        _clipBounds = clipBounds;
        _scrollContainerBounds = scrollContainerBounds;

        composer.BeginClip(clipBounds);
        composer.BeginChildElements(scrollContainerBounds);

        ItemStack? gear = GetGearIcon();
        Vec3d? playerPos = capi.World.Player?.Entity?.Pos?.XYZ;
        AssetLocation inspectIcon = new(ComerciantesLang.Domain, "textures/icons/close-deal.svg");
        AssetLocation mapIcon = new(ComerciantesLang.Domain, "textures/icons/map.svg");

        double y = 0;
        for (int idx = 0; idx < _filteredTraders.Count; idx++)
        {
            KnownTraderDto trader = _filteredTraders[idx];
            bool hasUpdatedList = KnownTradersRegistry.HasUpdatedList(trader.LastSeenTotalDays, trader.NextRefreshTotalDays, currentDays, isLive);

            string traderName = string.IsNullOrWhiteSpace(trader.TraderName)
                ? ComerciantesLang.Get("trader-default-name")
                : trader.TraderName;
            string archetypeName = ComerciantesLang.GetArchetypeName(trader.Archetype);

            double cardH = hasUpdatedList ? CompactCardHeight : FullCardHeight;
            composer.AddDynamicPanelCard(0, y, ListViewportWidth, cardH, Theme, $"dirCardBg_{idx}");

            var titleRb = new UiRichTextBuilder(capi, Theme)
                .Highlight(traderName, detail: false, bold: true)
                .Text($" {archetypeName}", Theme.BodyFont());

            if (hasUpdatedList)
            {
                titleRb.Muted($" {ComerciantesLang.Get("gui-dir-new-list")}", detail: false);
            }

            composer.AddRichTextBlock(titleRb.Build(), ElementBounds.Fixed(12, y + 10, 424, 24), $"dirTitle_{idx}");

            KnownTraderDto capturedTrader = trader;
            composer.AddIconActionButton(
                ComerciantesLang.Get("gui-dir-btn-inspect"),
                inspectIcon,
                () =>
                {
                    InspectTrader(capturedTrader);
                    return true;
                },
                ElementBounds.Fixed(444, y + 8, 186, 28),
                Theme,
                $"btnInspect_{idx}",
                iconSize: 16,
                enabled: instantTrade,
                leftPadding: 10);

            composer.AddIconActionButton(
                ComerciantesLang.Get("gui-dir-btn-map"),
                mapIcon,
                () =>
                {
                    FocusTraderOnMap(capturedTrader);
                    return true;
                },
                ElementBounds.Fixed(636, y + 8, 172, 28),
                Theme,
                $"btnMap_{idx}",
                iconSize: 16,
                enabled: true,
                leftPadding: 10);

            if (!hasUpdatedList)
            {
                double dist = playerPos != null
                    ? playerPos.DistanceTo(new Vec3d(trader.PosX, trader.PosY, trader.PosZ))
                    : 0;
                double rem = Math.Max(0.0, trader.NextRefreshTotalDays - currentDays);
                string rotStr = rem <= 0.05
                    ? ComerciantesLang.Get("gui-dir-rot-ready")
                    : TraderUiHelpers.FormatRotationRemaining(rem);
                string seenStr = TraderUiHelpers.FormatLastSeenAgo(currentDays, trader.LastSeenTotalDays);

                var metaRb = new UiRichTextBuilder(capi, Theme)
                    .Text(ComerciantesLang.Get("gui-dir-meta-funds"))
                    .Bold($"{trader.TraderMoney} ")
                    .ItemIcon(gear, 16, 4)
                    .Text(ComerciantesLang.Get("gui-dir-meta-rotation"))
                    .Bold(rotStr)
                    .Text(ComerciantesLang.Get("gui-dir-meta-seen"))
                    .Bold(seenStr)
                    .Text(ComerciantesLang.Get("gui-dir-meta-distance"))
                    .Bold($"~{dist:0}m");

                composer.AddRichTextBlock(metaRb.Build(), ElementBounds.Fixed(12, y + 40, ListViewportWidth - 24, 22), $"dirMeta_{idx}");

                int traderIdx = idx;
                var sellInv = new InventoryGeneric(
                    SellSlotTotal,
                    $"todir_sell_{idx}",
                    "local",
                    capi,
                    (slotIdx, inv) => new DisplayOnlyItemSlot(
                        inv,
                        null,
                        () => GetSellOfferTooltip(traderIdx, slotIdx),
                        () => IsSellOfferOutOfStock(traderIdx, slotIdx)));

                for (int i = 0; i < SellSlotTotal; i++)
                {
                    StockOfferEntry? offer = trader.SellingOffers.ElementAtOrDefault(i);
                    sellInv[i].Itemstack = offer != null
                        ? TraderUiHelpers.ResolveStack(capi.World, offer.StackBytes, offer.ItemCode, offer.StackSize)
                        : null;
                }
                _sellDisplayInvs.Add(sellInv);

                var wantInv = new InventoryGeneric(
                    WantedSlotTotal,
                    $"todir_want_{idx}",
                    "local",
                    capi,
                    (slotIdx, inv) => new DisplayOnlyItemSlot(
                        inv,
                        null,
                        () => GetWantedTooltip(traderIdx, slotIdx)));

                for (int i = 0; i < WantedSlotTotal; i++)
                {
                    WantedItemEntry? wanted = trader.WantedItems.ElementAtOrDefault(i);
                    wantInv[i].Itemstack = wanted != null
                        ? TraderUiHelpers.ResolveStack(capi.World, wanted.StackBytes, wanted.ItemCode, Math.Max(1, wanted.MinLotSize))
                        : null;
                }
                _wantedDisplayInvs.Add(wantInv);

                // Grilla izquierda: Vendo (10x2)
                composer.AddDynamicPanelCard(10, y + 66, 523, 138, Theme, $"dirSellCardBg_{idx}");
                composer.AddStockSlotGridSection(
                    ComerciantesLang.Get("gui-dir-sell-title"),
                    sellInv,
                    _ => { },
                    SellCols,
                    SellRows,
                    18,
                    y + 72,
                    Theme,
                    $"dirSellGrid_{idx}",
                    out _,
                    507,
                    dynamicLabel: true);

                // Grilla derecha: Necesito (+X% mas) (5x2)
                composer.AddDynamicPanelCard(541, y + 66, 268, 138, Theme, $"dirWantCardBg_{idx}");
                var wantHeaderRb = new UiRichTextBuilder(capi, Theme)
                    .Text(ComerciantesLang.Get("gui-trade-section-wanted-prefix"), Theme.TitleFont());
                if (trader.TraderRewardBonusPct > 0)
                {
                    wantHeaderRb.Positive(ComerciantesLang.Get("gui-trade-section-wanted-bonus", $"{trader.TraderRewardBonusPct:0}"), detail: false, bold: false);
                }
                composer.AddRichTextBlock(wantHeaderRb.Build(), ElementBounds.Fixed(549, y + 72, 252, 22), $"dirWantTitle_{idx}");
                composer.AddStockSlotGridOnly(
                    wantInv,
                    _ => { },
                    WantedCols,
                    WantedRows,
                    549,
                    y + 96,
                    $"dirWantGrid_{idx}");
            }

            if (idx < _filteredTraders.Count - 1)
            {
                composer.AddDynamicDivider(4, y + cardH + (CardGap / 2.0) - 1, ListViewportWidth - 8, Theme, $"dirDivider_{idx}");
            }

            y += cardH + CardGap;
        }

        composer.EndChildElements();
        composer.EndClip();

        composer.AddVerticalScrollbar(
            OnScrollbarValueChanged,
            ElementBounds.Fixed(ListViewportWidth + 8, listY, 18, ListViewportHeight),
            "dirScrollbar");
    }

    protected override void OnComposed(GuiComposer composer)
    {
        GuiElementScrollbar? scrollbar = composer.GetScrollbar("dirScrollbar");
        scrollbar?.SetHeights((float)ListViewportHeight, (float)_totalContentHeight);
        scrollbar?.SetScrollbarPosition(0);
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        base.OnMouseWheel(args);
        if (!args.IsHandled && _clipBounds != null && _clipBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY))
        {
            SingleComposer?.GetScrollbar("dirScrollbar")?.OnMouseWheel(capi, args);
        }
    }

    private void OnScrollbarValueChanged(float value)
    {
        if (_scrollContainerBounds != null)
        {
            _scrollContainerBounds.fixedY = -value;
            _scrollContainerBounds.CalcWorldBounds();
        }
    }

    private void OnArchetypeFilterChanged(string code, bool selected)
    {
        _selectedArchetype = code ?? string.Empty;
        RefreshView();
    }

    private void InspectTrader(KnownTraderDto trader)
    {
        TryClose();
        _mod.RequestInspectTrader(trader.EntityId);
    }

    private void FocusTraderOnMap(KnownTraderDto trader)
    {
        TryClose();

        WorldMapManager? mapManager = capi.ModLoader.GetModSystem<WorldMapManager>();
        if (mapManager == null) return;

        WaypointMapLayer? wpLayer = mapManager.MapLayers?.OfType<WaypointMapLayer>().FirstOrDefault();
        if (wpLayer != null)
        {
            wpLayer.Waypoints.RemoveAll(w => w.Temporary && w.Guid == "tradersoverhaul-target");
            string traderName = string.IsNullOrWhiteSpace(trader.TraderName)
                ? ComerciantesLang.Get("trader-default-name")
                : trader.TraderName;
            wpLayer.AddTemporaryWaypoint(new Waypoint
            {
                Guid = "tradersoverhaul-target",
                Position = new Vec3d(trader.PosX + 0.5, trader.PosY, trader.PosZ + 0.5),
                Title = $"{traderName} [{ComerciantesLang.GetArchetypeName(trader.Archetype)}]",
                Icon = "trader",
                Color = ColorUtil.ColorFromRgba(245, 200, 75, 255),
                ShowInWorld = true,
                Pinned = true,
                Temporary = true
            });
        }

        // Si el minimapa HUD esta abierto, IsOpened() ya es true: forzamos cambiar al mapa grande (EnumDialogType.Dialog).
        if (mapManager.worldMapDlg == null
            || !mapManager.worldMapDlg.IsOpened()
            || mapManager.worldMapDlg.DialogType != EnumDialogType.Dialog)
        {
            mapManager.ToggleMap(EnumDialogType.Dialog);
        }

        GuiDialogWorldMap? mapDlg = mapManager.worldMapDlg;
        if (mapDlg == null) return;

        GuiComposer? fullComposer = mapDlg.Composers?["worldmapDialog"] ?? mapDlg.SingleComposer;
        if (fullComposer == null) return;

        GuiElementMap? mapElem = fullComposer.GetElement("mapElem") as GuiElementMap;
        if (mapElem == null)
        {
            Dictionary<string, GuiElement>? elems = Traverse.Create(fullComposer)
                .Field<Dictionary<string, GuiElement>>("interactiveElements")
                .Value;
            mapElem = elems?.Values.OfType<GuiElementMap>().FirstOrDefault();
        }

        if (mapElem != null)
        {
            Traverse.Create(mapElem).Field("snapToPlayer").SetValue(false);
            mapElem.CenterMapTo(new BlockPos(trader.PosX, trader.PosY, trader.PosZ));
            mapElem.EnsureMapFullyLoaded();
        }

        mapDlg.Focus();
    }

    private bool IsSellOfferOutOfStock(int traderIndex, int slotIndex)
    {
        StockOfferEntry? offer = _filteredTraders.ElementAtOrDefault(traderIndex)?.SellingOffers.ElementAtOrDefault(slotIndex);
        return offer != null && offer.Stock <= 0;
    }

    private string GetSellOfferTooltip(int traderIndex, int slotIndex)
    {
        StockOfferEntry? offer = _filteredTraders.ElementAtOrDefault(traderIndex)?.SellingOffers.ElementAtOrDefault(slotIndex);
        return offer != null
            ? TraderUiHelpers.FormatOfferTooltip(offer, "tooltip-dir-sell-price", "tooltip-dir-stock", "tooltip-dir-out-of-stock")
            : string.Empty;
    }

    private string GetWantedTooltip(int traderIndex, int slotIndex)
    {
        WantedItemEntry? wanted = _filteredTraders.ElementAtOrDefault(traderIndex)?.WantedItems.ElementAtOrDefault(slotIndex);
        return wanted != null ? TraderUiHelpers.FormatWantedTooltip(wanted, "tooltip-dir-wanted") : string.Empty;
    }
}
