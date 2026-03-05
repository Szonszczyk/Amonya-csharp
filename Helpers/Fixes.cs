using Amonya.CustomClasses;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;


namespace Amonya.Helpers
{
    [Injectable(InjectionType.Singleton)]
    public class Fixes(
        ISptLogger<Amonya> logger,
        CustomBulletsManager customBulletsManager
    )
    {
        private Dictionary<string, Location> Locations { get; set; } = [];
        public void Initialize(DatabaseService databaseService)
        {
            Locations = databaseService.GetLocations().GetDictionary();
            FixLocationStaticAmmo();
        }

        private void FixLocationStaticAmmo()
        {
            foreach (var (_, location) in Locations)
            {
                if (location.StaticAmmo is null) continue;
                foreach (var (caliberId, bullets) in customBulletsManager.BulletsInCaliber)
                {
                    if (caliberId == "Airsoft") continue;

                    if (!location.StaticAmmo.TryGetValue(caliberId, out _))
                    {
                        //logger.LogWithColor($"[{GetType().Namespace}] Providing for {caliberId}, containing {bullets.Count} bullets!", LogTextColor.Red);
                        location.StaticAmmo.Add(caliberId, CreateListStaticAmmoDetails(bullets));
                    }
                }
            }
        }

        private static List<StaticAmmoDetails> CreateListStaticAmmoDetails(List<MongoId> bullets)
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
            return list;
        }
    }
}
