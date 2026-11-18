using Xunit;

namespace Comerciantes.Tests;

public sealed class TraderAffinityMatrixTests
{
    [Theory]
    [InlineData("game:axe-felling-copper", "field_gear")]
    [InlineData("game:chandelier-copper", "furniture_glass")]
    [InlineData("game:ingot-copper", "base_metals")]
    [InlineData("game:ingot-gold", "luxuries_relics")]
    [InlineData("game:pickaxe-iron", "weapons_mining")]
    [InlineData("game:hammer-iron", "crafting_tools")]
    [InlineData("game:scythe-tinbronze", "farming")]
    [InlineData("game:bread-rye", "food")]
    [InlineData("game:leather-normal-plain", "pelts_textiles")]
    [InlineData("game:clothes-upperbody-noble-tunic", "noble_clothing")]
    [InlineData("game:clothes-upperbody-peasants-shirt", "work_clothing")]
    [InlineData("game:gem-emerald-rough", "luxuries_relics")]
    [InlineData("game:charcoal", "artisan_raw")]
    [InlineData("game:planks-oak", "building_materials")]
    public void ClassifyItemCategory_MapsExpectedCategories(string itemCode, string expectedCategory)
    {
        var item = new TestItem(itemCode);
        Assert.Equal(expectedCategory, TraderAffinityMatrix.ClassifyItemCategory(item));
    }

    [Fact]
    public void Load_RealAsset_ResolvesArchetypeMultipliersAndFallback()
    {
        var matrix = new TraderAffinityMatrix();
        TraderAffinitiesFile file = TestRepoPaths.LoadTraderAffinitiesFile();
        matrix.Load(file);

        var ironPick = new TestItem("game:pickaxe-iron");
        var goldIngot = new TestItem("game:ingot-gold");

        Assert.Equal(1.35, matrix.GetAffinityMultiplier("treasurehunter", ironPick), 4);
        Assert.Equal(0.50, matrix.GetAffinityMultiplier("agriculture", ironPick), 4);
        Assert.Equal(1.35, matrix.GetAffinityMultiplier("luxuries", goldIngot), 4);
        Assert.Equal(file.DefaultMultiplier, matrix.GetAffinityMultiplier("nonexistent_archetype", ironPick), 4);
    }
}
