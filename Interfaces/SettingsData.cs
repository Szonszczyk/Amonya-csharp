
namespace Amonya.Interfaces
{
    public class SettingsData
    {
        public List<string> BaseWeaponExceptions { get; set; } = [];
        public Dictionary<string, string> CopyWeaponExceptions { get; set; } = [];
    }
}