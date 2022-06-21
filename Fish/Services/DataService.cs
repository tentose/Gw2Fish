using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Fish.Services
{
    public interface IDataService
    {
        public IEnumerable<Models.Achievement> AllAchievements { get; set; }
        public IEnumerable<Models.Fish> AllFishes { get; set; }
        public IEnumerable<Gw2Api.AccountAchievement> AllAccountAchievements { get; set; }
        public event EventHandler ApiDataUpdated;
        public Task LoadAppData(bool force = false);
        public Task LoadAllData(bool force = false);
        public Task RefreshApiData(bool fireEvent = false);
    }

    public class DataService : IDataService
    {
        private const int API_REFRESH_INTERVAL_MILLIS = 3 * 60 * 1000;

        private HttpClient httpClient;
        private ISettingsService settingsService;

        private List<Models.Achievement>? allAchievements;
        private List<Models.Fish>? allFishes;
        private List<Gw2Api.AccountAchievement>? allAccountAchievements;

        private Timer? apiRefreshTimer;
        private ReaderWriterLock allDataLock = new ReaderWriterLock();

        public DataService(HttpClient httpClient, ISettingsService settingsService)
        {
            this.httpClient = httpClient;
            this.settingsService = settingsService;
        }

        public IEnumerable<Models.Achievement> AllAchievements
        {
            get => allAchievements ?? (allAchievements = new List<Models.Achievement>());
            set => allAchievements = value.ToList();
        }

        public IEnumerable<Models.Fish> AllFishes
        {
            get => allFishes ?? (allFishes = new List<Models.Fish>());
            set => allFishes = value.ToList();
        }

        public IEnumerable<Gw2Api.AccountAchievement> AllAccountAchievements
        {
            get => allAccountAchievements ?? (allAccountAchievements = new List<Gw2Api.AccountAchievement>());
            set => allAccountAchievements = value.ToList();
        }

        public event EventHandler ApiDataUpdated;
        private void OnApiDataUpdated() => ApiDataUpdated?.Invoke(this, EventArgs.Empty);

        public async Task LoadAppData(bool force = false)
        {
            if (allAchievements == null || allAchievements.Count() == 0 || force)
            {
                allAchievements = await httpClient.GetFromJsonAsync<List<Models.Achievement>>("fish-data/merged_achievement.json");
            }

            if (allFishes == null || allFishes.Count() == 0 || force)
            {
                allFishes = await httpClient.GetFromJsonAsync<List<Models.Fish>>("fish-data/merged_fish.json");
                if (allFishes != null)
                {
                    foreach (var fish in allFishes)
                    {
                        fish.RaritySort = RarityStringToInt(fish.Rarity);
                    }
                    allFishes.Sort((a, b) => a.RaritySort - b.RaritySort);
                }
            }
        }

        public async Task LoadAllData(bool force = false)
        {
            await LoadAppData();

            if (allAccountAchievements == null || allAccountAchievements.Count() == 0 || force)
            {
                await settingsService.InitializeSettings();

                await RefreshApiData();

                if (apiRefreshTimer == null)
                {
                    apiRefreshTimer = new Timer(cb => RefreshApiData(true), null, API_REFRESH_INTERVAL_MILLIS, API_REFRESH_INTERVAL_MILLIS);
                }
            }
        }

        public async Task RefreshApiData(bool fireEvent = false)
        {
            try
            { 
                allDataLock.AcquireWriterLock(500);

                try
                {
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
                                    a.Completed = aa.done;

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
                finally
                {
                    allDataLock.ReleaseWriterLock();
                }

                if (fireEvent)
                {
                    OnApiDataUpdated();
                }
            }
            catch (ApplicationException)
            {
                Console.WriteLine("Timedout acquiring write lock for RefreshApiData");
            }
        }

        private int RarityStringToInt(string rarity)
        {
            if (rarity == "Junk")
            {
                return 1;
            }
            else if (rarity == "Basic")
            {
                return 0;
            }
            else if (rarity == "Fine")
            {
                return -1;
            }
            else if (rarity == "Masterwork")
            {
                return -2;
            }
            else if (rarity == "Rare")
            {
                return -3;
            }
            else if (rarity == "Exotic")
            {
                return -4;
            }
            else if (rarity == "Ascended")
            {
                return -5;
            }
            else
            {
                return -6;
            }
        }
    }
}
