using System.Text.Json;
using BulkMailSender.Models;
using Microsoft.Extensions.Options;

namespace BulkMailSender.Services;

public class SettingsManager : ISettingsManager
{
    private readonly SettingsStorageService _storageService;
    private readonly EmailPresets _presets;
    private readonly ILogger<SettingsManager> _logger;

    public SettingsManager(
        SettingsStorageService storageService,
        IOptions<EmailPresets> presets,
        ILogger<SettingsManager> logger)
    {
        _storageService = storageService;
        _presets = presets.Value;
        _logger = logger;
    }

    public async Task<EmailSettings> GetActiveSettingsAsync()
    {
        // 1. Try to load from persistent storage
        var savedSettings = await _storageService.LoadSettingsAsync();
        
        if (savedSettings != null && !string.IsNullOrWhiteSpace(savedSettings.Host))
        {
            _logger.LogDebug("Using saved SMTP settings from storage.");
            return savedSettings;
        }

        // 2. Fall back to ProductionDefault
        _logger.LogInformation("No saved settings found. Using ProductionDefault preset.");
        return GetPreset("ProductionDefault");
    }

    public async Task SaveSettingsAsync(EmailSettings settings)
    {
        await _storageService.SaveSettingsAsync(settings);
        _logger.LogInformation("Settings saved to persistent storage.");
    }

    public async Task ClearSettingsAsync()
    {
        await _storageService.DeleteSettingsAsync();
        _logger.LogInformation("Settings cleared from persistent storage.");
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
        return Task.FromResult(_storageService.HasSavedSettings());
    }
}
