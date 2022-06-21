using Blazored.LocalStorage;

namespace Fish.Services
{
    public interface ISettingsService
    {
        public Task InitializeSettings();

        public void SetDarkMode(bool dark);

        public void SetGw2ApiKey(string api);

        public event EventHandler DarkModeSettingChanged;

        public bool IsDarkMode { get; }
        public string Gw2ApiKey { get; }
    }

    public class SavedSettings
    {
        public bool IsDarkMode { get; set; }
        public string Gw2ApiKey { get; set; }
    }

    public class SettingsService : ISettingsService
    {
        private const string SETTINGS_KEY = "usersettings";

        private readonly ILocalStorageService _localStorage;

        private SavedSettings? savedSettings;

        public bool IsDarkMode { get; private set; }
        public string Gw2ApiKey { get; private set; }

        public SettingsService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task InitializeSettings()
        {
            savedSettings = await _localStorage.GetItemAsync<SavedSettings>(SETTINGS_KEY);
            if (savedSettings == null)
            {
                savedSettings = new SavedSettings();
                savedSettings.IsDarkMode = true;
                savedSettings.Gw2ApiKey = "";
                SaveSettings();
            }
            else
            {
                IsDarkMode = savedSettings.IsDarkMode;
                Gw2ApiKey = savedSettings.Gw2ApiKey;
            }
        }

        public event EventHandler DarkModeSettingChanged;
        private void OnDarkModeSettingChanged() => DarkModeSettingChanged?.Invoke(this, EventArgs.Empty);

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
    }
}
