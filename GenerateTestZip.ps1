# ZIP Test File Generator for BulkMailSender
# This script creates a sample ZIP file with test attachments

# Create a temporary directory for test files
$testDir = ".\TestAttachments"
$zipFile = ".\BulkMailTestData.zip"

# Clean up existing files
if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

# Create test directory
New-Item -ItemType Directory -Path $testDir | Out-Null

# Sample debtor codes
$debtorCodes = @("DEBT001", "DEBT002", "DEBT003", "ABC123", "XYZ789", "COMP456")
$docTypes = @("INV", "SOA", "OD", "OTHER")

Write-Host "Generating test files with new naming convention..." -ForegroundColor Cyan
Write-Host "Format: {DebtorCode} {DocType} {CustomCode}.ext`n" -ForegroundColor Yellow

$fileCount = 0

# Generate test files
foreach ($debtorCode in $debtorCodes) {
    # Create random number of documents (2-4) per debtor
    $numDocs = Get-Random -Minimum 2 -Maximum 5
    $selectedDocs = $docTypes | Get-Random -Count $numDocs
    
    foreach ($docType in $selectedDocs) {
        # Generate random custom code (4-6 digits)
        $codeLength = Get-Random -Minimum 4 -Maximum 7
        $customCode = -join ((1..$codeLength) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })
        
        # Random extension
        $extension = if ($docType -eq "OTHER") { 
            @(".xlsx", ".docx", ".pdf") | Get-Random 
        } else { 
            ".pdf" 
        }
        
        # NEW FORMAT: Space-separated
        $fileName = "$debtorCode $docType $customCode$extension"
        $filePath = Join-Path $testDir $fileName
        
        # Create a dummy file with some content
        $content = @"
Test Document
==============
Debtor Code: $debtorCode
Document Type: $docType
Custom Code: $customCode
Generated: $(Get-Date)

This is a sample attachment for bulk email testing.
"@
        Set-Content -Path $filePath -Value $content
        Write-Host "  ? Created: $fileName" -ForegroundColor Green
        $fileCount++
    }
}

# Add some unmatched files for testing
Write-Host "`nAdding unmatched files for error testing..." -ForegroundColor Yellow
$unmatchedFiles = @(
    "README.txt",
    "notes.txt", 
    "InvalidFormat_NoCode.pdf",
    "DEBT001_WRONG_FORMAT.pdf",
    "NOCODE 123.pdf"
)

foreach ($file in $unmatchedFiles) {
    $filePath = Join-Path $testDir $file
    Set-Content -Path $filePath -Value "This file doesn't match the naming convention"
    Write-Host "  ? Created: $file (unmatched)" -ForegroundColor Yellow
}

# Create ZIP file
Write-Host "`nCreating ZIP file..." -ForegroundColor Cyan
Compress-Archive -Path "$testDir\*" -DestinationPath $zipFile -Force

# Get file info
$zipInfo = Get-Item $zipFile
$zipSizeMB = [math]::Round($zipInfo.Length / 1MB, 2)

# Clean up test directory
Remove-Item $testDir -Recurse -Force

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "? Test ZIP file created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "File location: $zipFile" -ForegroundColor Cyan
Write-Host "File size: $zipSizeMB MB" -ForegroundColor Cyan
Write-Host "`nFile Statistics:" -ForegroundColor Cyan
Write-Host "  - Matched Files: $fileCount" -ForegroundColor White
Write-Host "  - Unmatched Files: $($unmatchedFiles.Count)" -ForegroundColor White
Write-Host "  - Total Files: $($fileCount + $unmatchedFiles.Count)" -ForegroundColor White
Write-Host "  - Debtors: $($debtorCodes.Count)" -ForegroundColor White
Write-Host "`nSample filenames generated:" -ForegroundColor Yellow
Write-Host "  - DEBT001 INV 12345.pdf" -ForegroundColor Gray
Write-Host "  - ABC123 SOA 987654.pdf" -ForegroundColor Gray
Write-Host "  - XYZ789 OTHER 456789.xlsx" -ForegroundColor Gray
Write-Host "`nYou can now upload this file to test the application." -ForegroundColor Green
