using Amonya.Interfaces;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using System.Reflection;


namespace Amonya.CustomClasses
{
    [Injectable(InjectionType.Singleton)]
    public class CustomItemCreator(
        ISptLogger<Amonya> logger,
        CustomItemService customItemService
    )
    {

        public int itemsLoaded = 0;

        private Globals? GlobalsData { get; set; } = null;
        private Dictionary<MongoId, TemplateItem> Items { get; set; } = [];
        private Dictionary<MongoId, Trader> Traders { get; set; } = [];
        private AirdropConfig? ConfigServerAirdropConfig { get; set; } = null;
        private TraderConfig? ConfigServerTraderConfig { get; set; } = null;
        private RagfairConfig? ConfigServerRagfairConfig { get; set; } = null;

        public void Initialize(DatabaseService databaseService, ConfigServer configServer)
        {
            GlobalsData = databaseService.GetGlobals();
            Items = databaseService.GetItems();
            Traders = databaseService.GetTraders();
            ConfigServerAirdropConfig = configServer.GetConfig<AirdropConfig>();
            ConfigServerTraderConfig = configServer.GetConfig<TraderConfig>();
            ConfigServerRagfairConfig = configServer.GetConfig<RagfairConfig>();
        }
        public void AddItemToDatabase(NewItemFromCloneDetails item, CustomItemConfig itemConfig, CustomBarterConfig barterConfig)
        {
            if (item.NewId == null) return;

            customItemService.CreateItemFromClone(item);

            if (itemConfig.AirdropBlacklisted && ConfigServerAirdropConfig is not null)
            {
                foreach (var (_, airdrop) in ConfigServerAirdropConfig.Loot)
                {
                    airdrop.ItemBlacklist.Add(item.NewId);
                }
            }
            if (itemConfig.FenceBlacklisted && ConfigServerTraderConfig is not null)
            {
                ConfigServerTraderConfig.Fence.Blacklist.Add(item.NewId);
            }
            if (itemConfig.FleaBlacklisted && ConfigServerRagfairConfig is not null)
            {
                ConfigServerRagfairConfig.Dynamic.Blacklist.Custom.Add(item.NewId);
            }
            if (itemConfig.AddToInventorySlots.Count > 0)
            {
                AddItemToInventorySlots(item.NewId, itemConfig);
            }
            if (itemConfig.MasteryName != "")
            {
                AddItemToMasteries(item.NewId, itemConfig);
            }
            if (itemConfig.Presets.Count > 0 && GlobalsData is not null)
            {
                foreach (var (presetId, preset) in itemConfig.Presets)
                {
                    GlobalsData.ItemPresets[presetId] = preset;
                }
            }
            if (barterConfig.LoyalLevel != 0 && barterConfig.BarterPrice.Count > 0)
            {
                AddItemToTrader(item.NewId, barterConfig);
            }
            if (itemConfig.AddToSpecialSlots)
            {
                AddItemToSpecialSlots(item.NewId);
            }

            itemsLoaded++;
        }

        private void AddItemToSpecialSlots(string itemId)
        {
            // SVM / "Pockets 1x4 with special slots" / "Pockets 1x4 TUE"
            HashSet<string> pocketsIds = ["a8edfb0bce53d103d3f62b9b", "627a4e6b255f7527fb05a0f6", "65e080be269cbd5c5005e529"];
            foreach (var pocketId in pocketsIds)
            {
                Items.TryGetValue(pocketId, out var pocket);
                if (pocket != null)
                {
                    if (pocket?.Properties?.Slots is null) continue;
                    foreach (var slot in pocket.Properties.Slots)
                    {
                        slot?.Properties?.Filters?.First()?.Filter?.Add(itemId);
                    }
                }
            }
        }

        private void AddItemToInventorySlots(string itemId, CustomItemConfig itemConfig)
        {
            TemplateItem defaultInventory = Items["55d7217a4bdc2d86028b456d"];
            if (defaultInventory.Properties == null) return;
            IEnumerable<Slot>? defaultInventorySlots = defaultInventory.Properties.Slots;
            if (defaultInventorySlots != null && defaultInventorySlots.Any())
            {
                foreach (var slot in defaultInventorySlots)
                {
                    if (slot.Name == null) continue;

                    if (itemConfig.AddToInventorySlots.Contains(slot.Name) && slot.Properties != null)
                    {
                        var filters = slot.Properties.Filters;
                        if (filters != null)
                        {
                            foreach (var filter in filters)
                            {
                                if (filter != null && filter.Filter != null && !filter.Filter.Contains(itemId))
                                {
                                    filter.Filter.Add(itemId);
                                }
                            }
                        }
                        
                    }
                }
            }
        }
        private void AddItemToMasteries(string itemId, CustomItemConfig itemConfig)
        {
            var mastering = GlobalsData.Configuration.Mastering;
            var existingMastery = mastering.FirstOrDefault(existing => existing.Name == itemConfig.MasteryName);
            if (existingMastery != null)
            {
                existingMastery.Templates = existingMastery.Templates.Append(itemId);
            }
            else
            {
                logger.LogWithColor($"[{GetType().Namespace}] MasteryName '{itemConfig.MasteryName}' is incorrect!", LogTextColor.Red);
            }
        }
        private void AddItemToTrader(string itemId, CustomBarterConfig barterConfig)
        {
            var traderId = GetTraderIdByName(barterConfig.TraderId);
            if (traderId == null)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Trader name / Trader ID '{traderId}' is incorrect!", LogTextColor.Red);
                return;
            }
            var trader = Traders[(MongoId)traderId];

            foreach (var (addBarterId, _) in barterConfig.BarterPrice)
            {
                var addBarter = GetItemIdByName(addBarterId);
                if (addBarter == null)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Barter item of id '{addBarterId}' is incorrect! Item {itemId} was not added to trader", LogTextColor.Red);
                    return;
                }
            }

            var newItem = new Item
            {
                Id = itemId,
                Template = itemId,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = barterConfig.UnlimitedCount,
                    StackObjectsCount = barterConfig.StackObjectsCount
                }
            };
            var assort = trader.Assort.Items;
            assort?.Add(newItem);

            List<BarterScheme> newBarterSchemes = [];

            foreach (var (addBarterId, price) in barterConfig.BarterPrice)
            {
                var newBarterScheme = new BarterScheme
                {
                    Count = price,
                    Template = (MongoId)addBarterId
                };
                newBarterSchemes.Add(newBarterScheme);

            }
            var assortBarterScheme = trader.Assort.BarterScheme;
            if (!assortBarterScheme.ContainsKey(itemId))
            {
                assortBarterScheme[itemId] = [];
                assortBarterScheme[itemId].Add(newBarterSchemes);
            }
            trader.Assort.LoyalLevelItems[itemId] = barterConfig.LoyalLevel;
        }

        public MongoId? GetTraderIdByName(string name)
        {
            var field = typeof(Traders).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.GetValue(null) is MongoId id)
            {
                return id;
            }
            if (!MongoId.IsValidMongoId(name) || !Traders.TryGetValue(name, out _)) return null;

            return name;
        }

        public MongoId? GetItemIdByName(string name)
        {
            var field = typeof(ItemTpl).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.GetValue(null) is MongoId id)
            {
                return id;
            }
            if (!MongoId.IsValidMongoId(name) || !Items.TryGetValue(name, out _)) return null;

            return name;
        }
    }
}
