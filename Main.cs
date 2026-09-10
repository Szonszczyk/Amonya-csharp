using Amonya.CustomClasses;
using Amonya.Generators;
using Amonya.Helpers;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace Amonya;

[Injectable(TypePriority = OnLoadOrder.Preload + 2123)]
public class AmonyaTrader(
    CustomTraderCreator customTraderCreator,
    CustomLocales customLocales,
    ModDataStorage modDataStorage
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        modDataStorage.Initialize();

        customLocales.Initialize();
        customTraderCreator.Initialize();
        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.Preload + 89123)]
public class AmonyaBulletLoad(
    ModDataStorage modDataStorage,
    CustomBulletsManager customBulletsManager
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        modDataStorage.RefreshDatabase();
        customBulletsManager.Initialize();
        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.Preload + 97123)]
public class Amonya(
    CustomLogger logger,
    ConfigLoader configLoader,
    CustomBulletsManager customBulletsManager,
    CustomWeaponsManager customWeaponsManager,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    ItemGenerator itemGenerator,
    CustomLocales customLocales,
    QuestGenerator questGenerator,
    BulletGenerator bulletGenerator,
    Fixes fixes,
    ModHelper modHelper,
    JsonUtil jsonUtil,
    ModDataStorage modDataStorage
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        modDataStorage.RefreshDatabase();
        customWeaponsManager.LoadAllWeaponsAndMagazines();
        itemGenerator.GenerateItems();
        questGenerator.Initialize();

        if (configLoader.Config.EnableBulletVariants)
            bulletGenerator.GenerateBullets();
        customBulletsManager.ChangeCaliberStackSizes();

        if (configLoader.Config.EnableBulletQuests)
            questGenerator.GenerateQuests();

        fixes.Initialize();

        idDatabaseManager.SaveDatabase();

        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        modFolder = Path.Combine(modFolder, "db", "98_Debug");
        if (configLoader.Config.DebugFiles.Bullets)
            File.WriteAllText(Path.Combine(modFolder, "Bullets.json"), jsonUtil.Serialize(customBulletsManager.bullets, true));
        if (configLoader.Config.DebugFiles.Quests)
            File.WriteAllText(Path.Combine(modFolder, "Quests.json"), jsonUtil.Serialize(questGenerator.AddedQuests, true));
        if (configLoader.Config.DebugFiles.Locales)
        {
            foreach (var (lang, locale) in customLocales.newLocale)
            {
                File.WriteAllText(Path.Combine(modFolder, $"Locales_{lang}.json"), jsonUtil.Serialize(locale, true));
            }
        }
        if (configLoader.Config.DebugFiles.Items)
            File.WriteAllText(Path.Combine(modFolder, "Items.json"), jsonUtil.Serialize(customItemCreator.ItemsAdded, true));

        logger.Ok($"Mod finished loading{(customItemCreator.ItemsAdded.Count > 0 ? $". Created {customItemCreator.ItemsAdded.Count} custom items!" : "")}");
        if (questGenerator.questsGenerated > 0)
            logger.Ok($"Added {questGenerator.questsGenerated} custom quests!");
        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 2)]
public class AmonyaSlotBulletVariants(
    CustomWeaponsManager customWeaponsManager,
    ModHelper modHelper,
    JsonUtil jsonUtil,
    ConfigLoader configLoader
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        customWeaponsManager.RefreshLoadedWeaponMagazines();
        customWeaponsManager.SlotNewBulletsIntoItems();
        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        modFolder = Path.Combine(modFolder, "db", "98_Debug");
        if (configLoader.Config.DebugFiles.Weapons)
        {
            File.WriteAllText(Path.Combine(modFolder, "BaseWeapons.json"), jsonUtil.Serialize(customWeaponsManager.baseWeapons, true));
            File.WriteAllText(Path.Combine(modFolder, "CopyWeapons.json"), jsonUtil.Serialize(customWeaponsManager.copyWeapons, true));
        }
        return Task.CompletedTask;
    }
}
