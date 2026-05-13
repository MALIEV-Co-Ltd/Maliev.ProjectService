# Maliev.ProjectService — Project Management Agent

This document contains instructions for AI agents operating in this repository.

## 1. Service Scope

**Service Name**: `Maliev.ProjectService`
**Role**: Manages customer manufacturing projects from creation through quotation acceptance.
**Domain**: Project Management (Projects, Parts, Quotations, Pricing).

### Key Responsibilities
- **Project Lifecycle**: Track projects through 14 states: `Draft` → `Configuring` → `Pricing` → `Quoting` → `AwaitingAcceptance` → `Accepted` → `InProgress` → `OnHold` → `Paused` → `PreparingShipment` → `Shipped` → `Delivered` → `Completed` → `Cancelled`
- **Project Numbers**: Format `PRJ-YYYY-NNNN` (e.g., PRJ-2026-0001)
- **Part Management**: Upload CAD files with process type (FDM, SLA, CNC, SheetMetal, Casting)
- **AI Pricing**: Integration with PricingService for automated cost estimation
- **Quotation**: Generate quotations from confirmed parts, trigger customer acceptance flow
- **Event Publishing**: Publish `ProjectCreatedEvent`, `ProjectPartAddedEvent`, `ProjectQuotationGeneratedEvent`, `ProjectQuotationAcceptedEvent`, `ProjectStatusChangedEvent`
- **Event Consumption**: Consume `QuotationAcceptedEvent`, `OrderCreatedEvent`, `JobStatusChangedEvent`

## 2. Environment & Build

- **Framework**: .NET 10.0 (C# 13)
- **Database**: PostgreSQL 18 (using Entity Framework Core 10)
- **Architecture**: Clean Architecture (Api, Application, Domain, Infrastructure, Tests)
- **TreatWarningsAsErrors**: ENABLED. Zero compilation warnings allowed.
- **Documentation**: Scalar UI at `/project/scalar`

### Commands

- **Build**: `dotnet build Maliev.ProjectService.slnx`
- **Test (All)**: `dotnet test`
- **Test (Single)**: `dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"`
- **Run API**: `dotnet run --project Maliev.ProjectService.Api`
- **Database Migrations**: `dotnet ef migrations add <MigrationName> --project Maliev.ProjectService.Infrastructure --startup-project Maliev.ProjectService.Infrastructure`
- **Database Update**: `dotnet ef database update --project Maliev.ProjectService.Infrastructure --startup-project Maliev.ProjectService.Infrastructure`

## 3. Code Style & Conventions

### General
- **Namespaces**: Use file-scoped namespaces (e.g., `namespace Maliev.ProjectService.Domain.Entities;`).
- **Formatting**: Standard C# conventions (PascalCase for classes/methods, camelCase for local variables).
- **Nullability**: `Nullable` context is ENABLED. Handle nulls explicitly. Use `?` for optional references.
- **Documentation**: XML documentation `///` is **REQUIRED** for all public methods and properties.

### Domain Entities
- **IDs**: Use `Guid` for primary keys.
- **Dates**: Use `DateTimeOffset` instead of `DateTime`.
- **Collections**: Initialize collection properties (e.g., `public ICollection<ProjectPart> Parts { get; set; } = new List<ProjectPart>();`).
- **Navigation Properties**: Mark as nullable if optional.

### Architecture Rules (Strict)
- **No AutoMapper**: Perform manual mapping.
- **No FluentValidation**: Use Data Annotations (`[Required]`, `[EmailAddress]`).
- **No FluentAssertions**: Use standard xUnit `Assert`.
- **No In-Memory DB**: Use **Testcontainers** for integration tests.
- **No Secrets**: Configuration via environment variables only.

## 4. Domain Rules

### Project Number Format
- Format: `PRJ-YYYY-NNNN` (e.g., PRJ-2026-0001)
- Generated via `ProjectNumberGenerator` in Infrastructure
- Year resets to current year, sequence increments per year

### Pricing Override
- Manual price adjustments require `reason` field
- Audit log required for all overrides

### Quotation Requirements
- ALL parts must be confirmed before quotation can be generated
- Quotation includes: part details, material costs, processing fees, lead time, terms

### Deletion Restrictions
- Only `Draft` and `Configuring` status projects can be deleted
- Deletion requires cascade delete of parts

### Part Management
- Parts cannot be removed after project status reaches `Quoting` or later
- Part status: `Pending` → `Confirming` → `Confirmed` → `Pricing` → `Priced` → `Error`

## 5. Permissions

Use GCP-style permissions with plural resource format:

| Permission | Resource | Action |
|------------|----------|--------|
| `project.projects.read` | projects | List, Get, StatusHistory |
| `project.projects.create` | projects | Create |
| `project.projects.update` | projects | Update, AddPart, ConfirmPart |
| `project.projects.delete` | projects | Delete (Draft/Configuring only) |
| `project.projects.quote` | projects | GenerateQuotation |
| `project.projects.accept` | projects | AcceptQuotation |

Security boundary: these are staff/service permissions unless the bearer token has a `customer_id` or `customerId` claim. Customer-scoped tokens must be constrained to that customer for create/search/detail/mutation routes. Before changing a controller or DTO in this service, verify request DTOs, BFF proxy payloads, service DTOs, JSON names, and tests that assert the actual wire shape.

## 6. Events

### Consumed
- `QuotationAcceptedEvent` — Not used by ProjectService (consumed by OrderService)
- `OrderCreatedEvent` — May receive for reference
- `JobStatusChangedEvent` — May receive for project status updates

### Published
- `ProjectCreatedEvent` — When new project is created
- `ProjectPartAddedEvent` — When part is added to project
- `ProjectQuotationGeneratedEvent` — When quotation is generated
- `ProjectQuotationAcceptedEvent` — When quotation is accepted (triggers OrderService)
- `ProjectStatusChangedEvent` — When project status changes

## 7. Testing Guidelines

- **Integration over Unit**: Prioritize integration tests using Testcontainers/PostgreSQL.
- **Naming**: `MethodName_StateUnderWhichTestIsRunning_ExpectedBehavior` (e.g., `AdvanceStatus_FromInProgressToFinishing_UpdatesState`).
- **Structure**: Arrange, Act, Assert comments are optional but encouraged for complex tests.

## 8. Agent Behavior
- **Proactive Fixes**: If you see a warning, fix it.
- **Verification**: ALWAYS run `dotnet build` after changes.
- **Safety**: Do not commit secrets.


## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.ProjectService.Infrastructure --startup-project Maliev.ProjectService.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead

<!-- MANUAL ADDITIONS START -->
<!-- Add service-specific agent instructions below this line -->

## 9. ProjectService Specific Notes

### Pricing Integration
- Uses `IPricingServiceClient` for AI-assisted pricing
- Mock implementation available for testing via `FakePricingServiceClient`

### Quotation Service Integration
- Uses `IQuotationServiceClient` for quotation generation
- Mock implementation available for testing via `FakeQuotationServiceClient`

### File Upload
- Part files are stored in Azure Blob Storage (configured via environment)
- Supported formats: STL, 3MF, OBJ, STEP, IGES

### Customer Portal
- Quotation acceptance via quote.maliev.com public portal
- Anonymous acceptance link with project-specific token

<!-- MANUAL ADDITIONS END -->
