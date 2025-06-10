# Helpers Camp Email API (Backend)

An ASP.NET Core 8 Web API for managing applicants and sending emails for Helpers Camp. This service handles CSV imports, email sending (single and batch), and provides various reporting and administrative endpoints.

## 📋 Table of Contents

* [Features](#-features)
* [Prerequisites](#-prerequisites)
* [Installation & Setup](#-installation--setup)
* [Configuration](#-configuration)
* [Database Migrations](#-database-migrations)
* [Available Endpoints](#-available-endpoints)
* [Models & DTOs](#-models--dtos)
* [Error Handling](#-error-handling)
* [Logging](#-logging)
* [Contributing](#-contributing)

## ✨ Features

* **CSV Import**: Bulk import applicants from CSV with custom mappings (supports Arabic/English headers).
* **Email Sending**: Send invitations either in batch (all unsent) or individually.
* **Email Logging**: Tracks send attempts (success/failure) in `EmailLogs`.
* **Reporting**:

  * Get history of all send attempts
  * List applicants not sent
  * Failed attempts grouped by email
  * Last attempt per applicant
* **Admin Operations**:

  * Add a single trainee via JSON
  * Delete all applicants and logs
  * Update trainee email
* CORS-enabled for frontend integration
* Swagger/OpenAPI for interactive API docs

## 📦 Prerequisites

* .NET 8 SDK
* SQL Server (Express or full edition)
* SMTP server credentials (Gmail, SendGrid, etc.)
* (Optional) [dotnet-ef CLI](https://docs.microsoft.com/ef/core/cli/dotnet)

## 🔧 Installation & Setup

1. **Clone the repository**

   ```bash
   git clone https://github.com/your-org/helpers-camp-email-api.git
   cd helpers-camp-email-api
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore
   ```

3. **Configure** (see [Configuration](#-configuration))

4. **Apply migrations**

   ```bash
   dotnet tool install --global dotnet-ef   # if needed
   dotnet ef database update
   ```

5. **Run the API**

   ```bash
   dotnet run
   ```

   The API will start on `https://localhost:7131` by default.

6. **Swagger UI**
   Open `https://localhost:7131/swagger` in your browser for interactive API docs.

## ⚙️ Configuration

Set up **appsettings.json** or user secrets with:

```json5
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=HelpersCamp;Trusted_Connection=True;"
  },
  "Smtp": {
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "Email": "noreply@example.com",
    "Password": "your-smtp-password"
  },
  "Camp": "Helpers Summer Camp"
}
```

* **DefaultConnection**: Your SQL Server connection string
* **SmtpServer/Port/Email/Password**: SMTP credentials
* **Camp**: Used in email templates as the camp name

## 📑 Database Migrations

This project uses EF Core Code-First migrations.

* **Add a new migration**:

  ```bash
  dotnet ef migrations add AddNewFeature
  ```
* **Apply migrations**:

  ```bash
  dotnet ef database update
  ```

## 🚀 Available Endpoints

Base URL: `https://localhost:7131/api/applicants`

| Method | Route              | Description                                   |
| ------ | ------------------ | --------------------------------------------- |
| POST   | `/upload`          | Upload CSV file (`multipart/form-data`)       |
| GET    | `/GetTrainees`     | List all trainees                             |
| POST   | `/AddTrainee`      | Add single trainee (JSON payload)             |
| DELETE | `/DeleteAll`       | Delete all applicants and email logs          |
| PUT    | `/edit-email`      | Update trainee email (`{ code, newMail }`)    |
| GET    | `/statistics`      | Get counts (total, sent, not sent)            |
| GET    | `/not-sent`        | List applicants not sent                      |
| GET    | `/failed-attempts` | Failed attempts grouped by email              |
| GET    | `/last-tries`      | Last attempt per applicant                    |
| GET    | `/history`         | Full email send history (with applicant data) |
| GET    | `/{id}`            | Get trainee by ID                             |

## 🗂 Models & DTOs

* **Trainee**: Represents an applicant/trainee. Includes `Id`, `Code`, `Email`, `FullName`, `Status`, `CreatedAt`, and navigation to `EmailLogs`.
* **EmailLog**: Tracks each send attempt. Fields: `Id`, `ApplicantId`, `SentAt`, `Success`, `ErrorMessage`.
* **AddTraineeDto**: `{ Code, Email, FullName, Status }`
* **UpdateTraineeEmailDto**: `{ Code, NewMail }`

## 🛠 Error Handling

* Uses exception filters and global error handling middleware.
* Model validation returns HTTP 400 with details.
* Conflicts (duplicate trainee) return HTTP 409.
* Not found resources return HTTP 404.

## 📊 Logging

* Console logging by default.
* Extend `appsettings.json` to configure other providers (e.g., file, Application Insights).

## 🤝 Contributing

1. Fork the repo
2. Feature branch: `git checkout -b feat/your-feature`
3. Commit changes: `git commit -m "feat: your feature"`
4. Push: `git push origin feat/your-feature`
5. Open a PR

