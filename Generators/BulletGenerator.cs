using Amonya.CustomClasses;
using Amonya.Interfaces;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace Amonya.Generators
{
    [Injectable(InjectionType.Singleton)]
    public class BulletGenerator(
        ISptLogger<Amonya> logger,
        ConfigLoader configLoader,
        ModDatabaseLoader modDatabaseLoader,
        IdDatabaseManager idDatabaseManager,
        CustomItemCreator customItemCreator,
        CustomPropertiesChanger customPropertiesChanger,
        CustomBulletsManager customBulletsManager,
        CustomWeaponsManager customWeaponsManager,
        QuestGenerator questGenerator,
        CustomLocales customLocales
    )
    {
        private Dictionary<MongoId, TemplateItem> Items { get; set; } = [];
        private List<HandbookItem> HandbookItems { get; set; } = [];
        public void Initialize(DatabaseService databaseService)
        {
            Items = databaseService.GetItems();
            HandbookItems = databaseService.GetHandbook().Items;
        }
        public void GenerateBullets()
        {
            foreach (var (variantName, config) in modDatabaseLoader.DbVariants)
            {
                if (!configLoader.Config.EnableBullets.TryGetValue(variantName, out var bulletEnabled)) continue;

                if (config is { FlavourText: not null, Description: not null, Explanation: not null, ShortName: not null, Bullets: not null, WeaponCategories: not null, Price: not null, Color: not null } variant)
                {
                    var bulletIds = new List<string>();
                    foreach (var bulletName in variant.Bullets.Keys)
                    {
                        var bullet = customBulletsManager.GetBulletByName(bulletName);
                        if (bullet == null) continue;
                        bulletIds.Add($"{{{bullet.Id} Name}}");
                    }
                    string bulletNamesInVariant = string.Join(" | ", bulletIds);
                    foreach (var (bulletName, questName) in variant.Bullets)
                    {
                        var bullet = customBulletsManager.GetBulletByName(bulletName);
                        if (bullet == null)
                        {
                            //logger.LogWithColor($"[{GetType().Namespace}] Bullet '{bulletName}' for {variantName} Variant not found!", LogTextColor.Red);
                            continue;
                        }
                        var id = bullet.Id;
                        var copiedItem = Items[id]!;
                        var caliberInfo = modDatabaseLoader.DbCalibers[bullet.Caliber];
                        string variantShortName = $"{caliberInfo.ShortName} {variant.ShortName}";
                        HandbookItem? copiedItemHandbook = HandbookItems.Find(t => t.Id == id);
                        var newId = idDatabaseManager.GetCustomId($"{variantShortName}:ID");
                        var variantShortnameDisplayed = configLoader.Config.BulletVariantsShortName
                            .Replace("<variant_fullname>", $"{{{variantName}.Name}}")
                            .Replace("<variant_shortname>", $"{{{variantName}.ShortName}}")
                            .Replace("<caliber_shortname>", caliberInfo.ShortName)
                            .Replace("<caliber_amonyaid>", caliberInfo.Id);
                        customLocales.RegisterTag("caliberInfo.Name", caliberInfo.Name);
                        var newItem = new NewItemFromCloneDetails
                        {
                            ItemTplToClone = id,
                            ParentId = copiedItem.Parent,
                            HandbookParentId = copiedItemHandbook!.ParentId,
                            NewId = newId,
                            FleaPriceRoubles = Math.Ceiling(bullet.Price * (double)variant.Price) * 2,
                            HandbookPriceRoubles = Math.Ceiling(bullet.Price * (double)variant.Price),
                            OverrideProperties = new TemplateItemProperties(),
                            Locales = customLocales.CreateItemLocale(
                                $"<b><color={variant.Color}>{{{copiedItem.Id} Name}} {{{variantName}.Name}} {{VariantWord}}</color></b>",
                                variantShortnameDisplayed,
                                string.Join("\n", new[] {
                                    $"<align=\"center\">{{{variantName}.FlavourText}}",
                                    $"",
                                    $"<color={variant.Color}><b>{{{variantName}.Name}} {{VariantWord}}</b></color>",
                                    $"{{{variantName}.Description}}",
                                    $"<i>{{{variantName}.Explanation}}</i>",
                                    $"{bulletNamesInVariant.Replace($"{{{copiedItem.Id} Name}}", $"<b><color={variant.Color}>{{{copiedItem.Id} Name}}</color></b>")}",
                                    $"",
                                    $"{{VariantDescription1}}",
                                    $"{string.Join(" | ", variant.WeaponCategories.Select(c => $"{{WeaponCategory.{c}}}"))}</align>"
                                })
                            )
                        };
                        if (!configLoader.Config.CheckColorConverterAPI || IsPluginLoaded())
                        {
                            newItem.OverrideProperties.BackgroundColor = $"{variant.Color}ff";
                            newItem.OverrideProperties.Tracer = true;
                            newItem.OverrideProperties.TracerColor = variant.Color;
                        }

                        if (variant.Properties != null)
                            newItem.OverrideProperties = customPropertiesChanger.ChangeItemProperties(variant.Properties, newItem.OverrideProperties, copiedItem, config, variantName);

                        if (configLoader.Config.EnableBulletQuests)
                        {
                            customItemCreator.AddItemToDatabase(newItem, new CustomItemConfig(), config.Barter ?? new CustomBarterConfig());
                        } else
                        {
                            var newBarterConfig = new CustomBarterConfig
                            {
                                TraderId = "ee840a5ba014e9c5478d5ccd",
                                LoyalLevel = 1,
                                StackObjectsCount = configLoader.Config.AmmoPrice.Max,
                                UnlimitedCount = configLoader.Config.AmmoPrice.UnlimitedCount
                            };
                            var newPrice = (double)(newItem.HandbookPriceRoubles * configLoader.Config.AmmoPrice.Multiplier);
                            newBarterConfig.BarterPrice.Add("5449016a4bdc2d6f028b456f", (int)Math.Ceiling(newPrice));
                            customItemCreator.AddItemToDatabase(newItem, new CustomItemConfig(), newBarterConfig);
                        }

                        customBulletsManager.AddBulletToDatabase(newId);
                        customWeaponsManager.RegisterNewBulletToAddToSlots(newId, id, bullet.Caliber, variant.WeaponCategories);
                        questGenerator.AddBulletVariantToQuest(newId, questName);
                    }
                } else
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Variant type {variantName} is missing one or more required properties! {config.Bullets is null}/{config.WeaponCategories is null}", LogTextColor.Red);
                }
            }
        }
        private static bool IsPluginLoaded()
        {
            const string pluginName = "rairai.colorconverterapi.dll";
            const string pluginsPath = "../BepInEx/plugins";

            try
            {
                if (!Directory.Exists(pluginsPath))
                    return false;

                var pluginList = Directory.GetFiles(pluginsPath)
                    .Select(System.IO.Path.GetFileName)
                    .Select(f => f?.ToLowerInvariant());
                return pluginList.Contains(pluginName);
            }
            catch
            {
                return false;
            }
        }
    }
}
