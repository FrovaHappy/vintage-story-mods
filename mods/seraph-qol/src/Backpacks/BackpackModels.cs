namespace SeraphQoL.Backpacks;

/// <summary>
/// Categorías de clasificación para el escalado de capacidad de mochilas y bolsas.
/// </summary>
public enum BackpackCategory
{
    Excluded,
    StandardBackpacks,
    SpecializedBags,
    Saddlebags,
    ModdedBackpacks
}

/// <summary>
/// Descriptor puro e independiente del motor para clasificar y escalar la capacidad de una mochila o bolsa.
/// </summary>
public sealed record BackpackCollectibleDescriptor(
    string Code,
    string ClassName,
    int BaseQuantitySlots,
    bool HasRestrictedStorageFlags,
    bool HasStorageTags);
