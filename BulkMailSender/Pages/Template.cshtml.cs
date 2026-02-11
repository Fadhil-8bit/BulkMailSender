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

    public async Task OnGetAsync()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        
        // Load raw templates for customization
        var soaTemplate = await _templateService.GetTemplateAsync(Models.TemplateType.SoaInv);
        var overdueTemplate = await _templateService.GetTemplateAsync(Models.TemplateType.Overdue);

        RawTemplates["SoaInv_Subject"] = soaTemplate.Subject;
        RawTemplates["SoaInv_Body"] = soaTemplate.Body;
        RawTemplates["Overdue_Subject"] = overdueTemplate.Subject;
        RawTemplates["Overdue_Body"] = overdueTemplate.Body;

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

    public async Task<IActionResult> OnPostPreviewAsync()
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
        var periodVal = string.IsNullOrWhiteSpace(Period) ? "<SET PERIOD>" : Period.Trim();

        var template = await _templateService.GetTemplateAsync(TemplateType.Value);
        
        var subjectValues = new Dictionary<string, string>
        {
             { "period", periodVal },
             { "debtor_code", DebtorCode},
             { "organization_name", orgName }
        };
        SubjectPreview = _templateService.RenderTemplate(template.Subject, subjectValues);

        var bodyValues = new Dictionary<string, string>
        {
            { "notes", notes },
            { "period", periodVal },
             { "debtor_code", DebtorCode},
             { "organization_name", orgName }
        };
        BodyPreview = _templateService.RenderTemplate(template.Body, bodyValues);
        
        _logger.LogInformation("Preview generated for debtor: {DebtorCode}, Type: {Type}, Period: {Period}", 
            DebtorCode, TemplateType, Period);
            
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        
        if (!TemplateType.HasValue || (TemplateType == Models.TemplateType.SoaInv && string.IsNullOrWhiteSpace(Period)))
        {
             LoadAvailableDebtorCodes();
             ModelState.AddModelError("", "Please configure Email Type and Period first.");
             TempData["ErrorMessage"] = "Please complete the configuration.";
             return Page();
        }

        var template = await CreateDraftTemplateAsync(TemplateType.Value, Period);
        HttpContext.Session.SetString("EmailTemplate", JsonSerializer.Serialize(template));
        
        return RedirectToPage("/Preview");
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();

        if (!TemplateType.HasValue || (TemplateType == Models.TemplateType.SoaInv && string.IsNullOrWhiteSpace(Period)))
        {
             TempData["ErrorMessage"] = "Please complete all fields in Step 1 before saving.";
             return Page();
        }

        var template = await CreateDraftTemplateAsync(TemplateType.Value, Period);
        
        try
        {
            HttpContext.Session.SetString("EmailTemplate", JsonSerializer.Serialize(template));
            _logger.LogInformation("Draft template saved. Type: {Type}, Period: {Period}", TemplateType.Value, Period);
            TempData["SuccessMessage"] = "Draft saved successfully. Ready for review.";
            
            if (string.IsNullOrEmpty(SubjectPreview) && !string.IsNullOrEmpty(DebtorCode))
            {
               LoadSelectedDebtorInfo();
               
               // Generate preview for current page
               var templateContent = await _templateService.GetTemplateAsync(TemplateType.Value);
               var periodVal = Period?.Trim() ?? string.Empty;
               var orgVal = SelectedOrganizationName ?? "{org}";
               var notesVal = SelectedNotes ?? "{notes}";

               SubjectPreview = _templateService.RenderTemplate(templateContent.Subject, new Dictionary<string, string> {
                    { "period", periodVal },
                    { "debtor_code", DebtorCode },
                    { "organization_name", orgVal }
               });

               BodyPreview = _templateService.RenderTemplate(templateContent.Body, new Dictionary<string, string> {
                    { "notes", notesVal },
                    { "period", periodVal },
                    { "debtor_code", DebtorCode },
                    { "organization_name", orgVal }
               });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save template");
            TempData["ErrorMessage"] = "Failed to save template.";
            return Page();
        }
        
        return Page();
    }

    private async Task<SavedTemplate> CreateDraftTemplateAsync(TemplateType type, string? period)
    {
        var templateContent = await _templateService.GetTemplateAsync(type);
        var periodVal = period?.Trim() ?? string.Empty;

        var subjectValues = new Dictionary<string, string>
        {
             { "period", periodVal },
             { "debtor_code", "{debtor code}"},
             { "organization_name", "{organization name}" }
        };
        var bodyValues = new Dictionary<string, string>
        {
            { "notes", "{notes}" },
             { "period", periodVal },
             { "debtor_code", "{debtor code}"},
             { "organization_name", "{organization name}" }
        };

        return new SavedTemplate
        {
            TemplateType = type,
            Period = periodVal,
            DebtorCode = "{debtor code}",
            OrganizationName = "{organization name}",
            Notes = "{notes}",
            Subject = _templateService.RenderTemplate(templateContent.Subject, subjectValues),
            Body = _templateService.RenderTemplate(templateContent.Body, bodyValues)
        };
    }

    public async Task<IActionResult> OnPostSaveMasterTemplateAsync()
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

        await _templateService.SaveCustomTemplateAsync(TemplateType.Value, EditSubject, EditBody);
        TempData["SuccessMessage"] = "Master template saved successfully.";
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
