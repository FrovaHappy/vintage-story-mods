using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Comerciantes;

/// <summary>
/// ModSystem principal de Comerciantes. Coordina configuracion, motor economico en servidor,
/// parches Harmony e integracion con System UI en cliente.
/// </summary>
public sealed class ComerciantesModSystem : ModSystem, ITraderClientGateway
{
    public const int SellSlotCount = 5;
    public const string SellInventoryClass = "comerciantessell";

    private const string ChannelName = "tradersoverhaul";
    private const string ConfigFileName = "tradersoverhaul.json";
    private const string HarmonyId = "seraphmods.tradersoverhaul";

    private Harmony? _harmony;
    private EnumAppSide _side;
    private ICoreServerAPI? _sapi;
    private ICoreClientAPI? _capi;
    private IServerNetworkChannel? _serverChannel;
    private IClientNetworkChannel? _clientChannel;

    private readonly Dictionary<string, InventoryGeneric> _serverSellInventories = new();
    private readonly Dictionary<string, long> _activeTraderSessions = new();
    private readonly HashSet<string> _registeredDirectoryCommands = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastConfigWriteUtc = DateTime.MinValue;

    private GuiDialogCustomTrader? _tradeDialog;
    private GuiDialogTraderDirectory? _directoryDialog;

    public ComerciantesConfig Config { get; private set; } = new();
    public ItemValuationRegistry Valuations { get; } = new();
    public TraderAffinityMatrix Affinities { get; } = new();
    public WorldSaturationRegistry Saturation { get; } = new();
    public KnownTradersRegistry Directory { get; } = new();
    public PriceCalculator? Calculator { get; private set; }

    public override void Start(ICoreAPI api)
    {
        _side = api.Side;
        Config = api.LoadModConfig<ComerciantesConfig>(ConfigFileName) ?? new ComerciantesConfig();
        Config.Normalize();
        api.StoreModConfig(Config, ConfigFileName);
        UpdateConfigTimestamp();

        Calculator = new PriceCalculator(Valuations, Affinities, Saturation);

        if (_side == EnumAppSide.Client)
        {
            TraderDialogPatch.ClientInstance = this;
        }

        if (!Harmony.HasAnyPatches(HarmonyId))
        {
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll();
        }
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        Valuations.LoadFromAssets(api);
        Affinities.LoadFromAssets(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;

        api.Event.SaveGameLoaded += () =>
        {
            Valuations.InferRecipeValues(api);
            Saturation.Load(api);
            Directory.Load(api);
        };
        api.Event.GameWorldSave += () =>
        {
            Saturation.Save(api);
            Directory.Save(api);
        };
        api.Event.PlayerDisconnect += player =>
        {
            _activeTraderSessions.Remove(player.PlayerUID);
            ReturnAndCloseSellInventory(player);
        };

        api.Event.RegisterEventBusListener(OnServerConfigLibSettingChanged, 0.5, "configlib:tradersoverhaul:setting-changed");
        api.Event.RegisterEventBusListener(OnServerConfigLibSaved, 0.5, "configlib:tradersoverhaul:config-saved");

        _serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<OpenTraderRequestPacket>()
            .RegisterMessageType<InspectTraderRequestPacket>()
            .RegisterMessageType<TraderStateSyncPacket>()
            .RegisterMessageType<TradeRequestPacket>()
            .RegisterMessageType<TradeResponsePacket>()
            .RegisterMessageType<SellSlotQuoteRequestPacket>()
            .RegisterMessageType<SellSlotQuoteResponsePacket>()
            .RegisterMessageType<CloseTradeSessionPacket>()
            .RegisterMessageType<DirectoryOpenRequestPacket>()
            .RegisterMessageType<KnownTradersSyncPacket>()
            .RegisterMessageType<ConfigReloadedNotifyPacket>()
            .SetMessageHandler<OpenTraderRequestPacket>(OnServerOpenTraderRequest)
            .SetMessageHandler<InspectTraderRequestPacket>(OnServerInspectTraderRequest)
            .SetMessageHandler<TradeRequestPacket>(OnServerTradeRequest)
            .SetMessageHandler<SellSlotQuoteRequestPacket>(OnServerSellQuoteRequest)
            .SetMessageHandler<CloseTradeSessionPacket>(OnServerCloseTradeSession)
            .SetMessageHandler<DirectoryOpenRequestPacket>(OnServerDirectoryRequest);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _capi = api;
        _tradeDialog = new GuiDialogCustomTrader(api, this);
        _directoryDialog = new GuiDialogTraderDirectory(api, this);

        api.Event.RegisterEventBusListener(OnClientConfigLibSettingChanged, 0.5, "configlib:tradersoverhaul:setting-changed");
        api.Event.RegisterEventBusListener(OnClientConfigLibSaved, 0.5, "configlib:tradersoverhaul:config-saved");

        _clientChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<OpenTraderRequestPacket>()
            .RegisterMessageType<InspectTraderRequestPacket>()
            .RegisterMessageType<TraderStateSyncPacket>()
            .RegisterMessageType<TradeRequestPacket>()
            .RegisterMessageType<TradeResponsePacket>()
            .RegisterMessageType<SellSlotQuoteRequestPacket>()
            .RegisterMessageType<SellSlotQuoteResponsePacket>()
            .RegisterMessageType<CloseTradeSessionPacket>()
            .RegisterMessageType<DirectoryOpenRequestPacket>()
            .RegisterMessageType<KnownTradersSyncPacket>()
            .RegisterMessageType<ConfigReloadedNotifyPacket>()
            .SetMessageHandler<TraderStateSyncPacket>(OnClientTraderState)
            .SetMessageHandler<TradeResponsePacket>(OnClientTradeResponse)
            .SetMessageHandler<SellSlotQuoteResponsePacket>(OnClientSellQuoteResponse)
            .SetMessageHandler<KnownTradersSyncPacket>(OnClientDirectorySync)
            .SetMessageHandler<ConfigReloadedNotifyPacket>(OnClientConfigReloaded);

        api.Input.RegisterHotKey(
            "comerciantes_directory",
            ComerciantesLang.Get("hotkey-directory"),
            GlKeys.K,
            HotkeyType.GUIOrOtherControls);
        api.Input.SetHotKeyHandler("comerciantes_directory", _ =>
        {
            OpenDirectoryFromClient();
            return true;
        });

        EnsureDirectoryCommandRegistered(api);
    }

    private void EnsureDirectoryCommandRegistered(ICoreClientAPI? api)
    {
        if (api == null || !Config.EnableDirectoryCommand) return;

        string cmdName = Config.GetEffectiveDirectoryCommandName();
        if (!_registeredDirectoryCommands.Add(cmdName)) return;
        if (api.ChatCommands.Get(cmdName) != null) return;

        api.ChatCommands.Create(cmdName)
            .WithDescription(ComerciantesLang.Get("cmd-directory-desc"))
            .WithPreCondition(_ =>
            {
                if (Config.EnableDirectoryCommand
                    && string.Equals(cmdName, Config.GetEffectiveDirectoryCommandName(), StringComparison.OrdinalIgnoreCase))
                {
                    return TextCommandResult.Success();
                }

                return TextCommandResult.Error(ComerciantesLang.Get("msg-command-disabled"));
            })
            .HandleWith(_ =>
            {
                OpenDirectoryFromClient();
                return TextCommandResult.Success();
            });
    }

    public void OpenDirectoryFromClient()
    {
        if (_directoryDialog == null) return;
        if (_directoryDialog.IsOpened())
        {
            _directoryDialog.TryClose();
        }
        else
        {
            _clientChannel?.SendPacket(new DirectoryOpenRequestPacket());
        }
    }

    public void ReloadConfig(ICoreAPI? api)
    {
        if (api == null) return;
        ComerciantesConfig? loaded = api.LoadModConfig<ComerciantesConfig>(ConfigFileName);
        if (loaded != null)
        {
            loaded.Normalize();
            Config = loaded;
        }
        UpdateConfigTimestamp();
    }

    private void ReloadConfigIfDiskChanged(ICoreAPI? api)
    {
        if (api == null) return;
        try
        {
            string path = Path.Combine(GamePaths.ModConfig, ConfigFileName);
            if (!File.Exists(path)) return;
            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (writeUtc > _lastConfigWriteUtc)
            {
                ReloadConfig(api);
            }
        }
        catch
        {
            // Ignora errores transitorios de lectura mientras ConfigLib guarda el archivo
        }
    }

    private void UpdateConfigTimestamp()
    {
        try
        {
            string path = Path.Combine(GamePaths.ModConfig, ConfigFileName);
            if (File.Exists(path))
            {
                _lastConfigWriteUtc = File.GetLastWriteTimeUtc(path);
            }
        }
        catch
        {
        }
    }

    private void OnServerConfigLibSettingChanged(string eventName, ref EnumHandling handling, IAttribute data)
    {
        if (data is not ITreeAttribute tree) return;
        string? setting = tree.GetString("setting");
        IAttribute? val = tree["value"];
        if (Config.ApplySetting(setting, val))
        {
            PushLiveRefreshAfterConfigChange($"ConfigLib live: {setting}");
        }
    }

    private void OnServerConfigLibSaved(string eventName, ref EnumHandling handling, IAttribute data)
    {
        if (_sapi == null) return;

        if (_sapi.Server.IsDedicated)
        {
            _sapi.StoreModConfig(Config, ConfigFileName);
            UpdateConfigTimestamp();
        }
        else
        {
            ReloadConfig(_sapi);
        }

        PushLiveRefreshAfterConfigChange("ConfigLib guardado");
    }

    private void OnClientConfigLibSettingChanged(string eventName, ref EnumHandling handling, IAttribute data)
    {
        if (data is not ITreeAttribute tree) return;
        string? setting = tree.GetString("setting");
        IAttribute? val = tree["value"];
        if (Config.ApplySetting(setting, val))
        {
            if (string.Equals(setting, nameof(ComerciantesConfig.EnableDirectoryCommand), StringComparison.Ordinal)
                || string.Equals(setting, nameof(ComerciantesConfig.DirectoryCommandName), StringComparison.Ordinal))
            {
                EnsureDirectoryCommandRegistered(_capi);
            }
            DebugLogClient($"Ajuste aplicado en vivo: {setting}");
        }
    }

    private void OnClientConfigLibSaved(string eventName, ref EnumHandling handling, IAttribute data)
    {
        ReloadConfig(_capi);
        EnsureDirectoryCommandRegistered(_capi);
        DebugLogClient("Configuracion recargada tras guardar en ConfigLib.");
    }

    private void PushLiveRefreshAfterConfigChange(string reason)
    {
        if (_sapi == null || _serverChannel == null) return;

        foreach ((string playerUid, long entityId) in _activeTraderSessions.ToList())
        {
            if (_sapi.World.PlayerByUid(playerUid) is not IServerPlayer player) continue;
            if (_sapi.World.GetEntityById(entityId) is not EntityTradingHumanoid trader) continue;

            InventoryGeneric sellInv = GetOrOpenServerSellInventory(player);
            TraderStateSyncPacket state = BuildTraderState(trader, sellInv);
            Directory.RecordVisit(player.PlayerUID, ToKnownRecord(trader, state));
            _serverChannel.SendPacket(state, player);
            DebugLogServer(player, $"{reason} -> modo {Config.EconomyMode}, bono {Config.DailyDemandMinPct:0}-{Config.DailyDemandMaxPct:0}%");
        }

        _serverChannel.BroadcastPacket(new ConfigReloadedNotifyPacket());
    }

    public void RequestOpenTrader(long entityId) =>
        _clientChannel?.SendPacket(new OpenTraderRequestPacket { EntityId = entityId });

    public void RequestInspectTrader(long entityId) =>
        _clientChannel?.SendPacket(new InspectTraderRequestPacket { EntityId = entityId });

    public void SendTradeRequest(TradeRequestPacket packet) => _clientChannel?.SendPacket(packet);

    public void RequestSellQuotes(long entityId) =>
        _clientChannel?.SendPacket(new SellSlotQuoteRequestPacket { EntityId = entityId });

    public void SendCloseTradeSession(long entityId) =>
        _clientChannel?.SendPacket(new CloseTradeSessionPacket { EntityId = entityId });

    private void OnServerCloseTradeSession(IServerPlayer player, CloseTradeSessionPacket packet)
    {
        _activeTraderSessions.Remove(player.PlayerUID);
        ReturnAndCloseSellInventory(player);
        if (_sapi != null && packet.EntityId > 0 && _sapi.World.GetEntityById(packet.EntityId) is EntityTradingHumanoid trader)
        {
            TraderEntityInterop.ClearInteractingPlayer(trader, player.Entity);
        }
    }

    public void AppendInventoryValuationTooltip(ItemSlot? inSlot, StringBuilder dsc, IWorldAccessor world)
    {
        if (world.Side != EnumAppSide.Client || !Config.ShowValuationInInventoryTooltip) return;
        if (inSlot?.Itemstack == null) return;

        InventoryValuationHint hint = Valuations.GetInventoryValuationHint(inSlot.Itemstack);
        if (!hint.IsValuable) return;

        if (hint.BaseValue < 1.0)
        {
            dsc.AppendLine(ComerciantesLang.Get("tooltip-inv-base-lot", hint.LotSize, hint.LotGearPayout));
        }
        else
        {
            dsc.AppendLine(ComerciantesLang.Get("tooltip-inv-base-single", $"{hint.BaseValue:0.#}"));
        }
    }

    private void OnServerOpenTraderRequest(IServerPlayer player, OpenTraderRequestPacket packet)
    {
        if (_sapi == null) return;
        if (_sapi.World.GetEntityById(packet.EntityId) is not EntityTradingHumanoid trader) return;
        if (player.Entity.Pos.DistanceTo(trader.Pos) > Config.MaxPhysicalTradeDistance + 2.0) return;

        HandleServerOpenTrader(trader, player);
    }

    private void OnServerInspectTraderRequest(IServerPlayer player, InspectTraderRequestPacket packet)
    {
        if (_sapi == null) return;

        ReloadConfigIfDiskChanged(_sapi);
        if (!Config.EnableInstantTrade) return;

        InventoryGeneric sellInv = GetOrOpenServerSellInventory(player);
        KnownTraderRecord? known = Directory.GetKnownTrader(player.PlayerUID, packet.EntityId);
        EntityTradingHumanoid? liveTrader = _sapi.World.GetEntityById(packet.EntityId) as EntityTradingHumanoid;

        TraderStateSyncPacket? state = liveTrader != null
            ? BuildTraderState(liveTrader, sellInv)
            : (known != null ? BuildTraderStateFromSnapshot(known, sellInv) : null);

        if (state == null) return;

        _activeTraderSessions[player.PlayerUID] = packet.EntityId;
        _serverChannel?.SendPacket(state, player);
        DebugLogServer(player, $"Comercio instantaneo: {state.TraderName} ({state.Archetype}) | EnVivo={liveTrader != null}");
    }

    public bool IsDebugActive()
    {
#if DEBUG
        return true;
#else
        return Config.EnableDebugLogging;
#endif
    }

    private void DebugLogServer(IServerPlayer? player, string message)
    {
        if (!IsDebugActive()) return;
        _sapi?.Logger.Notification($"[TradersOverhaul][DEBUG] {message}");
        player?.SendMessage(GlobalConstants.GeneralChatGroup, $"[TradersOverhaul DEBUG] {message}", EnumChatType.Notification);
    }

    private void DebugLogClient(string message)
    {
        if (!IsDebugActive()) return;
        _capi?.Logger.Notification($"[TradersOverhaul][DEBUG] {message}");
        _capi?.ShowChatMessage($"[TradersOverhaul DEBUG] {message}");
    }

    public void HandleServerOpenTrader(EntityTradingHumanoid trader, IServerPlayer player)
    {
        if (_sapi == null) return;

        ReloadConfigIfDiskChanged(_sapi);
        _activeTraderSessions[player.PlayerUID] = trader.EntityId;
        TraderEntityInterop.ClearInteractingPlayer(trader, player.Entity);
        InventoryGeneric sellInv = GetOrOpenServerSellInventory(player);
        TraderStateSyncPacket state = BuildTraderState(trader, sellInv);
        Directory.RecordVisit(player.PlayerUID, ToKnownRecord(trader, state));
        _serverChannel?.SendPacket(state, player);

        double daysLeft = Math.Max(0.0, state.NextRefreshTotalDays - state.CurrentTotalDays);
        DebugLogServer(player, $"Abierto {state.TraderName} ({state.Archetype}) | Fondo={state.TraderMoney} | Rotacion={daysLeft:0.0}d | Ofertas={state.SellingOffers.Count} | Reclamados={state.WantedItems.Count}");
    }

    private InventoryGeneric GetOrOpenServerSellInventory(IServerPlayer player)
    {
        if (!_serverSellInventories.TryGetValue(player.PlayerUID, out InventoryGeneric? inv))
        {
            inv = new InventoryGeneric(SellSlotCount, SellInventoryClass, player.PlayerUID, _sapi!, (id, parent) => new ItemSlotSurvival(parent));
            _serverSellInventories[player.PlayerUID] = inv;
        }
        player.InventoryManager.OpenInventory(inv);
        return inv;
    }

    private void ReturnAndCloseSellInventory(IServerPlayer player)
    {
        if (_sapi == null) return;
        if (!_serverSellInventories.TryGetValue(player.PlayerUID, out InventoryGeneric? inv)) return;

        for (int i = 0; i < inv.Count; i++)
        {
            ItemSlot slot = inv[i];
            if (slot.Empty || slot.Itemstack == null) continue;

            ItemStack stack = slot.TakeOutWhole();
            if (!player.InventoryManager.TryGiveItemstack(stack, true) && stack.StackSize > 0)
            {
                _sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ.Clone().Add(0, 0.5, 0));
            }
            slot.MarkDirty();
        }

        player.InventoryManager.CloseInventory(inv);
    }

    private void OnServerSellQuoteRequest(IServerPlayer player, SellSlotQuoteRequestPacket packet)
    {
        if (_sapi == null || Calculator == null) return;
        ReloadConfigIfDiskChanged(_sapi);

        InventoryGeneric sellInv = GetOrOpenServerSellInventory(player);
        List<SellSlotValuationEntry>? quotes = null;
        KnownTraderRecord? known = Directory.GetKnownTrader(player.PlayerUID, packet.EntityId);
        EntityTradingHumanoid? liveTrader = _sapi.World.GetEntityById(packet.EntityId) as EntityTradingHumanoid;

        bool isPhysicalSession = _activeTraderSessions.TryGetValue(player.PlayerUID, out long activeId) && activeId == packet.EntityId;
        if (liveTrader != null && (isPhysicalSession || Config.EnableInstantTrade || Config.DirectorySyncMode == DirectorySyncMode.LiveKnownTraders || known == null))
        {
            quotes = EvaluateSellInventory(liveTrader, sellInv);
        }
        else if (Config.EnableInstantTrade && known != null)
        {
            quotes = EvaluateSellInventoryFromSnapshot(known, sellInv);
        }

        if (quotes == null) return;

        if (IsDebugActive() && quotes.Count > 0)
        {
            foreach (SellSlotValuationEntry q in quotes)
            {
                DebugLogServer(player, $"Slot {q.SellSlotIndex}: {q.ItemCode} x{q.QuantityInSlot} | Base={q.BaseValue:0.###} Afin=x{q.AffinityMod:0.##} Dem=x{q.DemandMod:0.##} Sat=x{q.SaturationMod:0.##} -> +{q.TotalPayoutGears}eng ({q.ConsumedUnits}u)");
            }
        }

        _serverChannel?.SendPacket(new SellSlotQuoteResponsePacket
        {
            EntityId = packet.EntityId,
            SellSlotQuotes = quotes
        }, player);
    }

    private void OnServerTradeRequest(IServerPlayer player, TradeRequestPacket packet)
    {
        if (_sapi == null || Calculator == null) return;
        ReloadConfigIfDiskChanged(_sapi);

        EntityTradingHumanoid? liveTrader = _sapi.World.GetEntityById(packet.EntityId) as EntityTradingHumanoid;
        KnownTraderRecord? known = Directory.GetKnownTrader(player.PlayerUID, packet.EntityId);

        if (liveTrader?.Inventory == null && (!Config.EnableInstantTrade || known == null))
        {
            _serverChannel?.SendPacket(new TradeResponsePacket
            {
                Success = false,
                MessageKey = "msg-trader-not-found"
            }, player);
            return;
        }

        if (liveTrader != null && !Config.IsWithinTradeDistance(player.Entity.Pos.DistanceTo(liveTrader.Pos)))
        {
            _serverChannel?.SendPacket(new TradeResponsePacket
            {
                Success = false,
                MessageKey = "msg-too-far"
            }, player);
            return;
        }

        InventoryGeneric sellInv = GetOrOpenServerSellInventory(player);
        List<SellSlotValuationEntry> sellQuotes = liveTrader != null
            ? EvaluateSellInventory(liveTrader, sellInv)
            : EvaluateSellInventoryFromSnapshot(known!, sellInv);

        int totalSellGears = sellQuotes.Where(q => q.IsTradeable && q.ConsumedUnits > 0 && q.TotalPayoutGears > 0).Sum(q => q.TotalPayoutGears);

        // Validar carrito de compra
        int totalBuyCost = 0;
        List<(ItemSlotTrade? LiveSlot, StockOfferEntry? SnapshotOffer, int Packs)> validBuyLines = [];
        foreach (BuyCartLine line in packet.BuyCart)
        {
            if (line.PackCount <= 0) continue;

            if (liveTrader?.Inventory != null)
            {
                if (line.SellingSlotIndex < 0 || line.SellingSlotIndex >= liveTrader.Inventory.SellingSlots.Length)
                {
                    SendTradeFailure(player, liveTrader, known, sellInv, "msg-invalid-offer");
                    return;
                }

                ItemSlotTrade sellSlot = liveTrader.Inventory.SellingSlots[line.SellingSlotIndex];
                if (sellSlot?.Itemstack == null || sellSlot.TradeItem == null || sellSlot.TradeItem.Stock < line.PackCount)
                {
                    SendTradeFailure(player, liveTrader, known, sellInv, "msg-insufficient-stock");
                    return;
                }

                totalBuyCost += PriceCalculator.GetEffectiveBuyPrice(sellSlot.TradeItem.Price, Config) * line.PackCount;
                validBuyLines.Add((sellSlot, null, line.PackCount));
            }
            else
            {
                StockOfferEntry? offer = known!.SellingOffers.FirstOrDefault(o => o.SellingSlotIndex == line.SellingSlotIndex);
                if (offer == null)
                {
                    SendTradeFailure(player, null, known, sellInv, "msg-invalid-offer");
                    return;
                }
                if (offer.Stock < line.PackCount)
                {
                    SendTradeFailure(player, null, known, sellInv, "msg-insufficient-stock");
                    return;
                }

                totalBuyCost += offer.PriceGears * line.PackCount;
                validBuyLines.Add((null, offer, line.PackCount));
            }
        }

        if (totalSellGears == 0 && validBuyLines.Count == 0)
        {
            SendTradeFailure(player, liveTrader, known, sellInv, "msg-empty-trade");
            return;
        }

        int traderAssets = liveTrader?.Inventory != null
            ? liveTrader.Inventory.GetTraderAssets()
            : known!.TraderMoney;

        int netGears = totalSellGears - totalBuyCost;
        if (netGears < 0)
        {
            int gearsNeeded = -netGears;
            if (InventoryTrader.GetPlayerAssets(player.Entity) < gearsNeeded)
            {
                SendTradeFailure(player, liveTrader, known, sellInv, "msg-player-not-enough-gears", gearsNeeded);
                return;
            }
            InventoryTrader.DeductFromEntity(_sapi, player.Entity, gearsNeeded);
            if (liveTrader?.Inventory != null) liveTrader.Inventory.GiveToTrader(gearsNeeded);
            else known!.TraderMoney += gearsNeeded;
        }
        else if (netGears > 0)
        {
            if (traderAssets < netGears)
            {
                SendTradeFailure(player, liveTrader, known, sellInv, "msg-trader-not-enough-gears", traderAssets);
                return;
            }
            if (liveTrader?.Inventory != null) liveTrader.Inventory.DeductFromTrader(netGears);
            else known!.TraderMoney -= netGears;
            GiveGearsToPlayer(player, netGears);
        }

        // Consumir lotes vendidos de los 5 slots y registrar saturacion
        double totalDays = _sapi.World.Calendar.TotalDays;
        long traderEntityId = liveTrader?.EntityId ?? known!.EntityId;
        BlockPos traderPos = liveTrader != null ? liveTrader.Pos.AsBlockPos : known!.Position;
        foreach (SellSlotValuationEntry q in sellQuotes)
        {
            if (!q.IsTradeable || q.ConsumedUnits <= 0 || q.TotalPayoutGears <= 0) continue;
            int qtyToConsume = q.ConsumedUnits;
            ItemSlot slot = sellInv[q.SellSlotIndex];
            if (slot.Empty || slot.StackSize < qtyToConsume) continue;

            slot.TakeOut(qtyToConsume);
            slot.MarkDirty();

            Saturation.RecordSale(
                traderEntityId,
                traderPos,
                totalDays,
                q.ItemCode,
                q.BaseValue * qtyToConsume,
                Config);
        }

        // Entregar articulos comprados y descontar stock
        foreach ((ItemSlotTrade? liveSlot, StockOfferEntry? snapshotOffer, int packs) in validBuyLines)
        {
            if (liveSlot?.Itemstack != null && liveSlot.TradeItem != null)
            {
                liveSlot.TradeItem.Stock -= packs;
                DeliverItemChunksToPlayer(player, liveSlot.Itemstack, liveSlot.Itemstack.StackSize * packs);
                liveSlot.MarkDirty();
            }
            else if (snapshotOffer != null)
            {
                snapshotOffer.Stock -= packs;
                ItemStack? resolved = ResolveServerStack(snapshotOffer.StackBytes, snapshotOffer.ItemCode, snapshotOffer.StackSize);
                if (resolved != null)
                {
                    DeliverItemChunksToPlayer(player, resolved, snapshotOffer.StackSize * packs);
                }
            }
        }

        TraderStateSyncPacket updated;
        if (liveTrader != null)
        {
            updated = BuildTraderState(liveTrader, sellInv);
            Directory.RecordVisit(player.PlayerUID, ToKnownRecord(liveTrader, updated));
        }
        else
        {
            updated = BuildTraderStateFromSnapshot(known!, sellInv);
        }

        _serverChannel?.SendPacket(new TradeResponsePacket
        {
            Success = true,
            MessageKey = "msg-trade-success",
            MessageArgs = [totalBuyCost.ToString(), totalSellGears.ToString()],
            UpdatedState = updated
        }, player);

        DebugLogServer(player, $"Trade OK | Compra={totalBuyCost} | Venta=+{totalSellGears} | Neto={netGears} | Scope={Config.SaturationScope}");
    }

    private void SendTradeFailure(IServerPlayer player, EntityTradingHumanoid? liveTrader, KnownTraderRecord? known, InventoryGeneric sellInv, string messageKey, params object[] args)
    {
        TraderStateSyncPacket? updated = liveTrader != null
            ? BuildTraderState(liveTrader, sellInv)
            : (known != null ? BuildTraderStateFromSnapshot(known, sellInv) : null);

        _serverChannel?.SendPacket(new TradeResponsePacket
        {
            Success = false,
            MessageKey = messageKey,
            MessageArgs = args.Select(a => a.ToString() ?? string.Empty).ToList(),
            UpdatedState = updated
        }, player);
    }

    private void DeliverItemChunksToPlayer(IServerPlayer player, ItemStack template, int totalItems)
    {
        int remaining = totalItems;
        int maxStack = Math.Max(1, template.Collectible.MaxStackSize);
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, maxStack);
            ItemStack bought = template.Clone();
            bought.StackSize = chunk;
            if (!player.InventoryManager.TryGiveItemstack(bought, true) && bought.StackSize > 0)
            {
                _sapi!.World.SpawnItemEntity(bought, player.Entity.Pos.XYZ.Clone().Add(0, 0.5, 0));
            }
            remaining -= chunk;
        }
    }

    private ItemStack? ResolveServerStack(byte[]? stackBytes, string itemCode, int stackSize)
    {
        if (_sapi == null) return null;
        if (stackBytes != null && stackBytes.Length > 0)
        {
            ItemStack stack = new(stackBytes);
            if (stack.ResolveBlockOrItem(_sapi.World))
            {
                stack.StackSize = Math.Max(1, stackSize);
                return stack;
            }
        }

        if (string.IsNullOrEmpty(itemCode)) return null;
        AssetLocation loc = new(itemCode);
        CollectibleObject? obj = (CollectibleObject?)_sapi.World.GetItem(loc) ?? _sapi.World.GetBlock(loc);
        return obj != null ? new ItemStack(obj, Math.Max(1, stackSize)) : null;
    }

    private void GiveGearsToPlayer(IServerPlayer player, int amount)
    {
        Item? gearItem = _sapi?.World.GetItem(new AssetLocation("game", "gear-rusty"));
        if (gearItem == null || amount <= 0) return;

        int remaining = amount;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, gearItem.MaxStackSize);
            ItemStack gearStack = new(gearItem, chunk);
            if (!player.InventoryManager.TryGiveItemstack(gearStack, true) && gearStack.StackSize > 0)
            {
                _sapi!.World.SpawnItemEntity(gearStack, player.Entity.Pos.XYZ.Clone().Add(0, 0.5, 0));
            }
            remaining -= chunk;
        }
    }

    private void OnServerDirectoryRequest(IServerPlayer player, DirectoryOpenRequestPacket _)
    {
        if (_sapi == null) return;

        ReloadConfigIfDiskChanged(_sapi);
        IReadOnlyCollection<KnownTraderRecord> records = Directory.GetKnownTraders(player.PlayerUID);
        var response = new KnownTradersSyncPacket
        {
            IsLiveSyncMode = Config.DirectorySyncMode == DirectorySyncMode.LiveKnownTraders,
            IsInstantTradeEnabled = Config.EnableInstantTrade,
            CurrentTotalDays = _sapi.World.Calendar.TotalDays
        };

        foreach (KnownTraderRecord rec in records)
        {
            KnownTraderRecord source = rec;
            if (response.IsLiveSyncMode && _sapi.World.GetEntityById(rec.EntityId) is EntityTradingHumanoid liveTrader)
            {
                TraderStateSyncPacket liveState = BuildTraderState(liveTrader, null);
                source = ToKnownRecord(liveTrader, liveState);
            }

            response.Traders.Add(new KnownTraderDto
            {
                EntityId = source.EntityId,
                TraderName = source.TraderName,
                Archetype = source.Archetype,
                PosX = source.Position.X,
                PosY = source.Position.Y,
                PosZ = source.Position.Z,
                LastSeenTotalDays = source.LastSeenTotalDays,
                NextRefreshTotalDays = source.NextRefreshTotalDays,
                TraderMoney = source.TraderMoney,
                TraderRewardBonusPct = source.TraderRewardBonusPct,
                WantedItems = source.WantedItems,
                SellingOffers = source.SellingOffers
            });
        }

        response.Traders = KnownTradersRegistry.FilterAndSortByLastVisited(response.Traders, null);
        _serverChannel?.SendPacket(response, player);
    }

    private List<SellSlotValuationEntry> EvaluateSellInventory(EntityTradingHumanoid trader, InventoryGeneric? sellInv)
    {
        if (_sapi == null || Calculator == null || sellInv == null) return [];

        double totalDays = _sapi.World.Calendar.TotalDays;
        double nextRefreshTarget = totalDays + Math.Max(0.0, trader.NextRefreshTotalDays());
        double demandPct = PriceCalculator.ComputeTraderDemandBonusPct(trader.EntityId, nextRefreshTarget, Config);
        (string archetype, HashSet<string> wantedCodes, Dictionary<string, int> sellingPrices) = ExtractTraderContext(trader);
        return EvaluateSellInventoryCore(trader.EntityId, archetype, trader.Pos.AsBlockPos, totalDays, wantedCodes, sellingPrices, demandPct, sellInv);
    }

    private List<SellSlotValuationEntry> EvaluateSellInventoryFromSnapshot(KnownTraderRecord rec, InventoryGeneric? sellInv)
    {
        if (_sapi == null || Calculator == null || sellInv == null) return [];

        double totalDays = _sapi.World.Calendar.TotalDays;
        (HashSet<string> wantedCodes, Dictionary<string, int> sellingPrices) = ExtractSnapshotContext(rec);
        return EvaluateSellInventoryCore(rec.EntityId, rec.Archetype, rec.Position, totalDays, wantedCodes, sellingPrices, rec.TraderRewardBonusPct, sellInv);
    }

    private List<SellSlotValuationEntry> EvaluateSellInventoryCore(
        long entityId,
        string archetype,
        BlockPos pos,
        double totalDays,
        HashSet<string> wantedCodes,
        Dictionary<string, int> sellingPrices,
        double demandPct,
        InventoryGeneric? sellInv)
    {
        List<SellSlotValuationEntry> result = [];
        if (Calculator == null || sellInv == null) return result;

        double demandMult = 1.0 + (demandPct / 100.0);
        Dictionary<string, double> simulatedGev = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sellInv.Count; i++)
        {
            ItemSlot slot = sellInv[i];
            if (slot.Empty || slot.Itemstack == null) continue;

            result.Add(Calculator.EvaluateSellSlot(
                i,
                slot.Itemstack,
                entityId,
                archetype,
                pos,
                totalDays,
                wantedCodes,
                sellingPrices,
                Config,
                demandMult,
                simulatedGev));
        }

        return result;
    }

    private TraderStateSyncPacket BuildTraderStateFromSnapshot(KnownTraderRecord rec, InventoryGeneric? sellInv)
    {
        double totalDays = _sapi!.World.Calendar.TotalDays;
        return new TraderStateSyncPacket
        {
            EntityId = rec.EntityId,
            TraderName = rec.TraderName,
            Archetype = rec.Archetype,
            TraderMoney = rec.TraderMoney,
            CurrentTotalDays = totalDays,
            NextRefreshTotalDays = rec.NextRefreshTotalDays,
            TraderRewardBonusPct = rec.TraderRewardBonusPct,
            EconomyModeCode = Config.EconomyMode.ToString(),
            WantedItems = rec.WantedItems,
            SellingOffers = rec.SellingOffers,
            SellSlotQuotes = EvaluateSellInventoryFromSnapshot(rec, sellInv)
        };
    }

    private static (HashSet<string> WantedCodes, Dictionary<string, int> SellingPrices) ExtractSnapshotContext(KnownTraderRecord rec)
    {
        HashSet<string> wantedCodes = new(StringComparer.OrdinalIgnoreCase);
        foreach (WantedItemEntry w in rec.WantedItems)
        {
            if (!string.IsNullOrEmpty(w.ItemCode)) wantedCodes.Add(w.ItemCode);
        }

        Dictionary<string, int> sellingPrices = new(StringComparer.OrdinalIgnoreCase);
        foreach (StockOfferEntry s in rec.SellingOffers)
        {
            if (!string.IsNullOrEmpty(s.ItemCode)) sellingPrices[s.ItemCode] = s.PriceGears;
        }

        return (wantedCodes, sellingPrices);
    }

    private TraderStateSyncPacket BuildTraderState(EntityTradingHumanoid trader, InventoryGeneric? sellInv)
    {
        double totalDays = _sapi!.World.Calendar.TotalDays;
        double daysRemaining = Math.Max(0.0, trader.NextRefreshTotalDays());
        double nextRefreshTarget = totalDays + daysRemaining;
        double demandPct = PriceCalculator.ComputeTraderDemandBonusPct(trader.EntityId, nextRefreshTarget, Config);
        double demandMult = 1.0 + (demandPct / 100.0);

        BlockPos pos = trader.Pos.AsBlockPos;
        (string archetype, HashSet<string> wantedCodes, Dictionary<string, int> sellingPrices) = ExtractTraderContext(trader);

        List<WantedItemEntry> wantedEntries = [];
        if (trader.Inventory?.BuyingSlots != null)
        {
            foreach (ItemSlotTrade slot in trader.Inventory.BuyingSlots)
            {
                if (slot?.Itemstack?.Collectible?.Code == null) continue;
                string code = slot.Itemstack.Collectible.Code.ToString();
                ItemValuationResult eval = Calculator!.Evaluate(
                    slot.Itemstack,
                    trader.EntityId,
                    archetype,
                    pos,
                    totalDays,
                    wantedCodes,
                    sellingPrices,
                    Config,
                    demandMult);
                if (!eval.IsTradeable) continue;

                wantedEntries.Add(new WantedItemEntry
                {
                    ItemCode = code,
                    DisplayName = slot.Itemstack.GetName(),
                    MinLotSize = eval.MinLotSize,
                    LotGearPayout = eval.LotGearPayout,
                    DeltaPct = eval.DeltaPct,
                    SaturationPct = (1.0 - eval.SaturationMod) * 100.0,
                    StackBytes = slot.Itemstack.ToBytes()
                });
            }
        }

        List<StockOfferEntry> sellingOffers = [];
        if (trader.Inventory?.SellingSlots != null)
        {
            for (int i = 0; i < trader.Inventory.SellingSlots.Length; i++)
            {
                ItemSlotTrade slot = trader.Inventory.SellingSlots[i];
                if (slot?.Itemstack?.Collectible?.Code == null || slot.TradeItem == null) continue;
                sellingOffers.Add(new StockOfferEntry
                {
                    SellingSlotIndex = i,
                    ItemCode = slot.Itemstack.Collectible.Code.ToString(),
                    DisplayName = slot.Itemstack.GetName(),
                    StackSize = slot.Itemstack.StackSize,
                    Stock = slot.TradeItem.Stock,
                    PriceGears = PriceCalculator.GetEffectiveBuyPrice(slot.TradeItem.Price, Config),
                    StackBytes = slot.Itemstack.ToBytes()
                });
            }
        }

        return new TraderStateSyncPacket
        {
            EntityId = trader.EntityId,
            TraderName = trader.GetName() ?? string.Empty,
            Archetype = archetype,
            TraderMoney = trader.Inventory?.GetTraderAssets() ?? 0,
            CurrentTotalDays = totalDays,
            NextRefreshTotalDays = nextRefreshTarget,
            TraderRewardBonusPct = demandPct,
            EconomyModeCode = Config.EconomyMode.ToString(),
            WantedItems = wantedEntries,
            SellingOffers = sellingOffers,
            SellSlotQuotes = EvaluateSellInventory(trader, sellInv)
        };
    }

    private (string Archetype, HashSet<string> WantedCodes, Dictionary<string, int> SellingPrices) ExtractTraderContext(EntityTradingHumanoid trader)
    {
        string archetype = trader.Variant?["type"]
            ?? trader.Code?.Path?.Split('-').ElementAtOrDefault(2)
            ?? "commodities";

        HashSet<string> wantedCodes = new(StringComparer.OrdinalIgnoreCase);
        if (trader.Inventory?.BuyingSlots != null)
        {
            foreach (ItemSlotTrade slot in trader.Inventory.BuyingSlots)
            {
                if (slot?.Itemstack?.Collectible?.Code == null) continue;
                wantedCodes.Add(slot.Itemstack.Collectible.Code.ToString());
            }
        }

        Dictionary<string, int> sellingPrices = new(StringComparer.OrdinalIgnoreCase);
        if (trader.Inventory?.SellingSlots != null)
        {
            foreach (ItemSlotTrade slot in trader.Inventory.SellingSlots)
            {
                if (slot?.Itemstack?.Collectible?.Code == null || slot.TradeItem == null) continue;
                sellingPrices[slot.Itemstack.Collectible.Code.ToString()] = PriceCalculator.GetEffectiveBuyPrice(slot.TradeItem.Price, Config);
            }
        }

        return (archetype, wantedCodes, sellingPrices);
    }

    private static KnownTraderRecord ToKnownRecord(EntityTradingHumanoid trader, TraderStateSyncPacket state)
    {
        return new KnownTraderRecord
        {
            EntityId = state.EntityId,
            TraderName = state.TraderName,
            Archetype = state.Archetype,
            Position = trader.Pos.AsBlockPos,
            LastSeenTotalDays = state.CurrentTotalDays,
            NextRefreshTotalDays = state.NextRefreshTotalDays,
            TraderMoney = state.TraderMoney,
            TraderRewardBonusPct = state.TraderRewardBonusPct,
            WantedItems = state.WantedItems,
            SellingOffers = state.SellingOffers
        };
    }

    private void OnClientTraderState(TraderStateSyncPacket packet)
    {
        ReloadConfigIfDiskChanged(_capi);
        _tradeDialog?.UpdateState(packet);
        if (_tradeDialog != null && !_tradeDialog.IsOpened())
        {
            _tradeDialog.TryOpen();
        }
    }

    private void OnClientConfigReloaded(ConfigReloadedNotifyPacket _)
    {
        ReloadConfigIfDiskChanged(_capi);
        if (_directoryDialog != null && _directoryDialog.IsOpened())
        {
            _clientChannel?.SendPacket(new DirectoryOpenRequestPacket());
        }
    }

    private void OnClientTradeResponse(TradeResponsePacket packet)
    {
        string msg = !string.IsNullOrEmpty(packet.MessageKey)
            ? ComerciantesLang.Get(packet.MessageKey, packet.MessageArgs.Cast<object>().ToArray())
            : string.Empty;

        if (packet.UpdatedState != null)
        {
            _tradeDialog?.UpdateState(packet.UpdatedState, msg, !packet.Success);
        }
        else if (!string.IsNullOrEmpty(msg))
        {
            _tradeDialog?.SetStatusMessage(msg, !packet.Success);
        }
    }

    private void OnClientSellQuoteResponse(SellSlotQuoteResponsePacket packet)
    {
        _tradeDialog?.UpdateSellQuotes(packet.EntityId, packet.SellSlotQuotes);
    }

    private void OnClientDirectorySync(KnownTradersSyncPacket packet)
    {
        DebugLogClient($"Directorio recibido: {packet.Traders.Count} comerciantes (Live={packet.IsLiveSyncMode})");
        _directoryDialog?.UpdateData(packet);
        if (_directoryDialog != null && !_directoryDialog.IsOpened())
        {
            _directoryDialog.TryOpen();
        }
    }

    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.UnregisterEventBusListener(OnServerConfigLibSettingChanged);
            _sapi.Event.UnregisterEventBusListener(OnServerConfigLibSaved);
        }
        if (_capi != null)
        {
            _capi.Event.UnregisterEventBusListener(OnClientConfigLibSettingChanged);
            _capi.Event.UnregisterEventBusListener(OnClientConfigLibSaved);
        }
        _harmony?.UnpatchAll(HarmonyId);
        if (_side == EnumAppSide.Client)
        {
            TraderDialogPatch.ClientInstance = null;
        }
        base.Dispose();
    }
}
