using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Comerciantes;

/// <summary>
/// Slot de solo lectura para grillas visuales (mercancia del comerciante, carrito e items reclamados).
/// Permite ver icono, cantidad, indicador de agotado (X) y tooltip enriquecido.
/// </summary>
public sealed class DisplayOnlyItemSlot : ItemSlot
{
    private readonly Action<int, EnumMouseButton>? _onClicked;
    private readonly System.Func<string>? _extraTooltipProvider;
    private readonly System.Func<bool>? _isUnavailableProvider;

    public DisplayOnlyItemSlot(
        InventoryBase inventory,
        Action<int, EnumMouseButton>? onClicked = null,
        System.Func<string>? extraTooltipProvider = null,
        System.Func<bool>? isUnavailableProvider = null) : base(inventory)
    {
        _onClicked = onClicked;
        _extraTooltipProvider = extraTooltipProvider;
        _isUnavailableProvider = isUnavailableProvider;
    }

    public bool IsUnavailable => _isUnavailableProvider?.Invoke() ?? false;

    // Se dibuja el icono de agotado en StockOverlaySlotGrid a Z=140 (< ZSize=150)
    // para evitar el bug vanilla (Z=250) que se superpone sobre otras ventanas abiertas.
    public override bool DrawUnavailable => false;

    public override bool CanTake() => false;

    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) => false;

    public override bool CanHold(ItemSlot sourceSlot) => false;

    public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
    {
        if (inventory == null) return;
        int index = inventory.GetSlotId(this);
        _onClicked?.Invoke(index, op.MouseButton);
    }

    public override string GetStackDescription(IClientWorldAccessor world, bool extendedDebugInfo)
    {
        string baseDesc = base.GetStackDescription(world, extendedDebugInfo);
        if (_extraTooltipProvider == null) return baseDesc;

        string extra = _extraTooltipProvider();
        if (string.IsNullOrEmpty(extra)) return baseDesc;

        var sb = new StringBuilder(baseDesc);
        sb.AppendLine();
        sb.Append(extra);
        return sb.ToString();
    }
}

/// <summary>
/// Slot interactivo para la grilla de venta de 5 slots que anade el desglose economico al tooltip.
/// </summary>
public sealed class SellOfferItemSlot : ItemSlotSurvival
{
    private readonly System.Func<int, string>? _tooltipProvider;

    public SellOfferItemSlot(InventoryBase inventory, System.Func<int, string>? tooltipProvider = null) : base(inventory)
    {
        _tooltipProvider = tooltipProvider;
    }

    public override string GetStackDescription(IClientWorldAccessor world, bool extendedDebugInfo)
    {
        string baseDesc = base.GetStackDescription(world, extendedDebugInfo);
        if (_tooltipProvider == null || inventory == null) return baseDesc;

        int slotIndex = inventory.GetSlotId(this);
        string extra = _tooltipProvider(slotIndex);
        if (string.IsNullOrEmpty(extra)) return baseDesc;

        var sb = new StringBuilder(baseDesc);
        sb.AppendLine();
        sb.Append(extra);
        return sb.ToString();
    }
}
