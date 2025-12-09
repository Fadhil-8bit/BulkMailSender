using System.Text.Json;
using BulkMailSender.Models;

namespace BulkMailSender.Services;

/// <summary>
/// Service for persisting SMTP settings to a JSON file
/// </summary>
public class SettingsStorageService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsStorageService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public SettingsStorageService(IWebHostEnvironment env, ILogger<SettingsStorageService> logger)
    {
        _logger = logger;
        // Store settings in App_Data folder (persistent across runs)
        var appDataPath = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        _settingsFilePath = Path.Combine(appDataPath, "smtp-settings.json");
    }

    /// <summary>
    /// Save SMTP settings to JSON file
    /// </summary>
    public async Task<bool> SaveSettingsAsync(SmtpSettings settings)
    {
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger.LogInformation("SMTP settings saved to {FilePath}", _settingsFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save SMTP settings to file");
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load SMTP settings from JSON file
    /// </summary>
    public async Task<SmtpSettings?> LoadSettingsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.LogInformation("No saved settings file found at {FilePath}", _settingsFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<SmtpSettings>(json);
            
            if (settings != null)
            {
                _logger.LogInformation("SMTP settings loaded from {FilePath}", _settingsFilePath);
            }
            
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SMTP settings from file");
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Check if saved settings exist
    /// </summary>
    public bool HasSavedSettings()
    {
        return File.Exists(_settingsFilePath);
    }

    /// <summary>
    /// Delete saved settings file
    /// </summary>
    public async Task<bool> DeleteSettingsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                File.Delete(_settingsFilePath);
                _logger.LogInformation("Saved settings deleted from {FilePath}", _settingsFilePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete saved settings");
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
