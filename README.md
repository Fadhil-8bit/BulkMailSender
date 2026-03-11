# BulkMailSender

This is a web application developed during my IT Support Internship at ATP Sales & Services Sdn Bhd. It was built specifically for internal company use to automate and simplify the process of sending bulk emails, such as monthly Statements of Account (SOA), Invoices, and Overdue notices to clients.

**Note:** This project is intended for internal use only.

## Features

* **Automated Attachments:** Upload a ZIP file containing client documents, and the system automatically extracts and matches the files to the correct recipient.
* **Recipient Management:** Import recipient lists easily via a simple CSV upload.
* **Custom Templates:** Write email templates using dynamic placeholders (like `{DebtorCode}`) for personalization.
* **Background Processing:** Sends emails in the background to keep the app responsive, complete with automatic retries for failed deliveries.

## Tech Stack

* ASP.NET Core (.NET 10)
* Razor Pages
* Bootstrap 5
* Docker

## How to Run Locally

If you need to run or test the app locally, ensure you have the .NET 10 SDK installed.

1. Clone this repository.
2. Open your terminal or command prompt in the project folder.
3. Run the following commands:
   ```bash
   dotnet restore
   dotnet run
   ```
4.Open your web browser and navigate to http://localhost:5000.

### Docker Deployment

For a quick setup without installing the .**NET** **SDK**, you can use Docker:

```Bash 
# Build the image 
docker build -t bulkmailsender:latest -f Dockerfile.production .

# Run the container
docker run -d -p 8080:8080 -v "C:\BulkMailSenderData:/app/App_Data"--name mail-sender-app bulkmailsender:latest
 ```
Access the application at http://localhost:8080.
