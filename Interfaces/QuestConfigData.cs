namespace Amonya.Interfaces
{
    public class QuestConfig
    {
        public Dictionary<string, Difficulty> Difficulties { get; set; } = [];
        public Dictionary<string, List<string>> RequiresCategories { get; set; } = [];
        public List<string> BossNames { get; set; } = [];
    }
    public class Difficulty
    {
        public Dictionary<string, double> Requires { get; set; } = [];
        public Dictionary<string, double> Rewards { get; set; } = [];
    }
}
