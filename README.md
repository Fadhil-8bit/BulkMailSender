# BulkMailSender

This is a web application developed during my IT Support Internship at ATP Sales & Services Sdn Bhd. It was built specifically for internal company use to automate and simplify the process of sending bulk emails, such as monthly Statements of Account (SOA), Invoices, and Overdue notices to clients.

**Note:** This project is intended for internal use only.

## Features

* **Automated Attachments:** Upload a ZIP file containing client documents, and the system automatically extracts and matches the files to the correct recipient.
* **Recipient Management:** Import recipient lists easily via a simple CSV upload.
* **Custom Templates:** Write email templates using dynamic placeholders (like `{DebtorCode}`) for personalization.
* **Background Processing:** Sends emails in the background to keep the app responsive, complete with automatic retries for failed deliveries.

<video width="100%" controls>
  <source src="demo.mp4" type="video/mp4">
</video>

## Tech Stack

* ASP.NET Core (.NET 10)
* Razor Pages
* Bootstrap 5
* Docker


## Docker Deployment

To run the app using Docker and ensure your data (like settings and extracted files) is saved permanently on your machine, follow these steps:

**1. Create a Data Folder**
Before running the container, create a folder on your computer to hold the application data. 
* Create a new folder named `BulkMailSenderData` directly in your `C:\` drive. (The path should be `C:\BulkMailSenderData`).

**2. Build the Docker Image**
Open your terminal in the project folder and run:
```bash
docker build -t bulkmailsender:latest -f Dockerfile.production .
```
## Run the Container

Use the following command to start the app. This will link the app's internal data folder to the folder you created in Step 1:
```bash
docker run -d -p 8080:8080 -v "C:\BulkMailSenderData:/app/App_Data" --name mail-sender-app bulkmailsender:latest
```
Access the application by opening your web browser and navigating to: [http://localhost:**8080**](http://localhost:**8080**)

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

