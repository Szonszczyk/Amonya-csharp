using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
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
        TimeUtil timeUtil
    )
    {
        private Dictionary<MongoId, Trader> Traders { get; set; } = [];
        private LocaleBase? Locale { get; set; } = null;
        private TraderConfig? ConfigServerTraderConfig { get; set; } = null;
        private RagfairConfig? ConfigServerRagfairConfig { get; set; } = null;
        public void Initialize(DatabaseService databaseService, ConfigServer configServer)
        {
            Traders = databaseService.GetTraders();
            Locale = databaseService.GetLocales();
            ConfigServerTraderConfig = configServer.GetConfig<TraderConfig>();
            ConfigServerRagfairConfig = configServer.GetConfig<RagfairConfig>();
            RegisterTraderImage();
            SetTraderUpdateTime();
            ConfigServerRagfairConfig.Traders.TryAdd(modDatabaseLoader.TraderBase.Id, true);
            AddTraderWithEmptyAssortToDb();
            AddTraderToLocales(
                modDatabaseLoader.TraderBase,
                "Amonya",
                "A pony known for hoarding a large amount of ammunition-perhaps even too much for this grey-white, out-of-season creature. However, she might share some if you complete some of her quests..."
            );
            RegisterQuestImages();
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

            if (!Traders.TryAdd(traderDetailsToAdd.Id, traderDataToAdd))
            {
                logger.LogWithColor($"[{GetType().Namespace}] Failed to add Amonya to databases!", LogTextColor.Red);
            }
        }

        private void AddTraderToLocales(TraderBase baseJson, string firstName, string description)
        {
            var locales = Locale.Global;
            var newTraderId = baseJson.Id;
            var fullName = baseJson.Name;
            var nickName = baseJson.Nickname ?? "NICKNAME";
            var location = baseJson.Location ?? "LOCATION";

            foreach (var (localeKey, localeKvP) in locales)
            {
                localeKvP.AddTransformer(lazyloadedLocaleData =>
                {
                    if (lazyloadedLocaleData == null) return lazyloadedLocaleData;
                    lazyloadedLocaleData.Add($"{newTraderId} FullName", fullName);
                    lazyloadedLocaleData.Add($"{newTraderId} FirstName", firstName);
                    lazyloadedLocaleData.Add($"{newTraderId} Nickname", nickName);
                    lazyloadedLocaleData.Add($"{newTraderId} Location", location);
                    lazyloadedLocaleData.Add($"{newTraderId} Description", description);
                    return lazyloadedLocaleData;
                });
            }
        }

        private void SetTraderUpdateTime()
        {
            var refreshTimeSecondsMin = timeUtil.GetHoursAsSeconds(1);
            var refreshTimeSecondsMax = timeUtil.GetHoursAsSeconds(2);
            var baseJson = modDatabaseLoader.TraderBase;
            // Add refresh time in seconds to config
            var traderRefreshRecord = new UpdateTime
            {
                TraderId = baseJson.Id,
                Seconds = new MinMax<int>(refreshTimeSecondsMin, refreshTimeSecondsMax)
            };

            ConfigServerTraderConfig.UpdateTime.Add(traderRefreshRecord);
        }

        private void RegisterTraderImage()
        {
            var baseJson = modDatabaseLoader.TraderBase;
            if (baseJson.Avatar is null) return;
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            
            var traderImagePath = Path.Combine(pathToMod, "res", configLoader.Config.EnableNonPonyMode ? "AmonyaNP.png" : "Amonya.jpg");
            imageRouter.AddRoute(baseJson.Avatar.Replace(".jpg", ""), traderImagePath);
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
            }
        }
    }
}
