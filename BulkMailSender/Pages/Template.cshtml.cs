using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class TemplateModel : PageModel
{
    private readonly ILogger<TemplateModel> _logger;

    public TemplateModel(ILogger<TemplateModel> logger)
    {
        _logger = logger;
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

    public void OnGet()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        
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

        SubjectPreview = BuildSubject(TemplateType.Value, Period, DebtorCode, orgName);
        BodyPreview = BuildBody(TemplateType.Value, notes);
        
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
            Subject = BuildSubject(TemplateType.Value, Period, "{debtor code}", "{organization name}"),
            Body = BuildBody(TemplateType.Value, "{notes}")
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
            Subject = BuildSubject(TemplateType.Value, Period, "{debtor code}", "{organization name}"),
            Body = BuildBody(TemplateType.Value, "{notes}")
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
               SubjectPreview = BuildSubject(TemplateType.Value, Period, DebtorCode, SelectedOrganizationName ?? "{org}");
               BodyPreview = BuildBody(TemplateType.Value, SelectedNotes ?? "{notes}");
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

    private static string BuildSubject(Models.TemplateType type, string? period, string debtorCode, string organization)
    {
        period = string.IsNullOrWhiteSpace(period) ? "<SET PERIOD>" : period.Trim();
        debtorCode = string.IsNullOrWhiteSpace(debtorCode) ? "{debtor code}" : debtorCode.Trim();
        organization = string.IsNullOrWhiteSpace(organization) ? "{organization name}" : organization.Trim();
        if (type == Models.TemplateType.SoaInv)
        {
            return $"ATP INVOICE AND SOA {period} - {debtorCode} - {organization}";
        }
        else
        {
            return $"Reminder overdue account -{organization} - {debtorCode}";
        }
    }

    private static string BuildBody(Models.TemplateType type, string? notes)
    {
        if (type == Models.TemplateType.SoaInv)
        {
            return "Good day to you\nThe attached statement reflects your account balance.\n\nPlease check the statement provided.\n\nIf you have any questions regarding this statement or any clarification needs, please contact the ATP Careline 018-7864855\n\nAny overdue payment may lead to service interruption.\n\nThe below are the bank details for your payment purpose:-\nCompany Name: ATP SALES & SERVICES SDN BHD\nBank Name: Affin Bank Bhd\nBank Account Number: 10675 0000 898\nEmail (Banking Slip): <atgroupoperation02@gmail.com>\n\n***************************************************************************\n\nThis is an auto-generated email, please DO NOT REPLY. Any replies to this\nemail will be disregarded.\n\n***************************************************************************";
        }
        else
        {
            var notesText = string.IsNullOrWhiteSpace(notes) ? "{notes}" : notes.Trim();
            // notesText should be the term e.g. 60 DAYS/90Days; we keep as provided placeholder
            return $"Good day to you\nKindly find the attached statement of account and invoice.\n\nAccording to our payment term with your company, your are requested to make the payment within {notesText} after you receive the monthly statement of account. Please clear and remit, if any. If you have made the payment, please let me know and I will update accordingly\n\nBearing in mind, as company policy\n\nATP shall be entitled, at its absolute discretion, to suspend Customer's account and hold service call until overdue outstanding has been fully paid.\n\nThank you\n\nPIC name: Ms. Ika\nEmail: atgroupoperation02@gmail.com\nDirect Line: 018-7864855";
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
