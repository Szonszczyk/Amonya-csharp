using Amonya.Helpers;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;
using Path = System.IO.Path;

namespace Amonya.CustomClasses
{
    [Injectable(InjectionType.Singleton)]
    public class CustomTraderCreator(
        ISptLogger<Amonya> logger,
        ICloner cloner,
        ImageRouter imageRouter,
        ModHelper modHelper,
        ConfigLoader configLoader,
        ModDatabaseLoader modDatabaseLoader,
        TimeUtil timeUtil,
        CustomLocales customLocales,
        ModDataStorage modDataStorage
    )
    {
        public List<string> questImages { get; set; } = [];
        public void Initialize()
        {
            RegisterTraderImage();
            SetTraderUpdateTime();
            modDataStorage.ConfigServerRagfairConfig.Traders.TryAdd(modDatabaseLoader.TraderBase.Id, true);
            AddTraderWithEmptyAssortToDb();
            AddTraderToLocales(modDatabaseLoader.TraderBase);
            RegisterQuestImages();
        }
        private void RegisterTraderImage()
        {
            var baseJson = modDatabaseLoader.TraderBase;
            if (baseJson.Avatar is null) return;
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

            var traderImagePath = Path.Combine(pathToMod, "res", configLoader.Config.EnableNonPonyMode ? "AmonyaNP.png" : "Amonya.jpg");
            imageRouter.AddRoute(baseJson.Avatar.Replace(".jpg", ""), traderImagePath);
        }
        private void SetTraderUpdateTime()
        {
            var refreshTimeSecondsMin = timeUtil.GetMinutesAsSeconds(configLoader.Config.TraderUpdateTime.Minimum);
            var refreshTimeSecondsMax = timeUtil.GetMinutesAsSeconds(configLoader.Config.TraderUpdateTime.Maximum);
            var baseJson = modDatabaseLoader.TraderBase;
            // Add refresh time in seconds to config
            var traderRefreshRecord = new UpdateTime
            {
                TraderId = baseJson.Id,
                Seconds = new MinMax<int>((int)refreshTimeSecondsMin, (int)refreshTimeSecondsMax)
            };

            modDataStorage.ConfigServerTraderConfig.UpdateTime.Add(traderRefreshRecord);
        }
        private void AddTraderWithEmptyAssortToDb()
        {
            var traderDetailsToAdd = modDatabaseLoader.TraderBase;
            var emptyTraderItemAssortObject = new TraderAssort
            {
                Items = [],
                BarterScheme = [],
                LoyalLevelItems = []
            };
            var traderBase = cloner.Clone(traderDetailsToAdd);
            if (traderBase == null) return;
            var traderDataToAdd = new Trader
            {
                Assort = emptyTraderItemAssortObject,
                Base = traderBase,
                QuestAssort = new()
                {
                    { "started", new() },
                    { "success", new() },
                    { "fail", new() }
                },
                Dialogue = []
            };

            if (!modDataStorage.Traders.TryAdd(traderDetailsToAdd.Id, traderDataToAdd))
            {
                logger.LogWithColor($"[{GetType().Namespace}] Failed to add Amonya to databases!", LogTextColor.Red);
            }
        }
        private void AddTraderToLocales(TraderBase baseJson)
        {
            var newTraderId = baseJson.Id;
            customLocales.AddLocale($"{newTraderId} FullName", "Amonya.FullName");
            customLocales.AddLocale($"{newTraderId} FirstName", "Amonya.FirstName");
            customLocales.AddLocale($"{newTraderId} Nickname", "Amonya.Nickname");
            customLocales.AddLocale($"{newTraderId} Location", "Amonya.Location");
            customLocales.AddLocale($"{newTraderId} Description", $"Amonya{(configLoader.Config.EnableNonPonyMode ? "NP" : "")}.Description");
        }
        private void RegisterQuestImages()
        {
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var questImagesPath = Path.Combine(pathToMod, "res", "quests");
            var files = Directory.GetFiles(questImagesPath, "*.png", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var imageName = Path.GetFileNameWithoutExtension(file);
                imageRouter.AddRoute($"/files/quest/icon/{imageName}", file);
                questImages.Add(imageName);
            }
        }
    }
}
