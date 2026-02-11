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

    public string? TestResult { get; set; }
    public bool TestSuccess { get; set; }
    public bool HasSavedSettings { get; set; }

    public async Task OnGetAsync()
    {
        CurrentSettings = await _settingsManager.GetActiveSettingsAsync();
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();

        await _settingsManager.UpdateSessionAsync(CurrentSettings);
        
        if (HasSavedSettings)
        {
             _logger.LogInformation("Loaded settings from persistent storage and updated session");
        }
    }

    public async Task<IActionResult> OnPostUseDefaultAsync()
    {
        ModelState.Clear();
        CurrentSettings = _settingsManager.GetPreset("ProductionDefault");
        TestResult = "Production settings loaded. Please review and click 'Apply Changes' to save.";
        TestSuccess = true;
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUsePaperCutAsync()
    {
        ModelState.Clear();
        CurrentSettings = _settingsManager.GetPreset("Debug");
        TestResult = "Debug/Local settings loaded. Please review and click 'Apply Changes' to save.";
        TestSuccess = true;
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        await SaveToStorageAndSessionAsync();
        TestResult = " Settings saved successfully! They will persist across restarts.";
        TestSuccess = true;
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
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

        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
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
        HasSavedSettings = false;
        return Page();
    }

    private async Task SaveToStorageAndSessionAsync()
    {
        await _settingsManager.SaveSettingsAsync(CurrentSettings);
    }
}
