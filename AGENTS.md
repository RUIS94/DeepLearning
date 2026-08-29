# AGENTS.md — Project Map

A navigation file for AI collaborators: what this project looks like, where to find things, where to make changes, where new features should go, and what can be reused from where.

## What this project is

Backend for the NAATI CT translation practice software. .NET 10 + EF Core (Npgsql) + Supabase (hosted Postgres).
Standard four-layer Clean Architecture + CQRS (MediatR) + Repository/UnitOfWork.

**There is no frontend in this repo.** The design doc specifies a Next.js/React frontend, but nothing for it — no project, no scaffold, not even a folder — exists anywhere in this working directory. Don't assume one exists elsewhere in the workspace; it hasn't been started, and starting it is a separate scoping decision (App Router vs. other, TailwindCSS/shadcn vs. Ant Design, etc.) that hasn't been made yet.

## ⚠️ Read this section first: this is a "skeleton" project — many files only have a class name, no content

This isn't code that got deleted or something that's broken — the project was originally scaffolded from a Clean Architecture template with the entire folder structure and empty classes already in place (things like `namespace X { internal class Y {} }`), and only a small portion of the business logic has been filled in so far. **Before changing anything, confirm whether the target file is a "real implementation" or an "empty shell" — don't assume it already works.**

### Parts that are already real implementations

| Location | Content |
|---|---|
| `src/DeepLearning.Domain/Entities/*.cs` | All 29 entities, fields cross-checked one by one against the tables in Supabase (2026-08-29), trustworthy |
| `src/DeepLearning.Domain/Enums/*.cs` | All real, correspond to native Postgres enums |
| `src/DeepLearning.Domain/Common/Entity.cs` | Real (`Id` + value equality) |
| `src/DeepLearning.Infrastructure/Persistence/Configurations/*.cs` | All real, EF Fluent API mappings |
| `src/DeepLearning.Infrastructure/Persistence/AppDbContext.cs` | Real: DbSets, native enum registration, `ApplyConfigurationsFromAssembly` |
| `src/DeepLearning.Infrastructure/Persistence/{AppDbContextFactory,NpgsqlEnumConfiguration,UnitOfWork}.cs` | Real |
| `src/DeepLearning.Infrastructure/DependencyInjection.cs` | Real: registers DbContext + `IUnitOfWork` |
| `src/DeepLearning.Infrastructure/Persistence/Migrations/*` | Real, reflects the current schema |
| `src/DeepLearning.Application/Behaviors/*.cs` | Real: the three MediatR pipeline behaviors — Logging / Validation / UnhandledException |
| `src/DeepLearning.Application/DependencyInjection.cs` | Real: `AddApplication()` registers MediatR + FluentValidation + behaviors |
| `src/DeepLearning.Application/Interfaces/IUnitOfWork.cs` | Real |
| `src/DeepLearning.Api/Program.cs` | Real: `AddApplication()` + `AddInfrastructure()` are wired up, plus health checks, the global exception handler, and correlation-id middleware |
| `src/DeepLearning.Domain/Exceptions/{DomainException,NotFoundException,ConflictException}.cs` | Real: base type + the two cases the API layer maps to 404/409 |
| `src/DeepLearning.Api/Middleware/{GlobalExceptionHandler,CorrelationIdMiddleware}.cs` | Real |
| `src/DeepLearning.Api/Constants/{ApiRoutes,ApiErrorMessages}.cs` | Real |
| `Application/Interfaces/I{ExamType,AssessmentDimension,ErrorTaxonomy,PromptTemplate,User}Repository.cs` + matching `Infrastructure/Persistence/Repositories/*.cs` | Real, registered in DI |
| `Application/Interfaces/IPasswordHasher.cs` + `Infrastructure/Common/Pbkdf2PasswordHasher.cs` | Real: PBKDF2, no extra NuGet dependency |
| `Application/Features/ExamConfig/**` (Create/Get/List for exam types, assessment dimensions, error taxonomies, prompt templates) | Real — Create+GetById+List only, no Update/Delete (see rationale in the CQRS section below) |
| `Application/Features/Users/**` (`RegisterUser`, `GetUserById`) | Real — no login/JWT yet, that's still open |
| `Api/Controllers/{ExamTypes,AssessmentDimensions,ErrorTaxonomies,PromptTemplates,Users}Controller.cs` | Real, routed under `api/v1/...` |
| `tests/DeepLearning.UnitTests/TestInfrastructure/*.cs` | Real: `PostgresContainerFixture` (Testcontainers Postgres for repository tests) + `ApiWebApplicationFactory` (same, wired to a real ASP.NET Core host via `WebApplicationFactory<Program>`) |
| `tests/DeepLearning.UnitTests/{Api,Integration,Application/Features/ExamConfig}/**` | Real: unit tests for validator rules, integration tests hitting a real throwaway Postgres, API tests hitting a real HTTP host — see "Running the tests" below |

### Parts that are still empty shells, waiting to be filled in

- `src/DeepLearning.Domain/Common/{Result,Guard,ErrorCodes}.cs`
- `src/DeepLearning.Domain/Events/*.cs` (the domain event classes themselves are empty too; `AggregateRoot` doesn't yet have a mechanism for collecting events) — still open, needed starting the Step 6 domain-events work
- `src/DeepLearning.Domain/Exceptions/{InvalidSubmissionStateException,RubricVersionNotFoundException}.cs` — belong to the Submissions/grading work (Step 4/5), left stubbed on purpose
- `IQuestionRepository`, `ISubmissionRepository`, `IWeakPointRepository`, `ILlmClient`, `IExamConfigLoader`, `IGradingResultInterpreter`, `IProgressRepository`, `IStandardOverrideRepository` under `Application/Interfaces/`
- `src/DeepLearning.Application/Common/{PagedRequest,PagedResult}.cs`
- **All** business code under `Features/{Questions,Submissions,FollowUps,WeakPoints,Progress,ReviewLibrary,QuestionBank}/**` except the `EventHandlers` stubs and `Features/Questions/Commands/GenerateQuestion/` (still an empty-class stub — the folder shape can be copied, the contents can't)
- `src/DeepLearning.Infrastructure/Persistence/Repositories/{Question,Submission,WeakPoint}Repository.cs`
- `src/DeepLearning.Infrastructure/Ai/*.cs` (`LlmClient`, `ExamConfigLoader`, `PromptRenderer`, `GradingResultInterpreters/*`) and `src/DeepLearning.Infrastructure/BackgroundJobs/*.cs`
- `src/DeepLearning.Api/Controllers/{Questions,Submissions,FollowUps,WeakPoints,Progress}Controller.cs` (aside from the auto-generated `WeatherForecastController`, these 5 are still empty classes)
- Everything under `tests/DeepLearning.UnitTests/{Domain,Application/Features/Submissions}/**` (e.g. `SubmissionTests.cs`, `GradeSubmissionCommandHandlerTests.cs`) and the leftover template `UnitTest1.cs`

## Directory structure and dependency direction

```
DeepLearning.Domain          <- no dependencies at all, pure C#
    ↑
DeepLearning.Application     <- depends on Domain
    ↑
DeepLearning.Infrastructure  <- depends on Application + Domain
    ↑
DeepLearning.Api             <- depends on Infrastructure + Application
```

- **Domain**: entities (`Entities/`), enums (`Enums/`), domain events (`Events/`), domain exceptions (`Exceptions/`), base classes and shared value objects (`Common/`). Doesn't reference any other project, and doesn't reference frameworks like EF/MediatR.
- **Application**: business use cases. `Features/<BusinessDomain>/Commands|Queries/<UseCaseName>/` holds the CQRS four-piece set; `Behaviors/` holds the MediatR pipeline; `Interfaces/` holds abstractions for repositories and external services (LLM, etc.); `Common/` holds cross-use-case shared types (pagination, etc.). Only depends on Domain — doesn't reference concrete implementations like EF Core or Npgsql.
- **Infrastructure**: where the interfaces defined by Application land. `Persistence/` is EF Core + Npgsql (DbContext, Configurations, Migrations, Repositories, UnitOfWork). If external services like an LLM API or email get added later, they should also get a new folder in this layer implementing Application's interfaces.
- **Api**: the ASP.NET Core entry point. `Controllers/` only does parameter binding and calls `IMediator.Send(...)` — no business logic. `Middleware/` holds cross-cutting concerns like global exception handling.

## Database: Supabase (Postgres) + EF Core

- The connection string key is consistently `ConnectionStrings:DefaultConnection`. For local development it's filled in at [appsettings.Development.json](src/DeepLearning.Api/appsettings.Development.json) — **this file is excluded by `.gitignore` and isn't committed**, so after a fresh clone / on a new machine you need to fill it in by hand again (it contains the real Supabase password). Production goes through the `ConnectionStrings__DefaultConnection` environment variable.
- Runtime read entry point: [DependencyInjection.cs](src/DeepLearning.Infrastructure/DependencyInjection.cs). Design-time entry point for `dotnet ef` commands: [AppDbContextFactory.cs](src/DeepLearning.Infrastructure/Persistence/AppDbContextFactory.cs) (only reads environment variables, not `appsettings.json` — remember to `export`/`$env:` the environment variable before running `dotnet ef` commands locally).
- The project uses `EFCore.NamingConventions`'s snake_case convention globally (see `.UseSnakeCaseNamingConvention()` in [AppDbContext.cs](src/DeepLearning.Infrastructure/Persistence/AppDbContext.cs)). A C# property like `CreatedAt` is automatically mapped to the column `created_at` — no need to hardcode column names in Configurations unless overriding the default rule.
- **Native Postgres enums**: every time a new enum is added, it needs to be registered in two places — missing either one causes runtime/design-time errors:
  1. `modelBuilder.HasPostgresEnum<T>(name: "..._enum", nameTranslator: ...)` in `AppDbContext.OnModelCreating`
  2. `o.MapEnum<T>("..._enum", nameTranslator: ...)` in [NpgsqlEnumConfiguration.cs](src/DeepLearning.Infrastructure/Persistence/NpgsqlEnumConfiguration.cs)

  Most enums use `NpgsqlNullNameTranslator` (the C# member name matches the SQL label verbatim); `Visibility`/`MasteryLevel` use `NpgsqlSnakeCaseNameTranslator` instead because their labels contain C# reserved words (`new`/`private`) — the C# members are written in PascalCase (`New`/`Private`) and the snake_case translator converts them back to the label.
- [Migrations/schema.sql](src/DeepLearning.Infrastructure/Persistence/Migrations/schema.sql) is the script originally run by hand in the Supabase SQL Editor to create the schema — it is **not** exported from EF Migrations. Its own comments state that all future schema changes must go through `dotnet ef migrations add`; don't hand-edit this file or manually alter tables in Supabase again.
- The `__EFMigrationsHistory` table was manually created afterward (because the tables were created via schema.sql, not `dotnet ef database update`) — its column names follow the project's snake_case convention, written as `migration_id`/`product_version` (not EF's default PascalCase).
- A legacy issue was resolved on 2026-08-29: the primary key/foreign key/unique/CHECK constraints in `schema.sql` were written inline without explicit names, so the names Postgres auto-generated didn't match what the EF migration snapshot expected (`pk_*`/`fk_*`/`ix_*`/`ck_*`) — these have been manually aligned with `RENAME CONSTRAINT`. **For any future migration that needs to operate on a constraint by name, treat the names in [AppDbContextModelSnapshot.cs](src/DeepLearning.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs) as authoritative.**

## Order of steps for building a new CQRS feature

1. **Repository interface**: whatever persistence operations are needed, add them to `Application/Interfaces/I<Aggregate>Repository.cs`. One repository per aggregate root — don't split by table. The interface must be `public` (not `internal`), since the Infrastructure project implements it across assembly boundaries.
2. **Repository implementation**: `Infrastructure/Persistence/Repositories/<Aggregate>Repository.cs`, implemented directly with `AppDbContext`. **The repository itself never calls `SaveChanges`** — it's only responsible for querying/marking changes; persisting is handled uniformly through `IUnitOfWork`.
3. Register the new repository as `AddScoped` in `Infrastructure/DependencyInjection.cs`.
4. Build the four-piece set under `Application/Features/<BusinessDomain>/Commands|Queries/<UseCaseName>/`:
   - `<UseCase>Command`/`Query`: `: IRequest<TResult>`, pure data.
   - `<UseCase>Validator`: `: AbstractValidator<TCommand>`, auto-discovered by `AddValidatorsFromAssembly`; `ValidationBehavior` runs it automatically before the handler.
   - `<UseCase>Result`: the external DTO — don't return Domain entities across the boundary directly.
   - `<UseCase>CommandHandler`: `: IRequestHandler<TCommand, TResult>`, injects the repository + `IUnitOfWork`; after modifying the entity, call `await _unitOfWork.SaveChangesAsync(ct)` once, then map to Result and return. Queries generally don't need `IUnitOfWork`.
5. In the Api layer, the corresponding Controller injects `IMediator` and calls `await _mediator.Send(command)`.
6. If this feature needs to trigger domain events after saving (e.g., updating weak points/progress after grading a submission): `AggregateRoot` doesn't yet have a mechanism for collecting domain events, and `UnitOfWork.SaveChangesAsync` has no dispatch logic either — this needs to be designed separately (add a `DomainEvents` list to `AggregateRoot` + publish inside `UnitOfWork.SaveChangesAsync`). Don't assume this already works.

## Where to look when you have a question

| What you want to know/do | Where to go |
|---|---|
| What entities exist, what their fields are | `Domain/Entities/*.cs` (authoritative); `Migrations/schema.sql` is the database's actual current structure |
| A field's column name/type/constraints/index | `Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` |
| How the database connection works, how the connection string is read | `Infrastructure/DependencyInjection.cs` (runtime), `AppDbContextFactory.cs` (design-time) |
| MediatR pipeline order/how handlers are registered | `Application/DependencyInjection.cs` |
| Adding/modifying an enum | `Domain/Enums/` + `AppDbContext.OnModelCreating` + `NpgsqlEnumConfiguration.cs` (both need to change) |
| Adding a new repository method | `Application/Interfaces/I*Repository.cs` (interface) + `Infrastructure/Persistence/Repositories/*.cs` (implementation) |
| Adding a new command/query | `Application/Features/<Area>/Commands|Queries/<UseCase>/` |
| How to write a Controller | `Api/Controllers/`, inject `IMediator` — see `ExamTypesController.cs` for a real example; the other 5 (`Questions`/`Submissions`/`FollowUps`/`WeakPoints`/`Progress`) are still empty shells |
| An example of a fully-wired CQRS slice to copy | `Application/Features/ExamConfig/**` + `ExamTypesController.cs`/`AssessmentDimensionsController.cs` — repository → handler → validator → controller, end to end |
| How to change the database schema | `dotnet ef migrations add` — don't hand-edit `schema.sql` or manually alter tables in Supabase |

## Known pitfalls

- The existing empty-shell types/interfaces in `Application/Interfaces` and `Infrastructure/Persistence/Repositories` default to `internal`. Remember to change them to `public` when actually implementing the cross-assembly interface, otherwise the build fails (`IUnitOfWork` has already been changed — use it as a reference).
- Check the `.csproj` before adding a new project reference — when the project was first scaffolded, `Infrastructure` didn't reference `Application` and `Application` didn't reference `Domain`; this was only fixed on 2026-08-29. This means the skeleton's project references can't be fully trusted — verify for yourself before making changes.
- Before running `dotnet ef migrations add/list/database update` locally, set the `ConnectionStrings__DefaultConnection` environment variable first (the design-time factory doesn't read `appsettings.json`).
- There's no standalone design document file in the repo — the comment block at the top of `schema.sql` is currently the most complete written statement of the overall design intent, worth a look before changing the schema.
- [seed_naati_ct_en_zh.sql](src/DeepLearning.Infrastructure/Persistence/Migrations/seed_naati_ct_en_zh.sql) contains the real, official NAATI CT English→Chinese rubric data (`exam_types`, `assessment_dimensions` with verbatim Band text, `error_taxonomies`, `generation_policy`, `prompt_templates`) and has already been run by hand against the Supabase dev DB. Like `schema.sql`, it is **not** auto-executed by the app or by migrations — it's checked in purely as the historical record of what production data already exists.
- **Never point `ApiWebApplicationFactory`/Testcontainers-based tests at the real Supabase connection.** Its `InitializeAsync` overrides `ConnectionStrings__DefaultConnection` via `Environment.SetEnvironmentVariable` (not `WebApplicationFactory.ConfigureWebHost`'s `ConfigureAppConfiguration` — that hook applies too late for a minimal-hosting `Program.cs` where `AddInfrastructure(builder.Configuration)` reads and captures the connection string before `builder.Build()` even runs). If that override is ever broken again, the tests will silently write real HTTP-created exam types/users/prompt templates into the real dev Supabase DB, exactly like they did once already on 2026-08-29 before this was fixed (cleaned up by hand afterward — check `exam_types`/`users`/`prompt_templates` for stray `test_*`/`user_*`/`dup_*`/`api test content` rows if this regresses).
- Repository/integration tests that build their own `DbContextOptions` (see `PostgresContainerFixture`) must build it **once and reuse it**, not per-call — EF Core spins up a new internal service provider per distinct `DbContextOptions` instance, and after ~20 of them in one process it trips `ManyServiceProvidersCreatedWarning` and starts throwing.
- Testcontainers needs a Postgres image with the `vector` extension available (this schema's migrations enable pgvector) — use `pgvector/pgvector:pg16`, not plain `postgres:16-alpine`.
- [docker-compose.yml](docker-compose.yml) maps Postgres to **host port 5433, not 5432** — this machine (and possibly others) already runs a native Postgres service on 5432. Docker's port-proxy doesn't error on the collision; `psql`/`dotnet ef` just silently connect to the native install instead of the container and fail with a baffling password-auth error. Verified working end-to-end on 2026-08-29: `docker compose up -d` + `dotnet ef database update` against `Host=localhost;Port=5433;...` creates all 30 tables (29 + `__EFMigrationsHistory`) cleanly from empty.
- [.github/workflows/ci.yml](.github/workflows/ci.yml) runs `dotnet build` + `dotnet test` on push/PR to `master`/`main`. GitHub-hosted Ubuntu runners have Docker preinstalled, so the Testcontainers-backed integration/API tests run there with no extra service-container setup. Not yet actually exercised on GitHub itself (would need a push) — the YAML has only been syntax-checked locally.

## Running the tests

Requires Docker Desktop running (Testcontainers spins up real, throwaway Postgres containers — no mocked DbContext anywhere in the suite).

```bash
dotnet test DeepLearning.slnx
```

A `NU1903` warning about `SSH.NET` having a known high-severity advisory is expected and currently unresolved — it's a transitive dependency of `Testcontainers.PostgreSql` (used to talk to remote Docker contexts), not something this project calls directly, and it never ships in the Api project's output.

## Common commands

```bash
# Build (building the Api project also builds Infrastructure/Application/Domain)
dotnet build src/DeepLearning.Api

# EF-related commands need the environment variable set first, then run from src/DeepLearning.Api
export ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
dotnet ef migrations add <MigrationName>
dotnet ef migrations list
dotnet ef database update
```
