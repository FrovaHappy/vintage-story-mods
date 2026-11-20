namespace SeraphQoL.Interaction;

/// <summary>
/// Descriptor puro de un modo de herramienta para planificar el menu radial.
/// </summary>
public sealed record ToolModeDescriptor(
    int NativeModeIndex,
    string Code,
    string Name,
    string Description,
    bool IsSelected,
    bool IsEnabled);

/// <summary>
/// Sector calculado dentro del anillo radial para un modo de herramienta.
/// </summary>
public sealed record RadialToolModeSector(
    int SectorIndex,
    int NativeModeIndex,
    string Code,
    string Name,
    string Description,
    double StartAngleRad,
    double EndAngleRad,
    double MidAngleRad,
    bool IsSelected,
    bool IsEnabled);

/// <summary>
/// Geometria configurable del anillo radial.
/// </summary>
public sealed record RadialGeometryConfig(
    double OuterRadius,
    double InnerRadius,
    double DeadZoneRadius);

/// <summary>
/// Pagina resuelta de sectores radiales.
/// </summary>
public sealed record RadialMenuPage(
    IReadOnlyList<RadialToolModeSector> Sectors,
    int PageIndex,
    int TotalPages);

/// <summary>
/// Planificador puro para distribucion angular, paginacion y hit-testing del menu radial de herramientas (tecla F).
/// </summary>
public static class ToolModeRadialPlanner
{
    public const int DefaultMaxSectorsPerPage = 10;
    private const double TopAngleOffset = -Math.PI * 0.5;
    private const double TwoPi = Math.PI * 2.0;

    public static RadialMenuPage BuildSectors(
        IReadOnlyList<ToolModeDescriptor> items,
        int pageIndex = 0,
        int maxSectorsPerPage = DefaultMaxSectorsPerPage)
    {
        if (items.Count == 0)
        {
            return new RadialMenuPage(Array.Empty<RadialToolModeSector>(), 0, 1);
        }

        int perPage = Math.Max(1, maxSectorsPerPage);
        int totalPages = (int)Math.Ceiling(items.Count / (double)perPage);
        int safePage = ((pageIndex % totalPages) + totalPages) % totalPages;

        int start = safePage * perPage;
        int count = Math.Min(perPage, items.Count - start);
        double step = TwoPi / count;
        double startBase = TopAngleOffset - (step * 0.5);

        var sectors = new List<RadialToolModeSector>(count);
        for (int i = 0; i < count; i++)
        {
            ToolModeDescriptor item = items[start + i];
            double sAngle = startBase + (i * step);
            double eAngle = sAngle + step;
            double midAngle = sAngle + (step * 0.5);

            sectors.Add(new RadialToolModeSector(
                i,
                item.NativeModeIndex,
                item.Code,
                item.Name,
                item.Description,
                sAngle,
                eAngle,
                midAngle,
                item.IsSelected,
                item.IsEnabled));
        }

        return new RadialMenuPage(sectors, safePage, totalPages);
    }

    public static int HitTestSector(
        double dx,
        double dy,
        IReadOnlyList<RadialToolModeSector> sectors,
        RadialGeometryConfig geometry)
    {
        if (sectors.Count == 0) return -1;

        double dist = Math.Sqrt((dx * dx) + (dy * dy));
        if (dist < geometry.DeadZoneRadius) return -1;

        double step = TwoPi / sectors.Count;
        double startBase = TopAngleOffset - (step * 0.5);
        double angle = Math.Atan2(dy, dx);

        double rel = (angle - startBase) % TwoPi;
        if (rel < 0) rel += TwoPi;

        int index = (int)Math.Floor(rel / step);
        return Math.Clamp(index, 0, sectors.Count - 1);
    }

    public static (double X, double Y) ComputeIconCenter(
        RadialToolModeSector sector,
        RadialGeometryConfig geometry)
    {
        double midRadius = (geometry.InnerRadius + geometry.OuterRadius) * 0.5;
        return (Math.Cos(sector.MidAngleRad) * midRadius, Math.Sin(sector.MidAngleRad) * midRadius);
    }
}

/// <summary>
/// Rastrea si la tecla de apertura del menu radial fue pulsada brevemente (tap) o mantenida (hold)
/// para evitar cierres accidentales al mover el raton tras abrir con un toque rapido.
/// </summary>
public sealed class RadialHoldReleaseTracker
{
    private readonly long _holdThresholdMs;
    private long _openedAtMs;
    private bool _tappedEarly;
    private bool _heldPastThreshold;

    public RadialHoldReleaseTracker(long holdThresholdMs = 180)
    {
        _holdThresholdMs = Math.Max(0, holdThresholdMs);
    }

    public void Reset(long openedAtMs)
    {
        _openedAtMs = openedAtMs;
        _tappedEarly = false;
        _heldPastThreshold = false;
    }

    public bool ShouldSelectOnRelease(bool selectOnReleaseEnabled, bool isHotkeyDown, long nowMs, int hoveredSectorIndex)
    {
        if (!selectOnReleaseEnabled) return false;

        long elapsed = Math.Max(0, nowMs - _openedAtMs);
        if (!isHotkeyDown)
        {
            if (elapsed < _holdThresholdMs || !_heldPastThreshold)
            {
                _tappedEarly = true;
                return false;
            }

            if (hoveredSectorIndex >= 0)
            {
                return true;
            }

            // Si solto la tecla dentro de la zona muerta central, pasa a modo click-para-elegir.
            _tappedEarly = true;
            _heldPastThreshold = false;
            return false;
        }

        if (!_tappedEarly && elapsed >= _holdThresholdMs)
        {
            _heldPastThreshold = true;
        }

        return false;
    }
}
