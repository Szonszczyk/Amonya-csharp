using Amonya.CustomClasses;
using Amonya.Helpers;
using Amonya.Interfaces;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

namespace Amonya.Generators;

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
    CustomLocales customLocales,
    ModDataStorage modDataStorage
)
{
    public void GenerateBullets()
    {
        foreach (var (variantName, config) in modDatabaseLoader.DbVariants)
        {
            if (config is { ShortName: not null, Bullets: not null, WeaponCategories: not null, Price: not null, Color: not null } variant)
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
                    var copiedItem = modDataStorage.Items[id]!;
                    var caliberInfo = modDatabaseLoader.DbCalibers[bullet.Caliber];
                    string variantShortName = $"{caliberInfo.ShortName} {variant.ShortName}";
                    HandbookItem? copiedItemHandbook = modDataStorage.Handbook.Items.Find(t => t.Id == id);
                    var newId = idDatabaseManager.GetCustomId($"{variantShortName}:ID");
                    var variantNameDisplayed = ApplyBulletVariantTemplate(configLoader.Config.BulletVariantsName, variantName, copiedItem.Id, caliberInfo);
                    var variantShortnameDisplayed = ApplyBulletVariantTemplate(configLoader.Config.BulletVariantsShortName, variantName, copiedItem.Id, caliberInfo);
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
                            $"<b><color={variant.Color}>{variantNameDisplayed}</color></b>",
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
                            }),
                            newId
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

                    if (!configLoader.Config.EnableBullets.TryGetValue(variantName, out var bulletEnabled) || bulletEnabled is true)
                    {
                        if (configLoader.Config.EnableBulletQuests)
                        {
                            customItemCreator.AddItemToDatabase(newItem, new CustomItemConfig(), config.Barter ?? new CustomBarterConfig());
                        }
                        else
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
                    }
                    

                    customBulletsManager.AddBulletToDatabase(newId, true);
                    customWeaponsManager.RegisterNewBulletToAddToSlots(newId, id, bullet.Caliber, variant.WeaponCategories);
                    if (!configLoader.Config.EnableBullets.TryGetValue(variantName, out _) || bulletEnabled is true)
                        questGenerator.AddBulletVariantToQuest(newId, questName);
                }
            } else
            {
                logger.LogWithColor($"[{GetType().Namespace}] Variant type {variantName} is missing one or more required properties! {config.Bullets is null}/{config.WeaponCategories is null}", LogTextColor.Red);
            }
        }
    }
    private static string ApplyBulletVariantTemplate(
        string template,
        string variantName,
        string bulletId,
        CaliberInfo caliberInfo)
    {
        return template
            .Replace("<variant_fullname>", $"{{{variantName}.Name}}")
            .Replace("<variant_shortname>", $"{{{variantName}.ShortName}}")
            .Replace("<bullet_name>", $"{{{bulletId} Name}}")
            .Replace("<variant>", "{VariantWord}")
            .Replace("<caliber_fullname>", caliberInfo.Name)
            .Replace("<caliber_shortname>", caliberInfo.ShortName)
            .Replace("<caliber_amonyaid>", caliberInfo.Id);
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
