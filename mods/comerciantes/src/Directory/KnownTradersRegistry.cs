using Newtonsoft.Json;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Comerciantes;

public sealed class KnownTraderRecord
{
    public long EntityId { get; set; }
    public string TraderName { get; set; } = string.Empty;
    public string Archetype { get; set; } = string.Empty;
    public BlockPos Position { get; set; } = new(0, 0, 0);
    public double LastSeenTotalDays { get; set; }
    public double NextRefreshTotalDays { get; set; }
    public int TraderMoney { get; set; }
    public double TraderRewardBonusPct { get; set; }
    public List<WantedItemEntry> WantedItems { get; set; } = [];
    public List<StockOfferEntry> SellingOffers { get; set; } = [];
}

/// <summary>
/// Persistencia en servidor de comerciantes descubiertos por jugador para el menu global.
/// </summary>
public sealed class KnownTradersRegistry
{
    private const string SaveKey = "tradersoverhaul_known_traders";

    private Dictionary<string, Dictionary<long, KnownTraderRecord>> _byPlayer = new();

    public void Load(ICoreServerAPI sapi)
    {
        byte[]? raw = sapi.WorldManager.SaveGame.GetData(SaveKey);
        if (raw == null || raw.Length == 0)
        {
            _byPlayer = new();
            return;
        }

        ImportStateJson(System.Text.Encoding.UTF8.GetString(raw));
    }

    public void Save(ICoreServerAPI sapi)
    {
        string json = ExportStateJson();
        sapi.WorldManager.SaveGame.StoreData(SaveKey, System.Text.Encoding.UTF8.GetBytes(json));
    }

    public string ExportStateJson() => JsonConvert.SerializeObject(_byPlayer);

    public void ImportStateJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            _byPlayer = new();
            return;
        }

        _byPlayer = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<long, KnownTraderRecord>>>(json) ?? new();
    }

    public void RecordVisit(string playerUid, KnownTraderRecord snapshot)
    {
        if (!_byPlayer.TryGetValue(playerUid, out Dictionary<long, KnownTraderRecord>? map))
        {
            map = new Dictionary<long, KnownTraderRecord>();
            _byPlayer[playerUid] = map;
        }
        map[snapshot.EntityId] = snapshot;
    }

    public IReadOnlyCollection<KnownTraderRecord> GetKnownTraders(string playerUid)
    {
        if (_byPlayer.TryGetValue(playerUid, out Dictionary<long, KnownTraderRecord>? map))
        {
            return map.Values;
        }
        return Array.Empty<KnownTraderRecord>();
    }

    public KnownTraderRecord? GetKnownTrader(string playerUid, long entityId)
    {
        if (_byPlayer.TryGetValue(playerUid, out Dictionary<long, KnownTraderRecord>? map)
            && map.TryGetValue(entityId, out KnownTraderRecord? record))
        {
            return record;
        }
        return null;
    }

    public static bool HasUpdatedList(double lastSeenTotalDays, double nextRefreshTotalDays, double currentTotalDays, bool isLiveSyncMode)
    {
        if (isLiveSyncMode) return false;
        return nextRefreshTotalDays < lastSeenTotalDays
            || (nextRefreshTotalDays - currentTotalDays) <= 0.05;
    }

    public static List<KnownTraderDto> FilterAndSortByLastVisited(IEnumerable<KnownTraderDto> traders, string? archetypeFilter)
    {
        IEnumerable<KnownTraderDto> query = traders;
        if (!string.IsNullOrWhiteSpace(archetypeFilter))
        {
            query = query.Where(t => string.Equals(t.Archetype, archetypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderByDescending(t => t.LastSeenTotalDays)
            .ThenBy(t => t.TraderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
