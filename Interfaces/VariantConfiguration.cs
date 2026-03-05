using SPTarkov.Server.Core.Models.Common;

namespace Amonya.Interfaces
{
    public class VariantConfiguration
    {
        // [variant] Flavour text
        public string? FlavourText { get; set; }
        // [variant/item] Description
        public string? Description { get; set; }
        // [variant] Explanation of what is the variant doing
        public string? Explanation { get; set; }
        // [variant/item] ShortName
        public string? ShortName { get; set; }
        // [item] Item MongoId to clone
        public MongoId? ItemTplToClone { get; set; }

        // [variant/item] Dictionary for mixed-type values, needs to be set to type of default one or string => integer/float
        public Dictionary<string, object>? Properties { get; set; }
        // [variant/item] Changes made to slots/chambers/cartidges
        
        public ChangeSet? Changes { get; set; }
        // [N/A] IndividualChanges of given Weapon in Variant
        public Dictionary<string, IndividualChangeSet>? IndividualChanges { get; set; }
        // [variant/item] To add to trader for barter
        public CustomBarterConfig? Barter { get; set; }
        // [item] Handbook Price in Roubles
        public double? HandbookPriceRoubles { get; set; }

        // ### Additional variables only used with Bullets
        // [variant] Map Bullet name => Quest to add into
        public Dictionary<string, string>? Bullets { get; set; }
        // [variant] Weapon categories that this variant bullet can be used with
        public List<string>? WeaponCategories { get; set; }
        // [variant] Multiplier of original bullet price
        public double? Price { get; set; }
        // [variant/item] Background color and tracer color of bullet
        public string? Color { get; set; }
        // [item] To add a special slot
        public CustomItemConfig? CustomItemConfig { get; set; }
        // [item] Mod check
        public string? Mod { get; set; }
    }

    public class ChangeSet
    {
        // [variant/item] Change parent MongoId to other than this of base weapon
        public string? Parent { get; set; }
        public List<string>? AddtoInventorySlots { get; set; }
        // [variant] Minimum value of integer/float property
        public Dictionary<string, double>? Minimum { get; set; }

        public Dictionary<string, FilterSlotExtendedConfiguration>? Slots { get; set; }

        public FilterSlotExtendedConfiguration? Chambers { get; set; }
        // [item] Change type and count of cartidges in magazine
        public FilterSlotExtendedConfiguration? Cartridges { get; set; }
    }

    public class IndividualChangeSet
    {
        public Dictionary<string, object>? Properties { get; set; }

        public Dictionary<string, FilterSlotExtendedConfiguration>? Slots { get; set; }

        public FilterSlotExtendedConfiguration? Chambers { get; set; }
    }

    public class FilterSlotConfiguration
    {
        // [item] Count of cartidges in magazine
        public double? Count { get; set; }
        // [variant/item] Replace filter with this list
        public List<string>? Filter { get; set; }
        // [variant/item] Get filter from item id of the same slot/Chambers/Cartridges
        public List<string>? FromWeapons { get; set; }
        // [variant/item] Add to existing filter
        public List<string>? Add { get; set; }
    }

    public class FilterSlotExtendedConfiguration : FilterSlotConfiguration
    {
        public BasedOnConfiguration? BasedOn { get; set; }
    }

    public class BasedOnConfiguration
    {
        public string Property { get; set; } = string.Empty;

        public Dictionary<string, FilterSlotConfiguration> Cases { get; set; } = new();
    }
}
