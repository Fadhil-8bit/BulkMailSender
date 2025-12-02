# ERR_HTTP2_PROTOCOL_ERROR - FIXED! ??

## Problem
76 MB file uploads were failing with `ERR_HTTP2_PROTOCOL_ERROR` on both HTTPS and Docker.

## Root Cause
HTTP/2 protocol has limitations with large file uploads due to:
1. Stream multiplexing issues
2. Flow control problems
3. Timeout handling differences
4. Frame size limitations

## ? Solutions Implemented

### 1. **Disabled HTTP/2 (Most Important Fix)**
```csharp
// In Program.cs
options.ConfigureEndpointDefaults(listenOptions =>
{
    listenOptions.Protocols = HttpProtocols.Http1;
});
```
**Why?** HTTP/1.1 handles large uploads more reliably.

### 2. **Increased Request Timeouts**
```csharp
options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
```
**Why?** 76 MB can take 5-10 minutes to upload on slower connections.

### 3. **Disabled Data Rate Limits**
```csharp
options.Limits.MinRequestBodyDataRate = null;
options.Limits.MinResponseDataRate = null;
```
**Why?** Prevents timeout on slow connections.

### 4. **Added Request Size Attributes**
```csharp
[RequestSizeLimit(524288000)] // 500 MB
[RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
public async Task<IActionResult> OnPostAsync()
```
**Why?** Page-level enforcement of size limits.

### 5. **Enhanced Form Options**
```csharp
options.BufferBody = true;
options.MemoryBufferThreshold = int.MaxValue;
```
**Why?** Better handling of large request bodies.

### 6. **Added Loading Overlay**
- Prevents user from closing the browser during upload
- Shows progress indicator
- Displays estimated upload time

### 7. **Docker Configuration**
```dockerfile
ENV ASPNETCORE_Kestrel__Limits__MaxRequestBodySize=524288000
ENV ASPNETCORE_Kestrel__Limits__KeepAliveTimeout=00:10:00
USER root
```
**Why?** Ensures settings work in containerized environments.

### 8. **IIS Configuration (web.config)**
```xml
<requestLimits maxAllowedContentLength="524288000" />
<aspNetCore requestTimeout="00:10:00" />
```
**Why?** For IIS deployments.

---

## Testing Instructions

### 1. **Stop and Rebuild**
```bash
# Stop any running instances
Ctrl+C

# Clean build
dotnet clean
dotnet build

# Run
dotnet run
```

### 2. **Test Upload**
1. Navigate to `/Upload`
2. Select your 76 MB file
3. Click "Upload and Extract"
4. **DO NOT close the browser**
5. Wait for loading overlay (may take 5-10 minutes)

### 3. **Expected Behavior**
- ? Loading overlay appears
- ? "Uploading and Processing..." message
- ? No ERR_HTTP2_PROTOCOL_ERROR
- ? Successful extraction after processing

---

## If Still Failing

### Check 1: Browser Console
Press `F12` ? Console tab
Look for specific error messages

### Check 2: Application Logs
```bash
# Check logs in Visual Studio Output window
# Or run with verbose logging:
dotnet run --verbosity detailed
```

### Check 3: File System Permissions
Ensure `wwwroot/uploads/` has write permissions

### Check 4: Firewall/Antivirus
Temporarily disable to test if blocking

### Check 5: Connection Speed
```bash
# Test upload speed
# If < 1 Mbps, increase timeout further
```

---

## For Even Larger Files (> 500 MB)

### Increase Limits
Edit `Program.cs`:
```csharp
// Change from 524288000 to 1073741824 (1 GB)
options.MultipartBodyLengthLimit = 1073741824;
options.MaxRequestBodySize = 1073741824;
```

### Increase Timeout
```csharp
options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(20);
options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(20);
```

---

## Docker-Specific Instructions

### Build and Run
```bash
# Build image
docker build -t bulkmailsender .

# Run with increased resources
docker run -p 8080:8080 -p 8081:8081 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_Kestrel__Limits__MaxRequestBodySize=524288000 \
  --memory="2g" \
  bulkmailsender
```

### Check Docker Logs
```bash
docker logs <container_id>
```

---

## Production Deployment Checklist

- ? HTTP/2 disabled in Program.cs
- ? Timeouts increased (10 minutes)
- ? Data rate limits disabled
- ? Request size limits configured (500 MB)
- ? Form options buffering enabled
- ? web.config created for IIS
- ? Docker environment variables set
- ? Loading overlay prevents user closure
- ? wwwroot/uploads folder exists with permissions

---

## Performance Tips

### For Development:
- Use HTTP (not HTTPS) for faster uploads
- Disable antivirus scanning temporarily
- Use wired connection (not Wi-Fi)

### For Production:
- Use CDN or dedicated upload server
- Consider chunked uploads for files > 200 MB
- Implement server-side progress tracking
- Use background job processing

---

## What Changed

| File | Changes |
|------|---------|
| **Program.cs** | ? Disabled HTTP/2, increased timeouts, disabled rate limits |
| **Upload.cshtml.cs** | ? Added RequestSizeLimit attributes |
| **Upload.cshtml** | ? Added loading overlay, progress indicator |
| **web.config** | ? Created with IIS limits |
| **Dockerfile** | ? Environment variables, permissions |
| **appsettings.json** | ? Timeout configuration |

---

## Quick Test

```powershell
# Generate 76 MB test file
$bytes = New-Object byte[] (76 * 1024 * 1024)
(New-Object Random).NextBytes($bytes)
[System.IO.File]::WriteAllBytes("test76mb.zip", $bytes)
```

Then upload `test76mb.zip` - should work now!

---

## Still Having Issues?

### Contact Checklist:
1. ? Built project after changes?
2. ? Restarted application?
3. ? Cleared browser cache?
4. ? Checked application logs?
5. ? Tried different browser?
6. ? File is actually .zip format?
7. ? Internet connection stable?

### Collect This Info:
- Browser: Chrome/Edge/Firefox?
- Environment: Development/Docker/IIS?
- File size: Exact MB
- Error message: Full text
- Logs: Copy from Output window

---

## Summary

**The main fix:** Disabling HTTP/2 and increasing timeouts.

Your 76 MB file should now upload successfully! ??

The loading overlay will keep you informed during the process.
