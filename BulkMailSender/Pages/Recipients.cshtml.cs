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

    // Global CC from SMTP settings
    public string? GlobalCc { get; set; }

    // Parsed recipients
    public List<DebtorRecipient> Recipients { get; set; } = new();

    // Errors encountered during parsing - now includes Debtor Code context
    public List<string> Errors { get; set; } = new();

    // Structured error data for better tracking and reporting
    public List<ParseError> ParseErrors { get; set; } = new();

    // Debtor codes that fail style validation
    public List<string> InvalidDebtorCodes { get; set; } = new();

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

        // Load global CC from SMTP settings
        var smtpJson = HttpContext.Session.GetString("SmtpSettings");
        if (!string.IsNullOrEmpty(smtpJson))
        {
            try
            {
                var smtpSettings = JsonSerializer.Deserialize<SmtpSettings>(smtpJson);
                GlobalCc = smtpSettings?.GlobalCc;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load SMTP settings from session");
            }
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

        // Load invalid debtor codes from session (if previously parsed)
        var invalidJson = HttpContext.Session.GetString("InvalidDebtorCodes");
        if (!string.IsNullOrEmpty(invalidJson))
        {
            try { InvalidDebtorCodes = JsonSerializer.Deserialize<List<string>>(invalidJson) ?? new List<string>(); }
            catch { }
        }

        // Load parse errors from session (if previously parsed)
        var errorsJson = HttpContext.Session.GetString("ParseErrors");
        if (!string.IsNullOrEmpty(errorsJson))
        {
            try { ParseErrors = JsonSerializer.Deserialize<List<ParseError>>(errorsJson) ?? new List<ParseError>(); }
            catch { }
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
            int idxDebtor = IndexOfHeader(headers, new[] { "First Name" });
            int idxOrg = IndexOfHeader(headers, new[] { "Organization Name" });
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
            int invalidRecipients = 0; // rows with any issue
            var parsed = new List<DebtorRecipient>();
            var invalidDebtors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parseErrors = new List<ParseError>();

            // debtor code regex: uppercase letters/digits only on both sides of a single hyphen
            var debtorStyleRegex = new Regex("^[A-Z0-9]+-[A-Z0-9]+$", RegexOptions.Compiled);

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
                    invalidRecipients++;
                    parseErrors.Add(new ParseError
                    {
                        RowNumber = totalRows,
                        DebtorCode = null,
                        ErrorMessage = "Missing Debtor Code (First Name).",
                        ErrorType = "Missing"
                    });
                    Errors.Add($"Row {totalRows}: Missing Debtor Code (First Name).");
                    continue;
                }

                // Debtor code style validation: exactly one hyphen and uppercase alphanumeric groups
                var debtorForMatch = debtor.Trim().ToUpperInvariant();
                bool hasInvalidDebtorStyle = !debtorStyleRegex.IsMatch(debtorForMatch);
                if (hasInvalidDebtorStyle)
                {
                    invalidDebtors.Add(debtor.Trim());
                    parseErrors.Add(new ParseError
                    {
                        RowNumber = totalRows,
                        DebtorCode = debtor.Trim(),
                        ErrorMessage = "Invalid debtor code format. Must contain exactly one hyphen with uppercase alphanumeric groups on both sides (e.g., 3000-AT502).",
                        ErrorType = "DebtorCode"
                    });
                }

                bool rowHasAnyError = hasInvalidDebtorStyle;
                var rowRecipients = new List<DebtorRecipient>();

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
                        parseErrors.Add(new ParseError
                        {
                            RowNumber = totalRows,
                            DebtorCode = debtor.Trim(),
                            ErrorMessage = $"Incomplete email data for E-mail {pair.Number} (missing email or label).",
                            ErrorType = "Format"
                        });
                        Errors.Add($"Row {totalRows}: Incomplete email data for E-mail {pair.Number}.");
                        rowHasAnyError = true;
                        continue;
                    }

                    var label = NormalizeLabel(labelRaw);
                    if (label == null)
                    {
                        parseErrors.Add(new ParseError
                        {
                            RowNumber = totalRows,
                            DebtorCode = debtor.Trim(),
                            ErrorMessage = $"Invalid email label '{labelRaw}' for E-mail {pair.Number}. Valid labels: 'Work' (TO), 'View' (CC), 'Private' (BCC).",
                            ErrorType = "Label"
                        });
                        Errors.Add($"Row {totalRows}: Invalid email label '{labelRaw}' for E-mail {pair.Number}.");
                        rowHasAnyError = true;
                        continue;
                    }

                    if (!IsValidEmail(email))
                    {
                        parseErrors.Add(new ParseError
                        {
                            RowNumber = totalRows,
                            DebtorCode = debtor.Trim(),
                            ErrorMessage = $"Invalid email format: '{email}' for E-mail {pair.Number}.",
                            ErrorType = "Email"
                        });
                        Errors.Add($"Row {totalRows}: Invalid email '{email}' for E-mail {pair.Number}.");
                        rowHasAnyError = true;
                        continue;
                    }

                    rowRecipients.Add(new DebtorRecipient
                    {
                        DebtorCode = debtor.Trim(),
                        OrganizationName = string.IsNullOrWhiteSpace(org) ? null : org.Trim(),
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                        Email = email.Trim(),
                        Label = label.Value
                    });
                }

                // Only add recipients if row has no errors and has at least one valid email
                if (!rowHasAnyError && rowRecipients.Count > 0)
                {
                    parsed.AddRange(rowRecipients);
                    validRecipients += rowRecipients.Count;
                }
                else if (rowHasAnyError || rowRecipients.Count == 0)
                {
                    // Row has validation errors or no valid email entries
                    invalidRecipients++;
                }
            }

            Recipients = parsed;
            ParseSummary = new ParseSummary
            {
                TotalDebtorCodes = totalRows,
                ValidRecipients = validRecipients,
                InvalidRecipients = invalidRecipients
            };

            InvalidDebtorCodes = invalidDebtors.OrderBy(x => x).ToList();
            ParseErrors = parseErrors;

            // Store in session
            HttpContext.Session.SetString("Recipients", JsonSerializer.Serialize(Recipients));
            HttpContext.Session.SetString("InvalidDebtorCodes", JsonSerializer.Serialize(InvalidDebtorCodes));
            HttpContext.Session.SetString("ParseErrors", JsonSerializer.Serialize(ParseErrors));

            _logger.LogInformation($"Parsed recipients: Rows={totalRows}, Recipients={validRecipients}, InvalidRecipients={invalidRecipients}, InvalidDebtorCodes={InvalidDebtorCodes.Count}");
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
        if (cleaned.Contains("work"))
            return EmailLabel.To;

        if (cleaned.Contains("view"))
            return EmailLabel.Cc;

        if (cleaned.Contains("private"))
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

    // Debtor code must have exactly one hyphen and uppercase alphanumeric on both sides
    private static bool IsValidDebtorCodeStyle(string debtor)
    {
        if (string.IsNullOrWhiteSpace(debtor)) return false;
        debtor = debtor.Trim().ToUpperInvariant();
        return Regex.IsMatch(debtor, "^[A-Z0-9]+-[A-Z0-9]+$");
    }
}

public class ParseSummary
{
    public int TotalDebtorCodes { get; set; }
    public int ValidRecipients { get; set; }
    public int InvalidRecipients { get; set; }
}

public class EmailHeaderPair
{
    public int Number { get; set; }
    public int ValueIndex { get; set; }
    public int LabelIndex { get; set; }
}

public class ParseError
{
    public int RowNumber { get; set; }
    public string? DebtorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty; // "DebtorCode", "Email", "Label", "Format", "Missing"
}
