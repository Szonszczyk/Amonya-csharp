using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using System.Reflection.Metadata.Ecma335;

namespace Amonya.CustomClasses
{
    [Injectable(InjectionType.Singleton)]
    public class CustomBulletsManager(
        ISptLogger<Amonya> logger,
        ConfigLoader configLoader,
        ModDatabaseLoader modDatabaseLoader,
        ItemHelper itemHelper,
        LocaleService localeService
    )
    {
        public class BulletsDatabase
        {
            public MongoId Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ShortName { get; set; } = string.Empty;
            public string Caliber { get; set; } = string.Empty;
            public double Price { get; set; } = 100;
            public double NewPrice { get; set; } = 100;
            public string DMG {  get; set; } = string.Empty;
            public string PEN { get; set; } = string.Empty;

        }

        private readonly Dictionary<MongoId, BulletsDatabase> bullets = [];

        public readonly Dictionary<string, List<MongoId>> BulletsInCaliber = [];
        private readonly Dictionary<MongoId, string> BulletCalibers = [];

        private readonly List<MongoId> incorrectBullets = [
            "5ede47641cf3836a88318df1", //!!!DO NOT USE!!!40x46 mm M716(Smoke)
            "5f647fd3f6e4ab66c82faed6", // 23x75mm Volna-R rubber slug
            "63b35f281745dd52341e5da7", // F1 Shrapnel
            "66ec2aa6daf127599c0c31f1", // O-832DU Shrapnel
            "67ade494d748873e5f0161df", // VOG-30 Shrapnel
            "5e85aac65505fa48730d8af2", // !!!DO_NOT_USE!!!23x75mm "Cheremukha-7M"
            "677ae5df4be46b83620bf055", // Rocket
            "5485a8684bdc2da71d8b4567"  // All ammo
        ];

        private Dictionary<MongoId, TemplateItem> Items { get; set; } = [];
        private List<HandbookItem> HandbookItems { get; set; } = [];
        public void Initialize(DatabaseService databaseService)
        {
            Items = databaseService.GetItems();
            HandbookItems = databaseService.GetHandbook().Items;

            FixBulletsWithoutHandbook();
            var allBullets = itemHelper.GetItemTplsOfBaseType("5485a8684bdc2da71d8b4567");
            foreach (var id in allBullets)
            {
                if (id == "5485a8684bdc2da71d8b4567") continue;
                AddBulletToDatabase(id);
            }
        }

        public void AddBulletToDatabase(string id)
        {
            if (incorrectBullets.Contains(id)) return;

            var bulletItem = Items[id];

            if (bulletItem == null) return;

            var caliber = bulletItem?.Properties?.Caliber;

            if (caliber is null)
            {
                if (configLoader.Config.Debug)
                    logger.LogWithColor($"[{GetType().Namespace}] Bullet {id} is missing caliber!", LogTextColor.Red);
                return;
            }

            modDatabaseLoader.DbCalibers.TryGetValue(caliber, out var caliberInfo);
            if (caliberInfo is null || (caliberInfo.Incorrect is not null && (bool)caliberInfo.Incorrect)) return;

            HandbookItem? itemHandbook = HandbookItems.Find(t => t.Id == id);

            if (itemHandbook is null || itemHandbook.Price is null)
            {
                if (configLoader.Config.Debug)
                    logger.LogWithColor($"[{GetType().Namespace}] Bullet {id} is missing Handbook entry!", LogTextColor.Yellow);
                return;
            }
            var locale = localeService.GetLocaleDb("en");
            locale.TryGetValue($"{id} Name", out var name);
            locale.TryGetValue($"{id} ShortName", out var shortName);
            if (name is null && configLoader.Config.Debug)
            {
               logger.LogWithColor($"[{GetType().Namespace}] Bullet {id} is missing locale entry!", LogTextColor.Yellow);
            }
            var bullet = new BulletsDatabase
            {
                Id = id,
                Name = name ?? id,
                ShortName = name ?? id,
                Caliber = caliber,
                Price = (double)itemHandbook.Price,
                NewPrice = Math.Ceiling((double)itemHandbook.Price * configLoader.Config.AmmoPrice.Multiplier),
                DMG = bulletItem?.Properties?.ProjectileCount > 1 ? $"{bulletItem?.Properties?.ProjectileCount}x{bulletItem?.Properties?.Damage}" : $"{bulletItem?.Properties?.Damage}",
                PEN = $"{bulletItem?.Properties?.PenetrationPower}"
            };
            if (bullets.TryGetValue(id, out var item)) return;

            bullets.Add(id, bullet);
            if (!BulletsInCaliber.TryGetValue(caliber, out List<MongoId>? value))
            {
                value = [];
                BulletsInCaliber[caliber] = value;
            }

            if (!BulletsInCaliber[caliber].Contains(id))
            {
                value.Add(id);
                BulletCalibers.Add(id, caliber);
            }
        }

        public string? DeterminateBulletCaliber(MongoId Id)
        {
            if (incorrectBullets.Contains(Id)) return null;

            if (Id == "6241c316234b593b5676b637") return "Airsoft";

            if (!BulletCalibers.TryGetValue(Id, out string? caliber))
            {
                if (configLoader.Config.Debug)
                    logger.LogWithColor($"[{GetType().Namespace}] Bullet {Id} found in filter, is not existing", LogTextColor.Red);
                return null;
            }

            if (caliber is null) return null;

            return caliber;
        }

        public BulletsDatabase? GetBulletByName(string name)
        {
            var matches = bullets.Where(t => t.Value.Name == name);
            if (!matches.Any())
            {
                if (!MongoId.IsValidMongoId(name)) return null;
                if (bullets.TryGetValue(name, out var bullet))
                {
                    return bullet;
                } else
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Bullet '{name}' not found", LogTextColor.Red);
                    return null;
                }
            }
            return matches.First().Value;
        }

        public void FixBulletsWithoutHandbook()
        {
            if (HandbookItems.Find(t => t.Id == "5d70e500a4b9364de70d38ce") is null)
            {
                HandbookItems.Add(new()
                {
                    Id = "5d70e500a4b9364de70d38ce",
                    ParentId = "5b47574386f77428ca22b33b",
                    Price = 6006
                });
            }
        }

        public void ChangeCaliberStackSizes()
        {
            var caliberStacks = configLoader.Config.CaliberStacks;
            foreach (var (caliberId, newStackSize) in caliberStacks)
            {
                BulletsInCaliber.TryGetValue(caliberId, out var caliberIds);
                if (caliberIds is null)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Caliber from CaliberStacks option in config: '{caliberId}' does not exist.", LogTextColor.Red);
                    continue;
                }
                foreach (var id in caliberIds)
                {
                    if (Items[id]?.Properties?.StackMaxSize is not null)
                        Items[id].Properties.StackMaxSize = newStackSize; 
                }
            }
        }
    }
}