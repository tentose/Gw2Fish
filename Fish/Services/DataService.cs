using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Fish.Services
{
    public interface IDataService
    {
        public IEnumerable<Models.Achievement> AllAchievements { get; set; }
        public IEnumerable<Models.Achievement> TitleAchievements { get; set; }
        public IEnumerable<Models.Achievement> DailyAchievements { get; set; }
        public IEnumerable<Models.Fish> AllFishes { get; set; }
        public IEnumerable<Gw2Api.AccountAchievement> AllAccountAchievements { get; set; }
        public List<Models.Achievement> GetAllAchievementsInProgressOrder();
        public event EventHandler ApiDataUpdated;
        public Task LoadAppData(bool force = false);
        public Task LoadAllData(bool force = false);
        public Task RefreshApiData(bool fireEvent = false);
    }

    public class DataService : IDataService
    {
        private const int API_REFRESH_INTERVAL_MILLIS = 3 * 60 * 1000;
        private const int API_RETRY_INTERVAL_MILLIS = 20 * 1000;

        private HttpClient httpClient;
        private ISettingsService settingsService;

        private List<Models.Achievement>? allAchievements;
        private List<Models.Achievement>? titleAchievements;
        private List<Models.Achievement>? dailyAchievements;
        private List<Models.Fish>? allFishes;
        private List<Gw2Api.AccountAchievement>? allAccountAchievements;

        private Timer? apiRefreshTimer;
        private SemaphoreSlim dataLock = new SemaphoreSlim(1, 1);
        private DateTime lastDailyRefresh = DateTime.MinValue;

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

        public IEnumerable<Models.Achievement> TitleAchievements
        {
            get => titleAchievements ?? (titleAchievements = new List<Models.Achievement>());
            set => titleAchievements = value.ToList();
        }

        public IEnumerable<Models.Achievement> DailyAchievements
        {
            get => dailyAchievements ?? (dailyAchievements = new List<Models.Achievement>());
            set => dailyAchievements = value.ToList();
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

        public List<Models.Achievement> GetAllAchievementsInProgressOrder()
        {
            var achievementsWithOrder = AllAchievements.Select(a =>
            {
                int order = 0;

                if (a.Repeated > 0)
                {
                    order += 100;
                }
                else if (a.Completed)
                {
                    order += 500;
                }

                order += 100 - (int)(100.0f * a.CurrentProgress / a.PointRequirement);

                return (order, a);
            }).ToList();
            achievementsWithOrder.Sort((a, b) => a.order.CompareTo(b.order));
            return achievementsWithOrder.Select(ao => ao.a).ToList();
        }

        public event EventHandler ApiDataUpdated;
        private void OnApiDataUpdated() => ApiDataUpdated?.Invoke(this, EventArgs.Empty);

        public async Task LoadAppData(bool force = false)
        {
            await dataLock.WaitAsync();

            try
            {
                //allDataLock.AcquireWriterLock(30000);

                if (allAchievements == null || allAchievements.Count() == 0 || force)
                {
                    allAchievements = await httpClient.GetFromJsonAsync<List<Models.Achievement>>("fish-data/merged_achievement.json");
                }

                if (titleAchievements == null || titleAchievements.Count() == 0 || force)
                {
                    titleAchievements = await httpClient.GetFromJsonAsync<List<Models.Achievement>>("fish-data/title_achievements.json");
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
            finally
            {
                dataLock.Release();
            }
        }

        public async Task LoadAllData(bool force = false)
        {
            await LoadAppData();

            await dataLock.WaitAsync();

            try
            {
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
            finally
            {
                dataLock.Release();
            }
        }

        public async Task RefreshApiData(bool fireEvent = false)
        {
            try
            {
                if (settingsService.Gw2ApiKey != "")
                {
                    await RefreshAccountBasedApiData();
                }

                await RefreshDailydApiData();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed API refresh: " + e.Message);
                Console.WriteLine("Retry in " + API_RETRY_INTERVAL_MILLIS);
                apiRefreshTimer?.Change(API_RETRY_INTERVAL_MILLIS, API_REFRESH_INTERVAL_MILLIS);
            }

            if (fireEvent)
            {
                OnApiDataUpdated();
            }
        }

        private async Task RefreshDailydApiData()
        {
            DateTime currentDailyReset = DateTime.UtcNow;
            if (dailyAchievements != null && dailyAchievements.Count() > 0 && currentDailyReset.Date <= lastDailyRefresh.Date)
            {
                return;
            }

            // Always reset daily achievements so we don't show stale data
            dailyAchievements = new List<Models.Achievement>();

            var dailyEodAchievementCategory = await httpClient.GetFromJsonAsync<Gw2Api.AchievementCategory>("https://api.guildwars2.com/v2/achievements/categories/321");

            if (dailyEodAchievementCategory != null && dailyEodAchievementCategory.name == "Daily End of Dragons")
            {
                var dailyEodAchievements = await httpClient.GetFromJsonAsync<List<Gw2Api.Achievement>>("https://api.guildwars2.com/v2/achievements?ids=" + string.Join(',', dailyEodAchievementCategory.achievements));

                if (dailyEodAchievements != null)
                {
                    foreach (var dailyAchievement in dailyEodAchievements)
                    {
                        if (dailyAchievement.name.StartsWith("Daily") && dailyAchievement.name.EndsWith("Fisher"))
                        {
                            var achievement = new Models.Achievement();
                            achievement.Id = dailyAchievement.id;
                            achievement.Name = dailyAchievement.name;
                            achievement.Description = dailyAchievement.requirement;

                            dailyAchievements.Add(achievement);
                            lastDailyRefresh = DateTime.UtcNow;
                        }
                    }
                }
            }
        }

        private async Task RefreshAccountBasedApiData()
        {
            allAccountAchievements = await httpClient.GetFromJsonAsync<List<Gw2Api.AccountAchievement>>("https://api.guildwars2.com/v2/account/achievements?access_token=" + settingsService.Gw2ApiKey);

            if (allAccountAchievements != null && allAchievements != null && allFishes != null && titleAchievements != null)
            {
                // Merge account achievements into the achievement data

                // Some optimization (maybe) by doing a range comparison first instead of searching
                IEnumerable<int> fishingAchievementIds = allAchievements.Select(a => a.Id).Concat(titleAchievements.Select(a => a.Id));
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
                    if (aa != null)
                    {
                        if (aa.bits != null)
                        {
                            var bitIds = aa.bits.Select(bit => a.BitIds[bit]);
                            a.CompletedBitIds = bitIds.ToArray();
                            caughtFishIds.AddRange(bitIds);
                        }
                        else
                        {
                            a.CompletedBitIds = new int[] { };

                            // If an achievement is completed, it won't have CompletedBitIds
                            if (aa.done || aa.repeated > 0)
                            {
                                caughtFishIds.AddRange(a.BitIds);
                            }
                        }
                        a.Completed = aa.done;
                        a.CurrentProgress = aa.current;
                        a.PointRequirement = aa.max;
                        a.Repeated = aa.repeated;
                    }
                    else
                    {
                        a.PointRequirement = a.BitIds.Length;
                    }
                    return a;
                }).ToList();

                titleAchievements = titleAchievements.GroupJoin(fishingAccountAchievements, a => a.Id, aa => aa.id, (a, aas) =>
                {
                    var aa = aas.FirstOrDefault();
                    if (aa != null && aa.bits != null)
                    {
                        a.CurrentProgress = aa.current;
                        a.Completed = aa.done;
                    }
                    return a;
                }).ToList();
                titleAchievements.Sort((a, b) => a.PointRequirement.CompareTo(b.PointRequirement));

                // Mark all caught fishes as caught
                foreach (var fish in allFishes)
                {
                    fish.Caught = caughtFishIds.Contains(fish.Id);
                }
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
