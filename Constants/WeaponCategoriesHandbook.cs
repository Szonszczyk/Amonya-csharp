namespace Amonya.Constants
{
    internal static class WeaponCategoriesHandbook
    {
        // Dictionary: ID → Plural Category Name
        public static readonly Dictionary<string, string> CategoryPlural = new()
        {
            ["5b5f78e986f77447ed5636b1"] = "Assault carbines",
            ["5b5f78fc86f77409407a7f90"] = "Assault rifles",
            ["5b5f798886f77447ed5636b5"] = "Bolt-action rifles",
            ["5b5f791486f774093f2ed3be"] = "DMRs",
            ["5b5f79a486f77409407a7f94"] = "LMGs",
            ["5b5f796a86f774093f2ed3c0"] = "SMGs",
            ["5b5f792486f77447ed5636b3"] = "Pistols",
            ["5b5f794b86f77409407a7f92"] = "Shotguns",
            //["5b5f792486f77447ed5636b3"] = "Revolvers",
            ["5b5f79d186f774093f2ed3c2"] = "Grenade launchers",
            ["5b5f752e86f774093e6cb505"] = "Underbarrel grenade launchers"
        };

        /// <summary>
        /// Get the plural category name by ID.
        /// Returns null if ID not found.
        /// </summary>
        public static string? GetPlural(string id)
        {
            return CategoryPlural.TryGetValue(id, out var result) ? result : null;
        }

    }
}
