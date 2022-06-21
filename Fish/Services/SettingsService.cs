using Blazored.LocalStorage;

namespace Fish.Services
{
    public interface ISettingsService
    {
        public Task InitializeSettings();

        public void SetDarkMode(bool dark);

        public void SetGw2ApiKey(string api);

        public void SetHideCaughtFish(bool hide);

        public event EventHandler DarkModeSettingChanged;

        public event EventHandler HideCaughtFishChanged;

        public bool IsDarkMode { get; }
        public string Gw2ApiKey { get; }
        public bool HideCaughtFish { get; }
    }

    public class SavedSettings
    {
        public bool IsDarkMode { get; set; }
        public string Gw2ApiKey { get; set; }

        public bool HideCaughtFish { get; set; }
    }

    public class SettingsService : ISettingsService
    {
        private const string SETTINGS_KEY = "usersettings";

        private readonly ILocalStorageService _localStorage;

        private SavedSettings? savedSettings;

        private SemaphoreSlim dataLock = new SemaphoreSlim(1, 1);
        private bool loaded = false;

        public bool IsDarkMode { get; private set; }
        public string Gw2ApiKey { get; private set; }

        public bool HideCaughtFish { get; private set; }

        public SettingsService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task InitializeSettings()
        {
            await dataLock.WaitAsync();

            try
            {
                if (!loaded)
                {
                    savedSettings = await _localStorage.GetItemAsync<SavedSettings>(SETTINGS_KEY);
                    if (savedSettings == null)
                    {
                        savedSettings = new SavedSettings();
                        savedSettings.IsDarkMode = true;
                        savedSettings.Gw2ApiKey = "";
                        savedSettings.HideCaughtFish = false;
                        SaveSettings();
                    }
                    IsDarkMode = savedSettings.IsDarkMode;
                    Gw2ApiKey = savedSettings.Gw2ApiKey;
                    HideCaughtFish = savedSettings.HideCaughtFish;
                    loaded = true;
                }
            }
            finally
            {
                dataLock.Release();
            }
        }

        public event EventHandler DarkModeSettingChanged;
        private void OnDarkModeSettingChanged() => DarkModeSettingChanged?.Invoke(this, EventArgs.Empty);

        public event EventHandler HideCaughtFishChanged;
        private void OnHideCaughtFishChangedd() => HideCaughtFishChanged?.Invoke(this, EventArgs.Empty);

        private void SaveSettings()
        {
            _localStorage.SetItemAsync(SETTINGS_KEY, savedSettings);
        }

        public void SetDarkMode(bool dark)
        {
            if (dark != IsDarkMode)
            {
                IsDarkMode = dark;
                savedSettings.IsDarkMode = dark;
                SaveSettings();
                OnDarkModeSettingChanged();
            }
        }

        public void SetGw2ApiKey(string api)
        {
            Gw2ApiKey = api;
            savedSettings.Gw2ApiKey = api;
            SaveSettings();
        }

        public void SetHideCaughtFish(bool hide)
        {
            if (hide != HideCaughtFish)
            {
                HideCaughtFish = hide;
                savedSettings.HideCaughtFish = hide;
                SaveSettings();
                OnHideCaughtFishChangedd();
            }
        }
    }
}
