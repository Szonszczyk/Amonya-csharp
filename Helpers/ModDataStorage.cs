using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;

namespace Amonya.Helpers;

[Injectable(InjectionType.Singleton)]
public class ModDataStorage(
    TradersTable tradersTable,
    HideoutTable hideoutTable,
    HideoutConfig hideoutConfig,
    AirdropConfig airdropConfig,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    InventoryConfig inventoryConfig,
    BotTable botTable,
    LocationTable locationTable,
    GlobalTable globalTable,
    TemplateTable templateTable,
    LocaleService localeService
)
{
    public GlobalTable GlobalsData { get; private set; } = null!;
    public Dictionary<MongoId, TemplateItem> Items { get; private set; } = null!;
    public Dictionary<MongoId, Trader> Traders { get; private set; } = null!;
    public Dictionary<MongoId, Quest> Quests { get; private set; } = null!;
    public HideoutTable HideoutData { get; private set; } = null!;
    public HideoutConfig HideoutConfigData { get; private set; } = null!;
    public AirdropConfig ConfigServerAirdropConfig { get; private set; } = null!;
    public TraderConfig ConfigServerTraderConfig { get; private set; } = null!;
    public RagfairConfig ConfigServerRagfairConfig { get; private set; } = null!;
    public HandbookBase Handbook { get; private set; } = null!;
    public InventoryConfig InventoryConfigData { get; private set; } = null!;
    public Dictionary<string, string> LocaleEn { get; private set; } = null!;
    public LocationTable LocationsData { get; private set; } = null!;
    public BotTable Bots { get; private set; } = null!;

    public void Initialize()
    {
        GlobalsData = globalTable;
        Items = templateTable.Items;
        Traders = tradersTable;
        Quests = templateTable.Quests;
        HideoutData = hideoutTable;
        HideoutConfigData = hideoutConfig;
        ConfigServerAirdropConfig = airdropConfig;
        ConfigServerTraderConfig = traderConfig;
        ConfigServerRagfairConfig = ragfairConfig;
        Handbook = templateTable.Handbook;
        InventoryConfigData = inventoryConfig;
        LocaleEn = localeService.GetLocaleDb("en");
        LocationsData = locationTable;
        Bots = botTable;
    }

    public void RefreshDatabase()
    {
        LocaleEn = localeService.GetLocaleDb("en");
    }
}
