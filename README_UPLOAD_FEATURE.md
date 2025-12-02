# Bulk Mail Sender - Upload Feature

## ? Implementation Complete (Updated)

### What's Been Implemented:

#### 1. **Models** (`/Models`)
- **DebtorAttachment.cs** - Represents attachments grouped by debtor code with detailed file lists
- **FileAttachmentInfo.cs** - Contains individual file details (name, size, custom code, type)
- **UploadResult.cs** - Contains upload operation results and statistics

#### 2. **Services** (`/Services`)
- **IZipExtractionService.cs** - Interface for ZIP extraction
- **ZipExtractionService.cs** - Handles ZIP file extraction and categorization with new naming pattern

#### 3. **Pages** (`/Pages`)
- **Upload.cshtml** - File upload UI with expandable file preview, sizes, and validation
- **Upload.cshtml.cs** - Page model handling file upload logic
- **Recipients.cshtml** - Placeholder for next step (to be implemented)

#### 4. **Configuration**
- Service registration in `Program.cs`
- **500 MB file upload limit** configured
- Navigation menu updated with Upload link
- Bootstrap Icons added for better UI

---

## ?? File Naming Convention (UPDATED)

Files inside the ZIP must follow this pattern:

```
{DebtorCode} {DocumentType} {CustomCode}.extension

Examples:
? DEBT001 INV 12345.pdf       (Invoice with code 12345)
? DEBT001 SOA 98765.pdf       (Statement of Account)
? DEBT001 OD 456789.pdf       (Overdue Notice)
? ABC123 OTHER 7890.xlsx      (Other documents)
? COMP456 INV 123456.pdf      (6-digit custom code)

? DEBT001_INV_12345.pdf       (Using underscores)
? DEBT001 INV.pdf             (Missing custom code)
? invoice.pdf                 (Missing debtor code and type)
```

### Naming Rules:
- **Debtor Code:** Any alphanumeric identifier (e.g., DEBT001, ABC123, COMP456)
- **Document Type:** Must be one of:
  - **INV** - Invoice
  - **SOA** - Statement of Account
  - **OD** - Overdue Notice
  - **OTHER** - Any other document type
- **Custom Code:** 4 to 6 digit number (e.g., 1234, 12345, 123456)
- **Separator:** Single space between each component
- **Extension:** Any file extension (.pdf, .xlsx, .docx, etc.)

---

## ?? File Upload Limits

### Maximum File Size: **500 MB**
This has been configured in:
- `appsettings.json` - Configuration setting
- `Program.cs` - Kestrel and Form options
- Client-side validation in Upload page

### How to Change the Limit:
Edit `Program.cs`:
```csharp
options.MultipartBodyLengthLimit = 1073741824; // 1 GB (in bytes)
options.MaxRequestBodySize = 1073741824;
```

Common sizes:
- 100 MB = `104857600`
- 500 MB = `524288000` (current)
- 1 GB = `1073741824`
- 2 GB = `2147483648`

---

## ?? How to Use

### 1. Generate Test Data
Run the PowerShell script to create a test ZIP file:
```powershell
.\GenerateTestZip.ps1
```
This creates `BulkMailTestData.zip` with sample attachments using the correct naming convention.

### 2. Run the Application
```bash
dotnet run
```

### 3. Upload Files
1. Navigate to `/Upload` page
2. Select the ZIP file (up to 500 MB)
3. Click "Upload and Extract"
4. Review the categorization results with detailed file lists

---

## ?? Features

### ? Current Features:
- ZIP file upload with **500 MB limit**
- Automatic file extraction and categorization
- **Support for space-separated naming convention**
- **Support for 4-6 digit custom codes**
- **Support for OTHER document type**
- **Expandable accordion view** for each debtor
- **Detailed file lists** with individual file information
- **File size display** for each file and total per debtor
- Preview of categorized attachments by type (INV, SOA, OD, OTHER)
- **Multiple files per category** support (e.g., multiple invoices per debtor)
- Error handling for unmatched files
- Responsive UI with Bootstrap
- Statistics dashboard
- Client-side file validation

### ?? Next Steps (To Implement):
1. **Recipients Upload** - CSV/Excel file with debtor list
2. **Email Template** - Configure subject and body
3. **Email Preview** - Preview before sending
4. **Bulk Email Sending** - SMTP integration
5. **Job Tracking** - Monitor send progress
6. **History** - View past jobs

---

## ??? Project Structure

```
BulkMailSender/
??? Models/
?   ??? DebtorAttachment.cs          [UPDATED]
?   ??? FileAttachmentInfo.cs        [NEW]
?   ??? UploadResult.cs
??? Services/
?   ??? IZipExtractionService.cs
?   ??? ZipExtractionService.cs      [UPDATED]
??? Pages/
?   ??? Upload.cshtml                [UPDATED]
?   ??? Upload.cshtml.cs
?   ??? Recipients.cshtml
?   ??? Recipients.cshtml.cs
??? Program.cs                        [UPDATED - 500MB limit]
??? appsettings.json                  [UPDATED]
??? wwwroot/
    ??? uploads/                      (Created automatically)
```

---

## ?? Technical Details

### Dependencies:
- **.NET 10** (Current target framework)
- **System.IO.Compression** (Built-in, no package needed)
- **Bootstrap 5** (Already included)
- **Bootstrap Icons** (CDN)

### Storage:
- Uploaded files are extracted to `wwwroot/uploads/{guid}/`
- Each upload session gets a unique folder
- Files are cleaned up after processing

### File Size Configuration:
- **Kestrel MaxRequestBodySize:** 500 MB
- **FormOptions MultipartBodyLengthLimit:** 500 MB
- **Client-side validation:** Checks file size before upload
- **Configurable** via `appsettings.json` and `Program.cs`

### Security Considerations:
- File type validation (.zip only)
- File size limits (500 MB default, configurable)
- Unique folder per upload to prevent conflicts
- Automatic cleanup on failure
- Client and server-side validation

---

## ?? Testing

### Test Scenarios:
1. **Valid ZIP with all document types**
   - Result: All files categorized correctly by type
   
2. **ZIP with multiple files per debtor**
   - Result: Expandable view showing all files grouped by type
   
3. **ZIP with OTHER document type**
   - Result: Shows in separate "Other Documents" section
   
4. **ZIP with missing document types**
   - Result: Shows which documents are available per debtor
   
5. **ZIP with unmatched files**
   - Result: Listed in warnings section
   
6. **Invalid file (non-ZIP)**
   - Result: Error message displayed

7. **File over 500 MB**
   - Result: Client-side error before upload

### Sample Test Data:
Use `GenerateTestZip.ps1` to create test files with:
- 6 debtor codes
- Random document types (2-4 per debtor)
- Random custom codes (4-6 digits)
- 5 unmatched files for error testing

---

## ?? Example Output

After uploading a ZIP with 20 files for 6 debtors:

```
Total Files: 25
Categorized: 20
Uncategorized: 5
Debtors Found: 6
```

**Expandable Debtor View:**
```
DEBT001 [3 files] [245.5 KB]
??? Invoice Files (2)
?   ??? DEBT001 INV 12345.pdf (123.4 KB)
?   ??? DEBT001 INV 67890.pdf (89.2 KB)
??? Statement Files (1)
?   ??? DEBT001 SOA 98765.pdf (32.9 KB)

ABC123 [4 files] [512.3 KB]
??? Invoice Files (1)
??? Statement Files (1)
??? Overdue Files (1)
??? Other Documents (1)
    ??? ABC123 OTHER 4567.xlsx (45.8 KB)
```

---

## ?? Troubleshooting

### Issue: "Upload folder not found"
**Solution:** The folder is created automatically. Ensure the app has write permissions.

### Issue: "Files not categorized"
**Solution:** Check file naming convention:
- Use **spaces** not underscores
- Include 4-6 digit custom code
- Correct format: `DEBT001 INV 12345.pdf`

### Issue: "ZIP extraction fails"
**Solution:** Ensure the ZIP is not corrupted and is not password-protected

### Issue: "File size limit exceeded"
**Solution:** 
- Default limit is 500 MB
- To increase: Edit `Program.cs` and change `MultipartBodyLengthLimit`
- Restart the application after changes

### Issue: "Files showing as 'OTHER' when they should be INV/SOA/OD"
**Solution:** 
- Check document type is uppercase (INV, SOA, OD, OTHER)
- Service converts to uppercase automatically, but verify spacing

---

## ?? New Features Explained

### 1. Expandable Accordion View
Each debtor has a collapsible section showing all their files grouped by type.

### 2. File Size Display
- Individual file sizes shown for each attachment
- Total size calculated per debtor
- Human-readable format (B, KB, MB)

### 3. Multiple Files Per Category
- Debtors can have multiple invoices, statements, etc.
- Each file shown with its custom code
- Badge counters show quantity per type

### 4. OTHER Document Support
- Flexible for non-standard document types
- Supports any file extension
- Displayed in separate section

### 5. Custom Code Tracking
- Displayed next to each filename
- Helps identify specific document versions
- Supports 4-6 digit codes

---

## ?? UI Improvements

- **Color-coded badges** for different document types
- **Bootstrap Icons** for visual file type identification
- **Responsive accordion** for easy navigation
- **File size indicators** with appropriate units
- **Search-friendly** debtor code display
- **Warning section** with limited error display (max 20)

---

## ?? Support

For questions or issues:
1. Check the naming convention carefully
2. Verify file size is under 500 MB
3. Review logs in the Output window
4. Test with `GenerateTestZip.ps1` sample data

---

## ?? Version History

**v2.0** (Current)
- ? New naming convention with spaces
- ? 4-6 digit custom code support
- ? OTHER document type
- ? 500 MB upload limit
- ? Detailed file lists with sizes
- ? Expandable accordion UI
- ? Multiple files per category

**v1.0**
- Basic ZIP upload
- Underscore-based naming convention
- Simple summary table
