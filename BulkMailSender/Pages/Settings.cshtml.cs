using System.Text.Json;
using BulkMailSender.Models;
using BulkMailSender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Mail;
using System.Net;

namespace BulkMailSender.Pages;

public class SettingsModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsModel> _logger;
    private readonly ISettingsManager _settingsManager;
    private readonly IEmailService _emailService;

    public SettingsModel(
        IConfiguration configuration, 
        ILogger<SettingsModel> logger,
        ISettingsManager settingsManager,
        IEmailService emailService)
    {
        _configuration = configuration;
        _logger = logger;
        _settingsManager = settingsManager;
        _emailService = emailService;
    }

    [BindProperty]
    public EmailSettings CurrentSettings { get; set; } = new();

    [BindProperty]
    public string NewProfileName { get; set; } = string.Empty;

    public string ActiveProfile { get; set; } = string.Empty;
    public List<string> AvailableProfiles { get; set; } = new();

    public string? TestResult { get; set; }
    public bool TestSuccess { get; set; }
    public bool HasSavedSettings { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostUseDefaultAsync()
    {
        ModelState.Clear();
        CurrentSettings = _settingsManager.GetPreset("ProductionDefault");
        TestResult = "Production settings loaded. Please review and click 'Apply Changes' to save.";
        TestSuccess = true;
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUsePaperCutAsync()
    {
        ModelState.Clear();
        CurrentSettings = _settingsManager.GetPreset("Debug");
        TestResult = "Debug/Local settings loaded. Please review and click 'Apply Changes' to save.";
        TestSuccess = true;
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        await SaveToStorageAndSessionAsync();
        TestResult = " Settings saved successfully! They will persist across restarts.";
        TestSuccess = true;
        await LoadPageDataAsync();
        return Page();
    }
    
    public async Task<IActionResult> OnPostSaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName))
        {
            TestResult = "Profile Name is required.";
            TestSuccess = false;
            await LoadPageDataAsync();
            return Page();
        }

        // Switch to new profile (session update)
        await _settingsManager.SwitchProfileAsync(NewProfileName);
        
        // Save current form settings to this new profile
        await SaveToStorageAndSessionAsync();

        TestResult = $"Profile '{NewProfileName}' created and saved successfully.";
        TestSuccess = true;
        
        // Clear input
        NewProfileName = string.Empty;
        
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSwitchProfileAsync(string selectedProfile)
    {
        if (!string.IsNullOrWhiteSpace(selectedProfile))
        {
            await _settingsManager.SwitchProfileAsync(selectedProfile);
            // Reload settings from the new profile
            CurrentSettings = await _settingsManager.GetActiveSettingsAsync();
            
            TestResult = $"Switched to profile: {selectedProfile}";
            TestSuccess = true;
        }
        
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteProfileAsync(string selectedProfile)
    {
        if (!string.IsNullOrWhiteSpace(selectedProfile))
        {
            await _settingsManager.DeleteProfileAsync(selectedProfile);
            TestResult = $"Profile '{selectedProfile}' deleted.";
            TestSuccess = true;
            
            // If the deleted profile was active, we might want to revert logic or just let Fallback handle it.
            // For UI feedback, let's clear the ActiveProfile if it matches?
            if (string.Equals(ActiveProfile, selectedProfile, StringComparison.OrdinalIgnoreCase))
            {
                // We could force switch to default or just let it stay as "Active" but empty.
                // Switching to "smtp-settings" (Default) is safer.
                 await _settingsManager.SwitchProfileAsync("smtp-settings");
            }
        }
        
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        try
        {
            await _emailService.TestConnectionAsync(CurrentSettings);

            TestResult = $" SUCCESS! Test email sent to {CurrentSettings.FromEmail}. Check your inbox.";
            TestSuccess = true;
            await SaveToStorageAndSessionAsync();
        }
        catch (Exception ex)
        {
            TestResult = $" FAILED: {ex.Message}";
            TestSuccess = false;
            _logger.LogError(ex, "SMTP test failed");
        }

        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostClearSettingsAsync()
    {
        await _settingsManager.ClearSettingsAsync();
        
        // Reload default (active)
        CurrentSettings = await _settingsManager.GetActiveSettingsAsync();
        
        // Ensure session is updated with defaults
        await _settingsManager.UpdateSessionAsync(CurrentSettings);
        
        TestResult = "Saved settings cleared. Reverted to defaults.";
        TestSuccess = false;
        await LoadPageDataAsync();
        return Page();
    }

    private async Task SaveToStorageAndSessionAsync()
    {
        await _settingsManager.SaveSettingsAsync(CurrentSettings);
    }
    
    private async Task LoadPageDataAsync()
    {
        if (CurrentSettings == null || string.IsNullOrEmpty(CurrentSettings.Host))
        {
            CurrentSettings = await _settingsManager.GetActiveSettingsAsync();
        }
        
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
        ActiveProfile = _settingsManager.CurrentProfileName;
        AvailableProfiles = _settingsManager.GetAvailableProfiles();

        if (HttpContext.Request.Method == "GET")
        {
            await _settingsManager.UpdateSessionAsync(CurrentSettings);
            if (HasSavedSettings)
            {
                 _logger.LogInformation("Loaded settings from persistent storage and updated session");
            }
        }
    }
}
