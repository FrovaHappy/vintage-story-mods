using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Comerciantes;

/// <summary>
/// Centraliza el acceso por reflexión a miembros internos de entidades de Vintage Story
/// para mantener desacoplados los parches Harmony, el ModSystem y los diálogos.
/// </summary>
internal static class TraderEntityInterop
{
    public static void ClearInteractingPlayer(EntityTradingHumanoid trader, EntityPlayer? playerEntity)
    {
        List<EntityPlayer>? list = Traverse.Create(trader).Field<List<EntityPlayer>>("interactingWithPlayer").Value;
        if (list == null) return;

        if (playerEntity != null)
        {
            list.Remove(playerEntity);
        }
        else
        {
            list.Clear();
        }
    }
}
