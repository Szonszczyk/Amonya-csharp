using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace Amonya.Helpers
{
    [Injectable(InjectionType.Singleton)]
    public class ModsCompatibility(
        IReadOnlyList<SptMod> modlist
        )
    {
        public List<SptMod> Modlist { get; } = [.. modlist];

        public bool ModCheck(string guid)
        {
            var mod = Modlist.Find(t => t.ModMetadata.ModGuid == guid);
            return mod is not null;
        }
    }
}