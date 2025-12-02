# Answers to Your Questions

## ? Question 1: Filename Format

### Your Standard Format:
```
{DebtorCode} {INV/SOA/OD/OTHER} {4-6 digit custom code}
```

### ? **IMPLEMENTED!**

The system now supports your exact naming convention:

**Examples:**
- `DEBT001 INV 12345.pdf` ?
- `ABC123 SOA 987654.pdf` ?
- `XYZ789 OD 4567.pdf` ?
- `COMP456 OTHER 123456.xlsx` ?

**Key Features:**
- ? Space-separated components (not underscores)
- ? 4-6 digit custom codes supported
- ? Document types: INV, SOA, OD, **OTHER**
- ? Case-insensitive matching
- ? Any file extension supported

**Regex Pattern Used:**
```regex
^(.+?)\s+(INV|SOA|OD|OTHER)\s+(\d{4,6})$
```

---

## ? Question 2: Maximum Upload Size

### ? **500 MB** (Currently Configured)

**Where it's set:**
1. **Program.cs** - Kestrel configuration
   ```csharp
   options.MultipartBodyLengthLimit = 524288000; // 500 MB
   options.MaxRequestBodySize = 524288000;
   ```

2. **appsettings.json** - Configuration file
   ```json
   "FileUpload": {
     "MaxFileSizeMB": 500
   }
   ```

3. **Client-side validation** - Upload.cshtml
   - JavaScript checks file size before upload
   - Shows friendly error if exceeded

### How to Change:

**To increase to 1 GB:**
Edit `Program.cs`, line 12-13:
```csharp
options.MultipartBodyLengthLimit = 1073741824; // 1 GB
options.MaxRequestBodySize = 1073741824;
```

**Common Sizes:**
- 100 MB = `104857600`
- 250 MB = `262144000`
- 500 MB = `524288000` ?? Current
- 1 GB = `1073741824`
- 2 GB = `2147483648`

### Why 500 MB?
- ? Good for bulk operations (hundreds of PDFs)
- ? Prevents server overload
- ? Reasonable upload time
- ? Works on most hosting environments

**Typical use case:**
- 500 MB ? 500-1000 PDF attachments (depending on size)
- Average PDF: 0.5-2 MB

---

## ? Question 3: Is the File List Complete?

### ? **YES! Fully Complete**

The upload page now shows **COMPLETE detailed file lists** including:

### What You See:

#### 1. **Summary Dashboard**
- Total files uploaded
- Categorized vs uncategorized count
- Number of debtors found
- Overall statistics

#### 2. **Expandable Accordion View** (Per Debtor)
Each debtor card shows:
- Debtor code
- Total file count badge
- Total file size
- Type badges (INV: 2, SOA: 1, etc.)

#### 3. **Detailed File Listings** (Inside Each Accordion)

**For EACH document type:**
- ? **Complete filename** (e.g., "DEBT001 INV 12345.pdf")
- ? **Custom code** extracted (e.g., "Code: 12345")
- ? **Individual file size** (e.g., "123.4 KB")
- ? **Document type icon** (PDF, Excel, etc.)

**Grouped by type:**
- ?? **Invoice Files (INV)** - All invoices listed
- ?? **Statement Files (SOA)** - All statements listed
- ?? **Overdue Files (OD)** - All overdue notices listed
- ?? **Other Documents (OTHER)** - All other docs listed

### Example Display:

```
DEBT001 [5 files] [1.2 MB] [INV: 2] [SOA: 1] [OD: 1] [OTHER: 1]
??? Invoice Files (2)
?   ??? ?? DEBT001 INV 12345.pdf (Code: 12345) [456.2 KB]
?   ??? ?? DEBT001 INV 67890.pdf (Code: 67890) [234.1 KB]
??? Statement Files (1)
?   ??? ?? DEBT001 SOA 98765.pdf (Code: 98765) [321.5 KB]
??? Overdue Files (1)
?   ??? ?? DEBT001 OD 11111.pdf (Code: 11111) [123.8 KB]
??? Other Documents (1)
    ??? ?? DEBT001 OTHER 22222.xlsx (Code: 22222) [89.4 KB]
```

### UI Features:
- ? **First debtor expanded by default** (for quick preview)
- ? **Click any debtor to expand/collapse**
- ? **Color-coded badges** for easy identification
- ? **Icons** showing file types (PDF, Excel, etc.)
- ? **File sizes** in human-readable format (B, KB, MB)
- ? **Custom codes** shown next to each file
- ? **Responsive design** works on mobile

### What's NOT Shown:
- ? File preview/thumbnails (not implemented)
- ? Download links (files are server-side only)
- ? File content analysis

But you see **ALL file names and metadata**!

---

## ?? Summary Table

| Feature | Status | Details |
|---------|--------|---------|
| **Space-separated naming** | ? Complete | `DEBT001 INV 12345.pdf` |
| **4-6 digit codes** | ? Complete | Supports 1234, 12345, 123456 |
| **OTHER document type** | ? Complete | Flexible for misc. docs |
| **File upload limit** | ? 500 MB | Configurable, client+server validation |
| **Complete file list** | ? Complete | All files shown with details |
| **Individual file sizes** | ? Complete | Shown for each file + totals |
| **Multiple files per type** | ? Complete | E.g., 5 invoices for one debtor |
| **Expandable UI** | ? Complete | Bootstrap accordion |
| **Error handling** | ? Complete | Unmatched files shown separately |

---

## ?? Testing

**Generate test data with your format:**
```powershell
.\GenerateTestZip.ps1
```

This creates:
- 6 debtors with correctly formatted files
- Random 4-6 digit custom codes
- Multiple files per debtor
- INV, SOA, OD, and OTHER types
- Some unmatched files for error testing

**Then upload and you'll see:**
1. ? All files parsed correctly
2. ? Complete file list per debtor
3. ? File sizes displayed
4. ? Custom codes extracted
5. ? Organized by type

---

## ?? Next Steps

Now that upload is complete with:
- ? Your filename format
- ? 500 MB limit
- ? Complete file listings

**Ready to implement:**
1. Recipients upload (CSV/Excel with emails)
2. Email template configuration
3. Bulk sending engine

---

## ?? Questions?

All three of your questions are now **fully implemented**:
1. ? Filename format matches your standard
2. ? 500 MB upload limit (configurable)
3. ? Complete file list with all details

Test it with `GenerateTestZip.ps1` and let me know if you need any adjustments!
