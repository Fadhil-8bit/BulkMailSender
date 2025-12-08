# ?? HTTPS Deployment for BulkMailSender

## Why HTTPS?
This application handles customer data (emails, invoices, financial documents), so HTTPS is required even for internal use.

---

## ?? Quick Setup (3 Commands)

### 1. Generate Certificate (One Time Only)
```powershell
# Create certificate directory
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.aspnet\https"

# Generate certificate
dotnet dev-certs https -ep "$env:USERPROFILE\.aspnet\https\aspnetapp.pfx" -p "BulkMail2025!"

# Trust the certificate
dotnet dev-certs https --trust
```

### 2. Build Docker Image
```powershell
docker build -t bulkmailsender:latest -f Dockerfile.production .
```

### 3. Run Container
```powershell
docker run -d `
    -p 8080:8080 `
    -p 8081:8081 `
    --name bulkmail `
    -v "$env:USERPROFILE\.aspnet\https:/https:ro" `
    bulkmailsender:latest
```

### 4. Access Application
- **HTTPS:** https://localhost:8081 ?
- **HTTP:** http://localhost:8080 (redirects to HTTPS)

---

## ?? Access from Mobile Devices (Android/iOS)

### 1. Open Windows Firewall Ports (Run as Administrator)
```powershell
# Allow HTTP access (port 8080)
New-NetFirewallRule -DisplayName "BulkMailSender HTTP" `
    -Direction Inbound `
    -LocalPort 8080 `
    -Protocol TCP `
    -Action Allow

# Allow HTTPS access (port 8081)
New-NetFirewallRule -DisplayName "BulkMailSender HTTPS" `
    -Direction Inbound `
    -LocalPort 8081 `
    -Protocol TCP `
    -Action Allow
```

### 2. Find Your Windows PC IP Address
```powershell
ipconfig | findstr "IPv4"
```
Example output: `192.168.1.100`

### 3. Access from Mobile Browser
```
http://192.168.1.100:8080   (HTTP - will redirect to HTTPS)
https://192.168.1.100:8081  (HTTPS - accept certificate warning)
```

**Important:** Both devices must be on the same WiFi network!

---

## ?? Daily Usage

### Start Container
```powershell
docker start bulkmail
```

### Stop Container
```powershell
docker stop bulkmail
```

### View Logs
```powershell
docker logs bulkmail
```

### Restart Container
```powershell
docker restart bulkmail
```

---

## ?? Switching Between HTTP and HTTPS

### For HTTP Only (Development/Testing)
Use the regular Dockerfile:
```powershell
# Build
docker build -t bulkmailsender:dev -f BulkMailSender/Dockerfile .

# Run
docker run -d -p 8080:8080 --name bulkmail-dev bulkmailsender:dev

# Open firewall (if accessing from other devices)
New-NetFirewallRule -DisplayName "BulkMailSender HTTP" `
    -Direction Inbound `
    -LocalPort 8080 `
    -Protocol TCP `
    -Action Allow
```
Access: http://localhost:8080 or http://YOUR_IP:8080

### For HTTPS (Production/Customer Data)
Use Dockerfile.production (as shown above)

---

## ?? Notes

- **Certificate Location:** `%USERPROFILE%\.aspnet\https\aspnetapp.pfx`
- **Certificate Password:** `BulkMail2025!` (change in production)
- **Certificate Validity:** 1 year (regenerate annually)
- **Browser Warning:** Normal for self-signed certificates - click "Advanced" ? "Proceed"

---

## ?? Certificate Expiration

If certificate expires (after 1 year):
```powershell
# Clean old certificate
dotnet dev-certs https --clean

# Recreate directory and generate new certificate
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.aspnet\https"
dotnet dev-certs https -ep "$env:USERPROFILE\.aspnet\https\aspnetapp.pfx" -p "BulkMail2025!"
dotnet dev-certs https --trust

# Restart container
docker restart bulkmail
```

---

## ?? Troubleshooting

### Error: "Please create the target directory before exporting"
```powershell
# Solution: Create the directory first
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.aspnet\https"
```

### Error: "Container won't start" or "Certificate not found"
```powershell
# Check if certificate exists
Test-Path "$env:USERPROFILE\.aspnet\https\aspnetapp.pfx"

# If False, regenerate certificate
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.aspnet\https"
dotnet dev-certs https -ep "$env:USERPROFILE\.aspnet\https\aspnetapp.pfx" -p "BulkMail2025!"
```

### Browser Shows "Connection Refused"
```powershell
# Check if container is running
docker ps

# If not running, start it
docker start bulkmail

# Check logs for errors
docker logs bulkmail
```

### Cannot Access from Android/Mobile Device
**Symptoms:** Works on Windows (localhost:8080) but not from phone

**Cause:** Windows Firewall blocking external connections

**Solution:**
```powershell
# Run as Administrator on Windows
New-NetFirewallRule -DisplayName "BulkMailSender HTTP" `
    -Direction Inbound `
    -LocalPort 8080 `
    -Protocol TCP `
    -Action Allow

New-NetFirewallRule -DisplayName "BulkMailSender HTTPS" `
    -Direction Inbound `
    -LocalPort 8081 `
    -Protocol TCP `
    -Action Allow
```

**Verify:**
1. Check both devices are on same WiFi network
2. Get Windows IP: `ipconfig | findstr "IPv4"`
3. Try accessing: `http://WINDOWS_IP:8080` from mobile browser

### Port 8080 or 8081 Already in Use
```powershell
# Find what's using the port
netstat -ano | findstr "8080"

# Stop the process using that port
taskkill /PID <process_id> /F

# Or use a different port
docker run -d -p 9090:8080 --name bulkmail bulkmailsender:latest
# Access: http://localhost:9090
```

### Certificate Warning on Mobile Devices
**This is normal for self-signed certificates**

To proceed:
- **Android Chrome:** Click "Advanced" ? "Proceed to [IP] (unsafe)"
- **iOS Safari:** Click "Show Details" ? "visit this website"

The connection is still encrypted, browsers just don't trust self-signed certificates.

---

**Last Updated:** January 2025
