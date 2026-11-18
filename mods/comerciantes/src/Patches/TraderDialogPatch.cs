using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Comerciantes;

/// <summary>
/// Parches Harmony sobre EntityTradingHumanoid.TryOpenTradeDialog y tooltip opcional en inventario.
/// Delegan inmediatamente en TraderEntityInterop y ComerciantesModSystem.
/// </summary>
[HarmonyPatch]
public static class TraderDialogPatch
{
    public static ComerciantesModSystem? ClientInstance { get; set; }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EntityTradingHumanoid), "TryOpenTradeDialog")]
    public static bool PrefixTryOpenTradeDialog(EntityTradingHumanoid __instance, EntityAgent forEntity)
    {
        TraderEntityInterop.ClearInteractingPlayer(__instance, null);

        if (__instance.Api.Side == EnumAppSide.Client)
        {
            ClientInstance?.RequestOpenTrader(__instance.EntityId);
        }

        // Anula la apertura del GuiDialogTrader nativo tanto en cliente como en servidor.
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetHeldItemInfo))]
    public static void PostfixGetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
    {
        ClientInstance?.AppendInventoryValuationTooltip(inSlot, dsc, world);
    }
}
