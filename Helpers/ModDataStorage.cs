using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace Amonya.Helpers;

[Injectable(InjectionType.Singleton)]
public class ModDataStorage()
{
    public Globals GlobalsData { get; private set; } = null!;
    public Dictionary<MongoId, TemplateItem> Items { get; private set; } = null!;
    public Dictionary<MongoId, Trader> Traders { get; private set; } = null!;
    public Dictionary<MongoId, Quest> Quests { get; private set; } = null!;
    public SPTarkov.Server.Core.Models.Spt.Hideout.Hideout HideoutData { get; private set; } = null!;
    public HideoutConfig HideoutConfigData { get; private set; } = null!;
    public AirdropConfig ConfigServerAirdropConfig { get; private set; } = null!;
    public TraderConfig ConfigServerTraderConfig { get; private set; } = null!;
    public RagfairConfig ConfigServerRagfairConfig { get; private set; } = null!;
    public HandbookBase Handbook { get; private set; } = null!;
    public InventoryConfig InventoryConfigData { get; private set; } = null!;
    public Dictionary<string, string> LocaleEn { get; private set; } = null!;
    public LocaleBase Locale { get; private set; } = null!;

    public void Initialize(DatabaseService databaseService, ConfigServer configServer, LocaleService localeService)
    {
        GlobalsData = databaseService.GetGlobals();
        Items = databaseService.GetItems();
        Traders = databaseService.GetTraders();
        Quests = databaseService.GetQuests();
        HideoutData = databaseService.GetHideout();
        HideoutConfigData = configServer.GetConfig<HideoutConfig>();
        ConfigServerAirdropConfig = configServer.GetConfig<AirdropConfig>();
        ConfigServerTraderConfig = configServer.GetConfig<TraderConfig>();
        ConfigServerRagfairConfig = configServer.GetConfig<RagfairConfig>();
        Handbook = databaseService.GetHandbook();
        InventoryConfigData = configServer.GetConfig<InventoryConfig>();
        LocaleEn = localeService.GetLocaleDb("en");
        Locale = databaseService.GetLocales();
    }

    public void RefreshDatabase(LocaleService localeService)
    {
        LocaleEn = localeService.GetLocaleDb("en");
    }
}
