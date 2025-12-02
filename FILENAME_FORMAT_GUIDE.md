# Quick Reference - Filename Format

## ? Correct Format

```
{DebtorCode} {DocumentType} {CustomCode}.extension
```

### Components:
1. **Debtor Code** - Your identifier (e.g., DEBT001, ABC123, COMPANY456)
2. **Space** (single)
3. **Document Type** - One of: INV, SOA, OD, OTHER
4. **Space** (single)
5. **Custom Code** - 4 to 6 digit number
6. **File Extension** - .pdf, .xlsx, .docx, etc.

---

## ?? Valid Examples

| Filename | Debtor Code | Doc Type | Custom Code | Extension |
|----------|-------------|----------|-------------|-----------|
| `DEBT001 INV 12345.pdf` | DEBT001 | INV | 12345 | .pdf |
| `ABC123 SOA 987654.pdf` | ABC123 | SOA | 987654 | .pdf |
| `XYZ789 OD 4567.pdf` | XYZ789 | OD | 4567 | .pdf |
| `COMP456 OTHER 123456.xlsx` | COMP456 | OTHER | 123456 | .xlsx |
| `CUSTOMER1 INV 9999.docx` | CUSTOMER1 | INV | 9999 | .docx |

---

## ? Invalid Examples

| Filename | Issue | Should Be |
|----------|-------|-----------|
| `DEBT001_INV_12345.pdf` | Uses underscores | `DEBT001 INV 12345.pdf` |
| `DEBT001 INV.pdf` | Missing custom code | `DEBT001 INV 12345.pdf` |
| `INV 12345.pdf` | Missing debtor code | `DEBT001 INV 12345.pdf` |
| `DEBT001 INV 123.pdf` | Code too short (< 4 digits) | `DEBT001 INV 1234.pdf` |
| `DEBT001 INV 1234567.pdf` | Code too long (> 6 digits) | `DEBT001 INV 123456.pdf` |
| `DEBT001  INV  12345.pdf` | Multiple spaces | `DEBT001 INV 12345.pdf` |

---

## ?? Document Types

| Code | Meaning | Typical Use |
|------|---------|-------------|
| **INV** | Invoice | Billing documents, purchase orders |
| **SOA** | Statement of Account | Account summaries, balance statements |
| **OD** | Overdue | Overdue notices, payment reminders |
| **OTHER** | Other Documents | Contracts, receipts, misc. documents |

---

## ?? Tips

1. **Debtor Code can be anything** - alphanumeric, no spaces within
2. **Document types are case-insensitive** - INV, inv, Inv all work
3. **Use single spaces** - double spaces will cause mismatch
4. **Custom code must be numeric** - 4, 5, or 6 digits only
5. **Any extension works** - .pdf, .xlsx, .docx, .png, etc.

---

## ?? Regex Pattern

The system uses this pattern to match files:
```regex
^(.+?)\s+(INV|SOA|OD|OTHER)\s+(\d{4,6})$
```

**Translation:**
- `(.+?)` - Debtor code (any characters, non-greedy)
- `\s+` - One or more spaces
- `(INV|SOA|OD|OTHER)` - Document type (case-insensitive)
- `\s+` - One or more spaces
- `(\d{4,6})` - Custom code (4 to 6 digits)

---

## ?? Testing Your Files

Before uploading, verify:
1. ? Debtor code is present
2. ? Single space after debtor code
3. ? Document type is INV, SOA, OD, or OTHER
4. ? Single space after document type
5. ? Custom code is 4-6 digits
6. ? File has an extension

**Quick Test:**
Open PowerShell and run:
```powershell
.\GenerateTestZip.ps1
```
This creates correctly formatted sample files.

---

## ?? Multiple Files Per Debtor

Each debtor can have multiple files of the same type:

```
DEBT001 INV 12345.pdf
DEBT001 INV 67890.pdf  ? Valid! Multiple invoices
DEBT001 SOA 11111.pdf
DEBT001 OD 22222.pdf
DEBT001 OTHER 33333.xlsx
```

The system will group all files by debtor code and show counts per type.

---

## ?? File Size Limit

- **Maximum ZIP file size:** 500 MB
- **Individual file limits:** None (within ZIP limit)
- **Total files:** No hard limit

To change the limit, edit `Program.cs`.

---

## ?? Ready to Upload?

1. Organize files with correct naming
2. Create a ZIP file
3. Upload via the `/Upload` page
4. Review the extraction results
5. Proceed to recipient upload

---

**Need help?** Check `README_UPLOAD_FEATURE.md` for detailed documentation.
