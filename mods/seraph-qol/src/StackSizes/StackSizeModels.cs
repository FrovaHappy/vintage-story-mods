namespace SeraphQoL.StackSizes;

/// <summary>
/// Categorías de clasificación para cálculo de tamaño máximo de stack.
/// </summary>
public enum StackCategory
{
    ProtectedSingle,
    BuildingBlocks,
    ResourcesAndMetals,
    FoodAndAgriculture,
    Containers,
    FurnitureAndUtility,
    Clothing,
    MiscOther
}

/// <summary>
/// Descriptor puro e independiente del motor para clasificar y escalar el tamaño de stack de un objeto o bloque.
/// </summary>
public sealed record StackCollectibleDescriptor(
    string Code,
    string ClassName,
    int VanillaMaxStackSize,
    bool IsBlock,
    int Durability,
    bool HasTool,
    bool HasNutritionProps,
    bool IsLiquid,
    bool IsBackpackStorage,
    bool IsWearable,
    IReadOnlyList<string> CreativeTabs);
