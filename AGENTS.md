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
| `src/DeepLearning.Domain/Entities/*.cs` | The original 29 entities, fields cross-checked one by one against the tables in Supabase (2026-08-29), trustworthy. Plus `LlmProviderSettings` (Step 3, backing `llm_provider_settings` — not yet cross-checked against Supabase since that table's creation there is pending, see "Known pitfalls") |
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
| `Api/Controllers/{ExamTypes,AssessmentDimensions,ErrorTaxonomies,PromptTemplates,Users,Questions}Controller.cs` | Real, routed under `api/v1/...` |
| `Application/Interfaces/IQuestionRepository.cs` + `Infrastructure/Persistence/Repositories/QuestionRepository.cs` | Real, registered in DI — `Question` is the aggregate root; `MeaningCheckpoint`/`TaskBSeededError` are FK-only child rows fetched via separate repository methods, no EF collection nav properties (matches the existing FK-only convention on `ExamType`/`AssessmentDimension` etc.) |
| `Application/Features/Questions/Commands/ImportUserQuestion/**` + `Queries/{GetQuestionById,ListQuestions}/**` | Real — manual/scripted question entry (no AI yet, that's `GenerateQuestion`/Step 3), Create+GetById+List. The validator enforces TaskA/TaskB shape consistency and TaskB seeded-error position bounds/no-overlap — this is the one validator worth reading before writing a similar one elsewhere |
| `Application/Interfaces/{ILlmClient,ILlmClientResolver,IExamConfigLoader,IAiCallLogRepository,ILlmProviderSettingsRepository}.cs` | Real — see the "AI integration (Step 3)" section below for the full design |
| `Infrastructure/Ai/{ClaudeLlmClient,OpenAiCompatibleLlmClient,LlmResiliencePipeline,LlmClientResolver,ExamConfigLoader,PromptRenderer}.cs` + `Options/{ClaudeApiOptions,OpenAiCompatibleOptions}.cs` + `Infrastructure/Persistence/Repositories/{AiCallLogRepository,LlmProviderSettingsRepository}.cs` + `Domain/Entities/LlmProviderSettings.cs` | Real — **4 providers wired up**: Claude (own adapter, own request/response shape) plus OpenAI/DeepSeek/Mimo sharing one `OpenAiCompatibleLlmClient` (all three are OpenAI-Chat-Completions-shaped — confirmed against each provider's own docs, not assumed). `ClaudeLlmClient.cs` is a **rename** of the old empty `LlmClient.cs` stub; `LlmResiliencePipeline.cs` is a rename of `ClaudeResiliencePipeline.cs` once it started being shared by all 4. Which provider is active + its model/thinking/effort/extra settings are DB-driven (`llm_provider_settings`, via `LlmClientResolver`) — see below. |
| `Application/Features/Questions/Commands/GenerateQuestion/**` | Real — the AI-driven sibling of `ImportUserQuestion`. Reuses `IQuestionRepository`/`IExamTypeRepository` from Steps 1-2 unchanged |
| `Api/Controllers/QuestionsController.cs` `POST .../generate` action | Real |
| `Application/Features/LlmProviders/{Queries/ListLlmProviders,Commands/UpdateLlmProviderSettings,Commands/ActivateLlmProvider}/**` + `Api/Controllers/LlmProviderSettingsController.cs` | Real — the admin API for switching provider/model/thinking/effort at runtime (List/Update/Activate). See "AI integration (Step 3)" below for the endpoint list and the two-phase-save note on Activate |
| `tests/DeepLearning.UnitTests/TestInfrastructure/*.cs` | Real: `PostgresContainerFixture` (Testcontainers Postgres for repository tests) + `ApiWebApplicationFactory` (same, wired to a real ASP.NET Core host via `WebApplicationFactory<Program>`) |
| `tests/DeepLearning.UnitTests/{Api,Integration,Application/Features/ExamConfig,Application/Features/Questions,Infrastructure/Ai}/**` | Real: unit tests for validator rules, integration tests hitting a real throwaway Postgres, API tests hitting a real HTTP host — see "Running the tests" below. `Api/ClaudeLlmClientLiveTests.cs` makes a real Claude call and is excluded from the default run — see "AI integration (Step 3)" below. `Api/LlmProviderSettingsControllerTests.cs` covers List/Update/Activate against the shared Testcontainers Postgres — note its `SeedTwoProvidersAsync` helper explicitly clears any pre-existing `is_active=true` row before seeding, since the `ApiCollection` shares one DB across the whole test class and leftover active rows from other tests would otherwise collide with the partial unique index |

### Parts that are still empty shells, waiting to be filled in

- `src/DeepLearning.Domain/Common/{Result,Guard,ErrorCodes}.cs`
- `src/DeepLearning.Domain/Events/*.cs` (the domain event classes themselves are empty too; `AggregateRoot` doesn't yet have a mechanism for collecting events) — still open, needed starting the Step 6 domain-events work
- `src/DeepLearning.Domain/Exceptions/{InvalidSubmissionStateException,RubricVersionNotFoundException}.cs` — belong to the Submissions/grading work (Step 4/5), left stubbed on purpose
- `ISubmissionRepository`, `IWeakPointRepository`, `IGradingResultInterpreter`, `IProgressRepository`, `IStandardOverrideRepository` under `Application/Interfaces/`
- `src/DeepLearning.Application/Common/{PagedRequest,PagedResult}.cs`
- **All** business code under `Features/{Submissions,FollowUps,WeakPoints,Progress,ReviewLibrary,QuestionBank}/**` except the `EventHandlers` stubs
- `src/DeepLearning.Infrastructure/Persistence/Repositories/{Submission,WeakPoint}Repository.cs`
- `src/DeepLearning.Infrastructure/Ai/GradingResultInterpreters/*.cs` (grading, Step 4 — `ExamConfigLoader`/`PromptRenderer`/`ClaudeLlmClient` are now real, but the grading-specific interpreter strategies aren't) and `src/DeepLearning.Infrastructure/BackgroundJobs/*.cs`
- `src/DeepLearning.Api/Controllers/{Submissions,FollowUps,WeakPoints,Progress}Controller.cs` (aside from the auto-generated `WeatherForecastController`, these 4 are still empty classes)
- Everything under `tests/DeepLearning.UnitTests/{Domain,Application/Features/Submissions}/**` (e.g. `SubmissionTests.cs`, `GradeSubmissionCommandHandlerTests.cs`) and the leftover template `UnitTest1.cs`

## AI integration (Step 3): provider abstraction, config, and gotchas hit for real

- **Provider-neutral by design — 4 providers wired up for real: Claude, OpenAI, DeepSeek, Mimo.** `ILlmClient` (Application) knows nothing about any of them. Each is registered as a **keyed** service (`AddKeyedTransient<ILlmClient, ...>("claude" | "openai" | "deepseek" | "mimo")`, see `DependencyInjection.cs`). Handlers never inject `ILlmClient` directly or depend on a concrete provider — see the next bullet.
- **Which provider is active lives in the database, not config** (`llm_provider_settings` table, one row per provider, `is_active` marks the current one — a partial unique index guarantees at most one). `ILlmClientResolver.GetActiveClientAsync()` (Infrastructure/Ai/LlmClientResolver.cs) queries it per call and hands back the matching keyed `ILlmClient`, wrapped so the row's `Model`/`ThinkingEnabled`/`Effort`/`ExtraSettings` become that call's defaults (a caller can still override any of them per-request on `LlmCompletionRequest`). `GenerateQuestionCommandHandler` calls the resolver instead of injecting `ILlmClient`. **Switching providers/models/effort is a data UPDATE, not a redeploy** — takes effect on the very next AI call. Table creation and seeding are hand-run SQL (`Infrastructure/Persistence/Migrations/{add,seed}_llm_provider_settings.sql`) by design — see "Known pitfalls" below for why this one didn't get auto-applied like everything else.
- **Mimo is both the seeded default and the code-level fallback.** `seed_llm_provider_settings.sql` inserts `mimo` as the only `is_active=true` row. Separately, `LlmClientResolver.FallbackProviderKey = "mimo"` is a hardcoded safety net in `GetActiveClientAsync()`: if `llm_provider_settings` has no `is_active=true` row at all (table not yet applied to Supabase, or someone deactivated everything), it logs a warning and resolves the keyed `mimo` client directly instead of throwing — so a missing/misconfigured table degrades to "use Mimo with its appsettings-configured defaults," not a hard failure of every AI feature. These two are independent: the seed decides what the *DB says* is active; the fallback decides what happens when the DB has *no opinion*.
- **Admin API for switching provider/model/thinking/effort at runtime** — `LlmProviderSettingsController` (`Api/Controllers/LlmProviderSettingsController.cs`), CQRS slice under `Application/Features/LlmProviders/`:
  - `GET /api/v1/llm-provider-settings` — lists all provider rows (`ListLlmProvidersQuery`).
  - `PATCH /api/v1/llm-provider-settings/{providerKey}` — partial update of `Model`/`ThinkingEnabled`/`Effort`/`ExtraSettingsJson` (`UpdateLlmProviderSettingsCommand` — only non-null fields in the request body change; 404 via `NotFoundException` if the key doesn't exist).
  - `POST /api/v1/llm-provider-settings/{providerKey}/activate` — makes this provider the active one (`ActivateLlmProviderCommand`). Deliberately does **two sequential `SaveChangesAsync` calls** (deactivate every other currently-active row first, then activate the target) rather than one batched save — the partial unique index `ux_llm_provider_settings_single_active` is checked per-statement in Postgres, not deferred, so activating the new row before the old one's deactivation is committed would transiently violate it within the same transaction.
  - No create/delete endpoints — rows are meant to be seeded via SQL (one per known provider), matching the "reference data, hand-managed" treatment the rest of this table already gets.
- **Secrets (API keys, Claude's WorkspaceId) are environment variables, not config or database** — keys `Llm:Claude:ApiKey`, `Llm:Claude:WorkspaceId`, `Llm:OpenAi:ApiKey`, `Llm:DeepSeek:ApiKey`, `Llm:Mimo:ApiKey`. No code change was needed for this — ASP.NET Core's default config pipeline already merges env vars (`Llm__Claude__ApiKey`, double-underscore = nested-key separator) and **.NET User Secrets** on top of `appsettings.json`; the only change was **removing** the `ApiKey`/`WorkspaceId` values that used to live in `appsettings.Development.json`.
  - **Local dev**: set once via `dotnet user-secrets set "Llm:Claude:ApiKey" "..." --project src/DeepLearning.Api` (repeat per key) — stored in `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, **outside the repo entirely**, auto-loaded by both `dotnet run` and `dotnet test`/`WebApplicationFactory` in Development (verified: the live tests pass with zero `export` needed once secrets are set this way). The Api project's `<UserSecretsId>` in the `.csproj` is just a random GUID pointing at that file — not a secret itself, fine to commit.
  - **CI/production**: real environment variables (`Llm__Claude__ApiKey=...`), same convention as `ConnectionStrings__DefaultConnection` further down this file — User Secrets is Development-only by design and won't be present there.
  - `BaseUrl`, `Model` (fallback only — the DB row's `Model` wins when going through the resolver), and the OpenAI-compatible per-provider fields stay in `appsettings.Development.json` since they aren't secret.
- **Two adapter classes, not four.** `ClaudeLlmClient` has its own shape (Anthropic's Messages API is structurally different — top-level `system`, content-block array, `thinking`/`output_config.effort`). OpenAI/DeepSeek/Mimo all speak the **same** OpenAI Chat Completions wire format (confirmed against each provider's own docs on 2026-08-29 via WebFetch, not assumed from memory — Mimo in particular is easy to get wrong by guessing), so they share **one** `OpenAiCompatibleLlmClient`, parameterized per provider by `OpenAiCompatibleOptions` (base URL, auth header name/prefix — Mimo uses a bare `api-key` header, not `Authorization: Bearer`, model id, and the output-length field name — DeepSeek uses `max_tokens`, OpenAI/Mimo use `max_completion_tokens`). Adding a 5th OpenAI-shaped provider needs **zero new C#**: one more named-options section + one more `AddKeyedTransient` line in `DependencyInjection.cs`, plus one more `llm_provider_settings` row. Adding a structurally different provider (Gemini, etc.) means one more adapter class shaped like `ClaudeLlmClient`.
- **`ThinkingEnabled`/`Effort` are honestly scoped to Claude only.** Claude: `ThinkingEnabled=false` sends `thinking:{type:"disabled"}` (omitted otherwise, runs adaptive); `Effort` maps to `output_config.effort`. For OpenAI/DeepSeek/Mimo, "thinking" isn't a universal boolean in the Chat Completions format — OpenAI's reasoning models use separate model ids or a Responses-API-only field, DeepSeek's reasoning is a distinct model name, Mimo's mechanism isn't confirmed — so rather than guess a field name, `OpenAiCompatibleLlmClient` leaves those two alone and relies on `ExtraSettings` (JSONB passthrough, merged directly into the request body — e.g. `{"reasoning_effort":"high"}`) as the generic escape hatch for whatever provider-specific knob is needed once actually confirmed against that provider's docs.
- **Raw `HttpClient`, not official provider SDKs — deliberate.** Matches the design doc's own "HttpClient+Polly" architecture, and a uniform raw-HTTP adapter shape is what actually makes swapping/adding providers cheap; the resilience pipeline (`LlmResiliencePipeline.cs`, via `Microsoft.Extensions.Http.Resilience` — Polly v8 under the hood) attaches to `HttpClient`, not to an SDK client, and is shared by all 4 providers' HttpClients.
- **Real bugs found only by actual live calls, all fixed**:
  1. Sending `"system": null` in Claude's request body (instead of omitting the key) got a real 400 ("system: Input should be a valid array") — fixed with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` on `ClaudeLlmClient`'s request DTO.
  2. `AddStandardResilienceHandler`'s library defaults (10s per-attempt / 30s total timeout) are tuned for ordinary REST calls, not an LLM completion — a real Claude question-generation call with default adaptive thinking took longer than that and tripped `Polly.Timeout.TimeoutRejectedException`, uncaught by the original `catch (HttpRequestException)` clause, surfacing as a raw 500 instead of a clean `AiCallFailedException`→503. Fixed by widening the catch clause (both adapters) and raising `AttemptTimeout`/`TotalRequestTimeout` to 60s/180s in `LlmResiliencePipeline.Configure` — note the circuit breaker's `SamplingDuration` must stay ≥ 2× `AttemptTimeout` or `AddStandardResilienceHandler` fails **at startup** with an `OptionsValidationException`, not at call time.
  3. OpenAI's live test fails with 429 `insufficient_quota`/`credit_balance_exhausted` — that account has no billing credits. Not a code bug: the request is correctly authenticated (a bad key would be 401) and Polly correctly retried 3 times before giving up. DeepSeek and Mimo both succeeded for real on the same run, including again after secrets moved to env vars.
- **`prompt_templates` needed a new row for Step 3 to work at all**: the exam_specific/question_gen row seeded back in Step 1 only specifies content requirements (topic, difficulty), not an output contract — because the code that would consume it didn't exist yet. Added one new `shared_methodology`/`question_gen` row (subject_category=`translation`) via the real `POST /api/v1/prompt-templates` endpoint instructing the model to output *only* the JSON shape `GenerateQuestionCommandHandler` parses. If question generation ever starts failing to parse, check this row (`prompt_templates` where `template_type='question_gen' AND layer='shared_methodology'`) hasn't been deactivated or superseded incompatibly. This row instructs JSON output generically — it isn't Claude-specific, so it works unchanged no matter which provider is active.
- **Live-call tests are excluded from the default run** — see "Running the tests" below. `ClaudeLlmClientLiveTests.cs` and `OpenAiCompatibleLlmClientLiveTests.cs` (3 methods — one per OpenAI-shaped provider) both resolve their `ILlmClient` by DI key directly, bypassing `ILlmClientResolver`/the database entirely — they're testing the adapters, not the provider-switch mechanism. Requires the env vars above to be set.

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
- **`NpgsqlEnumConfiguration.MapEnums` must reuse the same `NpgsqlNullNameTranslator`/`NpgsqlSnakeCaseNameTranslator` instances every call, not `new` them each time — this bit for real on 2026-08-29.** `AddDbContext`'s configure action runs once per scope (i.e. once per HTTP request, since `AppDbContext` is Scoped), so a fresh `new NpgsqlNullNameTranslator()` per call made every request's resulting `DbContextOptions` fingerprint distinct in EF's internal service-provider cache (these translators don't have value equality) — the API host silently built a brand-new internal DI container on *every single request* instead of reusing one, and the 21st request in one test process tripped `ManyServiceProvidersCreatedWarning` into a hard 500. It went unnoticed through all of Step 1 because the Api test suite stayed just under ~20 requests; adding Step 2's `QuestionsControllerTests` pushed it over. Fixed by making the translators `private static readonly` fields. If this class is ever "simplified" back to per-call `new`, the bug returns — and it's confusing to debug because the *symptom* shows up as an unrelated test failing (e.g. a null/empty GUID from a JSON-deserialized 500-as-ProblemDetails response), not as the 500 itself.
- Testcontainers needs a Postgres image with the `vector` extension available (this schema's migrations enable pgvector) — use `pgvector/pgvector:pg16`, not plain `postgres:16-alpine`.
- [docker-compose.yml](docker-compose.yml) maps Postgres to **host port 5433, not 5432** — this machine (and possibly others) already runs a native Postgres service on 5432. Docker's port-proxy doesn't error on the collision; `psql`/`dotnet ef` just silently connect to the native install instead of the container and fail with a baffling password-auth error. Verified working end-to-end on 2026-08-29: `docker compose up -d` + `dotnet ef database update` against `Host=localhost;Port=5433;...` creates all 30 tables (29 + `__EFMigrationsHistory`) cleanly from empty.
- [.github/workflows/ci.yml](.github/workflows/ci.yml) runs `dotnet build` + `dotnet test` on push/PR to `master`/`main`. GitHub-hosted Ubuntu runners have Docker preinstalled, so the Testcontainers-backed integration/API tests run there with no extra service-container setup. Not yet actually exercised on GitHub itself (would need a push) — the YAML has only been syntax-checked locally.
- **`llm_provider_settings` is the one table whose real-Supabase creation is deliberately hand-run, by the user's own request** (they wanted DB schema changes reviewed/applied by hand rather than run by the agent) — unlike everything else, the EF migration (`20260829092630_AddLlmProviderSettings`) was generated locally so `dotnet ef` history and Testcontainers-driven tests stay correct, but was never applied to Supabase via `dotnet ef database update`. [add_llm_provider_settings.sql](src/DeepLearning.Infrastructure/Persistence/Migrations/add_llm_provider_settings.sql) is the literal idempotent SQL from `dotnet ef migrations script` (safe to run as-is — it inserts its own `__EFMigrationsHistory` row too), and [seed_llm_provider_settings.sql](src/DeepLearning.Infrastructure/Persistence/Migrations/seed_llm_provider_settings.sql) seeds the 4 provider rows (`mimo` active by default). **If this table/seed hasn't been applied to Supabase yet, AI question generation does not fail** — `LlmClientResolver` falls back to the hardcoded `mimo` keyed client (see the "AI integration (Step 3)" bullet on the fallback) — but it's still worth applying for real so the admin API (`LlmProviderSettingsController`) has real data to list/update against.

## Running the tests

Requires Docker Desktop running (Testcontainers spins up real, throwaway Postgres containers — no mocked DbContext anywhere in the suite).

```bash
# Default / CI: excludes real provider calls (cost money, need network + live keys)
dotnet test DeepLearning.slnx --filter "Category!=LlmIntegration"

# Run the live provider calls explicitly — needs the 5 provider secrets set first, either
# via `dotnet user-secrets set "Llm:Claude:ApiKey" "..." --project src/DeepLearning.Api`
# (set once, works forever, no export needed — recommended) or exported env vars
# (Llm__Claude__ApiKey etc.) for this shell session. See "AI integration" above.
dotnet test DeepLearning.slnx --filter "Category=LlmIntegration"
```

Plain `dotnet test` with no filter runs everything including the live Claude call — don't do that in CI or on a loop. `.github/workflows/ci.yml` already uses the filtered form.

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
