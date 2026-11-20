using SeraphQoL.Config;
using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SeraphQoL.Interaction;

/// <summary>
/// Menu radial Quick-Swap para cambiar rapidamente entre herramientas y fuentes de luz de la hotbar/mochila.
/// </summary>
public sealed class GuiDialogRadialQuickSwap : GuiDialog
{
    private readonly SeraphQoLModSystem _modSystem;
    private readonly RadialRenderHelper _renderHelper;
    private readonly RadialHoldReleaseTracker _releaseTracker = new(180);

    private IReadOnlyList<QuickSwapCandidate> _candidates = Array.Empty<QuickSwapCandidate>();
    private readonly List<ItemSlot?> _candidateSlots = new();
    private RadialMenuPage _page = new(Array.Empty<RadialToolModeSector>(), 0, 1);
    private RadialGeometryConfig _geometry = new(135, 48, 34);

    private int _renderedHoverIndex = -2;
    private int _renderedPageIndex = -1;
    private int _renderedOuterRadius = -1;
    private int _hoveredSectorIndex = -1;
    private int _pageIndex;

    public GuiDialogRadialQuickSwap(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
        : base(capi)
    {
        _modSystem = modSystem;
        _renderHelper = new RadialRenderHelper(capi);
    }

    public override string ToggleKeyCombinationCode => "seraphqol.quickswap";
    public override bool PrefersUngrabbedMouse => true;
    public override EnumDialogType DialogType => EnumDialogType.Dialog;
    public override double DrawOrder => 0.12;
    public override bool CaptureAllInputs() => false;

    public bool ToggleRadial()
    {
        if (!_modSystem.ClientConfig.EnableQuickSwapRadial) return false;

        if (IsOpened())
        {
            TryClose();
            return true;
        }

        if (!PopulateCandidates()) return false;

        _pageIndex = 0;
        _hoveredSectorIndex = -1;
        _renderedHoverIndex = -2;
        _releaseTracker.Reset(capi.World?.ElapsedMilliseconds ?? 0);
        RebuildPageGeometry();
        return TryOpen();
    }

    public override void OnRenderGUI(float deltaTime)
    {
        if (!IsOpened() || _page.Sectors.Count == 0) return;

        RebuildPageGeometry();

        double centerX = capi.Render.FrameWidth * 0.5;
        double centerY = capi.Render.FrameHeight * 0.5;
        double dx = capi.Input.MouseX - centerX;
        double dy = capi.Input.MouseY - centerY;

        _hoveredSectorIndex = ToolModeRadialPlanner.HitTestSector(dx, dy, _page.Sectors, _geometry);

        long nowMs = capi.World?.ElapsedMilliseconds ?? 0;
        bool hotkeyDown = RadialRenderHelper.IsHotkeyDown(capi, "seraphqol.quickswap");
        if (_releaseTracker.ShouldSelectOnRelease(_modSystem.ClientConfig.QuickSwapSelectOnKeyRelease, hotkeyDown, nowMs, _hoveredSectorIndex))
        {
            SelectSector(_hoveredSectorIndex);
            return;
        }

        int outerInt = (int)Math.Round(_geometry.OuterRadius);
        if (_hoveredSectorIndex != _renderedHoverIndex
            || _page.PageIndex != _renderedPageIndex
            || outerInt != _renderedOuterRadius
            || _renderHelper.TextureId <= 0)
        {
            _renderHelper.RebuildRingTexture(outerInt, _geometry, _page.Sectors, _hoveredSectorIndex);
            _renderedHoverIndex = _hoveredSectorIndex;
            _renderedPageIndex = _page.PageIndex;
            _renderedOuterRadius = outerInt;
        }

        _renderHelper.RenderRing(centerX, centerY);
        RenderCandidateItems(centerX, centerY);
        _renderHelper.RenderCenterLabel(_page, _geometry, _hoveredSectorIndex, centerX, centerY);
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (!IsOpened()) return;

        if (args.Button == EnumMouseButton.Right)
        {
            TryClose();
            args.Handled = true;
            return;
        }

        if (args.Button == EnumMouseButton.Left)
        {
            if (_hoveredSectorIndex >= 0 && _hoveredSectorIndex < _page.Sectors.Count)
            {
                SelectSector(_hoveredSectorIndex);
            }
            else
            {
                TryClose();
            }
            args.Handled = true;
        }
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (!IsOpened() || _page.TotalPages <= 1) return;

        int step = args.delta < 0 ? 1 : -1;
        _pageIndex = ((_pageIndex + step) % _page.TotalPages + _page.TotalPages) % _page.TotalPages;
        RebuildPageGeometry();
        _renderedPageIndex = -1;
        args.SetHandled(true);
    }

    public override void OnKeyDown(KeyEvent args)
    {
        if (!IsOpened()) return;

        if (args.KeyCode == (int)GlKeys.Escape)
        {
            TryClose();
            args.Handled = true;
            return;
        }

        base.OnKeyDown(args);
    }

    private bool PopulateCandidates()
    {
        IPlayerInventoryManager? mgr = capi.World?.Player?.InventoryManager;
        if (mgr == null) return false;

        IInventory? hotbar = mgr.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        IInventory? backpack = mgr.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (hotbar == null) return false;

        var snapshots = new List<QuickSwapSlotSnapshot>();
        int activeIdx = mgr.ActiveHotbarSlotNumber;

        for (int i = 0; i < Math.Min(10, hotbar.Count); i++)
        {
            AddSlotSnapshot(snapshots, hotbar[i], GlobalConstants.hotBarInvClassName, i, i == activeIdx);
        }

        if (backpack != null)
        {
            for (int i = 0; i < backpack.Count; i++)
            {
                AddSlotSnapshot(snapshots, backpack[i], GlobalConstants.backpackInvClassName, i, isActive: false);
            }
        }

        _candidates = QuickSwapPlanner.BuildCandidates(snapshots);
        _candidateSlots.Clear();

        foreach (QuickSwapCandidate c in _candidates)
        {
            IInventory? inv = string.Equals(c.InventoryClass, GlobalConstants.hotBarInvClassName, StringComparison.Ordinal)
                ? hotbar
                : backpack;
            _candidateSlots.Add(inv != null && c.SlotIndex >= 0 && c.SlotIndex < inv.Count ? inv[c.SlotIndex] : null);
        }

        return _candidates.Count > 0;
    }

    private static void AddSlotSnapshot(List<QuickSwapSlotSnapshot> target, ItemSlot? slot, string invClass, int index, bool isActive)
    {
        if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null) return;

        ItemStack stack = slot.Itemstack;
        CollectibleObject col = stack.Collectible;
        bool isTool = col.Tool != null;
        byte[]? hsv = col.GetLightHsv(null, null, stack);
        int light = hsv != null && hsv.Length >= 3 ? hsv[2] : 0;

        target.Add(new QuickSwapSlotSnapshot(
            invClass,
            index,
            col.Code?.ToString() ?? string.Empty,
            stack.GetName(),
            isTool,
            light,
            isActive));
    }

    private void RebuildPageGeometry()
    {
        double outer = GuiElement.scaled(_modSystem.ClientConfig.RadialToolModeOuterRadius);
        double inner = outer * 0.36;
        double deadZone = outer * 0.25;
        _geometry = new RadialGeometryConfig(outer, inner, deadZone);
        _page = ToolModeRadialPlanner.BuildSectors(QuickSwapPlanner.ToRadialDescriptors(_candidates), _pageIndex);
    }

    private void RenderCandidateItems(double centerX, double centerY)
    {
        float itemSize = (float)GuiElement.scaled(20.2);
        foreach (RadialToolModeSector sector in _page.Sectors)
        {
            if (sector.NativeModeIndex < 0 || sector.NativeModeIndex >= _candidateSlots.Count) continue;
            ItemSlot? slot = _candidateSlots[sector.NativeModeIndex];
            if (slot == null || slot.Empty) continue;

            (double ix, double iy) = ToolModeRadialPlanner.ComputeIconCenter(sector, _geometry);
            capi.Render.RenderItemstackToGui(
                slot,
                centerX + ix,
                centerY + iy,
                60.0,
                itemSize,
                -1,
                shading: true,
                rotate: false,
                showStackSize: true);
        }
    }

    private void SelectSector(int sectorIndex)
    {
        if (sectorIndex < 0 || sectorIndex >= _page.Sectors.Count) return;
        int idx = _page.Sectors[sectorIndex].NativeModeIndex;
        if (idx < 0 || idx >= _candidates.Count) return;

        QuickSwapCandidate choice = _candidates[idx];
        IPlayerInventoryManager? mgr = capi.World?.Player?.InventoryManager;
        if (mgr == null)
        {
            TryClose();
            return;
        }

        if (string.Equals(choice.InventoryClass, GlobalConstants.hotBarInvClassName, StringComparison.OrdinalIgnoreCase))
        {
            mgr.ActiveHotbarSlotNumber = choice.SlotIndex;
        }
        else
        {
            _modSystem.SendSwapSlotsPacket(new SwapSlotsPacket
            {
                SourceInventoryClass = choice.InventoryClass,
                SourceSlotIndex = choice.SlotIndex,
                TargetInventoryClass = GlobalConstants.hotBarInvClassName,
                TargetSlotIndex = mgr.ActiveHotbarSlotNumber
            });
        }

        capi.Gui.PlaySound("tick");
        TryClose();
    }

    public override void Dispose()
    {
        _renderHelper.Dispose();
        base.Dispose();
    }
}
