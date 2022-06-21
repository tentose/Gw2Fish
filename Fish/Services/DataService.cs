using System.Net.Http;
using System.Net.Http.Json;

namespace Fish.Services
{
    public interface IDataService
    {
        public IEnumerable<Models.Achievement> AllAchievements { get; set; }
        public IEnumerable<Models.Fish> AllFishes { get; set; }
        public IEnumerable<Gw2Api.AccountAchievement> AllAccountAchievements { get; set; }
        public Task LoadAppData();
        public Task LoadAllData();
    }

    public class DataService : IDataService
    {
        private HttpClient httpClient;
        private ISettingsService settingsService;

        private IEnumerable<Models.Achievement>? allAchievements;
        private IEnumerable<Models.Fish>? allFishes;
        private IEnumerable<Gw2Api.AccountAchievement>? allAccountAchievements;

        public DataService(HttpClient httpClient, ISettingsService settingsService)
        {
            this.httpClient = httpClient;
            this.settingsService = settingsService;
        }

        public IEnumerable<Models.Achievement> AllAchievements
        {
            get => allAchievements ?? (allAchievements = new List<Models.Achievement>());
            set => allAchievements = value;
        }

        public IEnumerable<Models.Fish> AllFishes
        {
            get => allFishes ?? (allFishes = new List<Models.Fish>());
            set => allFishes = value;
        }

        public IEnumerable<Gw2Api.AccountAchievement> AllAccountAchievements
        {
            get => allAccountAchievements ?? (allAccountAchievements = new List<Gw2Api.AccountAchievement>());
            set => allAccountAchievements = value;
        }

        public async Task LoadAppData()
        {
            if (allAchievements == null || allAchievements.Count() == 0)
            {
                allAchievements = await httpClient.GetFromJsonAsync<List<Models.Achievement>>("fish-data/merged_achievement.json");
            }

            if (allFishes == null || allFishes.Count() == 0)
            {
                allFishes = await httpClient.GetFromJsonAsync<List<Models.Fish>>("fish-data/merged_fish.json");
            }
        }

        public async Task LoadAllData()
        {
            await LoadAppData();

            if (allAccountAchievements == null || allAccountAchievements.Count() == 0)
            {
                await settingsService.InitializeSettings();
                if (settingsService.Gw2ApiKey != "")
                {
                    allAccountAchievements = await httpClient.GetFromJsonAsync<List<Gw2Api.AccountAchievement>>("https://api.guildwars2.com/v2/account/achievements?access_token=" + settingsService.Gw2ApiKey);

                    if (allAccountAchievements != null && allAchievements != null && allFishes != null)
                    {
                        // Merge account achievements into the achievement data

                        // Some optimization (maybe) by doing a range comparison first instead of searching
                        IEnumerable<int> fishingAchievementIds = allAchievements.Select(a => a.Id);
                        int minAchievementId = fishingAchievementIds.Min();
                        int maxAchievementId = fishingAchievementIds.Max();

                        var fishingAccountAchievements = allAccountAchievements.Where(aa =>
                                                                    aa.id >= minAchievementId &&
                                                                    aa.id <= maxAchievementId &&
                                                                    fishingAchievementIds.Contains(aa.id));

                        List<int> caughtFishIds = new();

                        allAchievements = allAchievements.GroupJoin(fishingAccountAchievements, a => a.Id, aa => aa.id, (a, aas) =>
                        {
                            var aa = aas.FirstOrDefault();
                            if (aa != null && aa.bits != null)
                            {
                                var bitIds = aa.bits.Select(bit => a.BitIds[bit]);
                                a.CompletedBitIds = bitIds.ToArray();
                                a.Completed = aa.done || aa.repeated > 0;

                                // If an achievement is completed, it won't have CompletedBitIds
                                if (a.Completed)
                                {
                                    caughtFishIds.AddRange(a.BitIds);
                                }
                                else
                                {
                                    caughtFishIds.AddRange(bitIds);
                                }
                            }
                            return a;
                        }).ToList();

                        // Mark all caught fishes as caught
                        foreach (var fish in allFishes)
                        {
                            fish.Caught = caughtFishIds.Contains(fish.Id);
                        }
                    }
                }
            }
        }
    }
}
