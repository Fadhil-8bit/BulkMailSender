# QUICK TEST SCRIPT
# Run this to test the fix

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ERR_HTTP2_PROTOCOL_ERROR FIX TEST" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean
Write-Host "[1/4] Cleaning previous build..." -ForegroundColor Yellow
dotnet clean | Out-Null
Write-Host "? Clean complete" -ForegroundColor Green
Write-Host ""

# Step 2: Build
Write-Host "[2/4] Building project..." -ForegroundColor Yellow
$buildResult = dotnet build 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed!" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

# Step 3: Instructions
Write-Host "[3/4] Starting application..." -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  IMPORTANT INSTRUCTIONS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Application will start with HTTP profile" -ForegroundColor White
Write-Host "2. Open browser to: " -NoNewline -ForegroundColor White
Write-Host "http://localhost:5003/Upload" -ForegroundColor Green
Write-Host "3. Select your 76 MB ZIP file" -ForegroundColor White
Write-Host "4. Click 'Upload and Extract'" -ForegroundColor White
Write-Host "5. WAIT 5-10 minutes (do not close browser)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Watch the logs below for progress:" -ForegroundColor Cyan
Write-Host "  - Starting upload..." -ForegroundColor Gray
Write-Host "  - Saving uploaded file..." -ForegroundColor Gray
Write-Host "  - File saved successfully..." -ForegroundColor Gray
Write-Host "  - Starting ZIP extraction..." -ForegroundColor Gray
Write-Host "  - ZIP file extracted..." -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop" -ForegroundColor Red
Write-Host ""
Write-Host "[4/4] Running application..." -ForegroundColor Yellow
Write-Host ""

# Step 4: Run
dotnet run --launch-profile http
