namespace Amonya.Interfaces
{
    public class QuestData
    {
        public string Category { get; set; } = "Assault";
        public string QuestForStart { get; set; } = "Introduction";
        public CaliberInfo Caliber { get; set; } = new CaliberInfo();
        public string? Mod { get; set; }
        public Dictionary<string, QuestSettings> Quests { get; set; } = [];
    }

    public class QuestSettings
    {
        public List<string> Unlocks { get; set; } = [];
        public string Diff { get; set; } = "1";
        public int Trader { get; set; } = 1;
    }
}
