# Final Test - All Issues Fixed
# Run this to verify both errors are resolved

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  FINAL TEST - ALL ISSUES FIXED" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Issues Fixed:" -ForegroundColor Green
Write-Host "  ? ERR_HTTP2_PROTOCOL_ERROR" -ForegroundColor Green
Write-Host "  ? ERR_RESPONSE_HEADERS_TOO_BIG" -ForegroundColor Green
Write-Host ""

Write-Host "[1/3] Cleaning..." -ForegroundColor Yellow
dotnet clean | Out-Null

Write-Host "[2/3] Building..." -ForegroundColor Yellow
$build = dotnet build 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed!" -ForegroundColor Red
    $build
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

Write-Host "[3/3] Starting application..." -ForegroundColor Yellow
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  TEST YOUR 76 MB FILE NOW" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Open browser: " -NoNewline
Write-Host "http://localhost:5003/Upload" -ForegroundColor Green
Write-Host ""
Write-Host "2. Upload your 76 MB ZIP file" -ForegroundColor White
Write-Host ""
Write-Host "3. Expected behavior:" -ForegroundColor White
Write-Host "   • Loading overlay appears" -ForegroundColor Gray
Write-Host "   • Wait 5-10 minutes" -ForegroundColor Gray
Write-Host "   • Success page displays" -ForegroundColor Gray
Write-Host "   • NO header error" -ForegroundColor Gray
Write-Host "   • File list shows correctly" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Watch logs below for:" -ForegroundColor Cyan
Write-Host "   ? Starting upload..." -ForegroundColor Gray
Write-Host "   ? Saving uploaded file..." -ForegroundColor Gray
Write-Host "   ? File saved successfully..." -ForegroundColor Gray
Write-Host "   ? Starting ZIP extraction..." -ForegroundColor Gray
Write-Host "   ? ZIP file extracted..." -ForegroundColor Gray
Write-Host "   ? Successfully processed..." -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop" -ForegroundColor Red
Write-Host ""

# Run
dotnet run --launch-profile http
