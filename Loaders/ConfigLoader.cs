using Amonya.Helpers;
using Amonya.Interfaces;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using System.Reflection;

namespace Amonya.Loaders;

[Injectable(InjectionType.Singleton)]
public class ConfigLoader
{
    public ConfigData Config { get; }
    public QuestConfig QConfig { get; }

    public ConfigLoader(ISptLogger<Amonya> logger, ModHelper modHelper)
    {
        Config = LoadConfig<ConfigData>(
            logger,
            modHelper,
            "config.json",
            "defaultConfig.json"
        );

        QConfig = LoadConfig<QuestConfig>(
            logger,
            modHelper,
            "questConfig.json",
            "defaultQuestConfig.json"
        );
    }

    private T LoadConfig<T>(
        ISptLogger<Amonya> logger,
        ModHelper modHelper,
        string fileName,
        string defaultFileName
    ) where T : new()
    {
        string modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string configDir = Path.Combine(modFolder, "config");
        string configPath = Path.Combine(configDir, fileName);
        string defaultConfigPath = Path.Combine(configDir, defaultFileName);

        try
        {
            if (!File.Exists(configPath))
            {
                if (File.Exists(defaultConfigPath))
                {
                    logger.Warning($"[Amonya] {fileName} not found. Copying {defaultFileName}...");
                    File.Copy(defaultConfigPath, configPath);
                }
                else
                {
                    logger.Error($"[Amonya] Neither {fileName} nor {defaultFileName} found. Using defaults.");
                    return new T();
                }
            }

            var config = modHelper.GetJsonDataFromFile<T>(modFolder, configPath);

            if (config == null)
            {
                logger.Error($"[Amonya] {fileName} is null. Using defaults.");
                return new T();
            }

            //logger.LogWithColor($"{fileName} loaded successfully.", LogTextColor.Green);
            return config;
        }
        catch (Exception ex)
        {
            logger.Error($"[Amonya] Failed to load {fileName}: {ex.Message}");
            return new T();
        }
    }
}
