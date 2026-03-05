using Amonya.Interfaces;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using System.Reflection;

namespace Amonya.Loaders
{
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2122)]
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
                        logger.LogWithColor(
                            $"[{GetType().Namespace}] {fileName} not found. Copying {defaultFileName}...",
                            LogTextColor.Yellow
                        );
                        File.Copy(defaultConfigPath, configPath);
                    }
                    else
                    {
                        logger.LogWithColor(
                            $"[{GetType().Namespace}] Neither {fileName} nor {defaultFileName} found. Using defaults.",
                            LogTextColor.Red
                        );
                        return new T();
                    }
                }

                var config = modHelper.GetJsonDataFromFile<T>(modFolder, configPath);

                if (config == null)
                {
                    logger.LogWithColor(
                        $"[{GetType().Namespace}] {fileName} is null. Using defaults.",
                        LogTextColor.Red
                    );
                    return new T();
                }

                //logger.LogWithColor($"[{GetType().Namespace}] {fileName} loaded successfully.", LogTextColor.Green);
                return config;
            }
            catch (Exception ex)
            {
                logger.LogWithColor(
                    $"[{GetType().Namespace}] Failed to load {fileName}: {ex.Message}",
                    LogTextColor.Red
                );
                return new T();
            }
        }
    }

}
