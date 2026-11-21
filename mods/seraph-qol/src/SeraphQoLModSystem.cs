using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SeraphQoL.Alloy;
using SeraphQoL.Backpacks;
using SeraphQoL.Client;
using SeraphQoL.Config;
using SeraphQoL.Exploration;
using SeraphQoL.Fauna;
using SeraphQoL.Interaction;
using SeraphQoL.Inventory;
using SeraphQoL.Network;
using SeraphQoL.Patches;
using SeraphQoL.StackSizes;
using SeraphQoL.Visuals;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace SeraphQoL;

/// <summary>
/// ModSystem principal de Seraph QoL. Orquesta ciclo de vida, configuracion dual (.json / ConfigLib),
/// Fauna QoL, red ProtoBuf e interfaz de inventario.
/// </summary>
public sealed class SeraphQoLModSystem : ModSystem
{
    public const string ChannelName = "seraphqol";
    private const string HarmonyId = "seraphmods.seraphqol";
    private const string ClientConfigFile = "seraphqol.json";
    private const string ServerConfigFile = "seraphqol-server.json";

    private Harmony? _harmony;
    private string? _harmonyId;
    private bool _isClient;
    private ICoreServerAPI? _sapi;
    private ICoreClientAPI? _capi;
    private IServerNetworkChannel? _serverChannel;
    private IClientNetworkChannel? _clientChannel;

    private readonly FaunaAssetsService _faunaService = new();
    private readonly StackSizeAssetsService _stackSizeService = new();
    private readonly BackpackAssetsService _backpackService = new();
    private ServerInventoryExecutor? _inventoryExecutor;
    private ProjectileTrackerServer? _projectileTrackerServer;
    private ProjectileTrackerClient? _projectileTrackerClient;
    private HotbarRefillMonitor? _hotbarRefillMonitor;
    private EquipLightClientService? _equipLightService;
    private GroundItemHighlightRenderer? _groundHighlightRenderer;
    private SlotIndicatorRenderer? _slotIndicatorRenderer;
    private AnimalSexIndicatorRenderer? _animalSexRenderer;
    private ResourceParticlesService? _resourceParticlesService;
    private AlloyDepositService? _alloyDepositService;
    private GuiDialogAlloyCalculator? _alloyCalculatorDialog;
    private GuiDialogRadialQuickSwap? _radialQuickSwapDialog;
    private InventoryButtonsComposer? _buttonsComposer;
    private GuiDialogRadialToolMode? _radialToolModeDialog;
    private long _clientTickListenerId;
    private int _clientTickCounter;
    private bool _suppressConfigLibEvents;

    public SeraphQoLClientConfig ClientConfig { get; private set; } = new();
    public SeraphQoLServerConfig ServerConfig { get; private set; } = new();
    public EffectiveServerPolicy EffectivePolicy { get; private set; } =
        EffectiveServerPolicy.FromServerConfig(new SeraphQoLServerConfig());

    public FavoritesManager Favorites { get; } = new();
    public LockedSlotsService LockedSlots { get; } = new();

    public override void Start(ICoreAPI api)
    {
        _isClient = api.Side == EnumAppSide.Client;
        if (_isClient)
        {
            ClientConfig = api.LoadModConfig<SeraphQoLClientConfig>(ClientConfigFile) ?? new SeraphQoLClientConfig();
            ClientConfig.Normalize();
            StoreMergedConfig(api, ClientConfigFile, ClientConfig);
            SeraphQoLClientPatches.ClientInstance = this;
            SeraphQoLCommonPatches.ClientInstance = this;
        }
        else
        {
            ServerConfig = api.LoadModConfig<SeraphQoLServerConfig>(ServerConfigFile) ?? new SeraphQoLServerConfig();
            MergeServerSettingsIfCombinedNewer(api, ServerConfig);
            ServerConfig.Normalize();
            api.StoreModConfig(ServerConfig, ServerConfigFile);
            EffectivePolicy = EffectiveServerPolicy.FromServerConfig(ServerConfig);
            SeraphQoLCommonPatches.ServerInstance = this;
        }

        string sideId = _isClient ? $"{HarmonyId}.client" : $"{HarmonyId}.server";
        if (!Harmony.HasAnyPatches(sideId))
        {
            _harmonyId = sideId;
            _harmony = new Harmony(sideId);
            if (!Harmony.HasAnyPatches($"{HarmonyId}.server"))
            {
                _harmony.CreateClassProcessor(typeof(SeraphQoLCommonPatches)).Patch();
            }
            if (_isClient)
            {
                _harmony.CreateClassProcessor(typeof(SeraphQoLClientPatches)).Patch();
            }
        }
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        if (api is ICoreServerAPI sapi)
        {
            RefreshEffectivePolicy();
            _faunaService.ApplyToEntityTypes(sapi, EffectivePolicy);
            _stackSizeService.ApplyToCollectibles(sapi, EffectivePolicy);
            _backpackService.ApplyToCollectibles(sapi, EffectivePolicy);
        }
        else if (api is ICoreClientAPI capi)
        {
            _resourceParticlesService?.ApplyConfiguredParticles();
            _stackSizeService.ApplyToCollectibles(capi, EffectivePolicy);
            _backpackService.ApplyToCollectibles(capi, EffectivePolicy);
        }
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        _inventoryExecutor = new ServerInventoryExecutor(LockedSlots);

        RefreshEffectivePolicy();

        _serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<ServerPolicySyncPacket>()
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<QuickStorePacket>()
            .RegisterMessageType<StoreNearbyPacket>()
            .RegisterMessageType<UpdateFavoritePacket>()
            .RegisterMessageType<UpdateLockedSlotsPacket>()
            .RegisterMessageType<RemeshContainersPacket>()
            .RegisterMessageType<HotbarRefillPacket>()
            .RegisterMessageType<SwapSlotsPacket>()
            .RegisterMessageType<ProjectileWaypointPacket>()
            .RegisterMessageType<RequestAlloyScanPacket>()
            .RegisterMessageType<AlloyScanResultPacket>()
            .RegisterMessageType<AlloyDepositPacket>()
            .RegisterMessageType<AlloyDepositResultPacket>()
            .SetMessageHandler<SortInventoryPacket>(OnServerSortInventory)
            .SetMessageHandler<QuickStorePacket>(OnServerQuickStore)
            .SetMessageHandler<StoreNearbyPacket>(OnServerStoreNearby)
            .SetMessageHandler<UpdateFavoritePacket>(OnServerUpdateFavorite)
            .SetMessageHandler<UpdateLockedSlotsPacket>(OnServerUpdateLockedSlots)
            .SetMessageHandler<HotbarRefillPacket>(OnServerHotbarRefill)
            .SetMessageHandler<SwapSlotsPacket>(OnServerSwapSlots)
            .SetMessageHandler<RequestAlloyScanPacket>(OnServerRequestAlloyScan)
            .SetMessageHandler<AlloyDepositPacket>(OnServerAlloyDeposit);

        _projectileTrackerServer = new ProjectileTrackerServer(api, _serverChannel, () => EffectivePolicy);
        _projectileTrackerServer.Start();

        api.Event.SaveGameLoaded += RefreshEffectivePolicy;
        api.Event.PlayerJoin += OnServerPlayerJoin;
        api.Event.PlayerNowPlaying += OnServerPlayerNowPlaying;
        api.Event.PlayerDisconnect += player =>
        {
            LockedSlots.ClearPlayer(player.PlayerUID);
            _backpackService.ClearPendingDrops(player.PlayerUID);
        };
        api.Event.OnEntitySpawn += entity => _faunaService.AdjustSpawnedEntity(entity, EffectivePolicy);

        RegisterServerConfigLib(api);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _capi = api;
        _buttonsComposer = new InventoryButtonsComposer(api, this);
        _radialToolModeDialog = new GuiDialogRadialToolMode(api, this);
        _hotbarRefillMonitor = new HotbarRefillMonitor(api, this);
        _equipLightService = new EquipLightClientService(api, this);
        _projectileTrackerClient = new ProjectileTrackerClient(api, this);
        _slotIndicatorRenderer = new SlotIndicatorRenderer(api, this);
        _alloyDepositService = new AlloyDepositService(api, this);
        _alloyCalculatorDialog = new GuiDialogAlloyCalculator(api, this, _alloyDepositService);
        _radialQuickSwapDialog = new GuiDialogRadialQuickSwap(api, this);
        _resourceParticlesService = new ResourceParticlesService(api, this);

        _groundHighlightRenderer = new GroundItemHighlightRenderer(api, this);
        api.Event.RegisterRenderer(_groundHighlightRenderer, EnumRenderStage.Ortho, "seraphqol-ground-highlight");

        _animalSexRenderer = new AnimalSexIndicatorRenderer(api, this);
        api.Event.RegisterRenderer(_animalSexRenderer, EnumRenderStage.Opaque, "seraphqol-animal-sex");

        _clientChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<ServerPolicySyncPacket>()
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<QuickStorePacket>()
            .RegisterMessageType<StoreNearbyPacket>()
            .RegisterMessageType<UpdateFavoritePacket>()
            .RegisterMessageType<UpdateLockedSlotsPacket>()
            .RegisterMessageType<RemeshContainersPacket>()
            .RegisterMessageType<HotbarRefillPacket>()
            .RegisterMessageType<SwapSlotsPacket>()
            .RegisterMessageType<ProjectileWaypointPacket>()
            .RegisterMessageType<RequestAlloyScanPacket>()
            .RegisterMessageType<AlloyScanResultPacket>()
            .RegisterMessageType<AlloyDepositPacket>()
            .RegisterMessageType<AlloyDepositResultPacket>()
            .SetMessageHandler<ServerPolicySyncPacket>(OnClientServerPolicySync)
            .SetMessageHandler<RemeshContainersPacket>(OnClientRemeshContainers)
            .SetMessageHandler<ProjectileWaypointPacket>(OnClientProjectileWaypoint)
            .SetMessageHandler<AlloyScanResultPacket>(OnClientAlloyScanResult)
            .SetMessageHandler<AlloyDepositResultPacket>(OnClientAlloyDepositResult);

        api.Event.LevelFinalize += () =>
        {
            _animalSexRenderer?.Initialize();
            _resourceParticlesService?.ApplyConfiguredParticles();
        };
        api.Event.PlayerEntitySpawn += _ =>
        {
            LockedSlotsService.SyncAllLocksToServer(api, ClientConfig);
            _hotbarRefillMonitor?.Reset();
        };
        api.Event.LeaveWorld += () =>
        {
            _hotbarRefillMonitor?.Reset();
            _radialToolModeDialog?.TryClose();
            _radialQuickSwapDialog?.TryClose();
            _alloyCalculatorDialog?.TryClose();
            _alloyDepositService?.ClearScanCache();
            _slotIndicatorRenderer?.ClearCache();
            _animalSexRenderer?.Reset();
            _groundHighlightRenderer?.Reset();
            _projectileTrackerClient?.ClearAll();
        };

        _clientTickListenerId = api.Event.RegisterGameTickListener(_ =>
        {
            _hotbarRefillMonitor?.Tick();
            if (++_clientTickCounter >= 20)
            {
                _clientTickCounter = 0;
                _projectileTrackerClient?.TickPruneExpired();
            }
        }, 50);

        RegisterHotkeys(api);
        RegisterConfigLib(api);
    }

    public void SaveClientConfig()
    {
        if (_capi == null) return;
        StoreMergedConfig(_capi, ClientConfigFile, ClientConfig);
    }

    public void SaveServerConfig()
    {
        if (_sapi == null) return;
        _sapi.StoreModConfig(ServerConfig, ServerConfigFile);
    }

    public void CycleSortOrderFromClient(int step)
    {
        ClientConfig.CycleSortOrder(step);
        SaveClientConfig();
        RefreshSortButtonTooltips();
    }

    public void SetStackPerishablesFromClient(bool value)
    {
        ClientConfig.StackPerishables = value;
        SaveClientConfig();
    }

    public void RequestSortFromClient(string inventoryId)
    {
        if (!EffectivePolicy.EnableInventoryManagement || string.IsNullOrWhiteSpace(inventoryId)) return;

        _clientChannel?.SendPacket(new SortInventoryPacket
        {
            InventoryId = inventoryId,
            SortOrder = (int)ClientConfig.SortOrder,
            SortHotbarWithBackpack = ClientConfig.SortHotbarWithBackpack,
            StackPerishables = ClientConfig.StackPerishables,
            SkipFavoritesWhenSorting = ClientConfig.SkipFavoritesWhenSorting
        });
    }

    public void RequestQuickStoreFromClient(string inventoryId)
    {
        if (!EffectivePolicy.EnableInventoryManagement || string.IsNullOrWhiteSpace(inventoryId)) return;

        _clientChannel?.SendPacket(new QuickStorePacket
        {
            InventoryId = inventoryId,
            StackPerishables = ClientConfig.StackPerishables,
            IncludeHotbar = ClientConfig.SortHotbarWithBackpack
        });
    }

    public void RequestStoreNearbyFromClient()
    {
        if (!EffectivePolicy.EnableInventoryManagement || !EffectivePolicy.EnableStoreNearby) return;

        _clientChannel?.SendPacket(new StoreNearbyPacket
        {
            StackPerishables = ClientConfig.StackPerishables,
            IncludeHotbar = ClientConfig.SortHotbarWithBackpack
        });
    }

    public void SendHotbarRefillPacket(HotbarRefillPacket packet)
    {
        if (!EffectivePolicy.AllowHotbarRefill) return;
        _clientChannel?.SendPacket(packet);
    }

    public void SendSwapSlotsPacket(SwapSlotsPacket packet)
    {
        _clientChannel?.SendPacket(packet);
    }

    public void RequestAlloyScanFromClient(BlockPos firepitPos, int radius)
    {
        if (firepitPos == null) return;
        _clientChannel?.SendPacket(new RequestAlloyScanPacket
        {
            FirepitPos = firepitPos.Copy(),
            SearchRadius = Math.Clamp(radius, SeraphQoLClientConfig.MinAlloySearchRadius, SeraphQoLClientConfig.MaxAlloySearchRadius)
        });
    }

    public void SendAlloyDepositPacket(AlloyDepositPacket packet)
    {
        _clientChannel?.SendPacket(packet);
    }

    public bool TryToggleRadialToolModeMenu(out bool handledResult)
    {
        handledResult = false;
        if (!ClientConfig.EnableRadialToolModeMenu || _radialToolModeDialog == null || _capi?.World?.Player == null)
        {
            return false;
        }

        if (_radialToolModeDialog.IsOpened())
        {
            _radialToolModeDialog.TryClose();
            handledResult = true;
            return true;
        }

        ItemSlot? activeSlot = _capi.World.Player.InventoryManager?.ActiveHotbarSlot;
        BlockSelection? blockSel = _capi.World.Player.CurrentBlockSelection;
        SkillItem[]? modes = activeSlot?.Itemstack?.Collectible?.GetToolModes(activeSlot, _capi.World.Player, blockSel);
        if (modes == null || modes.Length == 0 || activeSlot == null)
        {
            handledResult = false;
            return true;
        }

        _radialToolModeDialog.OpenForTool(activeSlot, blockSel, modes);
        handledResult = true;
        return true;
    }

    public void OnSurvivalInventoryComposed(GuiComposer composer)
    {
        IPlayer? player = _capi?.World?.Player;
        IInventory? backpack = player?.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (backpack == null) return;

        _buttonsComposer?.ComposePlayerInventoryButtons(composer, backpack);
    }

    public void OnContainerOpened(GuiComposer composer, IInventory? containerInventory)
    {
        if (containerInventory == null) return;
        _buttonsComposer?.ComposeContainerButtons(composer, containerInventory);
    }

    public void OnInventoryDialogClosed()
    {
        Favorites.IsFavoriteModeActive = false;
        LockedSlots.IsLockModeActive = false;
    }

    public void OnRenderSlotItemBefore(IRenderAPI render, ItemSlot slot, double posX, double posY, float size)
    {
        _slotIndicatorRenderer?.RenderBeforeItem(render, slot, posX, posY, size);
    }

    public void OnRenderSlotItemAfterScissor(IRenderAPI render)
    {
        _slotIndicatorRenderer?.RenderAfterScissor(render);
    }

    public void OnRenderSlotGrid(GuiElementItemSlotGridBase grid, IInventory inventory)
    {
        if (_buttonsComposer == null || _capi?.World?.Player == null) return;

        IPlayer player = _capi.World.Player;
        string? hoveredCode = (player.InventoryManager.CurrentHoveredSlot?.Itemstack
            ?? player.InventoryManager.MouseItemSlot?.Itemstack)?.Collectible?.Code?.ToString();

        bool isPlayerSlots = SlotProtectionPolicy.IsPlayerBackpack(inventory)
            || SlotProtectionPolicy.IsPlayerHotbar(inventory);
        bool favoriteModeActive = Favorites.IsFavoriteModeActive;
        bool lockModeActive = LockedSlots.IsLockModeActive;
        bool showFavorites = isPlayerSlots && (favoriteModeActive || !ClientConfig.HideFavoritesOverlay);
        bool showLocks = isPlayerSlots && (lockModeActive || !ClientConfig.HideLockedSlotOverlay);
        bool showInactiveHatch = isPlayerSlots && (favoriteModeActive || lockModeActive);
        bool showMatching = ClientConfig.EnableMatchingItemHighlight && !string.IsNullOrWhiteSpace(hoveredCode);

        if (!showFavorites && !showLocks && !showMatching && !showInactiveHatch) return;

        string invClass = inventory.ClassName ?? string.Empty;
        int visualIndex = 0;
        foreach (KeyValuePair<int, ItemSlot> entry in grid.renderedSlots)
        {
            if (visualIndex >= grid.SlotBounds.Length) break;
            ElementBounds slotBounds = grid.SlotBounds[visualIndex++];
            int slotId = entry.Key;
            ItemSlot slot = entry.Value;

            bool isFav = showFavorites && !slot.Empty && FavoritesManager.IsFavorite(player, slot.Itemstack);
            bool isLocked = showLocks && ClientConfig.IsSlotLocked(invClass, slotId);

            if (SlotSelectorOverlayPolicy.ShouldDrawInactiveHatch(
                    isPlayerSlots,
                    favoriteModeActive,
                    lockModeActive,
                    isFav,
                    isLocked))
            {
                _buttonsComposer.DrawInactiveSlotHatch(slotBounds);
            }

            if (showMatching && !slot.Empty)
            {
                string? slotCode = slot.Itemstack?.Collectible?.Code?.ToString();
                if (MatchingItemHighlightPolicy.ShouldHighlightSlot(hoveredCode, slotCode, featureEnabled: true))
                {
                    _buttonsComposer.DrawMatchingHighlight(slotBounds);
                }
            }

            if (!isPlayerSlots) continue;

            if (isFav || isLocked)
            {
                _buttonsComposer.DrawSlotBadge(slotBounds, isFav, isLocked);
            }
        }
    }

    public bool TryHandleSpecialSlotClick(ICoreClientAPI capi, IInventory inventory, int slotIndex)
    {
        if (!SlotProtectionPolicy.IsPlayerBackpack(inventory) && !SlotProtectionPolicy.IsPlayerHotbar(inventory))
        {
            return false;
        }

        if (slotIndex < 0 || slotIndex >= inventory.Count) return false;

        if (Favorites.IsFavoriteModeActive)
        {
            ItemSlot? slot = inventory[slotIndex];
            if (slot != null && !slot.Empty && slot.Itemstack != null)
            {
                Favorites.ToggleFavoriteClient(capi, slot.Itemstack);
                capi.Gui.PlaySound("tick");
            }
            return true;
        }

        if (LockedSlots.IsLockModeActive)
        {
            string invClass = inventory.ClassName ?? string.Empty;
            ClientConfig.ToggleLockedSlot(invClass, slotIndex);
            SaveClientConfig();
            LockedSlotsService.SyncInventoryLocksToServer(capi, ClientConfig, invClass);
            capi.Gui.PlaySound("tick");
            return true;
        }

        return false;
    }

    private void OnServerPlayerJoin(IServerPlayer player)
    {
        FavoritesManager.EnsureInitialized(player);
    }

    private void OnServerPlayerNowPlaying(IServerPlayer player)
    {
        RefreshEffectivePolicy();
        if (_sapi != null)
        {
            _backpackService.FlushPendingDrops(_sapi, player);
        }
        _serverChannel?.SendPacket(ServerPolicySyncPacket.FromPolicy(EffectivePolicy), player);
    }

    public void OnHeldBagGetOrCreateSlots(ItemStack bagStack, int targetSlots, InventoryBase parentInv, IWorldAccessor world)
    {
        _backpackService.HandleHeldBagSlotsSync(bagStack, targetSlots, parentInv, world);
    }

    private void OnServerSortInventory(IServerPlayer player, SortInventoryPacket packet)
    {
        _inventoryExecutor?.HandleSortInventory(player, packet, EffectivePolicy);
    }

    private void OnServerQuickStore(IServerPlayer player, QuickStorePacket packet)
    {
        _inventoryExecutor?.HandleQuickStore(player, packet, EffectivePolicy);
    }

    private void OnServerStoreNearby(IServerPlayer player, StoreNearbyPacket packet)
    {
        if (_inventoryExecutor == null) return;

        (int movedStacks, int containerCount, List<BlockPos> remeshPositions) =
            _inventoryExecutor.HandleStoreNearby(player, packet, EffectivePolicy);

        if (remeshPositions.Count > 0)
        {
            _serverChannel?.SendPacket(new RemeshContainersPacket { Positions = remeshPositions }, player);
        }

        player.SendMessage(
            GlobalConstants.GeneralChatGroup,
            SeraphQoLLang.Get("info-store-nearby-result", movedStacks, containerCount),
            EnumChatType.Notification);
    }

    private void OnServerUpdateFavorite(IServerPlayer player, UpdateFavoritePacket packet)
    {
        FavoritesManager.SetFavoriteServer(player, packet.ItemCode, packet.IsFavorite);
    }

    private void OnServerUpdateLockedSlots(IServerPlayer player, UpdateLockedSlotsPacket packet)
    {
        LockedSlots.UpdatePlayerLockedSlots(player.PlayerUID, packet.InventoryClass, packet.SlotIndices);
    }

    private void OnServerHotbarRefill(IServerPlayer player, HotbarRefillPacket packet)
    {
        _inventoryExecutor?.HandleHotbarRefill(player, packet, EffectivePolicy);
    }

    private void OnServerSwapSlots(IServerPlayer player, SwapSlotsPacket packet)
    {
        _inventoryExecutor?.HandleSwapSlots(player, packet);
    }

    private void OnServerRequestAlloyScan(IServerPlayer player, RequestAlloyScanPacket packet)
    {
        if (_inventoryExecutor == null) return;
        AlloyScanResultPacket result = _inventoryExecutor.HandleAlloyScan(player, packet, EffectivePolicy);
        _serverChannel?.SendPacket(result, player);
    }

    private void OnServerAlloyDeposit(IServerPlayer player, AlloyDepositPacket packet)
    {
        if (_inventoryExecutor == null) return;

        (int movedItems, int movedFuel, List<BlockPos> remeshPositions) =
            _inventoryExecutor.HandleAlloyDeposit(player, packet, EffectivePolicy);

        if (remeshPositions.Count > 0)
        {
            _serverChannel?.SendPacket(new RemeshContainersPacket { Positions = remeshPositions }, player);
        }

        if (packet.IncludeNearby && packet.FirepitPos != null)
        {
            AlloyScanResultPacket scan = _inventoryExecutor.HandleAlloyScan(
                player,
                new RequestAlloyScanPacket { FirepitPos = packet.FirepitPos.Copy(), SearchRadius = packet.SearchRadius },
                EffectivePolicy);
            _serverChannel?.SendPacket(scan, player);
        }

        _serverChannel?.SendPacket(new AlloyDepositResultPacket
        {
            FirepitPos = packet.FirepitPos?.Copy() ?? new BlockPos(0),
            MovedItems = movedItems,
            MovedFuel = movedFuel
        }, player);
    }

    private void OnClientServerPolicySync(ServerPolicySyncPacket packet)
    {
        EffectivePolicy = packet.ToDisplayPolicy();
        if (_capi != null)
        {
            _stackSizeService.ApplyToCollectibles(_capi, EffectivePolicy);
            _backpackService.ApplyToCollectibles(_capi, EffectivePolicy);
            BackpackAssetsService.ReloadOnlinePlayerBackpacks(_capi);
            ReloadOpenInventoryDialogs();
        }
    }

    private void OnClientRemeshContainers(RemeshContainersPacket packet)
    {
        if (_capi?.World == null) return;
        foreach (BlockPos pos in packet.Positions)
        {
            _capi.World.BlockAccessor.MarkBlockDirty(pos);
        }
    }

    private void OnClientProjectileWaypoint(ProjectileWaypointPacket packet)
    {
        _projectileTrackerClient?.HandlePacket(packet);
    }

    private void OnClientAlloyScanResult(AlloyScanResultPacket packet)
    {
        _alloyDepositService?.ApplyServerScanResult(packet);
        _alloyCalculatorDialog?.RefreshForFirepitPos(packet.FirepitPos);
    }

    private void OnClientAlloyDepositResult(AlloyDepositResultPacket packet)
    {
        _alloyCalculatorDialog?.RefreshForFirepitPos(packet.FirepitPos);
    }

    public void OnFirepitGuiOpened(GuiDialogBlockEntityFirepit firepitDialog)
    {
        _alloyCalculatorDialog?.AttachToFirepit(firepitDialog);
    }

    public void OnFirepitGuiClosed(GuiDialogBlockEntityFirepit firepitDialog)
    {
        _alloyCalculatorDialog?.DetachFromFirepit(firepitDialog);
    }

    public void OnFirepitSlotModified(GuiDialogBlockEntityFirepit firepitDialog)
    {
        _alloyCalculatorDialog?.RefreshIfOpen(firepitDialog);
    }

    public void OnWaypointLayerRebuilt(Vintagestory.GameContent.WaypointMapLayer layer)
    {
        _projectileTrackerClient?.SyncWithLayer(layer);
    }

    private void RefreshEffectivePolicy()
    {
        EffectivePolicy = EffectiveServerPolicy.FromServerConfig(ServerConfig);
    }

    private void RegisterHotkeys(ICoreClientAPI api)
    {
        api.Input.RegisterHotKey("seraphqol.sort", SeraphQoLLang.Get("hotkey-sort-inventory"), GlKeys.Unknown, HotkeyType.GUIOrOtherControls);
        api.Input.SetHotKeyHandler("seraphqol.sort", _ =>
        {
            IInventory? backpack = api.World?.Player?.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
            if (backpack == null) return false;
            RequestSortFromClient(backpack.InventoryID);
            return true;
        });

        api.Input.RegisterHotKey("seraphqol.sortcontainer", SeraphQoLLang.Get("hotkey-sort-container"), GlKeys.Unknown, HotkeyType.GUIOrOtherControls);
        api.Input.SetHotKeyHandler("seraphqol.sortcontainer", _ =>
        {
            GuiDialogBlockEntityInventory? openContainer = api.World?.Player != null
                ? api.Gui.OpenedGuis.OfType<GuiDialogBlockEntityInventory>().FirstOrDefault(g => g.IsOpened())
                : null;
            if (openContainer?.Inventory == null) return false;
            RequestSortFromClient(openContainer.Inventory.InventoryID);
            return true;
        });

        api.Input.RegisterHotKey("seraphqol.storenearby", SeraphQoLLang.Get("hotkey-store-nearby"), GlKeys.G, HotkeyType.GUIOrOtherControls, shiftPressed: true);
        api.Input.SetHotKeyHandler("seraphqol.storenearby", _ =>
        {
            RequestStoreNearbyFromClient();
            return true;
        });

        api.Input.RegisterHotKey("seraphqol.equiplightoffhand", SeraphQoLLang.Get("hotkey-equip-light-offhand"), GlKeys.L, HotkeyType.InventoryHotkeys, altPressed: true);
        api.Input.SetHotKeyHandler("seraphqol.equiplightoffhand", _ =>
            _equipLightService?.TryToggleLight(toOffhand: true) ?? false);

        api.Input.RegisterHotKey("seraphqol.equiplighthotbar", SeraphQoLLang.Get("hotkey-equip-light-hotbar"), GlKeys.Unknown, HotkeyType.InventoryHotkeys);
        api.Input.SetHotKeyHandler("seraphqol.equiplighthotbar", _ =>
            _equipLightService?.TryToggleLight(toOffhand: false) ?? false);

        api.Input.RegisterHotKey("seraphqol.equipshieldoffhand", SeraphQoLLang.Get("hotkey-equip-shield-offhand"), GlKeys.B, HotkeyType.InventoryHotkeys, altPressed: true);
        api.Input.SetHotKeyHandler("seraphqol.equipshieldoffhand", _ =>
            _equipLightService?.TryToggleShield(toOffhand: true) ?? false);

        api.Input.RegisterHotKey("seraphqol.equipshieldhotbar", SeraphQoLLang.Get("hotkey-equip-shield-hotbar"), GlKeys.Unknown, HotkeyType.InventoryHotkeys);
        api.Input.SetHotKeyHandler("seraphqol.equipshieldhotbar", _ =>
            _equipLightService?.TryToggleShield(toOffhand: false) ?? false);

        api.Input.RegisterHotKey("seraphqol.quickswap", SeraphQoLLang.Get("hotkey-quick-swap"), GlKeys.F, HotkeyType.InventoryHotkeys, altPressed: true);
        api.Input.SetHotKeyHandler("seraphqol.quickswap", _ => _radialQuickSwapDialog?.ToggleRadial() ?? false);
    }

    private void RegisterConfigLib(ICoreClientAPI api)
    {
        api.Event.RegisterEventBusListener(OnClientConfigLibSettingChanged, 0.5, "configlib:seraphqol:setting-changed");
        api.Event.RegisterEventBusListener(OnClientConfigLibSaved, 0.5, "configlib:seraphqol:config-saved");
    }

    private void RegisterServerConfigLib(ICoreServerAPI api)
    {
        api.Event.RegisterEventBusListener(OnServerConfigLibSettingChanged, 0.5, "configlib:seraphqol:setting-changed");
        api.Event.RegisterEventBusListener(OnServerConfigLibSaved, 0.5, "configlib:seraphqol:config-saved");
    }

    private void OnClientConfigLibSettingChanged(string eventName, ref EnumHandling handling, IAttribute data)
    {
        if (_suppressConfigLibEvents || _capi == null || data is not ITreeAttribute tree) return;

        string? setting = tree.GetString("setting");
        IAttribute? val = tree["value"];

        if (string.Equals(setting, nameof(SeraphQoLServerConfig.FaunaPreset), StringComparison.Ordinal)
            && val?.GetValue() is { } rawPreset
            && ConfigValueParser.TryEnum<FaunaPreset>(rawPreset, out FaunaPreset preset)
            && preset != FaunaPreset.Custom)
        {
            var preview = new SeraphQoLServerConfig();
            preview.ApplyPreset(preset);
            TryPushServerFaunaToConfigLib(_capi, preview, persistJson: true);
        }
        else if (IsFaunaSettingKey(setting))
        {
            SeraphQoLServerConfig? probe = ReadServerConfigFromConfigLib(_capi);
            if (probe != null)
            {
                probe.ApplySetting(setting, val);
                FaunaPreset detected = probe.DetectMatchingPreset();
                var entries = new List<(string Key, object Value)>(2)
                {
                    (nameof(SeraphQoLServerConfig.FaunaPreset), detected.ToString())
                };
                if (val?.GetValue() is { } rawChanged)
                {
                    entries.Add((setting!, rawChanged));
                }
                TryPushMultipleConfigLibSettings(_capi, entries, persistJson: true);
            }
        }

        if (string.Equals(setting, nameof(SeraphQoLServerConfig.StackSizePreset), StringComparison.Ordinal)
            && val?.GetValue() is { } rawStackPreset
            && ConfigValueParser.TryEnum<StackSizePreset>(rawStackPreset, out StackSizePreset stackPreset)
            && stackPreset != StackSizePreset.Custom)
        {
            var preview = new SeraphQoLServerConfig();
            preview.ApplyStackPreset(stackPreset);
            TryPushServerStackSizesToConfigLib(_capi, preview, persistJson: true);
        }
        else if (IsStackSettingKey(setting))
        {
            SeraphQoLServerConfig? probe = ReadServerConfigFromConfigLib(_capi);
            if (probe != null)
            {
                probe.ApplySetting(setting, val);
                StackSizePreset detected = probe.DetectMatchingStackPreset();
                var entries = new List<(string Key, object Value)>(2)
                {
                    (nameof(SeraphQoLServerConfig.StackSizePreset), detected.ToString())
                };
                if (val?.GetValue() is { } rawChanged)
                {
                    entries.Add((setting!, rawChanged));
                }
                TryPushMultipleConfigLibSettings(_capi, entries, persistJson: true);
            }
        }

        if (string.Equals(setting, nameof(SeraphQoLServerConfig.BackpackPreset), StringComparison.Ordinal)
            && val?.GetValue() is { } rawBackpackPreset
            && ConfigValueParser.TryEnum<BackpackPreset>(rawBackpackPreset, out BackpackPreset backpackPreset)
            && backpackPreset != BackpackPreset.Custom)
        {
            var preview = new SeraphQoLServerConfig();
            preview.ApplyBackpackPreset(backpackPreset);
            TryPushServerBackpacksToConfigLib(_capi, preview, persistJson: true);
        }
        else if (IsBackpackSettingKey(setting))
        {
            SeraphQoLServerConfig? probe = ReadServerConfigFromConfigLib(_capi);
            if (probe != null)
            {
                probe.ApplySetting(setting, val);
                BackpackPreset detected = probe.DetectMatchingBackpackPreset();
                var entries = new List<(string Key, object Value)>(2)
                {
                    (nameof(SeraphQoLServerConfig.BackpackPreset), detected.ToString())
                };
                if (val?.GetValue() is { } rawChanged)
                {
                    entries.Add((setting!, rawChanged));
                }
                TryPushMultipleConfigLibSettings(_capi, entries, persistJson: true);
            }
        }

        if (ClientConfig.ApplySetting(setting, val))
        {
            SaveClientConfig();
            if (string.Equals(setting, nameof(SeraphQoLClientConfig.AlloySearchNearbyContainers), StringComparison.Ordinal)
                || string.Equals(setting, nameof(SeraphQoLClientConfig.AlloySearchRadius), StringComparison.Ordinal))
            {
                _alloyDepositService?.ClearScanCache();
            }
            RefreshResourceParticles();
            ReloadOpenInventoryDialogs();
        }
    }

    private void OnClientConfigLibSaved(string eventName, ref EnumHandling handling, IAttribute data)
    {
        if (_suppressConfigLibEvents || _capi == null) return;
        SeraphQoLClientConfig? loaded = _capi.LoadModConfig<SeraphQoLClientConfig>(ClientConfigFile);
        if (loaded != null)
        {
            loaded.Normalize();
            ClientConfig = loaded;
        }
        RefreshResourceParticles();
        ReloadOpenInventoryDialogs();
    }

    private void RefreshResourceParticles()
    {
        _resourceParticlesService?.ApplyConfiguredParticles();
    }

    private void OnServerConfigLibSettingChanged(string eventName, ref EnumHandling handling, IAttribute data)
    {
        if (_suppressConfigLibEvents || _sapi == null || data is not ITreeAttribute tree) return;

        string? setting = tree.GetString("setting");
        IAttribute? val = tree["value"];
        if (!ServerConfig.ApplySetting(setting, val)) return;

        bool persistCombined = _sapi.Server.IsDedicated;
        if (string.Equals(setting, nameof(SeraphQoLServerConfig.FaunaPreset), StringComparison.Ordinal)
            && ServerConfig.FaunaPreset != FaunaPreset.Custom)
        {
            TryPushServerFaunaToConfigLib(_sapi, ServerConfig, persistJson: persistCombined);
        }
        else if (IsFaunaSettingKey(setting))
        {
            TryPushSingleConfigLibSetting(_sapi, nameof(SeraphQoLServerConfig.FaunaPreset), ServerConfig.FaunaPreset.ToString(), persistJson: persistCombined);
        }

        if (string.Equals(setting, nameof(SeraphQoLServerConfig.StackSizePreset), StringComparison.Ordinal)
            && ServerConfig.StackSizePreset != StackSizePreset.Custom)
        {
            TryPushServerStackSizesToConfigLib(_sapi, ServerConfig, persistJson: persistCombined);
        }
        else if (IsStackSettingKey(setting))
        {
            TryPushSingleConfigLibSetting(_sapi, nameof(SeraphQoLServerConfig.StackSizePreset), ServerConfig.StackSizePreset.ToString(), persistJson: persistCombined);
        }

        if (string.Equals(setting, nameof(SeraphQoLServerConfig.BackpackPreset), StringComparison.Ordinal)
            && ServerConfig.BackpackPreset != BackpackPreset.Custom)
        {
            TryPushServerBackpacksToConfigLib(_sapi, ServerConfig, persistJson: persistCombined);
        }
        else if (IsBackpackSettingKey(setting))
        {
            TryPushSingleConfigLibSetting(_sapi, nameof(SeraphQoLServerConfig.BackpackPreset), ServerConfig.BackpackPreset.ToString(), persistJson: persistCombined);
        }

        ApplyUpdatedServerPolicyLive(_sapi);
    }

    private void OnServerConfigLibSaved(string eventName, ref EnumHandling handling, IAttribute data)
    {
        if (_suppressConfigLibEvents || _sapi == null) return;

        SeraphQoLServerConfig? combined = LoadServerConfigFromCombined(_sapi);
        if (combined != null)
        {
            bool faunaPresetSwitched = combined.FaunaPreset != FaunaPreset.Custom
                && combined.FaunaPreset != ServerConfig.FaunaPreset
                && combined.DetectMatchingPreset() != combined.FaunaPreset;
            bool stackPresetSwitched = combined.StackSizePreset != StackSizePreset.Custom
                && combined.StackSizePreset != ServerConfig.StackSizePreset
                && combined.DetectMatchingStackPreset() != combined.StackSizePreset;
            bool backpackPresetSwitched = combined.BackpackPreset != BackpackPreset.Custom
                && combined.BackpackPreset != ServerConfig.BackpackPreset
                && combined.DetectMatchingBackpackPreset() != combined.BackpackPreset;

            ServerConfig.SyncConfigSettingsFrom(combined);

            if (faunaPresetSwitched)
            {
                ServerConfig.ApplyPreset(combined.FaunaPreset);
                TryPushServerFaunaToConfigLib(_sapi, ServerConfig, persistJson: _sapi.Server.IsDedicated);
            }
            else
            {
                ServerConfig.FaunaPreset = ServerConfig.DetectMatchingPreset();
            }

            if (stackPresetSwitched)
            {
                ServerConfig.ApplyStackPreset(combined.StackSizePreset);
                TryPushServerStackSizesToConfigLib(_sapi, ServerConfig, persistJson: _sapi.Server.IsDedicated);
            }
            else
            {
                ServerConfig.StackSizePreset = ServerConfig.DetectMatchingStackPreset();
            }

            if (backpackPresetSwitched)
            {
                ServerConfig.ApplyBackpackPreset(combined.BackpackPreset);
                TryPushServerBackpacksToConfigLib(_sapi, ServerConfig, persistJson: _sapi.Server.IsDedicated);
            }
            else
            {
                ServerConfig.BackpackPreset = ServerConfig.DetectMatchingBackpackPreset();
            }
        }

        ApplyUpdatedServerPolicyLive(_sapi);
    }

    private void ApplyUpdatedServerPolicyLive(ICoreServerAPI sapi)
    {
        SaveServerConfig();
        RefreshEffectivePolicy();
        _faunaService.ApplyToEntityTypes(sapi, EffectivePolicy);
        _faunaService.AdjustLoadedEntities(sapi, EffectivePolicy);
        _stackSizeService.ApplyToCollectibles(sapi, EffectivePolicy);
        _backpackService.ApplyToCollectibles(sapi, EffectivePolicy);
        BackpackAssetsService.ReloadOnlinePlayerBackpacks(sapi);
        _serverChannel?.BroadcastPacket(ServerPolicySyncPacket.FromPolicy(EffectivePolicy));
    }

    private void TryPushServerStackSizesToConfigLib(ICoreAPI api, SeraphQoLServerConfig cfg, bool persistJson = false) =>
        TryPushMultipleConfigLibSettings(api, cfg.GetStackPresetEntries(includePresetKey: true), persistJson);

    private void TryPushServerBackpacksToConfigLib(ICoreAPI api, SeraphQoLServerConfig cfg, bool persistJson = false) =>
        TryPushMultipleConfigLibSettings(api, cfg.GetBackpackPresetEntries(includePresetKey: true), persistJson);

    private static bool IsFaunaSettingKey(string? setting) =>
        setting is nameof(SeraphQoLServerConfig.EnableFaunaRebalance)
            or nameof(SeraphQoLServerConfig.SmallPreyHealthMultiplier)
            or nameof(SeraphQoLServerConfig.MediumLargePreyHealthMultiplier)
            or nameof(SeraphQoLServerConfig.PredatorHealthMultiplier)
            or nameof(SeraphQoLServerConfig.MeatDropMultiplier)
            or nameof(SeraphQoLServerConfig.FatDropMultiplier)
            or nameof(SeraphQoLServerConfig.HideDropMultiplier)
            or nameof(SeraphQoLServerConfig.ByproductDropMultiplier)
            or nameof(SeraphQoLServerConfig.ScaleDomesticatedHealth)
            or nameof(SeraphQoLServerConfig.ScaleDomesticatedDrops)
            or nameof(SeraphQoLServerConfig.IncludeAquaticFauna);

    private static bool IsStackSettingKey(string? setting) =>
        setting is nameof(SeraphQoLServerConfig.EnableStackSizeModifier)
            or nameof(SeraphQoLServerConfig.GlobalStackMultiplier)
            or nameof(SeraphQoLServerConfig.BuildingBlocksStackMultiplier)
            or nameof(SeraphQoLServerConfig.ResourcesStackMultiplier)
            or nameof(SeraphQoLServerConfig.FoodStackMultiplier)
            or nameof(SeraphQoLServerConfig.MiscStackMultiplier)
            or nameof(SeraphQoLServerConfig.AllowUnstackableContainersAndFurniture)
            or nameof(SeraphQoLServerConfig.EmptyContainersMaxStack)
            or nameof(SeraphQoLServerConfig.FurnitureUtilityMaxStack)
            or nameof(SeraphQoLServerConfig.ClothingMaxStack)
            or nameof(SeraphQoLServerConfig.MaxStackSizeCap);

    private static bool IsBackpackSettingKey(string? setting) =>
        setting is nameof(SeraphQoLServerConfig.EnableBackpackExpansion)
            or nameof(SeraphQoLServerConfig.GlobalBackpackMultiplier)
            or nameof(SeraphQoLServerConfig.StandardBackpacksMultiplier)
            or nameof(SeraphQoLServerConfig.SpecializedBagsMultiplier)
            or nameof(SeraphQoLServerConfig.SaddlebagsMultiplier)
            or nameof(SeraphQoLServerConfig.ModdedBackpacksMultiplier)
            or nameof(SeraphQoLServerConfig.MaxBackpackSlotsCap);

    private void TryPushServerFaunaToConfigLib(ICoreAPI api, SeraphQoLServerConfig cfg, bool persistJson = false) =>
        TryPushMultipleConfigLibSettings(api, cfg.GetFaunaPresetEntries(includePresetKey: true), persistJson);

    private void TryPushSingleConfigLibSetting(ICoreAPI api, string key, object value, bool persistJson = false) =>
        TryPushMultipleConfigLibSettings(api, [(key, value)], persistJson);

    private void TryPushMultipleConfigLibSettings(
        ICoreAPI api,
        IReadOnlyList<(string Key, object Value)> entries,
        bool persistJson = false)
    {
        object? modCfg = GetConfigLibConfig(api);
        if (modCfg == null)
        {
            if (persistJson)
            {
                StoreConfigEntries(api, ClientConfigFile, entries);
            }
            return;
        }

        MethodInfo? getSetting = modCfg.GetType().GetMethod("GetSetting", [typeof(string)]);
        if (getSetting == null) return;

        _suppressConfigLibEvents = true;
        try
        {
            foreach ((string key, object value) in entries)
            {
                object? settingObj = getSetting.Invoke(modCfg, [key]);
                PropertyInfo? valueProp = settingObj?.GetType().GetProperty("Value");
                if (valueProp == null || !valueProp.CanWrite) continue;

                var jsonValue = new JsonObject(JToken.FromObject(value));
                valueProp.SetValue(settingObj, jsonValue);
            }

            if (persistJson)
            {
                PersistConfigLibSnapshotToJson(api, modCfg, ClientConfigFile, entries);
            }
        }
        finally
        {
            _suppressConfigLibEvents = false;
        }
    }

    private static object? GetConfigLibConfig(ICoreAPI api)
    {
        ModSystem? cfgLib = api.ModLoader.GetModSystem("ConfigLib.ConfigLibModSystem");
        if (cfgLib == null) return null;

        MethodInfo? getConfig = cfgLib.GetType().GetMethod("GetConfig", [typeof(string)]);
        return getConfig?.Invoke(cfgLib, [SeraphQoLLang.Domain]);
    }

    private static SeraphQoLServerConfig? ReadServerConfigFromConfigLib(ICoreAPI api)
    {
        object? modCfg = GetConfigLibConfig(api);
        if (modCfg == null) return null;

        MethodInfo? getSetting = modCfg.GetType().GetMethod("GetSetting", [typeof(string)]);
        if (getSetting == null) return null;

        var jobj = new JObject();
        foreach (PropertyInfo prop in typeof(SeraphQoLServerConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite) continue;
            object? settingObj = getSetting.Invoke(modCfg, [prop.Name]);
            if (settingObj?.GetType().GetProperty("Value")?.GetValue(settingObj) is JsonObject jo && jo.Token != null)
            {
                jobj[prop.Name] = jo.Token.DeepClone();
            }
        }

        return jobj.ToObject<SeraphQoLServerConfig>();
    }

    private static void PersistConfigLibSnapshotToJson(
        ICoreAPI api,
        object modCfg,
        string fileName,
        IReadOnlyList<(string Key, object Value)> fallbackEntries)
    {
        JObject target = api.LoadModConfig<JObject>(fileName) ?? new JObject();
        MethodInfo? getSetting = modCfg.GetType().GetMethod("GetSetting", [typeof(string)]);

        if (getSetting != null)
        {
            foreach (string key in GetAllConfigLibKeys())
            {
                object? settingObj = getSetting.Invoke(modCfg, [key]);
                if (settingObj?.GetType().GetProperty("Value")?.GetValue(settingObj) is JsonObject jo && jo.Token != null)
                {
                    target[key] = jo.Token.DeepClone();
                }
            }
        }

        foreach ((string key, object value) in fallbackEntries)
        {
            target[key] = JToken.FromObject(value);
        }

        api.StoreModConfig(target, fileName);
    }

    private static IEnumerable<string> GetAllConfigLibKeys()
    {
        foreach (PropertyInfo prop in typeof(SeraphQoLClientConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanWrite) yield return prop.Name;
        }
        foreach (PropertyInfo prop in typeof(SeraphQoLServerConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanWrite) yield return prop.Name;
        }
    }

    private static void StoreConfigEntries(ICoreAPI api, string fileName, IReadOnlyList<(string Key, object Value)> entries)
    {
        JObject target = api.LoadModConfig<JObject>(fileName) ?? new JObject();
        foreach ((string key, object value) in entries)
        {
            target[key] = JToken.FromObject(value);
        }
        api.StoreModConfig(target, fileName);
    }

    private static void StoreMergedConfig(ICoreAPI api, string fileName, object source)
    {
        JObject target = api.LoadModConfig<JObject>(fileName) ?? new JObject();
        JObject patch = JObject.FromObject(source);
        foreach (JProperty prop in patch.Properties())
        {
            target[prop.Name] = prop.Value;
        }
        api.StoreModConfig(target, fileName);
    }

    private static void MergeServerSettingsIfCombinedNewer(ICoreAPI api, SeraphQoLServerConfig target)
    {
        string configDir = api.GetOrCreateDataPath("ModConfig");
        string combinedPath = Path.Combine(configDir, ClientConfigFile);
        string serverPath = Path.Combine(configDir, ServerConfigFile);
        if (!File.Exists(combinedPath)) return;

        if (File.Exists(serverPath) && File.GetLastWriteTimeUtc(combinedPath) <= File.GetLastWriteTimeUtc(serverPath))
        {
            return;
        }

        SeraphQoLServerConfig? combined = LoadServerConfigFromCombined(api);
        if (combined != null)
        {
            target.SyncConfigSettingsFrom(combined);
        }
    }

    private static SeraphQoLServerConfig? LoadServerConfigFromCombined(ICoreAPI api)
    {
        JObject? combined = api.LoadModConfig<JObject>(ClientConfigFile);
        if (combined == null) return null;
        if (!combined.ContainsKey(nameof(SeraphQoLServerConfig.EnableFaunaRebalance))
            && !combined.ContainsKey(nameof(SeraphQoLServerConfig.FaunaPreset))
            && !combined.ContainsKey(nameof(SeraphQoLServerConfig.EnableStackSizeModifier))
            && !combined.ContainsKey(nameof(SeraphQoLServerConfig.StackSizePreset))
            && !combined.ContainsKey(nameof(SeraphQoLServerConfig.EnableBackpackExpansion))
            && !combined.ContainsKey(nameof(SeraphQoLServerConfig.BackpackPreset)))
        {
            return null;
        }
        return combined.ToObject<SeraphQoLServerConfig>();
    }

    private void RefreshSortButtonTooltips()
    {
        if (_capi == null) return;
        string sortTip = SeraphQoLLang.Get("tooltip-sort", SeraphQoLLang.GetSortOrderName(ClientConfig.SortOrder));
        foreach (GuiDialog dialog in _capi.Gui.OpenedGuis)
        {
            if (!dialog.IsOpened()) continue;
            foreach (GuiComposer composer in dialog.Composers.Values)
            {
                (composer.GetElement("seraphqol-inv-sort") as SvgToolbarButton)?.UpdateTooltip(sortTip);
                (composer.GetElement("seraphqol-cont-sort") as SvgToolbarButton)?.UpdateTooltip(sortTip);
            }
        }
    }

    private void ReloadOpenInventoryDialogs()
    {
        if (_capi == null) return;
        foreach (GuiDialog dialog in _capi.Gui.OpenedGuis.ToArray())
        {
            if (dialog is GuiDialogInventory invDialog && invDialog.IsOpened())
            {
                invDialog.ComposeGui(true);
            }
        }
        RefreshSortButtonTooltips();
    }

    public override void Dispose()
    {
        if (_capi != null && _clientTickListenerId != 0)
        {
            _capi.Event.UnregisterGameTickListener(_clientTickListenerId);
            _clientTickListenerId = 0;
        }
        _projectileTrackerServer?.Dispose();
        _projectileTrackerClient?.ClearAll();
        _radialToolModeDialog?.Dispose();
        _radialQuickSwapDialog?.Dispose();
        _alloyCalculatorDialog?.Dispose();
        _groundHighlightRenderer?.Dispose();
        _slotIndicatorRenderer?.Dispose();
        _animalSexRenderer?.Dispose();
        _buttonsComposer?.Dispose();
        _harmony?.UnpatchAll(_harmonyId);
        if (_isClient)
        {
            SeraphQoLClientPatches.ClientInstance = null;
            if (ReferenceEquals(SeraphQoLCommonPatches.ClientInstance, this))
            {
                SeraphQoLCommonPatches.ClientInstance = null;
            }
        }
        else if (ReferenceEquals(SeraphQoLCommonPatches.ServerInstance, this))
        {
            SeraphQoLCommonPatches.ServerInstance = null;
        }
        base.Dispose();
    }
}
