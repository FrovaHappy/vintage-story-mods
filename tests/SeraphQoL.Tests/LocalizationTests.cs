using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SeraphQoL.Config;
using Xunit;

namespace SeraphQoL.Tests;

public sealed class LocalizationTests
{
    private static readonly string[] SupportedLocales = ["en", "es-es", "es-419", "ru"];

    private static string GetAssetsDir()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VintageStoryMods.sln")))
            {
                return Path.Combine(dir.FullName, "mods", "seraph-qol", "assets", "seraphqol");
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate VintageStoryMods.sln from test output directory.");
    }

    private static Dictionary<string, Dictionary<string, string>> LoadAllLangFiles()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string langDir = Path.Combine(GetAssetsDir(), "lang");

        foreach (string locale in SupportedLocales)
        {
            string filePath = Path.Combine(langDir, $"{locale}.json");
            Assert.True(File.Exists(filePath), $"Missing language file: {filePath}");
            Dictionary<string, string>? parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));
            Assert.NotNull(parsed);
            result[locale] = parsed!;
        }

        return result;
    }

    [Fact]
    public void AllLanguageFiles_HaveMatchingKeysNonEmptyValuesAndSamePlaceholders()
    {
        Dictionary<string, Dictionary<string, string>> langs = LoadAllLangFiles();
        Dictionary<string, string> baseline = langs["en"];
        var placeholderRegex = new Regex(@"\{\d+\}", RegexOptions.Compiled);

        foreach ((string locale, Dictionary<string, string> dict) in langs)
        {
            Assert.Equal(baseline.Keys.OrderBy(k => k), dict.Keys.OrderBy(k => k));

            foreach ((string key, string baseVal) in baseline)
            {
                string localizedVal = dict[key];
                Assert.False(string.IsNullOrWhiteSpace(localizedVal), $"Empty value for '{key}' in {locale}.json");

                string[] expectedPlaceholders = placeholderRegex.Matches(baseVal).Select(m => m.Value).Distinct().OrderBy(x => x).ToArray();
                string[] actualPlaceholders = placeholderRegex.Matches(localizedVal).Select(m => m.Value).Distinct().OrderBy(x => x).ToArray();
                Assert.Equal(expectedPlaceholders, actualPlaceholders);
            }
        }
    }

    [Fact]
    public void ConfigLibPatches_AllTranslationKeysExistInAllLanguages()
    {
        Dictionary<string, Dictionary<string, string>> langs = LoadAllLangFiles();
        string cfgPath = Path.Combine(GetAssetsDir(), "config", "configlib-patches.json");
        JObject root = JObject.Parse(File.ReadAllText(cfgPath));
        JArray settings = (JArray)root["settings"]!;

        var referencedKeys = new List<string>();
        foreach (JObject entry in settings.OfType<JObject>())
        {
            foreach (string field in new[] { "title", "ingui", "comment" })
            {
                string? raw = entry[field]?.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    Assert.StartsWith($"{SeraphQoLLang.Domain}:", raw);
                    referencedKeys.Add(raw[(SeraphQoLLang.Domain.Length + 1)..]);
                }
            }
        }

        Assert.NotEmpty(referencedKeys);
        foreach (string locale in SupportedLocales)
        {
            foreach (string key in referencedKeys)
            {
                Assert.True(langs[locale].ContainsKey(key), $"Missing ConfigLib key '{key}' in {locale}.json");
            }
        }
    }

    [Fact]
    public void SeraphQoLLang_FallbackHandlesUnloadedRuntime()
    {
        Assert.Equal("custom-missing-key", SeraphQoLLang.Get("custom-missing-key"));
    }
}
