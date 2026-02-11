using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using BulkMailSender.Models;
using BulkMailSender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class TemplateModel : PageModel
{
    private readonly ILogger<TemplateModel> _logger;
    private readonly IEmailTemplateService _templateService;

    public TemplateModel(ILogger<TemplateModel> logger, IEmailTemplateService templateService)
    {
        _logger = logger;
        _templateService = templateService;
    }

    public bool HasUploadData { get; set; }

    [BindProperty]
    public TemplateType? TemplateType { get; set; }

    [BindProperty]
    public string? Period { get; set; }

    [BindProperty]
    public string DebtorCode { get; set; } = string.Empty;

    // Available debtor codes from recipients
    public List<DebtorCodeOption> AvailableDebtorCodes { get; set; } = new();

    // Selected debtor info for preview
    public string? SelectedOrganizationName { get; set; }
    public string? SelectedNotes { get; set; }

    public string? SubjectPreview { get; set; }
    public string? BodyPreview { get; set; }

    [BindProperty]
    public string EditSubject { get; set; } = string.Empty;

    [BindProperty]
    public string EditBody { get; set; } = string.Empty;

    public Dictionary<string, string> RawTemplates { get; set; } = new();

    public void OnGet()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        
        // Load raw templates for customization
        RawTemplates["SoaInv_Subject"] = _templateService.GetRawSubject(Models.TemplateType.SoaInv);
        RawTemplates["SoaInv_Body"] = _templateService.GetRawBody(Models.TemplateType.SoaInv);
        RawTemplates["Overdue_Subject"] = _templateService.GetRawSubject(Models.TemplateType.Overdue);
        RawTemplates["Overdue_Body"] = _templateService.GetRawBody(Models.TemplateType.Overdue);

        // Check if there's a saved template and pre-populate fields
        var templateJson = HttpContext.Session.GetString("EmailTemplate");
        if (!string.IsNullOrEmpty(templateJson))
        {
            try
            {
                var savedTemplate = JsonSerializer.Deserialize<SavedTemplate>(templateJson);
                if (savedTemplate != null)
                {
                    TemplateType = savedTemplate.TemplateType;
                    Period = savedTemplate.Period;
                    _logger.LogInformation("Loaded existing template from session. Type: {Type}, Period: {Period}", TemplateType, Period);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load saved template from session");
            }
        }
        else
        {
            AttemptAutoSelectTemplate();
        }
    }

    private void AttemptAutoSelectTemplate()
    {
        var uploadResultJson = HttpContext.Session.GetString("UploadResult");
        if (string.IsNullOrEmpty(uploadResultJson)) return;

        try
        {
            var uploadResult = JsonSerializer.Deserialize<UploadResult>(uploadResultJson);
            if (uploadResult?.DebtorAttachments == null) return;

            int overdueCount = 0;
            int soaInvCount = 0;

            foreach (var da in uploadResult.DebtorAttachments)
            {
                if (da.OverdueFiles.Any() || !string.IsNullOrEmpty(da.OverdueFile)) overdueCount++;
                if (da.InvoiceFiles.Any() || !string.IsNullOrEmpty(da.InvoiceFile) || 
                    da.StatementFiles.Any() || !string.IsNullOrEmpty(da.StatementFile)) soaInvCount++;
            }

            if (overdueCount > soaInvCount)
            {
                TemplateType = Models.TemplateType.Overdue;
                _logger.LogInformation("Auto-selected Overdue template. Overdue: {Overdue}, SoaInv: {SoaInv}", overdueCount, soaInvCount);
            }
            else if (soaInvCount > 0)
            {
                TemplateType = Models.TemplateType.SoaInv;
                _logger.LogInformation("Auto-selected SoaInv template. Overdue: {Overdue}, SoaInv: {SoaInv}", overdueCount, soaInvCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-select template from upload data");
        }
    }

    public IActionResult OnPostPreview()
    {
        // CRITICAL: Clear ModelState to prevent validation errors from [BindProperty] fields
        // that might be incomplete or validated differently during preview
        ModelState.Clear();
        
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        
        // Get values from form directly (using the main field names)
        var colTypeStr = Request.Form["TemplateType"].ToString();
        var colPeriodStr = Request.Form["Period"].ToString();
        
        // Parse template type
        if (!string.IsNullOrEmpty(colTypeStr) && Enum.TryParse<Models.TemplateType>(colTypeStr, out var parsedType))
        {
            TemplateType = parsedType;
        }
        
        Period = colPeriodStr;
        
        LoadSelectedDebtorInfo();

        // Validate fields for preview
        if (!TemplateType.HasValue)
        {
            TempData["ErrorMessage"] = "Please select an Email Type to preview.";
            return Page();
        }

        if (TemplateType == Models.TemplateType.SoaInv && string.IsNullOrWhiteSpace(Period))
        {
            TempData["ErrorMessage"] = "Please enter a Period (e.g., OCT 2025) to preview.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(DebtorCode))
        {
            TempData["ErrorMessage"] = "Please select a Debtor Code for preview.";
            return Page();
        }

        var orgName = string.IsNullOrWhiteSpace(SelectedOrganizationName) ? "{organization name}" : SelectedOrganizationName;
        var notes = string.IsNullOrWhiteSpace(SelectedNotes) ? "{notes}" : SelectedNotes;

        SubjectPreview = _templateService.BuildSubject(TemplateType.Value, Period, DebtorCode, orgName);
        BodyPreview = _templateService.BuildBody(TemplateType.Value, notes);
        
        _logger.LogInformation("Preview generated for debtor: {DebtorCode}, Type: {Type}, Period: {Period}", 
            DebtorCode, TemplateType, Period);
            
        return Page();
    }

    public IActionResult OnPost()
    {
        // This is the "Next" action
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        
        // 1. Validate
        if (!TemplateType.HasValue || (TemplateType == Models.TemplateType.SoaInv && string.IsNullOrWhiteSpace(Period)))
        {
             LoadAvailableDebtorCodes();
             ModelState.AddModelError("", "Please configure Email Type and Period first.");
             TempData["ErrorMessage"] = "Please complete the configuration.";
             return Page();
        }
        
        // 2. Save
        var template = new SavedTemplate
        {
            TemplateType = TemplateType.Value,
            Period = Period?.Trim() ?? string.Empty,
            DebtorCode = "{debtor code}",
            OrganizationName = "{organization name}",
            Notes = "{notes}",
            Subject = _templateService.BuildSubject(TemplateType.Value, Period, "{debtor code}", "{organization name}"),
            Body = _templateService.BuildBody(TemplateType.Value, "{notes}")
        };
        
        HttpContext.Session.SetString("EmailTemplate", JsonSerializer.Serialize(template));
        
        // 3. Redirect to Preview
        return RedirectToPage("/Preview");
    }

    public IActionResult OnPostSave()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();

        // Validate
        if (!TemplateType.HasValue || (TemplateType == Models.TemplateType.SoaInv && string.IsNullOrWhiteSpace(Period)))
        {
             TempData["ErrorMessage"] = "Please complete all fields in Step 1 before saving.";
             return Page();
        }

        // Create and save template to session
        var template = new SavedTemplate
        {
            TemplateType = TemplateType.Value,
            Period = Period?.Trim() ?? string.Empty,
            DebtorCode = "{debtor code}",
            OrganizationName = "{organization name}",
            Notes = "{notes}",
            Subject = _templateService.BuildSubject(TemplateType.Value, Period, "{debtor code}", "{organization name}"),
            Body = _templateService.BuildBody(TemplateType.Value, "{notes}")
        };
        
        try
        {
            HttpContext.Session.SetString("EmailTemplate", JsonSerializer.Serialize(template));
            _logger.LogInformation("Draft template saved. Type: {Type}, Period: {Period}", TemplateType.Value, Period);
            TempData["SuccessMessage"] = "Draft saved successfully. Ready for review.";
            
            // Generate preview for current page if user hasn't previewed yet (optional, but good UX)
             if (string.IsNullOrEmpty(SubjectPreview) && !string.IsNullOrEmpty(DebtorCode))
            {
               LoadSelectedDebtorInfo();
               SubjectPreview = _templateService.BuildSubject(TemplateType.Value, Period, DebtorCode, SelectedOrganizationName ?? "{org}");
               BodyPreview = _templateService.BuildBody(TemplateType.Value, SelectedNotes ?? "{notes}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save template");
            TempData["ErrorMessage"] = "Failed to save template.";
            return Page();
        }
        
        // Removed redirect, just stay on page (like upload/recipient do with next button)
        // Or if the user clicked "Save draft", maybe just stay.
        // But the button was "Save Draft".
        // The "Next" button goes to Review page.
        
        return Page();
    }

    public async Task<IActionResult> OnPostSaveCustomTemplateAsync()
    {
        if (!TemplateType.HasValue)
        {
             TempData["ErrorMessage"] = "Please select an Email Type first.";
             return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(EditSubject) || string.IsNullOrWhiteSpace(EditBody))
        {
            TempData["ErrorMessage"] = "Subject and Body cannot be empty.";
            return RedirectToPage();
        }

        await _templateService.SaveUserTemplateAsync(TemplateType.Value, EditSubject, EditBody);
        TempData["SuccessMessage"] = "Template customization saved successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetTemplateAsync()
    {
        await _templateService.ResetUserTemplateAsync();
        TempData["SuccessMessage"] = "Templates reset to system defaults.";
        return RedirectToPage();
    }

    private void LoadAvailableDebtorCodes()
    {
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        if (string.IsNullOrEmpty(recipientsJson)) return;

        try
        {
            var recipients = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson) ?? new List<DebtorRecipient>();
            AvailableDebtorCodes = recipients
                .GroupBy(r => r.DebtorCode)
                .Select(g => new DebtorCodeOption
                {
                    DebtorCode = g.Key,
                    OrganizationName = g.First().OrganizationName ?? string.Empty,
                    Notes = g.First().Notes ?? string.Empty,
                    EmailCount = g.Count()
                })
                .OrderBy(d => d.DebtorCode)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load available debtor codes");
        }
    }

    private void LoadSelectedDebtorInfo()
    {
        if (string.IsNullOrWhiteSpace(DebtorCode)) return;

        var selected = AvailableDebtorCodes.FirstOrDefault(d => d.DebtorCode.Equals(DebtorCode, StringComparison.OrdinalIgnoreCase));
        if (selected != null)
        {
            SelectedOrganizationName = selected.OrganizationName;
            SelectedNotes = selected.Notes;
        }
    }
}

public class DebtorCodeOption
{
    public string DebtorCode { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int EmailCount { get; set; }
}
