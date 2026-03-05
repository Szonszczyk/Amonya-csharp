using Amonya.CustomClasses;
using Amonya.Generators;
using Amonya.Helpers;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace Amonya;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2123)]
public class AmonyaTrader(
    DatabaseService databaseService,
    ConfigServer configServer,
    CustomTraderCreator customTraderCreator
) : IOnLoad
{
    public Task OnLoad()
    {
        customTraderCreator.Initialize(databaseService, configServer);
        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 97123)]
public class Amonya(
    ISptLogger<Amonya> logger,
    DatabaseService databaseService,
    ConfigServer configServer,
    LocaleService localeService,
    ConfigLoader configLoader,
    CustomBulletsManager customBulletsManager,
    CustomWeaponsManager customWeaponsManager,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    CustomSlotsChanger customSlotsChanger,
    ItemGenerator itemGenerator,
    CustomLocales customLocales,
    QuestGenerator questGenerator,
    BulletGenerator bulletGenerator,
    Fixes fixes
) : IOnLoad
{
    public Task OnLoad()
    {
        customBulletsManager.Initialize(databaseService);
        customWeaponsManager.Initialize(databaseService, localeService);
        customItemCreator.Initialize(databaseService, configServer);
        customSlotsChanger.Initialize(databaseService);

        fixes.Initialize(databaseService);

        itemGenerator.Initialize(databaseService);
        customLocales.Initialize(databaseService, localeService);
        questGenerator.Initialize(databaseService);
        bulletGenerator.Initialize(databaseService);

        if (configLoader.Config.EnableBulletVariants)
            bulletGenerator.GenerateBullets();
        customBulletsManager.ChangeCaliberStackSizes();

        if (configLoader.Config.EnableBulletQuests)
            questGenerator.GenerateQuests();

        idDatabaseManager.SaveDatabase();

        logger.LogWithColor($"[{GetType().Namespace}] Mod finished loading{(customItemCreator.itemsLoaded > 0 ? $". Created {customItemCreator.itemsLoaded} custom items!" : "")}", LogTextColor.Green);
        if (questGenerator.questsGenerated > 0)
            logger.LogWithColor($"[{GetType().Namespace}] Added {questGenerator.questsGenerated} custom quests!", LogTextColor.Green);
        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 2)]
public class AmonyaSlotBulletVariants(
    CustomWeaponsManager customWeaponsManager
    
) : IOnLoad
{
    public Task OnLoad()
    {
        customWeaponsManager.RefreshLoadedWeaponMagazines();
        customWeaponsManager.SlotNewBulletsIntoItems();
        return Task.CompletedTask;
    }
}
