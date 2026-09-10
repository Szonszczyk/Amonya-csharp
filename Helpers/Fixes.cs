using Amonya.CustomClasses;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;


namespace Amonya.Helpers;

[Injectable(InjectionType.Singleton)]
public class Fixes(
    CustomLogger logger,
    CustomBulletsManager customBulletsManager,
    IdDatabaseManager idDatabaseManager,
    ConfigLoader configLoader,
    TemplateTable templateTable,
    LocationTable locationTable
)
{
    public void Initialize()
    {
        FixLocationStaticAmmo();
        AdjustCustomItems();
    }

    private void FixLocationStaticAmmo()
    {
        foreach (var (_, location) in locationTable.GetDictionary())
        {
            if (location.StaticAmmo is null) continue;
            foreach (var (caliberId, bullets) in customBulletsManager.BulletsInCaliber)
            {
                if (caliberId == "Airsoft") continue;

                if (!location.StaticAmmo.TryGetValue(caliberId, out _))
                {
                    var list = new List<StaticAmmoDetails>();
                    foreach (var bulletId in bullets)
                    {
                        list.Add(new StaticAmmoDetails
                        {
                            Tpl = bulletId,
                            RelativeProbability = 1
                        });
                    }
                    //logger.LogWithColor($"[{GetType().Namespace}] Providing for {caliberId}, containing {bullets.Count} bullets!", LogTextColor.Red);
                    location.StaticAmmo.Add(caliberId, list);
                }
            }
        }
    }

    private void AdjustCustomItems()
    {
        var MoHsIds = new List<string>() { "Pocket Sized Mag of Holding", "Satchel Sized Mag of Holding", "Crate Sized Mag of Holding" };
        foreach (var MoH in MoHsIds)
        {
            templateTable.Items.TryGetValue(idDatabaseManager.GetCustomId($"{MoH}:ID"), out var item);
            if (item?.Properties?.LoadUnloadModifier != null)
            {
                configLoader.Config.MoHLoadingSpeed.TryGetValue(MoH, out var MoHLoadingSpeed);
                if (MoHLoadingSpeed < 0 && MoHLoadingSpeed > -100)
                {
                    item.Properties.LoadUnloadModifier = MoHLoadingSpeed;
                }
                else
                {
                    logger.Error($"Value {MoHLoadingSpeed} provided in \"MoHLoadingSpeed\" config option is incorrect. Should be between 0 and -100");
                }
            } else
            {
                logger.Error($"Item {MoH} provided in \"MoHLoadingSpeed\" config option is incorrect");
            }
        }
        
    }
}
