using Amonya.CustomClasses;
using Amonya.Helpers;
using Amonya.Interfaces;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace Amonya.Generators
{
    [Injectable(InjectionType.Singleton)]
    public class ItemGenerator(
        CustomLogger logger,
        ModDatabaseLoader modDatabaseLoader,
        IdDatabaseManager idDatabaseManager,
        CustomItemCreator customItemCreator,
        CustomPropertiesChanger customPropertiesChanger,
        CustomSlotsChanger customSlotsChanger,
        ModsCompatibility modsCompatibility,
        ConfigLoader configLoader,
        CustomLocales customLocales,
        ModDataStorage modDataStorage
    )
    {

        public void GenerateItems()
        {
            foreach (var (variantName, config) in modDatabaseLoader.DbItems)
            {
                if (config.Mod is not null && !modsCompatibility.ModCheck(config.Mod)) continue;

                if (config is { Description: not null, ShortName: not null, ItemTplToClone: not null, HandbookPriceRoubles: not null, Color: not null } variant)
                {
                    if (!MongoId.IsValidMongoId(variant.ItemTplToClone)) {
                        logger.Error($"ItemTplToClone {variant.ItemTplToClone} is incorrect ({variantName})!");
                        continue;
                    }
                    MongoId itemTplToClone = (MongoId)variant.ItemTplToClone;
                    TemplateItem copiedItem = modDataStorage.Items[itemTplToClone];
                    HandbookItem? copiedItemHandbook = modDataStorage.Handbook.Items.Find(t => t.Id == itemTplToClone);
                    if (copiedItemHandbook == null) {
                        logger.Error($"Item {itemTplToClone} handbook entry is missing ({variantName})!");
                        continue;
                    }
                    var newId = idDatabaseManager.GetCustomId($"{variantName}:ID");
                    var newItem = new NewItemFromCloneDetails
                    {
                        NewItemName = variantName,
                        ItemTplToClone = itemTplToClone,
                        ParentId = variant.Changes?.Parent != null ? variant.Changes.Parent : copiedItem.Parent,
                        HandbookParentId = copiedItemHandbook.ParentId,
                        NewId = newId,
                        FleaPriceRoubles = variant.HandbookPriceRoubles * 2,
                        HandbookPriceRoubles = variant.HandbookPriceRoubles,
                        OverrideProperties = new TemplateItemProperties
                        {
                            BackgroundColor = "red"
                        },
                        Locales = customLocales.CreateItemLocale(
                            $"<b><color={variant.Color}>{{{variantName}.Name}}</color></b>",
                            $"{{{variantName}.ShortName}}",
                            $"<align=\"center\">{{{variantName}.Description}}</align>",
                            newId
                        )
                    };


                    if (!configLoader.Config.CheckColorConverterAPI || IsPluginLoaded())
                    {
                        newItem.OverrideProperties.BackgroundColor = $"{variant.Color}ff";
                    }

                    if (variant.Properties != null)
                        newItem.OverrideProperties = customPropertiesChanger.ChangeItemProperties(variant.Properties, newItem.OverrideProperties, copiedItem, config, variantName);

                    if (variant?.Changes?.Cartridges != null)
                    {
                        var newCartridges = customSlotsChanger.CartridgesChanger(variant.Changes.Cartridges, copiedItem, newItem, variantName);
                        if (newCartridges != null) newItem.OverrideProperties.Cartridges = newCartridges;

                        // Revolver slot changes when changing cartidges filter!
                        if (copiedItem.Properties?.Slots?.Count() > 0)
                        {
                            foreach (var slot in copiedItem.Properties.Slots)
                            {
                                if (slot?.Name is not null && slot.Name.Contains("camora_"))
                                {
                                    if (variant.Changes.Slots is null) variant.Changes.Slots = [];
                                    variant.Changes.Slots.Add(slot.Name, variant.Changes.Cartridges);
                                }
                            }
                        }
                    }

                    if (variant?.Changes?.Slots != null)
                    {
                        var newSlots = customSlotsChanger.SlotsChanger(variant.Changes.Slots, copiedItem, newItem, variantName);
                        if (newSlots != null) newItem.OverrideProperties.Slots = newSlots;
                    }
                    if (config.Barter is not null)
                    {
                        foreach (var (barter, price) in config.Barter.BarterPrice.ToList())
                        {
                            var barterId = customSlotsChanger.GetItemFromString(barter)?.Id;
                            if (barterId is null) continue;
                            config.Barter.BarterPrice.Remove(barter);
                            config.Barter.BarterPrice[barterId] = price;
                        }
                    }
                    customItemCreator.AddItemToDatabase(newItem, config.CustomItemConfig ?? new CustomItemConfig(), config.Barter ?? new CustomBarterConfig());
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
