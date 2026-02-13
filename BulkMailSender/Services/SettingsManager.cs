using System.Text.Json;
using BulkMailSender.Models;
using Microsoft.Extensions.Options;

namespace BulkMailSender.Services;

public class SettingsManager : ISettingsManager
{
    private readonly SettingsStorageService _storageService;
    private readonly EmailPresets _presets;
    private readonly ILogger<SettingsManager> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private const string DefaultProfileKey = "CurrentProfileName";
    private const string DefaultProfile = "smtp-settings";

    public SettingsManager(
        SettingsStorageService storageService,
        IOptions<EmailPresets> presets,
        ILogger<SettingsManager> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _storageService = storageService;
        _presets = presets.Value;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public string CurrentProfileName
    {
        get
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var path = session?.GetString(DefaultProfileKey);
            return string.IsNullOrEmpty(path) ? DefaultProfile : path;
        }
    }

    public string GetActiveProfileName()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var profileName = session?.GetString(DefaultProfileKey);
        return string.IsNullOrEmpty(profileName) ? "Default" : profileName;
    }

    public async Task<EmailSettings> GetActiveSettingsAsync()
    {
        // 1. Try to load from persistent storage using current profile
        var savedSettings = await _storageService.LoadSettingsAsync(CurrentProfileName);
        
        if (savedSettings != null && !string.IsNullOrWhiteSpace(savedSettings.Host))
        {
            _logger.LogDebug("Using saved SMTP settings from storage (Profile: {Profile}).", CurrentProfileName);
            return savedSettings;
        }

        // 2. Fall back to ProductionDefault
        _logger.LogInformation("No saved settings found for profile {Profile}. Using ProductionDefault preset.", CurrentProfileName);
        return GetPreset("ProductionDefault");
    }

    public async Task SaveSettingsAsync(EmailSettings settings)
    {
        await _storageService.SaveSettingsAsync(settings, CurrentProfileName);
        await UpdateSessionAsync(settings);
        _logger.LogInformation("Settings saved to persistent storage (Profile: {Profile}) and session updated.", CurrentProfileName);
    }

    public EmailSettings GetPreset(string presetName)
    {
        EmailSettings? source = null;

        if (string.Equals(presetName, "Debug", StringComparison.OrdinalIgnoreCase))
        {
            source = _presets.Debug;
        }
        else if (string.Equals(presetName, "ProductionDefault", StringComparison.OrdinalIgnoreCase))
        {
            source = _presets.ProductionDefault;
        }

        // Fallback or empty if not found
        source ??= new EmailSettings();

        // return deep copy
        return Clone(source);
    }

    private static EmailSettings Clone(EmailSettings source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<EmailSettings>(json) ?? new EmailSettings();
    }

    public Task<bool> HasSavedSettingsAsync()
    {
        return Task.FromResult(_storageService.HasSavedSettings(CurrentProfileName));
    }

    public Task UpdateSessionAsync(EmailSettings settings)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString("SmtpSettings", JsonSerializer.Serialize(settings));
        }
        return Task.CompletedTask;
    }

    public async Task SwitchProfileAsync(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;

        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString(DefaultProfileKey, profileName);

            // Persist active profile choice to disk
            await _storageService.SaveActiveProfileNameAsync(profileName);

            _logger.LogInformation("Switched to profile: {Profile}", profileName);

            // Reload settings for new profile
            var settings = await GetActiveSettingsAsync();
            await UpdateSessionAsync(settings);
        }
    }

    public List<string> GetAvailableProfiles()
    {
        return _storageService.ListProfiles();
    }

    public async Task DeleteProfileAsync(string profileName)
    {
        // If deleting the currently active profile, switch to default first
        if (string.Equals(profileName, CurrentProfileName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Deleting active profile '{Profile}'. Switching to default before deletion.", profileName);
            await SwitchProfileAsync(DefaultProfile);
        }

        _storageService.DeleteProfileFile(profileName);
    }
}
