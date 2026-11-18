using SystemUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Comerciantes;

/// <summary>
/// Ventana unificada de comercio basada en grillas y tarjetas enriquecidas sobre System UI.
/// </summary>
public sealed class GuiDialogCustomTrader : UiDialogBase
{
    private const int BuyOfferCols = 5;
    private const int BuyOfferRows = 4;
    private const int BuyOfferCount = BuyOfferCols * BuyOfferRows;
    private const int WantedSlotCount = 9;
    private const int CartSlotCount = 5;

    private readonly ITraderClientGateway _mod;
    private readonly InventoryGeneric _buyOfferInv;
    private readonly InventoryGeneric _buyCartInv;
    private readonly InventoryGeneric _wantedInv;
    private InventoryGeneric? _sellOfferInv;

    private readonly Dictionary<int, int> _buyCartPacks = new();
    private TraderStateSyncPacket? _state;
    private List<SellSlotValuationEntry> _sellQuotes = [];
    private string _statusMessage = string.Empty;
    private bool _lastStatusError;
    private ItemStack? _gearIconStack;

    public GuiDialogCustomTrader(ICoreClientAPI capi, ITraderClientGateway mod) : base(capi)
    {
        _mod = mod;

        _buyOfferInv = new InventoryGeneric(
            BuyOfferCount,
            "comerciantesbuy",
            "local",
            capi,
            (id, inv) => new DisplayOnlyItemSlot(inv, OnBuyOfferClicked, () => GetBuyOfferTooltip(id), () => IsBuyOfferOutOfStock(id)));

        _buyCartInv = new InventoryGeneric(
            CartSlotCount,
            "comerciantescart",
            "local",
            capi,
            (id, inv) => new DisplayOnlyItemSlot(inv, OnCartSlotClicked, () => GetCartSlotTooltip(id)));

        _wantedInv = new InventoryGeneric(
            WantedSlotCount,
            "comercianteswanted",
            "local",
            capi,
            (id, inv) => new DisplayOnlyItemSlot(inv, null, () => GetWantedTooltip(id)));
    }

    protected override string ComposerKey => "comerciantes_custom_trader";
    protected override string DialogTitle
    {
        get
        {
            if (_state == null) return ComerciantesLang.Get("gui-trade-title-fallback");
            string traderName = string.IsNullOrWhiteSpace(_state.TraderName)
                ? ComerciantesLang.Get("trader-default-name")
                : _state.TraderName;
            return $"{traderName} ({ComerciantesLang.GetArchetypeName(_state.Archetype)})";
        }
    }

    protected override double DialogWidth => 780;
    protected override double DialogHeight => 500;

    private InventoryGeneric EnsureSellInventory()
    {
        if (_sellOfferInv == null)
        {
            string uid = capi.World.Player?.PlayerUID ?? "local";
            _sellOfferInv = new InventoryGeneric(
                ComerciantesModSystem.SellSlotCount,
                ComerciantesModSystem.SellInventoryClass,
                uid,
                capi,
                (_, inv) => new SellOfferItemSlot(inv, GetSellSlotTooltip));
            _sellOfferInv.SlotModified += OnSellOfferSlotModified;
        }
        return _sellOfferInv;
    }

    private ItemStack? GetGearIconStack()
    {
        if (_gearIconStack == null)
        {
            Item? gear = capi.World.GetItem(new AssetLocation("game", "gear-rusty"));
            if (gear != null) _gearIconStack = new ItemStack(gear, 1);
        }
        return _gearIconStack;
    }

    public void UpdateState(TraderStateSyncPacket state, string? statusMessage = null, bool isError = false)
    {
        if (_state != null && _state.EntityId != state.EntityId)
        {
            _buyCartPacks.Clear();
        }

        _state = state;
        _sellQuotes = state.SellSlotQuotes ?? [];
        if (statusMessage != null)
        {
            _statusMessage = statusMessage;
            _lastStatusError = isError;
        }
        else
        {
            _statusMessage = string.Empty;
            _lastStatusError = false;
        }

        foreach (int slotIdx in _buyCartPacks.Keys.ToList())
        {
            StockOfferEntry? offer = _state.SellingOffers.FirstOrDefault(o => o.SellingSlotIndex == slotIdx);
            if (offer == null || offer.Stock <= 0)
            {
                _buyCartPacks.Remove(slotIdx);
            }
            else if (_buyCartPacks[slotIdx] > offer.Stock)
            {
                _buyCartPacks[slotIdx] = offer.Stock;
            }
        }

        EnsureSellInventory();
        PopulateDisplayGrids();

        if (IsOpened())
        {
            ComposeDialog();
        }
    }

    public void UpdateSellQuotes(long entityId, List<SellSlotValuationEntry> quotes)
    {
        if (_state == null || _state.EntityId != entityId) return;
        _sellQuotes = quotes;
        UpdateDynamicSummaries();
    }

    public void SetStatusMessage(string message, bool isError)
    {
        _statusMessage = message;
        _lastStatusError = isError;
        UpdateDynamicSummaries();
    }

    protected override void ComposeContent(GuiComposer composer, ElementBounds contentBounds)
    {
        if (_state == null)
        {
            composer.AddStaticText(ComerciantesLang.Get("gui-trade-loading"), Theme.BodyFont(), ElementBounds.Fixed(0, 0, DialogWidth, 30));
            return;
        }

        double topY = contentBounds.fixedY;
        double leftX = 0;
        double leftCardW = 280;
        double rightX = 290;
        double rightCardW = 490;

        // Tarjeta 1 (Izq): Vendo (5x4 = 20 slots)
        composer.AddPanelCard(leftX, topY, leftCardW, 248, Theme);
        composer.AddStockSlotGridSection(
            ComerciantesLang.Get("gui-trade-section-buy"),
            _buyOfferInv,
            _ => { },
            BuyOfferCols,
            BuyOfferRows,
            leftX + 10,
            topY + 8,
            Theme,
            "buyGrid",
            out _,
            leftCardW - 20);

        // Tarjeta 2 (Izq): Carrito (5x1)
        double cartY = topY + 258;
        composer.AddPanelCard(leftX, cartY, leftCardW, 96, Theme);
        composer.AddStockSlotGridSection(
            ComerciantesLang.Get("gui-trade-section-cart"),
            _buyCartInv,
            _ => { },
            CartSlotCount,
            1,
            leftX + 10,
            cartY + 8,
            Theme,
            "cartGrid",
            out _,
            leftCardW - 20);

        // Tarjeta 3 (Der): Necesito (+X% mas) (9x1)
        composer.AddPanelCard(rightX, topY, rightCardW, 96, Theme);
        composer.AddRichTextBlock(
            BuildWantedHeaderRichText(),
            ElementBounds.Fixed(rightX + 10, topY + 8, rightCardW - 20, 22),
            "wantedHeader");
        composer.AddStockSlotGridOnly(
            _wantedInv,
            _ => { },
            WantedSlotCount,
            1,
            rightX + 10,
            topY + 32,
            "wantedGrid");

        // Tarjeta 4 (Der): Que te Compro? (5x1 + resumen compacto de 5 lineas)
        double sellY = topY + 106;
        composer.AddPanelCard(rightX, sellY, rightCardW, 248, Theme);
        composer.AddStockSlotGridSection(
            ComerciantesLang.Get("gui-trade-section-sell"),
            EnsureSellInventory(),
            p => capi.Network.SendPacketClient(p),
            ComerciantesModSystem.SellSlotCount,
            1,
            rightX + 10,
            sellY + 8,
            Theme,
            "sellGrid",
            out _,
            rightCardW - 20);

        composer.AddRichTextBlock(
            BuildSellRichText(),
            ElementBounds.Fixed(rightX + 10, sellY + 92, rightCardW - 20, 148),
            "sellSummary");

        // Pie de ventana en 3 columnas: info izquierda, resumen central, botones derecha
        double footerY = topY + 366;

        composer.AddRichTextBlock(
            BuildHeaderRichText(),
            ElementBounds.Fixed(4, footerY + 8, 236, 88),
            "headerInfo");

        composer.AddPanelCard(248, footerY, 284, 100, Theme);
        composer.AddRichTextBlock(
            BuildBalanceRichText(),
            ElementBounds.Fixed(260, footerY + 8, 260, 84),
            "balanceBar");

        composer.AddIconActionButton(
            ComerciantesLang.Get("gui-trade-btn-directory"),
            new AssetLocation(ComerciantesLang.Domain, "textures/icons/directory.svg"),
            () =>
            {
                _mod.OpenDirectoryFromClient();
                return true;
            },
            ElementBounds.Fixed(548, footerY, 232, 28),
            Theme,
            "btnDirectory");

        composer.AddIconActionButton(
            ComerciantesLang.Get("gui-trade-btn-clear-cart"),
            new AssetLocation(ComerciantesLang.Domain, "textures/icons/clear-cart.svg"),
            () =>
            {
                _buyCartPacks.Clear();
                RefreshCartDisplay();
                UpdateDynamicSummaries();
                return true;
            },
            ElementBounds.Fixed(548, footerY + 36, 232, 28),
            Theme,
            "btnClearCart");

        composer.AddIconActionButton(
            ComerciantesLang.Get("gui-trade-btn-trade"),
            new AssetLocation(ComerciantesLang.Domain, "textures/icons/close-deal.svg"),
            OnExecuteTradeClicked,
            ElementBounds.Fixed(548, footerY + 72, 232, 28),
            Theme,
            "btnTrade");

        composer.AddRichTextBlock(
            BuildStatusRichText(),
            ElementBounds.Fixed(4, footerY + 106, DialogWidth - 8, 22),
            "statusMsg");
    }

    public override void OnGuiOpened()
    {
        capi.World.Player.InventoryManager.OpenInventory(EnsureSellInventory());
        base.OnGuiOpened();
    }

    public override void OnGuiClosed()
    {
        _buyCartPacks.Clear();
        if (_sellOfferInv != null)
        {
            capi.World.Player.InventoryManager.CloseInventory(_sellOfferInv);
        }

        long traderId = _state?.EntityId ?? 0;
        if (traderId > 0)
        {
            if (capi.World.GetEntityById(traderId) is EntityTradingHumanoid trader)
            {
                TraderEntityInterop.ClearInteractingPlayer(trader, null);
            }
            capi.Network.SendEntityPacket(traderId, 1212);
        }

        _mod.SendCloseTradeSession(traderId);
        base.OnGuiClosed();
    }

    private void OnSellOfferSlotModified(int _)
    {
        if (_state == null) return;
        _mod.RequestSellQuotes(_state.EntityId);
    }

    private bool OnExecuteTradeClicked()
    {
        if (_state == null) return true;

        List<BuyCartLine> lines = _buyCartPacks
            .Where(kv => kv.Value > 0)
            .Select(kv => new BuyCartLine { SellingSlotIndex = kv.Key, PackCount = kv.Value })
            .ToList();

        _mod.SendTradeRequest(new TradeRequestPacket
        {
            EntityId = _state.EntityId,
            BuyCart = lines
        });
        return true;
    }

    private void OnBuyOfferClicked(int slotIndex, EnumMouseButton button)
    {
        if (_state == null) return;
        StockOfferEntry? offer = _state.SellingOffers.ElementAtOrDefault(slotIndex);
        if (offer == null || offer.Stock <= 0) return;

        int key = offer.SellingSlotIndex;
        int current = _buyCartPacks.TryGetValue(key, out int existing) ? existing : 0;

        if (button == EnumMouseButton.Right)
        {
            if (current > 1) _buyCartPacks[key] = current - 1;
            else _buyCartPacks.Remove(key);
        }
        else
        {
            if (_buyCartPacks.Count >= CartSlotCount && !_buyCartPacks.ContainsKey(key))
            {
                _statusMessage = ComerciantesLang.Get("gui-trade-cart-limit", CartSlotCount);
                _lastStatusError = true;
                UpdateDynamicSummaries();
                return;
            }
            if (current < offer.Stock)
            {
                _buyCartPacks[key] = current + 1;
            }
        }

        RefreshCartDisplay();
        UpdateDynamicSummaries();
    }

    private void OnCartSlotClicked(int cartSlotIndex, EnumMouseButton button)
    {
        List<int> keys = _buyCartPacks.Keys.ToList();
        if (cartSlotIndex < 0 || cartSlotIndex >= keys.Count) return;

        int offerKey = keys[cartSlotIndex];
        if (button == EnumMouseButton.Right)
        {
            _buyCartPacks.Remove(offerKey);
        }
        else if (_buyCartPacks[offerKey] > 1)
        {
            _buyCartPacks[offerKey]--;
        }
        else
        {
            _buyCartPacks.Remove(offerKey);
        }

        RefreshCartDisplay();
        UpdateDynamicSummaries();
    }

    private void PopulateDisplayGrids()
    {
        EntityTradingHumanoid? liveTrader = _state != null
            ? capi.World.GetEntityById(_state.EntityId) as EntityTradingHumanoid
            : null;

        for (int i = 0; i < _buyOfferInv.Count; i++)
        {
            StockOfferEntry? offer = _state?.SellingOffers.ElementAtOrDefault(i);
            ItemStack? liveStack = offer != null && liveTrader?.Inventory?.SellingSlots != null
                && offer.SellingSlotIndex >= 0 && offer.SellingSlotIndex < liveTrader.Inventory.SellingSlots.Length
                ? liveTrader.Inventory.SellingSlots[offer.SellingSlotIndex]?.Itemstack?.Clone()
                : null;

            _buyOfferInv[i].Itemstack = liveStack ?? (offer != null ? TraderUiHelpers.ResolveStack(capi.World, offer.StackBytes, offer.ItemCode, offer.StackSize) : null);
            _buyOfferInv[i].MarkDirty();
        }

        for (int i = 0; i < _wantedInv.Count; i++)
        {
            WantedItemEntry? wanted = _state?.WantedItems.ElementAtOrDefault(i);
            _wantedInv[i].Itemstack = wanted != null ? TraderUiHelpers.ResolveStack(capi.World, wanted.StackBytes, wanted.ItemCode, Math.Max(1, wanted.MinLotSize)) : null;
            _wantedInv[i].MarkDirty();
        }

        RefreshCartDisplay();
    }

    private void RefreshCartDisplay()
    {
        List<KeyValuePair<int, int>> entries = _buyCartPacks.Where(kv => kv.Value > 0).ToList();
        for (int i = 0; i < _buyCartInv.Count; i++)
        {
            if (i < entries.Count && _state != null)
            {
                StockOfferEntry? offer = _state.SellingOffers.FirstOrDefault(o => o.SellingSlotIndex == entries[i].Key);
                _buyCartInv[i].Itemstack = offer != null
                    ? TraderUiHelpers.ResolveStack(capi.World, offer.StackBytes, offer.ItemCode, offer.StackSize * entries[i].Value)
                    : null;
            }
            else
            {
                _buyCartInv[i].Itemstack = null;
            }
            _buyCartInv[i].MarkDirty();
        }
    }

    private void UpdateDynamicSummaries()
    {
        if (SingleComposer == null) return;
        SingleComposer.UpdateRichTextBlock("sellSummary", BuildSellRichText());
        SingleComposer.UpdateRichTextBlock("balanceBar", BuildBalanceRichText());
        SingleComposer.UpdateRichTextBlock("statusMsg", BuildStatusRichText());
    }

    private int ComputeBuyTotal()
    {
        if (_state == null) return 0;
        int total = 0;
        foreach ((int slotIdx, int packs) in _buyCartPacks)
        {
            StockOfferEntry? offer = _state.SellingOffers.FirstOrDefault(o => o.SellingSlotIndex == slotIdx);
            if (offer != null) total += offer.PriceGears * packs;
        }
        return total;
    }

    private int ComputeSellTotal() =>
        _sellQuotes.Where(q => q.IsTradeable && q.ConsumedUnits > 0 && q.TotalPayoutGears > 0).Sum(q => q.TotalPayoutGears);

    private RichTextComponentBase[] BuildWantedHeaderRichText()
    {
        return new UiRichTextBuilder(capi, Theme)
            .Text(ComerciantesLang.Get("gui-trade-section-wanted-prefix"), Theme.TitleFont())
            .Positive(ComerciantesLang.Get("gui-trade-section-wanted-bonus", $"{_state?.TraderRewardBonusPct ?? 0:0}"), detail: false, bold: false)
            .Build();
    }

    private RichTextComponentBase[] BuildHeaderRichText()
    {
        double daysLeft = _state != null ? Math.Max(0, _state.NextRefreshTotalDays - _state.CurrentTotalDays) : 0;
        string mode = ComerciantesLang.GetEconomyModeName(_state?.EconomyModeCode);
        return new UiRichTextBuilder(capi, Theme)
            .Text(ComerciantesLang.Get("gui-trade-header-funds"), Theme.BodyFont())
            .Bold($"{_state?.TraderMoney ?? 0} ", detail: false)
            .ItemIcon(GetGearIconStack(), 18, 2)
            .NewLine()
            .Text(ComerciantesLang.Get("gui-trade-header-rotation"), Theme.BodyFont())
            .Bold(TraderUiHelpers.FormatRotationRemaining(daysLeft), detail: false)
            .NewLine()
            .Text(ComerciantesLang.Get("gui-trade-header-mode"), Theme.BodyFont())
            .Bold(mode, detail: false)
            .Build();
    }

    private RichTextComponentBase[] BuildSellRichText()
    {
        var rb = new UiRichTextBuilder(capi, Theme);
        if (_sellQuotes.Count == 0)
        {
            return rb.Muted(ComerciantesLang.Get("gui-trade-sell-empty", ComerciantesModSystem.SellSlotCount)).Build();
        }

        ItemStack? gear = GetGearIconStack();
        bool first = true;
        foreach (SellSlotValuationEntry q in _sellQuotes.Take(5))
        {
            if (!first) rb.NewLine();
            first = false;

            string itemName = _sellOfferInv != null && q.SellSlotIndex >= 0 && q.SellSlotIndex < _sellOfferInv.Count
                ? (_sellOfferInv[q.SellSlotIndex]?.Itemstack?.GetName() ?? q.DisplayName)
                : q.DisplayName;

            if (!q.IsTradeable)
            {
                rb.Bold("0 ").ItemIcon(gear, 16, 4).Text($"• {itemName} ")
                    .Negative(ComerciantesLang.Get(q.IsOverstocked ? "gui-trade-sell-overstocked" : "gui-trade-sell-untradeable"), detail: true, bold: false);
                continue;
            }

            int lotSize = Math.Max(1, q.CurrentLotSize);
            if (q.TotalPayoutGears <= 0)
            {
                int missing = Math.Max(1, lotSize - q.QuantityInSlot);
                rb.Bold("0 ").ItemIcon(gear, 16, 4).Text($"• {itemName} ")
                    .Warning(ComerciantesLang.Get("gui-trade-sell-missing", missing), detail: true, bold: false)
                    .Muted(ComerciantesLang.Get("gui-trade-sell-lot-rate", lotSize, q.LotGearPayout));
                continue;
            }

            rb.Bold($"{q.TotalPayoutGears} ").ItemIcon(gear, 16, 4).Text($"• {itemName} ");

            if (lotSize > 1 && q.ConsumedLots > 0 && q.ConsumedUnits == lotSize * q.ConsumedLots)
            {
                rb.Bold($"{lotSize}×{q.ConsumedLots} ");
            }
            else if (q.ConsumedUnits > 1)
            {
                rb.Bold($"×{q.ConsumedUnits} ");
            }

            if (Math.Abs(q.DeltaPct) <= 0.5)
            {
                rb.Muted("0%");
            }
            else
            {
                rb.Text(SystemUITheme.FormatTrend(q.DeltaPct, spaced: true, includeNeutralPct: false), Theme.TrendFont(q.DeltaPct));
            }
        }
        return rb.Build();
    }

    private RichTextComponentBase[] BuildBalanceRichText()
    {
        int buy = ComputeBuyTotal();
        int sell = ComputeSellTotal();
        int net = sell - buy;
        ItemStack? gear = GetGearIconStack();

        var rb = new UiRichTextBuilder(capi, Theme)
            .Text(ComerciantesLang.Get("gui-trade-balance-sell"), Theme.BodyFont())
            .Positive($"+{sell} ", detail: false)
            .ItemIcon(gear, 18, 2)
            .NewLine()
            .Text(ComerciantesLang.Get("gui-trade-balance-buy"), Theme.BodyFont());

        if (buy > 0) rb.Warning($"-{buy} ", detail: false);
        else rb.Muted("0 ", detail: false);

        rb.ItemIcon(gear, 18, 2)
            .NewLine()
            .Muted("─────────────────────────")
            .NewLine()
            .Text(ComerciantesLang.Get("gui-trade-balance-net"), Theme.TitleFont());

        if (net >= 0) rb.Positive($"+{net} ", detail: false);
        else rb.Negative($"{net} ", detail: false);

        rb.ItemIcon(gear, 20, 2);
        return rb.Build();
    }

    private RichTextComponentBase[] BuildStatusRichText()
    {
        var rb = new UiRichTextBuilder(capi, Theme);
        if (string.IsNullOrEmpty(_statusMessage)) return rb.Build();
        return (_lastStatusError ? rb.Negative(_statusMessage) : rb.Positive(_statusMessage)).Build();
    }

    private string GetSellSlotTooltip(int slotIndex)
    {
        SellSlotValuationEntry? q = _sellQuotes.FirstOrDefault(x => x.SellSlotIndex == slotIndex);
        if (q == null) return string.Empty;
        if (!q.IsTradeable)
        {
            return ComerciantesLang.Get(q.IsOverstocked ? "tooltip-sell-overstocked" : "tooltip-sell-untradeable");
        }

        int lotSize = Math.Max(1, q.CurrentLotSize);
        string arrow = SystemUITheme.FormatTrend(q.DeltaPct, spaced: true, includeNeutralPct: false);
        string payoutLine = q.TotalPayoutGears > 0
            ? ComerciantesLang.Get("tooltip-sell-payout", q.TotalPayoutGears, q.ConsumedUnits, q.QuantityInSlot)
            : ComerciantesLang.Get("tooltip-sell-missing", Math.Max(1, lotSize - q.QuantityInSlot), q.LotGearPayout);

        return ComerciantesLang.Get(
            "tooltip-sell-details",
            $"{q.BaseValue:0.###}",
            $"{q.AffinityMod:0.##}",
            $"{q.DemandMod:0.##}",
            $"{q.SaturationMod:0.##}",
            $"{(1.0 - q.SaturationMod) * 100:0}",
            lotSize,
            q.LotGearPayout,
            arrow,
            payoutLine);
    }

    private bool IsBuyOfferOutOfStock(int index)
    {
        StockOfferEntry? offer = _state?.SellingOffers.ElementAtOrDefault(index);
        return offer != null && offer.Stock <= 0;
    }

    private string GetBuyOfferTooltip(int index)
    {
        StockOfferEntry? offer = _state?.SellingOffers.ElementAtOrDefault(index);
        return offer != null
            ? TraderUiHelpers.FormatOfferTooltip(offer, "tooltip-buy-price", "tooltip-buy-stock", "tooltip-buy-out-of-stock")
            : string.Empty;
    }

    private string GetCartSlotTooltip(int index)
    {
        List<KeyValuePair<int, int>> entries = _buyCartPacks.Where(kv => kv.Value > 0).ToList();
        if (index < 0 || index >= entries.Count || _state == null) return string.Empty;
        StockOfferEntry? offer = _state.SellingOffers.FirstOrDefault(o => o.SellingSlotIndex == entries[index].Key);
        if (offer == null) return string.Empty;
        return ComerciantesLang.Get("tooltip-cart-slot", entries[index].Value, offer.PriceGears * entries[index].Value);
    }

    private string GetWantedTooltip(int index)
    {
        WantedItemEntry? wanted = _state?.WantedItems.ElementAtOrDefault(index);
        return wanted != null ? TraderUiHelpers.FormatWantedTooltip(wanted, "tooltip-wanted-trade") : string.Empty;
    }
}
