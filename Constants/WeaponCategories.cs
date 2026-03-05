namespace Amonya.Constants
{
    internal static class WeaponCategories
    {
        // Dictionary: ID → Plural Category Name
        public static readonly Dictionary<string, string> CategoryPlural = new()
        {
            ["5447b5fc4bdc2d87278b4567"] = "Assault carbines",
            ["5447b5f14bdc2d61278b4567"] = "Assault rifles",
            ["5447b6254bdc2dc3278b4568"] = "Bolt-action rifles",
            ["5447b6194bdc2d67278b4567"] = "DMRs",
            ["5447bed64bdc2d97278b4568"] = "LMGs",
            ["5447b5e04bdc2d62278b4567"] = "SMGs",
            ["5447b5cf4bdc2d65278b4567"] = "Pistols",
            ["5447b6094bdc2dc3278b4567"] = "Shotguns",
            ["617f1ef5e8b54b0998387733"] = "Revolvers",
            ["5447bedf4bdc2d87278b4568"] = "Grenade launchers"
        };

        // Dictionary: ID → Singular Category Name
        public static readonly Dictionary<string, string> CategorySingular = new()
        {
            ["5447b5fc4bdc2d87278b4567"] = "Assault carbine",
            ["5447b5f14bdc2d61278b4567"] = "Assault rifle",
            ["5447b6254bdc2dc3278b4568"] = "Bolt-action rifle",
            ["5447b6194bdc2d67278b4567"] = "DMR",
            ["5447bed64bdc2d97278b4568"] = "LMG",
            ["5447b5e04bdc2d62278b4567"] = "SMG",
            ["5447b5cf4bdc2d65278b4567"] = "Pistol",
            ["5447b6094bdc2dc3278b4567"] = "Shotgun",
            ["617f1ef5e8b54b0998387733"] = "Revolver",
            ["5447bedf4bdc2d87278b4568"] = "Grenade launcher"
        };

        /// <summary>
        /// Returns a list of all category IDs.
        /// </summary>
        public static List<string> GetAllIds()
        {
            return CategoryPlural.Keys.ToList();
        }
        /// <summary>
        /// Returns a list of all category IDs.
        /// </summary>
        public static List<string> GetAllPlural()
        {
            return CategoryPlural.Values.ToList();
        }

        /// <summary>
        /// Get the plural category name by ID.
        /// Returns null if ID not found.
        /// </summary>
        public static string? GetPlural(string id)
        {
            return CategoryPlural.TryGetValue(id, out var result) ? result : null;
        }

        /// <summary>
        /// Get the singular category name by ID.
        /// Returns null if ID not found.
        /// </summary>
        public static string? GetSingular(string id)
        {
            return CategorySingular.TryGetValue(id, out var result) ? result : null;
        }
    }
}
