# IT Service Desk Management System

## Table of Contents

- [Project Overview](#project-overview)
- [Key Features](#key-features)
- [Technology Stack](#technology-stack)
- [Project Architecture](#project-architecture)
- [Folder Structure](#folder-structure)
- [Prerequisites](#prerequisites)
- [Installation and Setup](#installation-and-setup)
- [Application URLs](#application-urls)
- [API Documentation](#api-documentation)
- [Testing](#testing)
- [Build Commands](#build-commands)
- [Screenshots](#screenshots)
- [Possible Future Enhancements](#possible-future-enhancements)
- [Author and Project Information](#author-and-project-information)

## Project Overview

The IT Service Desk Management System is a full-stack application for submitting, organizing, assigning, and tracking IT support requests.

It addresses the common problem of support requests being managed through disconnected messages or informal processes. Users can register, authenticate, and create tickets with categories, priorities, descriptions, and optional attachments. Service desk administrators can manage tickets, assignments, categories, priorities, users, audit records, and reporting data through role-protected screens.

The project is implemented as a local-development capstone application. No production deployment is claimed by this repository.

## Key Features

### Authentication and authorization

- User registration and login through the ASP.NET Core Web API.
- ASP.NET Core Identity account and role management.
- JWT Bearer authentication for protected API requests.
- Three application roles:
  - `User`
  - `Admin`
  - `SuperAdmin`
- Role-protected Angular routes and backend authorization policies.
- Admin and SuperAdmin seed-account provisioning from configuration.
- Existing seeded-account passwords are not changed automatically when the application starts.

### Ticket management

- Create tickets with a title, description, category, and priority.
- User ticket views restricted to the authenticated user's tickets.
- Administrative ticket listing and detail views.
- Ticket search and paginated API access.
- Ticket editing, deletion, and status updates.
- Supported status values include `Open`, `In Progress`, `Resolved`, and `Closed`.
- Category and priority management for authorized administrative users.

### Assignment and notifications

- Assign tickets to users through the administrative assignment workflow.
- View assignment records.
- Assignment actions create audit records.
- The API includes optional SMTP email notification handling through `EmailSettings` configuration.

### Dashboards and reports

- Admin dashboard statistics.
- Category-based ticket statistics.
- Angular reports page for administrative reporting data.
- Separate User, Admin, and SuperAdmin dashboard routes.

### Audit logs

- Audit records are created for supported administrative and ticket workflow actions.
- Authorized Admin and SuperAdmin users can retrieve audit log data.

### Comments and attachments

- Backend ticket comment endpoints and service support are implemented.
- Authorized users can list and create comments for accessible tickets through the API.
- Authorized attachment listing and download.
- User ticket creation supports one optional attachment upload after the ticket is created.
- Allowed attachment types:
  - JPG
  - JPEG
  - PNG
  - PDF
  - DOC
  - DOCX
- Maximum attachment size: 5 MB.
- Backend validation checks file content and stores files using generated names under the runtime `Uploads` directory.
- Attachment download and cleanup enforce path and ticket-access checks.

## Technology Stack

| Area | Technology |
|---|---|
| Backend | ASP.NET Core Web API |
| Backend target | .NET 10 (`net10.0`) |
| Authentication | ASP.NET Core Identity and JWT Bearer |
| Data access | Entity Framework Core 10 |
| Database | SQL Server |
| Frontend | Angular 21 standalone application |
| Frontend language | TypeScript 5.9 |
| Frontend HTTP | Angular `HttpClient` and RxJS |
| UI support | Bootstrap 5.3 and component-specific CSS |
| API documentation | Swagger/OpenAPI |
| Logging | Serilog console and rolling file sinks |
| Backend tests | xUnit, Moq, and EF Core InMemory |
| Package manager | npm 11.8.0, as declared by the client package manifest |

## Project Architecture

The solution is organized into separate backend, domain, frontend, and test projects.

### `ITServiceDesk.API`

The ASP.NET Core Web API project contains:

- HTTP controllers and DTOs.
- ASP.NET Core Identity and JWT configuration.
- Entity Framework Core database context and migrations.
- Ticket, assignment, category, priority, comment, attachment, audit, and email services.
- Authorization policies and CORS configuration.
- Swagger configuration.
- Serilog logging and global exception middleware.

### `ITServiceDesk.Core`

The Core project contains shared domain models, including tickets, categories, priorities, comments, attachments, audit logs, and ticket assignments.

### `ITServiceDesk.Client`

The Angular application contains:

- Standalone UI components.
- Role-aware routing.
- Authentication services and HTTP interception.
- User, Admin, and SuperAdmin dashboards.
- Ticket creation, management, reporting, assignment, audit-log, category, and user-management pages.

### `ITServiceDesk.Tests`

The test project contains .NET unit tests for ticket, assignment, attachment, audit, and comment services. Tests primarily use EF Core's in-memory provider and Moq.

## Folder Structure

Generated files, dependency folders, logs, and uploaded documents are intentionally omitted from this source tree.

```text
ITServiceDesk/
├── .gitignore
├── ITServiceDesk.slnx
├── ITServiceDesk.API/
│   ├── Controllers/
│   ├── Data/
│   ├── DTOs/
│   ├── Migrations/
│   ├── Middleware/
│   ├── Properties/
│   ├── Services/
│   ├── ITServiceDesk.API.csproj
│   └── Program.cs
├── ITServiceDesk.Client/
│   ├── public/
│   ├── src/
│   │   └── app/
│   │       ├── guards/
│   │       ├── interceptors/
│   │       ├── pages/
│   │       ├── services/
│   │       └── shared/
│   ├── angular.json
│   ├── package.json
│   ├── package-lock.json
│   └── tsconfig*.json
├── ITServiceDesk.Core/
│   ├── Models/
│   └── ITServiceDesk.Core.csproj
├── ITServiceDesk.Tests/
│   ├── *Tests.cs
│   └── ITServiceDesk.Tests.csproj
└── docs/
    └── ITServiceDesk_Capstone_Report.md
```

The API's local `Logs/` and `Uploads/` directories are runtime directories and must not be committed.

## Prerequisites

Install the following software before setup:

- .NET 10 SDK.
- SQL Server instance accessible to the API.
- Node.js with an npm version compatible with npm `11.8.0`.
- Angular CLI is available through the project's local npm dependencies; global Angular CLI installation is not required.
- Git.

The exact supported Node.js version is not pinned in the project configuration. Use a current Node.js release compatible with npm 11 and Angular 21.

## Installation and Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd ITServiceDesk
```

### 2. Configure backend secrets

The API reads sensitive configuration through .NET User Secrets or environment variables. Do not place real secrets in `appsettings.json`.

From the repository root, configure the local SQL Server connection:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-sql-server-connection-string>" --project ITServiceDesk.API
```

The API also requires a JWT signing key. Configure it locally:

```bash
dotnet user-secrets set "Jwt:Key" "<generate-a-long-random-signing-key>" --project ITServiceDesk.API
```

Optional privileged seed-account settings use these keys:

```text
AdminSetup:Email
AdminSetup:Password
SuperAdminSetup:Email
SuperAdminSetup:Password
```

Optional SMTP settings use these keys:

```text
EmailSettings:Username
EmailSettings:Password
EmailSettings:SenderEmail
```

The API can start without the optional seed-account and SMTP credential pairs, but the corresponding features will not be configured. Never commit User Secrets, passwords, connection strings, tokens, or SMTP credentials.

### 3. Restore backend dependencies

```bash
dotnet restore ITServiceDesk.slnx
```

### 4. Apply database migrations

The repository includes Entity Framework Core migrations. After configuring SQL Server:

```bash
dotnet ef database update \
  --project ITServiceDesk.API \
  --startup-project ITServiceDesk.API
```

If the `dotnet ef` command is not installed, install the EF Core command-line tool once:

```bash
dotnet tool install --global dotnet-ef
```

### 5. Run the ASP.NET Core backend

The configured HTTP development profile runs at `http://localhost:5204`:

```bash
dotnet run --project ITServiceDesk.API --launch-profile http
```

The HTTPS profile also exposes `https://localhost:7058` and the HTTP endpoint at `http://localhost:5204`:

```bash
dotnet run --project ITServiceDesk.API --launch-profile https
```

### 6. Install and run the Angular frontend

Open a second terminal:

```bash
cd ITServiceDesk.Client
npm install
npm start
```

The Angular development server runs at `http://localhost:4200` and is configured to call the API at `http://localhost:5204`.

## Application URLs

| Purpose | URL |
|---|---|
| Angular client | `http://localhost:4200` |
| API HTTP | `http://localhost:5204` |
| API HTTPS | `https://localhost:7058` |
| API status endpoint | `http://localhost:5204/` |
| API test endpoint | `http://localhost:5204/api/test` |
| Swagger UI in Development | `http://localhost:5204/swagger` |

Swagger is enabled only when the ASP.NET Core environment is `Development`.

## API Documentation

When the backend is running in the Development environment, open:

```text
http://localhost:5204/swagger
```

The Swagger document includes a Bearer JWT security definition. Authenticate through the login endpoint, copy the returned token, select **Authorize**, and enter:

```text
Bearer <your-jwt-token>
```

The API uses role and ownership checks in addition to the JWT authentication requirement.

## Testing

Run all .NET tests from the repository root:

```bash
dotnet test ITServiceDesk.slnx
```

Run the Release test suite:

```bash
dotnet test ITServiceDesk.slnx --configuration Release
```

The client also defines an Angular test script:

```bash
cd ITServiceDesk.Client
npm test
```

## Build Commands

### .NET Release build

```bash
dotnet build ITServiceDesk.slnx --configuration Release
```

### Angular production build

```bash
cd ITServiceDesk.Client
npm run build
```

Angular build output is generated under the client `dist/` directory, which is intentionally excluded from Git.

## Screenshots

Screenshots can be added here after they are captured:

- `[Screenshot: Login page]`
- `[Screenshot: User dashboard]`
- `[Screenshot: Admin ticket management]`
- `[Screenshot: Reports page]`
- `[Screenshot: Swagger UI]`

No screenshot files are currently claimed by this README.

## Possible Future Enhancements

The following are possible future improvements and are not represented as current functionality:

- Add a user-facing password reset flow.
- Add configurable production deployment profiles and HTTPS certificate guidance.
- Add richer client-side automated test coverage.
- Add notification templates and configurable notification providers.
- Add more advanced reporting filters and export options.
- Add documented production operations, backup, monitoring, and retention procedures.

## Author and Project Information

This repository is an IT Service Desk capstone project. No author name, organization, or contact information is specified in the project source, so none is included here.
