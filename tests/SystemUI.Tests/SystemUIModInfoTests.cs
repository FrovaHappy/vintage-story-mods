using System.Text.Json;
using Vintagestory.API.Common;
using Xunit;

namespace SystemUI.Tests;

public sealed class SystemUIModInfoTests
{
    private static string GetSystemUiModDir()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VintageStoryMods.sln")))
            {
                return Path.Combine(dir.FullName, "mods", "system-ui");
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate VintageStoryMods.sln from test output directory.");
    }

    [Fact]
    public void ModInfo_DeclaresUniversalSide_ForServerAndClientAutoInstall()
    {
        string modInfoPath = Path.Combine(GetSystemUiModDir(), "modinfo.json");
        Assert.True(File.Exists(modInfoPath), $"Missing modinfo.json at {modInfoPath}");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(modInfoPath));
        JsonElement root = doc.RootElement;

        Assert.Equal("seraphui", root.GetProperty("modid").GetString());
        Assert.Equal("Universal", root.GetProperty("side").GetString());
        Assert.Equal("modicon.png", root.GetProperty("iconPath").GetString());
    }

    [Fact]
    public void SystemUIModSystem_ShouldLoad_OnlyOnClientSide()
    {
        var modSystem = new SystemUIModSystem();

        Assert.True(modSystem.ShouldLoad(EnumAppSide.Client));
        Assert.False(modSystem.ShouldLoad(EnumAppSide.Server));
        Assert.False(modSystem.ShouldLoad(EnumAppSide.Universal));
    }
}
