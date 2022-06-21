namespace Gw2Api
{
    public class AccountAchievement
    {
        public int id { get; set; }
        public int[]? bits { get; set; }
        public int current { get; set; }
        public int max { get; set; }
        public bool done { get; set; }
        public int repeated { get; set; }
        public bool unlocked { get; set; }
    }

    public class TokenInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string[] permissions { get; set; }
    }

    public class AchievementCategory
    { 
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int[] achievements { get; set; }
    }

    public class Achievement
    {
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string requirement { get; set; }
    }
}
