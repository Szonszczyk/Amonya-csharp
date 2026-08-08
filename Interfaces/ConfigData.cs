namespace Amonya.Interfaces;

public class ConfigData
{
    public bool EnableNonPonyMode { get; set; } = false;

    public AmmoPriceConfig AmmoPrice { get; set; } = new AmmoPriceConfig();

    public TraderUpdate TraderUpdateTime { get; set; } = new TraderUpdate();
    public bool EnableBulletQuests { get; set; } = true;
    public bool QuestBarterFoundInRaid { get; set; } = true;
    public bool AddModdedBulletsToQuests { get; set; } = true;
    public bool EnableBulletVariants { get; set; } = true;
    public Dictionary<string, bool> EnableBullets { get; set; } = [];
    public string BulletVariantsShortName { get; set; } = "<variant_shortname> <caliber_shortname>";
    public string BulletVariantsName { get; set; } = "<bullet_name> <variant_fullname> <variant>";
    public Dictionary<string, int> CaliberStacks { get; set; } = new () { ["Caliber127x108"] = 60, ["Caliber40x46"] = 5, ["Caliber30x29"] = 5, ["Caliber40mmRU"] = 5, ["Caliber20x1mm"] = 200 };
    public Dictionary<string, int> MoHLoadingSpeed { get; set; } = new() { ["Pocket Sized Mag of Holding"] = -90, ["Satchel Sized Mag of Holding"] = -92, ["Crate Sized Mag of Holding"] = -94 };
    public Dictionary<string, Dictionary<string, double>> BulletRatingWeights { get; set; } = [];
    public bool CheckColorConverterAPI { get; set; } = true;
    public bool Debug { get; set; } = false;
    public DebugFileTypes DebugFiles { get; set; } = new DebugFileTypes();
}

public class AmmoPriceConfig
{
    public double Multiplier { get; set; } = 0.7;
    public bool UnlimitedCount { get; set; } = false;
    public int Max { get; set; } = 1000;
}

public class TraderUpdate
{
    public int Minimum { get; set; } = 60;
    public int Maximum { get; set; } = 120;
}

public class DebugFileTypes
{
    public bool Bullets { get; set; } = false;
    public bool Weapons { get; set; } = false;
    public bool Quests { get; set; } = false;
    public bool Locales { get; set; } = false;
    public bool Items { get; set; } = false;
}
