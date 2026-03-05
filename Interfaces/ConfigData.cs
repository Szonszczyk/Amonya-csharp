namespace Amonya.Interfaces
{
    public class ConfigData
    {
        public bool EnableNonPonyMode { get; set; } = false;

        public AmmoPriceConfig AmmoPrice {  get; set; } = new AmmoPriceConfig();
        public bool EnableBulletQuests { get; set; } = true;
        public bool EnableBulletVariants { get; set; } = true;
        public Dictionary<string, bool> EnableBullets { get; set; } = [];
        public string BulletVariantsShortName { get; set; } = "<variant_shortname> <caliber_shortname>";
        public Dictionary<string, int> CaliberStacks { get; set; } = [];
        public bool CheckColorConverterAPI { get; set; } = true;
        public bool Debug { get; set; } = false;
    }

    public class AmmoPriceConfig
    {
        public double Multiplier { get; set; } = 0.7;
        public bool UnlimitedCount { get; set; } = false;
        public int Max { get; set; } = 1000;
    }
}
