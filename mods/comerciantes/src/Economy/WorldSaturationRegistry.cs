using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Comerciantes;

public sealed class TraderSaturationState
{
    public BlockPos LastPos { get; set; } = new(0, 0, 0);
    public double LastUpdatedTotalDays { get; set; }
    public Dictionary<string, double> VolumesByGroup { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class WorldSaturationSnapshot
{
    public Dictionary<long, TraderSaturationState> Traders { get; set; } = new();
    public Dictionary<string, double> GlobalVolumes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double GlobalLastUpdatedTotalDays { get; set; }
}

/// <summary>
/// Registro persistente en servidor del volumen de ventas (GEV) para anti-farmeo,
/// con propagacion regional y recuperacion diaria.
/// </summary>
public sealed class WorldSaturationRegistry
{
    private const string SaveKey = "tradersoverhaul_world_saturation";

    private Dictionary<long, TraderSaturationState> _traders = new();
    private Dictionary<string, double> _globalVolumes = new(StringComparer.OrdinalIgnoreCase);
    private double _globalLastUpdatedTotalDays;

    public void Load(ICoreServerAPI sapi)
    {
        byte[]? raw = sapi.WorldManager.SaveGame.GetData(SaveKey);
        if (raw == null || raw.Length == 0)
        {
            _traders = new Dictionary<long, TraderSaturationState>();
            _globalVolumes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            _globalLastUpdatedTotalDays = 0;
            return;
        }

        ImportStateJson(System.Text.Encoding.UTF8.GetString(raw));
    }

    public void Save(ICoreServerAPI sapi)
    {
        string json = ExportStateJson();
        sapi.WorldManager.SaveGame.StoreData(SaveKey, System.Text.Encoding.UTF8.GetBytes(json));
    }

    public string ExportStateJson()
    {
        var snapshot = new WorldSaturationSnapshot
        {
            Traders = _traders,
            GlobalVolumes = _globalVolumes,
            GlobalLastUpdatedTotalDays = _globalLastUpdatedTotalDays
        };
        return JsonConvert.SerializeObject(snapshot);
    }

    public void ImportStateJson(string? json)
    {
        _traders = new Dictionary<long, TraderSaturationState>();
        _globalVolumes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        _globalLastUpdatedTotalDays = 0;

        if (string.IsNullOrWhiteSpace(json)) return;

        JObject obj = JObject.Parse(json);
        if (obj.ContainsKey(nameof(WorldSaturationSnapshot.Traders)) || obj.ContainsKey(nameof(WorldSaturationSnapshot.GlobalVolumes)))
        {
            WorldSaturationSnapshot? snapshot = obj.ToObject<WorldSaturationSnapshot>();
            if (snapshot != null)
            {
                _traders = snapshot.Traders ?? new();
                _globalVolumes = new Dictionary<string, double>(snapshot.GlobalVolumes ?? new(), StringComparer.OrdinalIgnoreCase);
                _globalLastUpdatedTotalDays = snapshot.GlobalLastUpdatedTotalDays;
            }
            return;
        }

        _traders = obj.ToObject<Dictionary<long, TraderSaturationState>>() ?? new();
    }

    public double GetCurrentVolume(long traderId, BlockPos pos, double currentTotalDays, string group, ComerciantesConfig config)
    {
        if (config.SaturationScope == SaturationScope.Global)
        {
            ApplyDailyRecovery(_globalVolumes, ref _globalLastUpdatedTotalDays, currentTotalDays, config);
            return _globalVolumes.TryGetValue(group, out double gVol) ? gVol : 0.0;
        }

        TraderSaturationState state = GetOrCreateState(traderId, pos, currentTotalDays);
        ApplyDailyRecovery(state, currentTotalDays, config);
        return state.VolumesByGroup.TryGetValue(group, out double vol) ? vol : 0.0;
    }

    public double EvaluateMultiplier(double volume, ComerciantesConfig config)
    {
        if (volume <= 0.0) return 1.0;
        double ratio = volume / Math.Max(0.01, config.SaturationS50);
        double factor = 1.0 / (1.0 + Math.Pow(ratio, config.SaturationCurvePower));
        return Math.Max(config.SaturationMinFloor, factor);
    }

    public void RecordSale(long traderId, BlockPos pos, double currentTotalDays, string group, double deltaGev, ComerciantesConfig config)
    {
        if (deltaGev <= 0.0) return;

        if (config.SaturationScope == SaturationScope.Global)
        {
            if (_globalVolumes.Count == 0 && _globalLastUpdatedTotalDays <= 0.0)
            {
                _globalLastUpdatedTotalDays = currentTotalDays;
            }
            else
            {
                ApplyDailyRecovery(_globalVolumes, ref _globalLastUpdatedTotalDays, currentTotalDays, config);
            }
            _globalVolumes[group] = (_globalVolumes.TryGetValue(group, out double cur) ? cur : 0.0) + deltaGev;
            return;
        }

        TraderSaturationState origin = GetOrCreateState(traderId, pos, currentTotalDays);
        ApplyDailyRecovery(origin, currentTotalDays, config);
        origin.LastPos = pos.Copy();
        origin.VolumesByGroup[group] = (origin.VolumesByGroup.TryGetValue(group, out double localCur) ? localCur : 0.0) + deltaGev;

        if (config.SaturationScope != SaturationScope.NearbyPartial || config.SaturationSpilloverFactor <= 0.0)
        {
            return;
        }

        double maxRadius = config.SaturationSpilloverRadius;
        foreach ((long otherId, TraderSaturationState other) in _traders)
        {
            if (otherId == traderId) continue;

            double distance = pos.DistanceTo(other.LastPos);
            if (distance > maxRadius) continue;

            ApplyDailyRecovery(other, currentTotalDays, config);
            double weight = config.SaturationSpilloverFactor * (1.0 - (distance / maxRadius));
            double propagated = deltaGev * Math.Max(0.0, weight);
            if (propagated <= 0.0) continue;

            other.VolumesByGroup[group] = (other.VolumesByGroup.TryGetValue(group, out double oCur) ? oCur : 0.0) + propagated;
        }
    }

    private TraderSaturationState GetOrCreateState(long traderId, BlockPos pos, double currentTotalDays)
    {
        if (!_traders.TryGetValue(traderId, out TraderSaturationState? state))
        {
            state = new TraderSaturationState
            {
                LastPos = pos.Copy(),
                LastUpdatedTotalDays = currentTotalDays
            };
            _traders[traderId] = state;
        }
        return state;
    }

    private static void ApplyDailyRecovery(TraderSaturationState state, double currentTotalDays, ComerciantesConfig config)
    {
        double lastUpdated = state.LastUpdatedTotalDays;
        ApplyDailyRecovery(state.VolumesByGroup, ref lastUpdated, currentTotalDays, config);
        state.LastUpdatedTotalDays = lastUpdated;
    }

    private static void ApplyDailyRecovery(
        Dictionary<string, double> volumes,
        ref double lastUpdatedTotalDays,
        double currentTotalDays,
        ComerciantesConfig config)
    {
        double elapsedDays = currentTotalDays - lastUpdatedTotalDays;
        if (elapsedDays <= 0.001) return;

        double recovery = elapsedDays * config.SaturationRecoveryPerDay;
        foreach (string key in volumes.Keys.ToList())
        {
            double next = Math.Max(0.0, volumes[key] - recovery);
            if (next <= 0.0001)
            {
                volumes.Remove(key);
            }
            else
            {
                volumes[key] = next;
            }
        }
        lastUpdatedTotalDays = currentTotalDays;
    }
}
