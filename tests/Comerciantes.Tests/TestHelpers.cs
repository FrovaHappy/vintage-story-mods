using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Comerciantes.Tests;

internal sealed class TestItem : Item
{
    private readonly int _maxDurability;
    private readonly int _remainingDurability;

    public TestItem(
        string code,
        int maxStackSize = 64,
        int maxDurability = 0,
        int remainingDurability = 0,
        EnumTool? tool = null,
        Dictionary<string, string>? variants = null)
    {
        Code = new AssetLocation(code);
        MaxStackSize = maxStackSize;
        _maxDurability = maxDurability;
        _remainingDurability = remainingDurability > 0 || maxDurability == 0 ? remainingDurability : 0;
        Tool = tool;
        if (variants != null)
        {
            Variant = new Vintagestory.API.Util.RelaxedReadOnlyDictionary<string, string>(variants);
        }
    }

    public override int GetMaxDurability(ItemStack itemstack) => _maxDurability;

    public override int GetRemainingDurability(ItemStack itemstack) => _remainingDurability;

    public override string GetHeldItemName(ItemStack itemStack) => Code?.ToString() ?? string.Empty;
}

internal sealed class TestBlock : Block
{
    public TestBlock(string code, int maxStackSize = 64)
    {
        Code = new AssetLocation(code);
        MaxStackSize = maxStackSize;
    }

    public override string GetHeldItemName(ItemStack itemStack) => Code?.ToString() ?? string.Empty;
}

internal static class TestRepoPaths
{
    public static string RepoRoot
    {
        get
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "VintageStoryMods.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate VintageStoryMods.sln from test output directory.");
        }
    }

    public static string ComerciantesAssetsDir =>
        Path.Combine(RepoRoot, "mods", "comerciantes", "assets", "tradersoverhaul");

    public static ItemValuesFile LoadItemValuesFile()
    {
        string path = Path.Combine(ComerciantesAssetsDir, "config", "item_values.json");
        return JsonConvert.DeserializeObject<ItemValuesFile>(File.ReadAllText(path))!;
    }

    public static TraderAffinitiesFile LoadTraderAffinitiesFile()
    {
        string path = Path.Combine(ComerciantesAssetsDir, "config", "trader_affinities.json");
        return JsonConvert.DeserializeObject<TraderAffinitiesFile>(File.ReadAllText(path))!;
    }
}
