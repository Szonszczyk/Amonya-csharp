using Amonya.Interfaces;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using System.Reflection;
using Path = System.IO.Path;

namespace Amonya.Loaders
{
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2122)]
    public class ModDatabaseLoader
    {
        private readonly string modFolder;
        private readonly ISptLogger<Amonya> _logger;
        private readonly ModHelper _modHelper;

        public TraderBase TraderBase { get; private set; }

        public Dictionary<string, VariantConfiguration> DbVariants { get; private set; }
        public Dictionary<string, VariantConfiguration> DbItems { get; private set; }
        public Dictionary<string, QuestData> DbQuests { get; private set; }
        public Dictionary<string, CaliberInfo> DbCalibers { get; private set; }
        public Dictionary<string, Dictionary<string, string>> DbLocales { get; private set; }
        public SettingsData DbAddSettings { get; private set; }
        public ModDatabaseLoader(ISptLogger<Amonya> logger, ModHelper modHelper) 
        {
            modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            _logger = logger;
            _modHelper = modHelper;

            TraderBase = LoadTraderBase();
            DbVariants = LoadDbVariants(Path.Combine(modFolder, "db", "01_Variants"));
            DbItems = LoadDbVariants(Path.Combine(modFolder, "db", "02_Items"));
            DbQuests = LoadDbQuests(Path.Combine(modFolder, "db", "03_Quests"));
            DbCalibers = LoadDbCalibers(Path.Combine(modFolder, "db", "05_Calibers"));
            DbLocales = LoadDbLocales(Path.Combine(modFolder, "db", "06_Locales"));
            DbAddSettings = LoadDbAddSettings(Path.Combine(modFolder, "db", "07_Settings"));
        }

        private TraderBase LoadTraderBase()
        {
            var file = Path.Combine(modFolder, "db", "04_trader", "base.json");
            var data = _modHelper.GetJsonDataFromFile<TraderBase>(modFolder, file);
            if (data != null) return data;
            _logger.LogWithColor($"[{GetType().Namespace}] Could not read {file}!", LogTextColor.Red);
            return new TraderBase();
        }

        private Dictionary<string, VariantConfiguration> LoadDbVariants(string directoryPath)
        {
            var combinedData = new Dictionary<string, VariantConfiguration>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor($"[{GetType().Namespace}] Directory not found: {directoryPath}!", LogTextColor.Yellow);
                return combinedData;
            }
            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var data = _modHelper.GetJsonDataFromFile<Dictionary<string, VariantConfiguration>>(modFolder, file);
                    if (data == null) continue;
                    foreach (var (key, value) in data)
                    {
                        if (combinedData.TryGetValue(key, out var existing))
                        {
                            // Both have Description → log error and skip
                            if (!string.IsNullOrEmpty(existing.Description) && !string.IsNullOrEmpty(value.Description))
                            {
                                _logger.LogWithColor($"[{GetType().Namespace}] Duplicate Description conflict for key '{key}' in {Path.GetFileName(file)}. Only one variant config should have 'Description' property!", LogTextColor.Red);
                                continue;
                            }

                            // Determine which is the "original" (the one with Description)
                            var original = !string.IsNullOrEmpty(existing.Description) ? existing : value;
                            var duplicate = ReferenceEquals(original, existing) ? value : existing;

                            // --- Merge Bullets ---
                            if (duplicate.Bullets?.Count > 0)
                            {
                                original.Bullets ??= [];

                                foreach(var (k,v) in duplicate.Bullets)
                                {
                                    original.Bullets[k] = v;
                                }
                            }
                            combinedData[key] = original;
                        }
                        else
                        {
                            combinedData[key] = value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWithColor($"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}", LogTextColor.Red);
                }
            }
            return combinedData;
        }

        private Dictionary<string, CaliberInfo> LoadDbCalibers(string directoryPath)
        {
            var combinedData = new Dictionary<string, CaliberInfo>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor($"[{GetType().Namespace}] Directory not found: {directoryPath}!", LogTextColor.Yellow);
                return combinedData;
            }
            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var data = _modHelper.GetJsonDataFromFile<Dictionary<string, CaliberInfo>>(modFolder, file);
                    if (data == null) continue;
                    foreach (var kvp in data)
                    {
                        combinedData[kvp.Key] = kvp.Value; // overwrite duplicates
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWithColor($"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}", LogTextColor.Red);
                }
            }
            return combinedData;
        }

        private Dictionary<string, QuestData> LoadDbQuests(string directoryPath)
        {
            var combinedData = new Dictionary<string, QuestData>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor($"[{GetType().Namespace}] Directory not found: {directoryPath}!", LogTextColor.Yellow);
                return combinedData;
            }
            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var data = _modHelper.GetJsonDataFromFile<Dictionary<string, QuestData>>(modFolder, file);
                    if (data == null) continue;
                    foreach (var kvp in data)
                    {
                        combinedData[kvp.Key] = kvp.Value; // overwrite duplicates
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWithColor($"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}", LogTextColor.Red);
                }
            }
            return combinedData;
        }
        private Dictionary<string, Dictionary<string, string>> LoadDbLocales(string directoryPath)
        {
            var combinedData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor($"[{GetType().Namespace}] Directory not found: {directoryPath}!", LogTextColor.Yellow);
                return combinedData;
            }
            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var data = _modHelper.GetJsonDataFromFile<Dictionary<string, Dictionary<string, string>>>(modFolder, file);
                    if (data == null) continue;
                    foreach (var kvp in data)
                    {
                        if (combinedData.TryGetValue(kvp.Key, out var existingData))
                        {
                            foreach (var innerKvp in kvp.Value)
                            {
                                existingData[innerKvp.Key] = innerKvp.Value; // overwrite duplicates
                            }
                        }
                        else
                        {
                            combinedData[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWithColor($"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}", LogTextColor.Red);
                }
            }
            return combinedData;
        }
        private SettingsData LoadDbAddSettings(string directoryPath)
        {
            var combinedData = new SettingsData();

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor(
                    $"[{GetType().Namespace}] Directory not found: {directoryPath}!",
                    LogTextColor.Yellow);
                return combinedData;
            }

            var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                try
                {
                    var data = _modHelper.GetJsonDataFromFile<SettingsData>(modFolder, file);
                    if (data == null) continue;

                    // Merge BaseWeaponExceptions (avoid duplicates)
                    foreach (var exception in data.BaseWeaponExceptions)
                    {
                        if (!combinedData.BaseWeaponExceptions.Contains(exception))
                        {
                            combinedData.BaseWeaponExceptions.Add(exception);
                        }
                    }

                    // Merge CopyWeaponExceptions (overwrite duplicates)
                    foreach (var kvp in data.CopyWeaponExceptions)
                    {
                        combinedData.CopyWeaponExceptions[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWithColor(
                        $"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}",
                        LogTextColor.Red);
                }
            }

            return combinedData;
        }
    }
}
