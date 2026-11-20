using SeraphQoL.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace SeraphQoL.Inventory;

/// <summary>
/// Gestiona los items favoritos del jugador persistidos en WatchedAttributes y sincronizados al servidor.
/// </summary>
public sealed class FavoritesManager
{
    public const string FavoritesAttributeKey = "seraphQolFavorites";

    public bool IsFavoriteModeActive { get; set; }

    public static void EnsureInitialized(IPlayer player)
    {
        if (player.Entity?.WatchedAttributes == null) return;
        if (player.Entity.WatchedAttributes.GetTreeAttribute(FavoritesAttributeKey) == null)
        {
            player.Entity.WatchedAttributes.SetAttribute(FavoritesAttributeKey, new TreeAttribute());
            player.Entity.WatchedAttributes.MarkPathDirty(FavoritesAttributeKey);
        }
    }

    public static bool IsFavorite(IPlayer? player, ItemStack? stack)
    {
        if (player?.Entity?.WatchedAttributes == null || stack?.Collectible?.Code == null)
        {
            return false;
        }

        ITreeAttribute? tree = player.Entity.WatchedAttributes.GetTreeAttribute(FavoritesAttributeKey);
        return tree != null && tree.GetBool(GetItemKey(stack));
    }

    public static void SetFavoriteServer(IPlayer player, string itemCode, bool isFavorite)
    {
        if (player.Entity?.WatchedAttributes == null || string.IsNullOrWhiteSpace(itemCode)) return;

        EnsureInitialized(player);
        ITreeAttribute? tree = player.Entity.WatchedAttributes.GetTreeAttribute(FavoritesAttributeKey);
        if (tree == null) return;

        if (isFavorite)
        {
            tree.SetBool(itemCode, true);
        }
        else
        {
            tree.RemoveAttribute(itemCode);
        }

        player.Entity.WatchedAttributes.MarkPathDirty(FavoritesAttributeKey);
    }

    public void ToggleFavoriteClient(ICoreClientAPI capi, ItemStack stack)
    {
        if (stack.Collectible?.Code == null) return;
        IPlayer? player = capi.World?.Player;
        if (player?.Entity?.WatchedAttributes == null) return;

        EnsureInitialized(player);
        string key = GetItemKey(stack);
        bool nextState = !IsFavorite(player, stack);

        SetFavoriteServer(player, key, nextState);
        capi.Network.GetChannel("seraphqol")?.SendPacket(new UpdateFavoritePacket
        {
            ItemCode = key,
            IsFavorite = nextState
        });
    }

    public static string GetItemKey(ItemStack stack) => stack.Collectible.Code.ToString();
}
