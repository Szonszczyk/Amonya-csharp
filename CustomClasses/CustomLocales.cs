using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using System.Text.RegularExpressions;

namespace Amonya.CustomClasses
{
    [Injectable(InjectionType.Singleton)]
    public class CustomLocales(
        ISptLogger<Amonya> logger,
        ModDatabaseLoader modDatabaseLoader
    )
    {
        private readonly Dictionary<string, Dictionary<string, string>> newLocale = [];

        private readonly Dictionary<string, string> temp = [];

        private LocaleBase? Locale { get; set; } = null;
        private HashSet<string> AllLangs { get; set; } = [];
        public void Initialize(DatabaseService databaseService)
        {
            Locale = databaseService.GetLocales();
            AllLangs = Locale.Global.Keys.ToHashSet();
        }

        public void RegisterTag(string key, string value)
        {
            temp[key] = value;
        }
        public bool KeyExistsInDefaultLang(string key)
        {
            if (TryGetLocaleText("en", key) is not null) { return true; } else return false;
        }

        public bool DefaultLangLocaleContainsText(string key, string value)
        {
            newLocale.TryGetValue("en", out var language);
            if (language != null)
            {
                language.TryGetValue(key, out var text);
                if (text != null)
                {
                    return text.Contains(value);
                }
            }
            return false;
        }

        public void AddLocale(string localeKey, string key, bool clean = false)
        {
            var langs = modDatabaseLoader.DbLocales;
            
            foreach (var (lang, _) in langs)
            {
                var text = TryGetLocaleText(lang, key);
                text ??= key;
                var tags = ExtractTags(text);
                do
                {
                    foreach (var tag in tags)
                    {
                        var tagText = TryGetLocaleText(lang, tag);
                        text ??= $"LOCALE:MISSING:{lang}:{tag}";
                        do
                        {
                            text = text.Replace($"<{tag}>", tagText);
                        } while (text.Contains($"<{tag}>"));
                    }
                    tags = ExtractTags(text);
                } while (tags.Count > 0);
                newLocale.TryGetValue(lang, out var language);
                if (language is null) newLocale[lang] = [];

                if (newLocale[lang].TryGetValue(localeKey, out _))
                    newLocale[lang][localeKey] = clean ? StripHtml(text) : text;
                else
                    newLocale[lang].Add(localeKey, clean ? StripHtml(text) : text);
                //logger.LogWithColor($"[{GetType().Namespace}] Registred {lang}-{localeKey}: {key} => {text}", LogTextColor.Yellow);
            }
        }

        public void AddToExistingLocale(string localeKey, string key, bool clean = false)
        {
            AddLocale(localeKey, $"<{localeKey}>{key}", clean);
        }

        public void RegisterLocales()
        {
            foreach (var langId in AllLangs)
            {
                if (Locale is not null && Locale.Global.TryGetValue(langId, out var lazyloadedValue))
                {
                    newLocale.TryGetValue(langId, out var newLocaleToAdd);
                    if (newLocaleToAdd is null)
                        newLocale.TryGetValue("en", out newLocaleToAdd);

                    if (newLocaleToAdd is null) continue;

                    lazyloadedValue.AddTransformer(lazyloadedLocaleData =>
                    {
                        if (lazyloadedLocaleData is null) return lazyloadedLocaleData;
                        foreach (var (key, value) in newLocaleToAdd)
                        {
                            lazyloadedLocaleData[key] = value;
                        }
                        return lazyloadedLocaleData;
                    });
                }
            }
        }

        private string? TryGetLocaleText(string lang, string key)
        {
            modDatabaseLoader.DbLocales.TryGetValue(lang, out var language);
            if (language == null) modDatabaseLoader.DbLocales.TryGetValue("en", out language);
            if (language == null) return null;

            language.TryGetValue(key, out var text);
            if (text is null)
            {
                newLocale.TryGetValue(lang, out var newLocaleToGet);
                if (newLocaleToGet is not null)
                    newLocale.TryGetValue("en", out newLocaleToGet);
                if (newLocaleToGet == null) return null;
                newLocaleToGet.TryGetValue(key, out text);
                if (text is null)
                {
                    temp.TryGetValue(key, out text);
                }
                return text;
            }
            return text;
        }
        private static List<string> ExtractTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<string>();

            var matches = Regex.Matches(input, @"<([a-zA-Z0-9_:\s]+)>");

            return matches
                .Select(m => m.Groups[1].Value.Trim())
                .Where(tag => tag.Length > 2)
                .Distinct()
                .ToList();
        }
        private static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty);
        }

    }
}
