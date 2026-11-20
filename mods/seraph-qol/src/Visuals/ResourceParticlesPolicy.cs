namespace SeraphQoL.Visuals;

public enum ResourceCategory
{
    None,
    Resin,
    TerraPreta,
    MeteoricIron
}

public sealed record ResourceParticlesOptions(
    bool Resin,
    bool TerraPreta,
    bool MeteoricIron);

/// <summary>
/// Clasifica bloques clave de exploracion (resina, Terra Preta, hierro meteorico) para particulas nativas.
/// </summary>
public static class ResourceBlockClassifier
{
    public static ResourceCategory Classify(string? blockCode, ResourceParticlesOptions options)
    {
        if (string.IsNullOrWhiteSpace(blockCode)) return ResourceCategory.None;

        string path = blockCode;
        int colon = path.IndexOf(':');
        if (colon >= 0) path = path[(colon + 1)..];

        if (options.Resin && path.StartsWith("log-resin-", StringComparison.OrdinalIgnoreCase))
        {
            return ResourceCategory.Resin;
        }

        if (options.TerraPreta && path.StartsWith("soil-compost-", StringComparison.OrdinalIgnoreCase))
        {
            return ResourceCategory.TerraPreta;
        }

        if (options.MeteoricIron
            && (path.StartsWith("meteorite-", StringComparison.OrdinalIgnoreCase)
                || path.Contains("meteoriciron", StringComparison.OrdinalIgnoreCase)))
        {
            return ResourceCategory.MeteoricIron;
        }

        return ResourceCategory.None;
    }
}
