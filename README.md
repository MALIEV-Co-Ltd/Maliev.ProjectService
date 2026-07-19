# Maliev Project Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/MALIEV-Co-Ltd/Maliev.ProjectService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL-blue)](https://www.postgresql.org/)

Manages customer manufacturing projects from creation through quotation acceptance.

**Role in MALIEV Architecture**: The Project Service is the entry point for customer manufacturing projects. It handles project creation, part uploads, AI-assisted pricing, quotation generation, and acceptance. Once a quotation is accepted, it triggers OrderService to create orders.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **Database**: PostgreSQL with Entity Framework Core 10.x
- **Messaging**: RabbitMQ via MassTransit
- **API Documentation**: OpenAPI 3.1 + Scalar UI
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
- ❌ **Swagger / Swashbuckle**: Using **Scalar** for API documentation.
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations or manual logic only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **Aspire Integration**: Fully integrated with Maliev.Aspire for local development.

---

## ✨ Key Features

- **Project Management**: Create and manage manufacturing projects with unique project numbers (PRJ-YYYY-NNNN)
- **Part Upload**: Upload CAD files for manufacturing with process type detection (FDM, SLA, CNC)
- **AI-Assisted Pricing**: Automated pricing based on geometry analysis and material costs
- **Quotation Generation**: Generate professional quotations from confirmed parts
- **Quotation Portal**: Customer-facing quote.maliev.com integration for acceptance

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL & RabbitMQ

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/MALIEV-Co-Ltd/Maliev.ProjectService.git
cd Maliev.ProjectService
```

2. **Run via Aspire**
The easiest way to run the service is through the `Maliev.Aspire.AppHost` project.

3. **Manual Run**
```bash
dotnet run --project Maliev.ProjectService.Api
```

The service will be available at `http://localhost:5200/project`. Access the interactive documentation at `http://localhost:5200/project/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/project/v1/`.

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/projects` | List all projects | `project.projects.read` |
| GET | `/projects/{id}` | Get project details | `project.projects.read` |
| POST | `/projects` | Create new project | `project.projects.create` |
| PATCH | `/projects/{id}` | Update project | `project.projects.update` |
| DELETE | `/projects/{id}` | Delete project | `project.projects.delete` |
| POST | `/projects/{id}/parts` | Add part to project | `project.projects.update` |
| POST | `/projects/{id}/parts/{partId}/confirm` | Confirm part for pricing | `project.projects.update` |
| POST | `/projects/{id}/quote` | Generate quotation | `project.projects.quote` |
| POST | `/projects/{id}/accept` | Accept quotation | `project.projects.accept` |
| GET | `/projects/{id}/status-history` | Get status change history | `project.projects.read` |

### Permission Model

Project permissions are staff/service permissions by default. Tokens that carry a `customer_id` or `customerId` claim are treated as customer-scoped: list/search requests are forced to that customer, create requests must use that customer ID, and project object routes return `403` for projects owned by another customer. Customer-facing flows should continue to use the BFF/portal boundary instead of granting broad project permissions directly.

### Health Probes
- Liveness: `GET /project/liveness`
- Readiness: `GET /project/readiness`
- Metrics: `GET /project/metrics`

---

## 🧪 Testing

```bash
dotnet test --verbosity normal
```

---

## 📦 Deployment

Deployment is managed via ArgoCD using the `maliev-gitops` repository.

---

## 📄 License

Proprietary - © 2026 MALIEV Co., Ltd. All rights reserved.
