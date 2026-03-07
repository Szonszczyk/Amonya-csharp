using Amonya.Constants;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using System.Text.RegularExpressions;

namespace Amonya.CustomClasses
{
    [Injectable(InjectionType.Singleton)]
    public class CustomWeaponsManager(
        ISptLogger<Amonya> logger,
        ConfigLoader configLoader,
        ItemHelper itemHelper,
        CustomBulletsManager customBulletsManager,
        ModDatabaseLoader modDatabaseLoader
    )
    {
        private class WeaponsDatabase
        {
            public string Name { get; set; } = string.Empty;
            public string ShortName { get; set; } = string.Empty;
            public string Caliber { get; set; } = string.Empty;
            public List<string> SecondaryCalibers { get; set; } = [];
            public string Category { get; set; } = string.Empty;
            public HashSet<MongoId> Magazines { get; set; } = [];
            public List<MongoId> ChamberFilter { get; set; } = [];
            public List<MongoId> Copies { get; set; } = [];
        }

        private readonly Dictionary<MongoId, WeaponsDatabase> baseWeapons = [];
        private readonly Dictionary<MongoId, WeaponsDatabase> copyWeapons = [];
        private Dictionary<MongoId, WeaponsDatabase> AllWeapons { get; set; } = [];
        private readonly Dictionary<MongoId, HashSet<string>> checkedMags = [];


        private Dictionary<MongoId, TemplateItem> Items { get; set; } = [];
        private Dictionary<string, string> Locale { get; set; } = [];
        public void Initialize(DatabaseService databaseService, LocaleService localeService)
        {
            Items = databaseService.GetItems();
            Locale = localeService.GetLocaleDb("en");
            LoadAllWeaponsAndMagazines();
        }

        public void LoadAllWeaponsAndMagazines()
        {
            var categoryIds = WeaponCategories.GetAllIds();

            foreach (var categoryId in categoryIds)
            {
                var weaponsInCategory = itemHelper.GetItemTplsOfBaseType(categoryId);
                foreach(var id in weaponsInCategory)
                {
                    var item = Items[id];

                    // Add ShotgunDispersion to all weapons
                    if (item?.Properties?.ShotgunDispersion is not null && item.Properties.ShotgunDispersion == 0) item.Properties.ShotgunDispersion = 5;

                    // Weapon should have chambers - even if it is empty
                    if (item?.Parent is null || item?.Properties?.Chambers is null) continue;

                    // Check if weapon is incorrect
                    if (CheckIfWeaponIsIncorrect(id)) continue;

                    var weapon = new WeaponsDatabase
                    {
                        Name = StripHtml(Locale[$"{id} Name"]),
                        ShortName = Locale[$"{id} ShortName"],
                        Category = WeaponCategories.GetPlural(categoryId)
                    };

                    var isCopy = CheckIfWeaponCopy(id, weapon);

                    // Determinate weapon caliber
                    var caliberList = new List<string>();
                    var hasChamber = item.Properties.Chambers.Any();
                    if (hasChamber)
                    {
                        var filterInChamber = item.Properties.Chambers.First()?.Properties?.Filters?.First().Filter;
                        if (filterInChamber is not null)
                        {
                            weapon.ChamberFilter = [.. filterInChamber];
                            foreach (var bullet in filterInChamber)
                            {
                                var bulletCaliber = customBulletsManager.DeterminateBulletCaliber(bullet);
                                
                                if (bulletCaliber is not null) caliberList.Add(bulletCaliber); // && !caliberList.Contains(bulletCaliber)
                            }
                        }
                    }
                    var magazines = item.Properties.Slots?.FirstOrDefault(t => t?.Name == "mod_magazine")?.Properties?.Filters?.FirstOrDefault()?.Filter;
                    if (magazines is not null)
                    {
                        weapon.Magazines = magazines;
                        if (!hasChamber)
                        {
                            foreach (var magazineId in magazines)
                            {
                                var magCalibers = DeterminateMagazineCaliber(magazineId);
                                if (magCalibers is not null && !hasChamber)
                                {
                                    caliberList.AddRange(magCalibers);
                                }
                            }
                        }
                    }
                    // Skip flares etc. It isn't a weapon if it is without chamber and magazines
                    if (!hasChamber && weapon.Magazines.Count == 0) continue;

                    caliberList = [.. caliberList.Distinct()];
                    if (caliberList.Count == 0) {
                        if (configLoader.Config.Debug)
                            logger.LogWithColor($"[{GetType().Namespace}] Couldn't determinate caliber of weapon '{weapon.Name}'! Chambers: {hasChamber}, Magazines: {weapon.Magazines.Count}", LogTextColor.Red);
                        continue;
                    } else
                    {
                        weapon.Caliber = caliberList.First();
                        if (caliberList.Count > 1)
                        {
                            weapon.SecondaryCalibers = caliberList.GetRange(1, caliberList.Count - 1);
                        }
                    }
                    if (!isCopy)
                    {
                        baseWeapons.Add(id, weapon);
                    } else
                    {
                        copyWeapons.Add(id, weapon);
                    }
                }
            }
            var copyWeaponEx = modDatabaseLoader.DbAddSettings.CopyWeaponExceptions;
            var baseWeaponsByName = baseWeapons.GroupBy(kvp => kvp.Value.Name).ToDictionary(g => g.Key, g => g.First().Key);
            foreach (var (id, weaponCopy) in copyWeapons.ToList())
            {
                if (copyWeaponEx.TryGetValue(id, out var baseId))
                {
                    if (baseWeapons.TryGetValue(baseId, out var baseWeapon))
                    {
                        baseWeapon.Copies.Add(id);
                    } else
                    {
                        logger.LogWithColor($"[{GetType().Namespace}] Weapon {id}/{weaponCopy.Name} base weapon set in 07_Settings in missing in database!", LogTextColor.Red);
                    }
                    continue;
                }
                var baseWeaponsId = baseWeaponsByName.Where(t => weaponCopy.Name.Contains(t.Key)).ToList();
                if (baseWeaponsId.Count != 1)
                {
                    if (configLoader.Config.Debug)
                        logger.LogWithColor($"[{GetType().Namespace}] Weapon determinated copy {id}/{weaponCopy.Name} matches {baseWeaponsId.Count} base weapons instead of 1", LogTextColor.Yellow);
                }
                if (baseWeaponsId.Count > 0)
                {
                    var firstBaseWeaponId = baseWeaponsId[0].Value;
                    baseWeapons[firstBaseWeaponId].Copies.Add(id);
                } else
                {
                    baseWeapons.Add(id, weaponCopy);
                    copyWeapons.Remove(id);
                }
            }
            AllWeapons = baseWeapons.Concat(copyWeapons).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        private static bool CheckIfWeaponIsIncorrect(MongoId mongoId)
        {
            string id = mongoId.ToString();
            List<string> weaponExceptions = [
                "5ae083b25acfc4001a5fc702",
                "657857faeff4c850222dff1b",
                "639c3fbbd0446708ee622ee9", // FN40GL Mk2 grenade launcher
                //"5d52cc5ba4b9367408500062", // AGS-30 30x29mm automatic grenade launcher
                "620109578d82e67e7911abf2", // ZiD SP-81 26x75 signal pistol
                "639af924d0446708ee62294e"  // FN40GL Mk2 grenade launcher (smoke edition of smth idk)
            ];
            // Incorrect weapon
            if (weaponExceptions.Contains(id)) return true;
            return false;
        }

        private HashSet<string>? DeterminateMagazineCaliber(MongoId mongoId)
        {
            if (checkedMags.TryGetValue(mongoId, out var value)) return value;

            HashSet<string> calibers = [];

            Items.TryGetValue(mongoId, out var magazine);
            if (magazine is null) return null;
            var filterInCartridges = magazine?.Properties?.Cartridges?.FirstOrDefault()?.Properties?.Filters?.FirstOrDefault()?.Filter;
            if (filterInCartridges is null) return null;
            foreach (var bullet in filterInCartridges)
            {
                var bulletCaliber = customBulletsManager.DeterminateBulletCaliber(bullet);
                if (bulletCaliber is not null) calibers.Add(bulletCaliber); // && !calibers.Contains(bulletCaliber)
            }

            checkedMags[mongoId] = calibers;
            return calibers;
        }

        private bool CheckIfWeaponCopy(MongoId id, WeaponsDatabase weapon)
        {
            // If in special list - it is not a copy
            var baseWeaponEx = modDatabaseLoader.DbAddSettings.BaseWeaponExceptions;
            if (baseWeaponEx.Contains(id)) return false;

            var itemDesc = Locale[$"{id} Description"];
            // If variant from DWV or is different paint - it is a copy
            if (itemDesc.Contains("Variant</b></color>") || weapon.Name.Contains('(')) return true;
            return false;
        }

        public List<MongoId> GetAllCopiesFromBaseWeapon(string id)
        {
            baseWeapons.TryGetValue(id, out var weapons);
            return weapons?.Copies ?? [];
        }


        // cache key = (caliber, category, includeSecondary)
        private readonly Dictionary<(string, string, bool), List<MongoId>> _magazinesCache = [];
        private readonly Dictionary<(string, string, bool, bool, bool), List<MongoId>> _weaponIdsCache = [];

        public List<MongoId> GetMagazines(
            string caliber,
            List<string> categories,
            bool includeSecondary = true
        )
        {
            var key = (caliber, string.Join(",", categories), includeSecondary);

            if (_magazinesCache.TryGetValue(key, out var cached))
                return cached;

            var result = AllWeapons
                .Where(pair =>
                    MatchesCaliber(pair.Value, caliber, includeSecondary) &&
                    MatchesCategory(pair.Value, categories))
                .SelectMany(pair => pair.Value.Magazines)
                .Distinct()
                .ToList();

            _magazinesCache[key] = result;
            return result;
        }

        public List<MongoId> GetWeaponIds(
            string caliber,
            List<string> categories,
            bool includeSecondary = true,
            bool includeCopies = true,
            bool onlyBaseWeapons = false
        )
        {
            var key = (caliber, string.Join(",", categories), includeSecondary, includeCopies, onlyBaseWeapons);

            if (_weaponIdsCache.TryGetValue(key, out var cached))
                return cached;

            var useDatabase = onlyBaseWeapons ? baseWeapons : AllWeapons;

            var result = useDatabase
                .Where(pair =>
                    MatchesCaliber(pair.Value, caliber, includeSecondary) &&
                    MatchesCategory(pair.Value, categories))
                .SelectMany(pair =>
                {
                    var list = new List<MongoId>
                    {
                        pair.Key
                    };
                    if (includeCopies) list.AddRange(pair.Value.Copies);
                    return list;
                })
                .Distinct()
                .ToList();

            _weaponIdsCache[key] = result;
            return result;
        }
        private static bool MatchesCaliber(WeaponsDatabase w, string caliber, bool includeSecondary)
        {
            if (w.Caliber == caliber)
                return true;

            return includeSecondary && w.SecondaryCalibers.Contains(caliber);
        }
        private static bool MatchesCategory(WeaponsDatabase w, List<string> categories)
        {
            if (categories.Count > 0 && categories[0] == "ALL")
                return true;

            return categories.Contains(w.Category);
        }
        public static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public string? GetWeaponShortName(MongoId id)
        {
            baseWeapons.TryGetValue(id, out var weapon);
            if (weapon == null) copyWeapons.TryGetValue(id, out weapon);
            return weapon?.ShortName;
        }

        public void RefreshLoadedWeaponMagazines()
        {
            foreach (var (weaponId, weapon) in AllWeapons)
            {
                var item = Items[weaponId];
                var magazines = item?.Properties?.Slots?.FirstOrDefault(t => t?.Name == "mod_magazine")?.Properties?.Filters?.FirstOrDefault()?.Filter;
                if (magazines != null) weapon.Magazines = magazines;
            }
        }
        private class NewBullets
        {
            public string OriginalId { get; set; } = string.Empty;
            public string Caliber { get; set; } = string.Empty;
            public List<string> Categories { get; set; } = [];
        }

        private readonly Dictionary<string, NewBullets> newBullets = [];
        public void RegisterNewBulletToAddToSlots(string id, string originalId, string caliber, List<string> categories)
        {
            newBullets.Add(id, new()
            {
                OriginalId = originalId,
                Caliber = caliber,
                Categories = categories
            });
        }

        public void SlotNewBulletsIntoItems()
        {
            foreach (var (id, bullet) in newBullets)
            {
                var magazinesToAddBulletTo = GetMagazines(bullet.Caliber, bullet.Categories, true);
                foreach (var magazineId in magazinesToAddBulletTo)
                {
                    Items.TryGetValue(magazineId, out var magazine);
                    if (magazine is null) continue;
                    var filterList = magazine?.Properties?.Cartridges?.FirstOrDefault()?.Properties?.Filters?.FirstOrDefault()?.Filter;

                    if (filterList is null)
                    {
                        if (magazineId != "5448bc234bdc2d3c308b4569")
                            logger.LogWithColor($"[{GetType().Namespace}] Magazine {magazineId} is missing filter in Cartridges!", LogTextColor.Red);
                        continue;
                    }
                    // don't add variant if original bullet is missing in filter
                    if (!filterList.Contains(bullet.OriginalId)) continue;

                    filterList.Add(id);

                    // resolve cylinders because reasons lol
                    if (magazine?.Properties?.Slots is null || !magazine.Properties.Slots.Any()) continue;

                    foreach (var slot in magazine.Properties.Slots)
                    {
                        if (slot?.Name is not null && slot.Name.Contains("camora_"))
                        {
                            slot?.Properties?.Filters?.FirstOrDefault()?.Filter?.Add(id);
                        }
                    }
                }
                var weaponsToAddBulletTo = GetWeaponIds(bullet.Caliber, bullet.Categories, true);
                foreach (var weaponId in weaponsToAddBulletTo)
                {
                    var weapon = Items[weaponId];
                    var chambers = weapon?.Properties?.Chambers;
                    if (chambers is null || !chambers.Any()) continue;
                    foreach (var chamber in chambers)
                    {
                        var filterContainer = chamber?.Properties?.Filters?.FirstOrDefault();
                        var filterList = filterContainer?.Filter;
                        if (filterList is null || !filterList.Contains(bullet.OriginalId)) continue;
                        filterList?.Add(id);
                    }
                }
            }
        }
    }
}