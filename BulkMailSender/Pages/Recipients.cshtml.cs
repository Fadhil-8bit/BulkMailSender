using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public class RecipientsModel : PageModel
{
    private readonly ILogger<RecipientsModel> _logger;

    public RecipientsModel(ILogger<RecipientsModel> logger)
    {
        _logger = logger;
    }

    public string? ExtractionPath { get; set; }
    public bool HasUploadData { get; set; }

    // Parsed recipients
    public List<DebtorRecipient> Recipients { get; set; } = new();

    // Errors encountered during parsing
    public List<string> Errors { get; set; } = new();

    public ParseSummary? ParseSummary { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        // Get extraction path from session
        ExtractionPath = HttpContext.Session.GetString("ExtractionPath");
        HasUploadData = !string.IsNullOrEmpty(ExtractionPath);

        if (!HasUploadData)
        {
            _logger.LogWarning("Recipients page accessed without upload data in session");
        }

        // Load previously parsed recipients from session if available
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        if (!string.IsNullOrEmpty(recipientsJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson);
                if (list != null)
                {
                    Recipients = list;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize recipients from session");
            }
        }
    }

    [BindProperty]
    public IFormFile? RecipientsFile { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        // Validate file
        if (RecipientsFile == null || RecipientsFile.Length == 0)
        {
            ErrorMessage = "Please select a CSV file.";
            OnGet();
            return Page();
        }
        if (!RecipientsFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Only CSV files are supported currently.";
            OnGet();
            return Page();
        }

        try
        {
            using var stream = RecipientsFile.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // Read header
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                ErrorMessage = "CSV header row is missing.";
                OnGet();
                return Page();
            }

            var headers = SplitCsvLine(headerLine).Select(h => h.Trim()).ToList();

            // Google CSV fixed headers
            int idxDebtor = IndexOfHeader(headers, new[] { "First Name", "Debtor Code", "Debtor" });
            int idxOrg = IndexOfHeader(headers, new[] { "Organization Name", "Company", "Company Name" });
            int idxNotes = IndexOfHeader(headers, new[] { "Notes", "Note" });

            // Collect all Email {digit} - Value/Label pairs
            var emailPairs = FindEmailHeaderPairs(headers);

            if (idxDebtor < 0)
            {
                ErrorMessage = "Required column 'First Name' (Debtor Code) not found.";
                OnGet();
                return Page();
            }
            if (emailPairs.Count == 0)
            {
                ErrorMessage = "Required email columns not found. Expect pairs like 'E-mail 1 - Value' & 'E-mail 1 - Type' (or Label).";
                OnGet();
                return Page();
            }

            int totalRows = 0; // CSV rows
            int validRecipients = 0; // per-email records created
            int invalidRows = 0;
            var parsed = new List<DebtorRecipient>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                totalRows++;
                var cols = SplitCsvLine(line);

                string debtor = GetCol(cols, idxDebtor);
                string? org = idxOrg >= 0 ? GetCol(cols, idxOrg) : null;
                string? notes = idxNotes >= 0 ? GetCol(cols, idxNotes) : null;

                if (string.IsNullOrWhiteSpace(debtor))
                {
                    invalidRows++;
                    Errors.Add($"Row {totalRows}: Missing Debtor Code (First Name)." );
                    continue;
                }

                int createdForRow = 0;
                foreach (var pair in emailPairs)
                {
                    string email = GetCol(cols, pair.ValueIndex);
                    string labelRaw = GetCol(cols, pair.LabelIndex);

                    if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(labelRaw))
                    {
                        // No data for this pair; skip silently
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(labelRaw))
                    {
                        Errors.Add($"Row {totalRows}: Incomplete email data for E-mail {pair.Number}.");
                        continue;
                    }

                    var label = NormalizeLabel(labelRaw);
                    if (label == null)
                    {
                        Errors.Add($"Row {totalRows}: Invalid email label '{labelRaw}' for E-mail {pair.Number}.");
                        continue;
                    }

                    if (!IsValidEmail(email))
                    {
                        Errors.Add($"Row {totalRows}: Invalid email '{email}' for E-mail {pair.Number}.");
                        continue;
                    }

                    parsed.Add(new DebtorRecipient
                    {
                        DebtorCode = debtor.Trim(),
                        OrganizationName = string.IsNullOrWhiteSpace(org) ? null : org.Trim(),
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                        Email = email.Trim(),
                        Label = label.Value
                    });
                    validRecipients++;
                    createdForRow++;
                }

                if (createdForRow == 0)
                {
                    // No valid email entries created for this debtor row
                    invalidRows++;
                }
            }

            Recipients = parsed;
            ParseSummary = new ParseSummary
            {
                TotalRows = totalRows,
                ValidRows = validRecipients,
                InvalidRows = invalidRows
            };

            // Store in session
            HttpContext.Session.SetString("Recipients", JsonSerializer.Serialize(Recipients));

            _logger.LogInformation($"Parsed recipients: Rows={totalRows}, Recipients={validRecipients}, InvalidRows={invalidRows}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing recipients CSV");
            ErrorMessage = $"Error parsing CSV: {ex.Message}";
        }

        OnGet();
        return Page();
    }

    private static int IndexOfHeader(List<string> headers, IEnumerable<string> candidates)
    {
        foreach (var c in candidates)
        {
            var idx = headers.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) return idx;
        }
        return -1;
    }

    private static List<EmailHeaderPair> FindEmailHeaderPairs(List<string> headers)
    {
        // Support both 'Email' and 'E-mail' and label synonyms (Type/Label)
        var valueRegex = new Regex(@"^(E-?mail|Email)\s*(\d+)\s*-\s*Value$", RegexOptions.IgnoreCase);
        var labelRegex = new Regex(@"^(E-?mail|Email)\s*(\d+)\s*-\s*(Type|Label)$", RegexOptions.IgnoreCase);

        // Find all indices matching Email {digit} - Value and Email {digit} - Label, pair by the same digit
        var valueMatches = new List<(int Index, int Number)>();
        var labelMatches = new List<(int Index, int Number)>();
        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i];
            var vm = valueRegex.Match(h);
            if (vm.Success)
            {
                valueMatches.Add((i, int.Parse(vm.Groups[2].Value)));
                continue;
            }
            var lm = labelRegex.Match(h);
            if (lm.Success)
            {
                labelMatches.Add((i, int.Parse(lm.Groups[2].Value)));
                continue;
            }
        }

        var pairs = new List<EmailHeaderPair>();
        foreach (var v in valueMatches)
        {
            var lbl = labelMatches.FirstOrDefault(l => l.Number == v.Number);
            if (lbl != default)
            {
                pairs.Add(new EmailHeaderPair
                {
                    Number = v.Number,
                    ValueIndex = v.Index,
                    LabelIndex = lbl.Index
                });
            }
        }

        // Sort by email number ascending for deterministic processing
        pairs.Sort((a, b) => a.Number.CompareTo(b.Number));
        return pairs;
    }

    private static List<string> SplitCsvLine(string line)
    {
        // Simple CSV split supporting quoted values and commas
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    private static string GetCol(List<string> cols, int index)
    {
        if (index < 0 || index >= cols.Count) return string.Empty;
        return cols[index].Trim();
    }

    private static EmailLabel? NormalizeLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Remove non-letter characters (e.g., leading '*', punctuation, parentheses) and normalize spacing
        var cleaned = Regex.Replace(raw, "[^A-Za-z]", " ").Trim().ToLowerInvariant();

        // Normalize common tokens
        if (cleaned.Contains("work") || cleaned.Contains("to") || cleaned.Contains("primary") || cleaned.Contains("home"))
            return EmailLabel.To;

        if (cleaned.Contains("cc") || cleaned.Contains("view") || cleaned.Contains("other") || cleaned.Contains("office"))
            return EmailLabel.Cc;

        if (cleaned.Contains("bcc") || cleaned.Contains("private") || cleaned.Contains("hidden"))
            return EmailLabel.Bcc;

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

public class ParseSummary
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
}

public class EmailHeaderPair
{
    public int Number { get; set; }
    public int ValueIndex { get; set; }
    public int LabelIndex { get; set; }
}
