namespace SeraphQoL.Exploration;

/// <summary>
/// Regla pura para resaltar slots con el mismo item que el slot bajo el cursor o mano del raton.
/// </summary>
public static class MatchingItemHighlightPolicy
{
    public static bool ShouldHighlightSlot(string? hoveredItemCode, string? slotItemCode, bool featureEnabled)
    {
        if (!featureEnabled) return false;
        if (string.IsNullOrWhiteSpace(hoveredItemCode) || string.IsNullOrWhiteSpace(slotItemCode)) return false;
        return string.Equals(hoveredItemCode, slotItemCode, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Candidato de item en el suelo (EntityItem) detectado alrededor del jugador.
/// </summary>
public sealed record GroundItemCandidate(
    long EntityId,
    string ItemCode,
    string DisplayName,
    int StackSize,
    double Distance,
    double WorldX,
    double WorldY,
    double WorldZ);

/// <summary>
/// Indicador proyectable para un item en el suelo con etiqueta y opacidad calculadas.
/// </summary>
public sealed record GroundItemIndicator(
    long EntityId,
    string Label,
    double WorldX,
    double WorldY,
    double WorldZ,
    float Alpha,
    bool ShowLabel);

/// <summary>
/// Planificador puro para filtrado, formateo y atenuacion por distancia de items en el suelo.
/// </summary>
public static class GroundItemHighlightPlanner
{
    public static IReadOnlyList<GroundItemIndicator> BuildIndicators(
        IEnumerable<GroundItemCandidate> candidates,
        int maxRadius,
        bool showLabels)
    {
        if (maxRadius <= 0) return Array.Empty<GroundItemIndicator>();

        var filtered = new List<(GroundItemCandidate Candidate, GroundItemIndicator Indicator)>();
        foreach (GroundItemCandidate candidate in candidates)
        {
            if (candidate.Distance < 0 || candidate.Distance > maxRadius) continue;

            float ratio = (float)(candidate.Distance / maxRadius);
            float alpha = Math.Clamp(1.0f - (0.58f * ratio), 0.40f, 1.0f);
            string baseName = string.IsNullOrWhiteSpace(candidate.DisplayName) ? candidate.ItemCode : candidate.DisplayName;
            string label = candidate.StackSize > 1 ? $"{baseName} x{candidate.StackSize}" : baseName;

            var indicator = new GroundItemIndicator(
                candidate.EntityId,
                label,
                candidate.WorldX,
                candidate.WorldY,
                candidate.WorldZ,
                alpha,
                showLabels);
            filtered.Add((candidate, indicator));
        }

        // Orden pintor: los mas lejanos primero para que los cercanos queden arriba.
        filtered.Sort((a, b) => b.Candidate.Distance.CompareTo(a.Candidate.Distance));
        return filtered.Select(x => x.Indicator).ToArray();
    }
}

/// <summary>
/// Maquina de estados pura para mostrar items en el suelo al agacharse con retardo configurable al ponerse de pie.
/// </summary>
public sealed class GroundHighlightVisibilityState
{
    private bool _wasSneaking;
    private long _expireAtMs;

    public bool IsVisible(bool featureEnabled, bool alwaysHighlight, bool isSneaking, long nowMs, double standUpDelaySeconds)
    {
        if (!featureEnabled)
        {
            Reset();
            return false;
        }

        if (alwaysHighlight)
        {
            return true;
        }

        if (isSneaking)
        {
            _wasSneaking = true;
            _expireAtMs = 0;
            return true;
        }

        if (_wasSneaking)
        {
            _wasSneaking = false;
            double delaySec = double.IsNaN(standUpDelaySeconds) ? 0.0 : Math.Max(0.0, standUpDelaySeconds);
            long delayMs = (long)Math.Round(delaySec * 1000.0);
            _expireAtMs = delayMs > 0 ? nowMs + delayMs : 0;
        }

        if (_expireAtMs > 0 && nowMs < _expireAtMs)
        {
            return true;
        }

        _expireAtMs = 0;
        return false;
    }

    public void Reset()
    {
        _wasSneaking = false;
        _expireAtMs = 0;
    }
}

public sealed record TrackedProjectileRecord(
    long EntityId,
    string OwnerPlayerUid,
    string WaypointKey,
    string ItemName,
    bool HasLanded,
    long CreatedAtMs = 0,
    bool IsChunkUnloaded = false);

public static class ProjectileWaypointFormatter
{
    public static string FormatWaypointKey(long entityId) => $"seraphqol-proj-{entityId}";

    public static string FormatTitle(string? itemName) =>
        string.IsNullOrWhiteSpace(itemName) ? "Projectile" : itemName.Trim();
}

/// <summary>
/// Registro puro del ciclo de vida de proyectiles disparados por jugadores para generar/limpiar waypoints.
/// </summary>
public sealed class ProjectileTrackerState
{
    private readonly Dictionary<long, TrackedProjectileRecord> _records = new();

    public int Count => _records.Count;

    public void Register(long entityId, string ownerPlayerUid, string? itemName, long createdAtMs = 0)
    {
        if (entityId == 0 || string.IsNullOrWhiteSpace(ownerPlayerUid)) return;
        _records[entityId] = new TrackedProjectileRecord(
            entityId,
            ownerPlayerUid,
            ProjectileWaypointFormatter.FormatWaypointKey(entityId),
            ProjectileWaypointFormatter.FormatTitle(itemName),
            HasLanded: false,
            CreatedAtMs: createdAtMs,
            IsChunkUnloaded: false);
    }

    public bool TryMarkLanded(long entityId, out TrackedProjectileRecord? record)
    {
        if (!_records.TryGetValue(entityId, out TrackedProjectileRecord? existing) || existing.HasLanded)
        {
            record = null;
            return false;
        }

        record = existing with { HasLanded = true, IsChunkUnloaded = false };
        _records[entityId] = record;
        return true;
    }

    public bool TryMarkChunkUnloaded(long entityId)
    {
        if (!_records.TryGetValue(entityId, out TrackedProjectileRecord? existing)) return false;
        _records[entityId] = existing with { IsChunkUnloaded = true };
        return true;
    }

    public bool TryMarkChunkLoaded(long entityId)
    {
        if (!_records.TryGetValue(entityId, out TrackedProjectileRecord? existing)) return false;
        _records[entityId] = existing with { IsChunkUnloaded = false };
        return true;
    }

    public bool TryGet(long entityId, out TrackedProjectileRecord? record) =>
        _records.TryGetValue(entityId, out record);

    public bool TryRemove(long entityId, out TrackedProjectileRecord? removed)
    {
        return _records.Remove(entityId, out removed);
    }

    public IReadOnlyList<TrackedProjectileRecord> CollectExpired(long nowMs, int timeoutSeconds)
    {
        if (timeoutSeconds <= 0 || _records.Count == 0) return Array.Empty<TrackedProjectileRecord>();

        long timeoutMs = timeoutSeconds * 1000L;
        var expired = new List<TrackedProjectileRecord>();
        foreach (TrackedProjectileRecord rec in _records.Values.ToArray())
        {
            if (nowMs - rec.CreatedAtMs >= timeoutMs)
            {
                _records.Remove(rec.EntityId);
                expired.Add(rec);
            }
        }
        return expired;
    }

    public IReadOnlyList<long> GetTrackedEntityIds() => _records.Keys.ToArray();

    public void Clear() => _records.Clear();
}
