namespace Models
{
    public class Achievement
    {
        // From JSON
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[] Icon { get; set; }
        public int[] BitIds { get; set; }
        public string Region { get; set; }

        // Filled in at run time
        public int[] CompletedBitIds { get; set; }
        public bool Completed { get; set; } = false;
    }

    public class Fish
    {
        // From JSON
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { set; get; }
        public string Rarity { set; get; }
        public string ChatLink { set; get; }
        public string[] Area { set; get; }
        public string[] Hole { get; set; }
        public string[] Bait { get; set; }
        public string[] Time { get; set; }
        public string Hint { get; set; }
        public int[] AchievementIds { get; set; }

        // Filled in at run time
        public bool Caught { get; set; } = false;
        public int RaritySort { set; get; }
    }
}