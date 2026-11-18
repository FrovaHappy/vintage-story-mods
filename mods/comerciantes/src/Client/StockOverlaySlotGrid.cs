using SystemUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Comerciantes;

/// <summary>
/// Grilla de slots que dibuja el indicador de agotado (X) dentro del rango Z propio de la ventana
/// (Z=140, menor que GuiDialog.ZSize=150) para que no traspase otras ventanas ni el directorio.
/// </summary>
public sealed class StockOverlaySlotGrid : GuiElementItemSlotGrid
{
    private const float OutOfStockOverlayZ = 140f;

    public StockOverlaySlotGrid(
        ICoreClientAPI capi,
        IInventory inventory,
        Action<object> sendPacketHandler,
        int columns,
        int[]? selectiveSlots,
        ElementBounds bounds)
        : base(capi, inventory, sendPacketHandler, columns, selectiveSlots, bounds)
    {
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        base.RenderInteractiveElements(deltaTime);

        if (crossedOutTexture.TextureId <= 0 || Bounds?.ParentBounds == null) return;

        ElementBounds clipBounds = InsideClipBounds ?? Bounds.ParentBounds;
        int slotIndex = 0;
        double clipAbsY = clipBounds.absY;
        foreach (KeyValuePair<int, ItemSlot> entry in renderedSlots)
        {
            if (slotIndex >= SlotBounds.Length) break;

            ElementBounds slotBounds = SlotBounds[slotIndex];
            slotIndex++;

            if (slotBounds.absFixedY + slotBounds.OuterHeight < 0 || slotBounds.absY + slotBounds.absInnerHeight < clipAbsY)
            {
                continue;
            }
            if (!slotBounds.PartiallyInside(clipBounds))
            {
                continue;
            }

            if (entry.Value is DisplayOnlyItemSlot displaySlot && displaySlot.IsUnavailable && !displaySlot.Empty)
            {
                api.Render.Render2DTexturePremultipliedAlpha(
                    crossedOutTexture.TextureId,
                    (float)slotBounds.renderX,
                    (float)slotBounds.renderY,
                    crossedOutTexture.Width,
                    crossedOutTexture.Height,
                    OutOfStockOverlayZ,
                    null);
            }
        }
    }
}

public static class StockSlotGridComposerExtensions
{
    public static GuiComposer AddStockSlotGridSection(
        this GuiComposer composer,
        string title,
        IInventory inventory,
        Action<object> sendPacket,
        int columns,
        int rows,
        double x,
        double y,
        SystemUITheme theme,
        string gridKey,
        out ElementBounds gridBounds,
        double titleWidth = 0,
        bool dynamicLabel = false)
    {
        return composer.AddSlotGridSection(
            title,
            inventory,
            sendPacket,
            columns,
            rows,
            x,
            y,
            theme,
            gridKey,
            out gridBounds,
            titleWidth,
            (api, inv, packetHandler, cols, bounds) => new StockOverlaySlotGrid(api, inv, packetHandler, cols, null, bounds),
            dynamicLabel);
    }

    public static GuiComposer AddStockSlotGridOnly(
        this GuiComposer composer,
        IInventory inventory,
        Action<object> sendPacket,
        int columns,
        int rows,
        double x,
        double y,
        string gridKey)
    {
        ElementBounds gridBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, columns, rows);
        if (!composer.Composed)
        {
            var grid = new StockOverlaySlotGrid(composer.Api, inventory, sendPacket, columns, null, gridBounds);
            composer.AddInteractiveElement(grid, gridKey);
            GuiElementItemSlotGridBase.UpdateLastSlotGridFlag(composer);
        }
        return composer;
    }
}
