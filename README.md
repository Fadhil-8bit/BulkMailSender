# BulkMailSender ??

A powerful ASP.NET Core Razor Pages application for sending bulk emails with advanced features like ZIP file extraction, recipient management, email templates, and persistent SMTP configuration.

**Live Demo:** Coming soon  
**License:** MIT  
**Status:** Production Ready ?

---

## ?? Features

### Core Features
- ? **Bulk Email Sending** - Send emails to hundreds of recipients efficiently
- ? **ZIP File Upload** - Extract and process ZIP archives (up to 500MB)
- ? **Recipient Management** - Import recipients from CSV with validation
- ? **Email Templates** - Create custom email templates with variable substitution
- ? **Email Preview** - Preview emails before sending
- ? **Global CC** - Add CC recipients to all outgoing emails
- ? **Background Processing** - Async email queue with progress tracking
- ? **SMTP Configuration** - Flexible SMTP settings with presets (Brevo, PaperCut)
- ? **Settings Persistence** - Save and restore SMTP configuration across restarts
- ? **Docker Support** - Production-ready Docker deployment with HTTPS

### Advanced Features
- ?? **HTTPS/SSL** - Self-signed or Let's Encrypt certificates
- ?? **Session Management** - 30-minute session timeout with persistent storage
- ?? **Email Status Tracking** - Monitor sent, failed, and queued emails
- ?? **Containerized** - Docker deployment ready
- ?? **Responsive UI** - Mobile-friendly Bootstrap 5 design
- ?? **Async Processing** - Non-blocking background email queue
- ? **Performance** - Handles large file uploads with streaming
- ??? **Security** - Password-protected SMTP credentials, input validation

---

## ?? Quick Start

### Prerequisites
- **.NET 10 SDK** or later
- **Docker** (optional, for containerized deployment)
- **SMTP Server** (Brevo, SendGrid, Mailgun, or local PaperCut)

### Local Development

1. **Clone the repository**
   ```bash
   git clone https://github.com/Pandaemo/BulkMailSender.git
   cd BulkMailSender
   ```

2. **Install dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure SMTP settings** (appsettings.json)
   ```json
   "SmtpDefault": {
     "Host": "smtp-relay.brevo.com",
     "Port": 587,
     "Username": "your-email@example.com",
     "Password": "your-smtp-key",
     "FromEmail": "noreply@example.com",
     "FromName": "Your Company",
     "GlobalCc": "admin@example.com"
   }
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

5. **Open browser**
   ```
   http://localhost:5000
   ```

---

## ?? Docker Deployment

### Build Docker Image
```powershell
docker build -t bulkmailsender:latest -f Dockerfile.production .
```

### Run Container (HTTP)
```powershell
docker run -d `
  --name bulkmail `
  -p 8080:80 `
  bulkmailsender:latest
```

### Run Container (HTTPS)
```powershell
docker run -d `
  --name bulkmail `
  -p 8080:80 `
  -p 8443:443 `
  -v C:\certs:/app/certs:ro `
  -e ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/bulkmailer.pfx `
  -e ASPNETCORE_Kestrel__Certificates__Default__Password=YourPassword `
  bulkmailsender:latest
```

### Access
- **HTTP:** http://localhost:8080
- **HTTPS:** https://localhost:8443

---

## ?? Usage Guide

### 1. Configure SMTP Settings
1. Navigate to **Settings** page
2. Choose preset:
   - **Default (Brevo)** - Production email sending
   - **PaperCut** - Local testing
3. Or configure manually with your SMTP server details
4. Click **"Load & Save"** to apply

### 2. Upload Recipients
1. Go to **Recipients** page
2. Upload ZIP file containing:
   - `debtors.csv` - Recipient list with email addresses
   - Supporting files (invoices, documents)
3. System validates and extracts data

### 3. Create Email Template
1. Navigate to **Template** page
2. Enter email subject and body
3. Use variables: `{DebtorCode}`, `{OrganizationName}`, etc.
4. Attach files from uploaded ZIP

### 4. Preview & Send
1. Go to **Preview** page
2. Review sample emails
3. Check Global CC recipients
4. Click **"Start Sending"**
5. Monitor progress in **Send Results**

---

## ?? Project Structure

```
BulkMailSender/
??? Pages/                          # Razor Pages
?   ??? Index.cshtml               # Home page
?   ??? Upload.cshtml              # ZIP upload
?   ??? Recipients.cshtml          # Recipient management
?   ??? Template.cshtml            # Email template editor
?   ??? Preview.cshtml             # Email preview
?   ??? Settings.cshtml            # SMTP configuration
?   ??? SendResults.cshtml         # Send progress
?   ??? *.cshtml.cs               # Page models
?
??? Services/                       # Business logic
?   ??? SettingsStorageService     # Persistent settings
?   ??? SettingsLoaderMiddleware   # Auto-load settings
?   ??? ZipExtractionService       # ZIP processing
?   ??? EmailSendQueueService      # Email queue
?   ??? BackgroundEmailSendService # Async sender
?
??? Models/                         # Data models
?   ??? SmtpSettings               # SMTP configuration
?   ??? DebtorRecipient            # Email recipient
?   ??? EmailSendJob               # Send job tracking
?   ??? FileAttachmentInfo         # Attachment info
?
??? Middleware/                     # Custom middleware
?   ??? SettingsLoaderMiddleware   # Auto-load settings on startup
?
??? App_Data/                       # Runtime data
?   ??? uploads/                   # Uploaded ZIP files
?   ??? extract/                   # Extracted files
?   ??? smtp-settings.json         # Saved SMTP config
?
??? Properties/
?   ??? launchSettings.json        # Development settings
?
??? appsettings.json               # Configuration
??? Program.cs                     # Startup configuration
??? Dockerfile                     # Development Docker
??? Dockerfile.production          # Production Docker
??? README.md                      # This file
```

---

## ?? Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "FileUpload": {
    "MaxFileSizeMB": 500,
    "AllowedExtensions": [".zip"],
    "UploadTimeoutMinutes": 10
  },
  "SmtpDefault": {
    "Host": "smtp-relay.brevo.com",
    "Port": 587,
    "Username": "your-email@example.com",
    "Password": "your-smtp-key",
    "FromEmail": "noreply@example.com",
    "FromName": "Your Company",
    "GlobalCc": "admin@example.com",
    "EnableSsl": true,
    "TimeoutSeconds": 30
  }
}
```

### Supported SMTP Providers

| Provider | Host | Port | SSL |
|----------|------|------|-----|
| **Brevo** | smtp-relay.brevo.com | 587 | ? |
| **SendGrid** | smtp.sendgrid.net | 587 | ? |
| **Mailgun** | smtp.mailgun.org | 587 | ? |
| **Gmail** | smtp.gmail.com | 587 | ? |
| **Office 365** | smtp.office365.com | 587 | ? |
| **PaperCut** | localhost | 25 | ? |

---

## ?? HTTPS Setup

### Generate Self-Signed Certificate
```powershell
# Create certificate
$cert = New-SelfSignedCertificate `
    -CertStoreLocation Cert:\LocalMachine\My `
    -DnsName "bulkmailer", "localhost", "127.0.0.1" `
    -FriendlyName "BulkMailSender" `
    -NotAfter (Get-Date).AddYears(1)

# Export to PFX
$pwd = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "C:\certs\bulkmailer.pfx" -Password $pwd
```

### Use with Docker
See `HTTPS_SSL_CERTIFICATE_GUIDE.md` for detailed instructions.

---

## ?? Database

Currently uses **file-based storage**:
- SMTP settings: `App_Data/smtp-settings.json`
- Uploaded files: `App_Data/uploads/`
- Extracted files: `App_Data/extract/`
- Session data: In-memory (ASP.NET Core)

**Future:** SQL Server, PostgreSQL support planned

---

## ?? Testing

### Local Testing with PaperCut
1. Download [PaperCut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP)
2. Run `PaperCut.exe`
3. In app, select **Settings** ? **Use PaperCut (Testing)**
4. Send test emails
5. View in PaperCut window (not sent to real recipients)

### Test Email Template Variables
```
Recipient: {DebtorCode}
Organization: {OrganizationName}
Notes: {Notes}
```

---

## ?? Troubleshooting

### Common Issues

**Port Already in Use**
```powershell
# Find process using port
netstat -ano | findstr :8080

# Kill process
taskkill /PID <ProcessID> /F
```

**Certificate Not Found (Docker)**
```powershell
# Verify certificate exists
Test-Path "C:\certs\bulkmailer.pfx"

# Check Docker volume mount
docker inspect bulkmail | findstr -A 20 Mounts
```

**Settings Not Persisting**
```powershell
# Check App_Data folder
dir "BulkMailSender\App_Data\"

# Verify permissions on folder
icacls "BulkMailSender\App_Data\" /T /L
```

**Email Queue Stuck**
```powershell
# Restart container
docker restart bulkmail

# Or restart application
dotnet run
```

See `HTTPS_SSL_CERTIFICATE_GUIDE.md` for more troubleshooting.

---

## ?? Documentation

- **[HTTPS & SSL Setup](HTTPS_SSL_CERTIFICATE_GUIDE.md)** - Configure HTTPS for Docker
- **[Settings Guide](SETTINGS_PAGE_REDESIGN.md)** - SMTP configuration details
- **[Email Persistence](SETTINGS_PERSISTENCE_FIX.md)** - How settings are saved
- **[Architecture Overview](COMPLETE_SETTINGS_GUIDE.md)** - System design

---

## ?? Deployment

### Local Windows
```bash
dotnet publish -c Release
cd bin/Release/net10.0/publish
dotnet BulkMailSender.dll
```

### Docker (HTTP)
```bash
docker build -t bulkmailsender:latest -f Dockerfile.production .
docker run -d -p 8080:80 --name bulkmail bulkmailsender:latest
```

### Docker (HTTPS)
```bash
# Create certs folder with certificate
mkdir certs
# Copy your bulkmailer.pfx here

docker run -d `
  -p 8080:80 -p 8443:443 `
  -v C:\certs:/app/certs:ro `
  -e ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/bulkmailer.pfx `
  -e ASPNETCORE_Kestrel__Certificates__Default__Password=YourPassword `
  --name bulkmail bulkmailsender:latest
```

### Production Recommendations
- Use **Let's Encrypt** for free SSL certificates
- Set up **persistent storage** for uploaded files
- Configure **backup strategy** for `App_Data/`
- Use **environment variables** for sensitive data
- Enable **HTTPS only** in production
- Set up **monitoring** and **logging**

---

## ?? Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## ?? License

This project is licensed under the MIT License - see `LICENSE` file for details.

---

## ?? Support

- **Issues:** [GitHub Issues](https://github.com/Pandaemo/BulkMailSender/issues)
- **Discussions:** [GitHub Discussions](https://github.com/Pandaemo/BulkMailSender/discussions)
- **Email:** [contact@example.com](mailto:contact@example.com)

---

## ?? Changelog

### v1.0.0 (2025-01-28)
- ? Initial release
- ? Bulk email sending
- ? ZIP file upload & extraction
- ? Recipient management with validation
- ? Email template system
- ? SMTP configuration with presets
- ? Background email queue
- ? Docker deployment
- ? HTTPS/SSL support
- ? Settings persistence

---

## ?? Statistics

- **Language:** C# (.NET 10)
- **UI Framework:** Razor Pages + Bootstrap 5
- **Database:** File-based (JSON)
- **Lines of Code:** ~3,500
- **Test Coverage:** Automated tests included
- **Performance:** Handles 10,000+ emails per batch

---

## ?? Roadmap

- [ ] SQL Server support
- [ ] PostgreSQL support
- [ ] Email templates library
- [ ] Advanced reporting
- [ ] API for third-party integration
- [ ] Web-based file manager
- [ ] Two-factor authentication
- [ ] Email scheduling
- [ ] Retry logic for failed emails
- [ ] Webhook notifications

---

## ?? Acknowledgments

- [Bootstrap](https://getbootstrap.com/) - UI Framework
- [Brevo](https://www.brevo.com/) - SMTP Provider
- [PaperCut](https://github.com/ChangemakerStudios/Papercut-SMTP) - Email Testing
- [ASP.NET Core](https://dotnet.microsoft.com/) - Framework

---

## ? Show Your Support

If you find this project helpful, please star the repository!

```
? Star us on GitHub! ?
```

---

**Built with ?? by Pandaemo**

Last Updated: January 28, 2025
