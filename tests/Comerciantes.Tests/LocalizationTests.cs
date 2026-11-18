using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Comerciantes.Tests;

public sealed class LocalizationTests
{
    private static readonly string[] SupportedLocales = ["en", "es-es", "es-419", "ru"];

    private static Dictionary<string, Dictionary<string, string>> LoadAllLangFiles()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string langDir = Path.Combine(TestRepoPaths.ComerciantesAssetsDir, "lang");

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
        string cfgPath = Path.Combine(TestRepoPaths.ComerciantesAssetsDir, "config", "configlib-patches.json");
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
                    Assert.StartsWith($"{ComerciantesLang.Domain}:", raw);
                    referencedKeys.Add(raw[(ComerciantesLang.Domain.Length + 1)..]);
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
    public void ArchetypesAndEconomyModes_HaveTranslationsInAllLanguages()
    {
        Dictionary<string, Dictionary<string, string>> langs = LoadAllLangFiles();
        TraderAffinitiesFile affinities = TestRepoPaths.LoadTraderAffinitiesFile();

        foreach (string locale in SupportedLocales)
        {
            foreach (string archetype in affinities.Archetypes.Keys)
            {
                string key = $"archetype-{archetype.ToLowerInvariant()}";
                Assert.True(langs[locale].ContainsKey(key), $"Missing '{key}' in {locale}.json");
            }

            foreach (EconomyMode mode in Enum.GetValues<EconomyMode>())
            {
                string key = $"mode-{mode.ToString().ToLowerInvariant()}";
                Assert.True(langs[locale].ContainsKey(key), $"Missing '{key}' in {locale}.json");
            }
        }
    }

    [Fact]
    public void ComerciantesLang_FallbacksHandleUnknownValuesGracefully()
    {
        Assert.Equal("custom_archetype", ComerciantesLang.GetArchetypeName("custom_archetype"));
        Assert.Equal("custom_mode", ComerciantesLang.GetEconomyModeName("custom_mode"));
    }
}
