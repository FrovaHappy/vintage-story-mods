using SeraphQoL.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace SeraphQoL.Interaction;

/// <summary>
/// Menu radial interactivo que reemplaza el dialogo vanilla GuiDialogToolMode (tecla F).
/// </summary>
public sealed class GuiDialogRadialToolMode : GuiDialog
{
    private readonly SeraphQoLModSystem _modSystem;
    private readonly RadialRenderHelper _renderHelper;
    private readonly RadialHoldReleaseTracker _releaseTracker = new(180);
    private readonly List<ToolModeDescriptor> _descriptors = new();

    private SkillItem[] _skillItems = Array.Empty<SkillItem>();
    private BlockSelection? _blockSelection;
    private RadialMenuPage _page = new(Array.Empty<RadialToolModeSector>(), 0, 1);
    private RadialGeometryConfig _geometry = new(135, 48, 34);

    private int _renderedHoverIndex = -2;
    private int _renderedPageIndex = -1;
    private int _renderedOuterRadius = -1;
    private int _hoveredSectorIndex = -1;
    private int _pageIndex;

    public GuiDialogRadialToolMode(ICoreClientAPI capi, SeraphQoLModSystem modSystem)
        : base(capi)
    {
        _modSystem = modSystem;
        _renderHelper = new RadialRenderHelper(capi);
    }

    public override string ToggleKeyCombinationCode => string.Empty;
    public override bool PrefersUngrabbedMouse => true;
    public override EnumDialogType DialogType => EnumDialogType.Dialog;
    public override double DrawOrder => 0.11;

    public override bool CaptureAllInputs() => false;

    public void OpenForTool(ItemSlot slot, BlockSelection? blockSel, SkillItem[] items)
    {
        _skillItems = items;
        _blockSelection = blockSel?.Clone();
        _pageIndex = 0;
        _hoveredSectorIndex = -1;
        _renderedHoverIndex = -2;
        _releaseTracker.Reset(capi.World?.ElapsedMilliseconds ?? 0);

        int currentMode = capi.World?.Player != null
            ? (slot.Itemstack?.Collectible?.GetToolMode(slot, capi.World.Player, _blockSelection) ?? 0)
            : 0;

        _descriptors.Clear();
        for (int i = 0; i < items.Length; i++)
        {
            SkillItem sk = items[i];
            if (sk == null) continue;
            _descriptors.Add(new ToolModeDescriptor(
                NativeModeIndex: i,
                Code: sk.Code?.ToString() ?? $"mode-{i}",
                Name: sk.Name ?? string.Empty,
                Description: sk.Description ?? string.Empty,
                IsSelected: i == currentMode,
                IsEnabled: sk.Enabled));
        }

        RebuildPageGeometry();

        if (!IsOpened())
        {
            TryOpen();
        }
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
        bool hotkeyDown = RadialRenderHelper.IsHotkeyDown(capi, "toolmodeselect");
        if (_releaseTracker.ShouldSelectOnRelease(_modSystem.ClientConfig.ToolModeSelectOnKeyRelease, hotkeyDown, nowMs, _hoveredSectorIndex))
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
        RenderSectorIcons(deltaTime, centerX, centerY);
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

    private void RebuildPageGeometry()
    {
        double outer = GuiElement.scaled(_modSystem.ClientConfig.RadialToolModeOuterRadius);
        double inner = outer * 0.36;
        double deadZone = outer * 0.25;
        _geometry = new RadialGeometryConfig(outer, inner, deadZone);
        _page = ToolModeRadialPlanner.BuildSectors(_descriptors, _pageIndex);
    }

    private void RenderSectorIcons(float deltaTime, double centerX, double centerY)
    {
        double iconSize = GuiElement.scaled(21.7);
        double iconHalf = iconSize * 0.5;

        foreach (RadialToolModeSector sector in _page.Sectors)
        {
            if (sector.NativeModeIndex < 0 || sector.NativeModeIndex >= _skillItems.Length) continue;
            SkillItem skill = _skillItems[sector.NativeModeIndex];
            if (skill == null) continue;

            (double ix, double iy) = ToolModeRadialPlanner.ComputeIconCenter(sector, _geometry);
            double drawX = centerX + ix;
            double drawY = centerY + iy;

            if (skill.Texture != null && skill.Texture.TextureId > 0)
            {
                Vec4f? color = sector.IsEnabled ? null : new Vec4f(0.5f, 0.5f, 0.5f, 0.55f);
                if (skill.TexturePremultipliedAlpha)
                {
                    capi.Render.Render2DTexturePremultipliedAlpha(
                        skill.Texture.TextureId,
                        (float)(drawX - iconHalf),
                        (float)(drawY - iconHalf),
                        (float)iconSize,
                        (float)iconSize,
                        60f,
                        color);
                }
                else
                {
                    capi.Render.Render2DTexture(
                        skill.Texture.TextureId,
                        (float)(drawX - iconHalf),
                        (float)(drawY - iconHalf),
                        (float)iconSize,
                        (float)iconSize,
                        60f,
                        color);
                }
            }
            else if (skill.RenderHandler != null)
            {
                skill.RenderHandler.Invoke(skill.Code, deltaTime, drawX, drawY);
            }
        }
    }

    private void SelectSector(int sectorIndex)
    {
        if (sectorIndex < 0 || sectorIndex >= _page.Sectors.Count) return;
        RadialToolModeSector sector = _page.Sectors[sectorIndex];
        if (!sector.IsEnabled) return;

        IPlayer? player = capi.World?.Player;
        ItemSlot? slot = player?.InventoryManager?.ActiveHotbarSlot;
        if (player == null || slot?.Itemstack?.Collectible == null)
        {
            TryClose();
            return;
        }

        int modeIndex = sector.NativeModeIndex;
        slot.Itemstack.Collectible.SetToolMode(slot, player, _blockSelection, modeIndex);

        var tm = new Packet_ToolMode { Mode = modeIndex };
        if (_blockSelection != null)
        {
            tm.X = _blockSelection.Position.X;
            tm.Y = _blockSelection.Position.InternalY;
            tm.Z = _blockSelection.Position.Z;
            tm.SelectionBoxIndex = _blockSelection.SelectionBoxIndex;
            tm.Face = _blockSelection.Face.Index;
            tm.HitX = CollectibleNet.SerializeDouble(_blockSelection.HitPosition.X);
            tm.HitY = CollectibleNet.SerializeDouble(_blockSelection.HitPosition.Y);
            tm.HitZ = CollectibleNet.SerializeDouble(_blockSelection.HitPosition.Z);
        }

        capi.Network.SendPacketClient(new Packet_Client
        {
            Id = Packet_ClientIdEnum.SetToolMode,
            ToolMode = tm
        });
        slot.MarkDirty();
        capi.Gui.PlaySound("tick");
        TryClose();
    }

    public override void Dispose()
    {
        _renderHelper.Dispose();
        base.Dispose();
    }
}
