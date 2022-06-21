namespace Models
{
    public class Achievement
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[] Icon { get; set; }
        public int[] BitIds { get; set; }
    }

    public class Fish
    {
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
    }
}