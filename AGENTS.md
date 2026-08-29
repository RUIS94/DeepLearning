# AGENTS.md — 项目地图

给 AI 协作者看的导航文件：这个项目长什么样、东西该去哪儿找、该在哪儿改、新功能该加在哪儿、能从哪儿复用。

## 项目是什么

NAATI CT 翻译练习软件的后端。.NET 10 + EF Core(Npgsql)+ Supabase(Postgres 托管)。
架构是标准 Clean Architecture 四层 + CQRS(MediatR)+ Repository/UnitOfWork。

## ⚠️ 先读这一节：这是一个"骨架"项目，很多文件只有类名没有内容

这不是代码被删了或者哪里坏了——项目最初是按 Clean Architecture 模板把整个目录结构和空类都建好了（`namespace X { internal class Y {} }` 这种），业务逻辑目前只填了一小部分。**在改动前，先确认目标文件是"真实实现"还是"空壳"，不要假设它已经工作。**

### 已经是真实实现的部分

| 位置 | 内容 |
|---|---|
| `src/DeepLearning.Domain/Entities/*.cs` | 全部 29 个实体，字段已跟 Supabase 里的表逐一核对过（2026-08-29），可信 |
| `src/DeepLearning.Domain/Enums/*.cs` | 全部真实，对应 Postgres 原生 enum |
| `src/DeepLearning.Domain/Common/Entity.cs` | 真实（`Id` + 值相等） |
| `src/DeepLearning.Infrastructure/Persistence/Configurations/*.cs` | 全部真实，EF Fluent API 映射 |
| `src/DeepLearning.Infrastructure/Persistence/AppDbContext.cs` | 真实：DbSet、原生 enum 注册、`ApplyConfigurationsFromAssembly` |
| `src/DeepLearning.Infrastructure/Persistence/{AppDbContextFactory,NpgsqlEnumConfiguration,UnitOfWork}.cs` | 真实 |
| `src/DeepLearning.Infrastructure/DependencyInjection.cs` | 真实：注册 DbContext + `IUnitOfWork` |
| `src/DeepLearning.Infrastructure/Persistence/Migrations/*` | 真实，反映当前 schema |
| `src/DeepLearning.Application/Behaviors/*.cs` | 真实：Logging / Validation / UnhandledException 三个 MediatR pipeline behavior |
| `src/DeepLearning.Application/DependencyInjection.cs` | 真实：`AddApplication()` 注册 MediatR + FluentValidation + behavior |
| `src/DeepLearning.Application/Interfaces/IUnitOfWork.cs` | 真实 |
| `src/DeepLearning.Api/Program.cs` | 真实：`AddApplication()` + `AddInfrastructure()` 已接线 |

### 还是空壳、等你填的部分

- `src/DeepLearning.Domain/Common/{Result,Guard,ErrorCodes}.cs`
- `src/DeepLearning.Domain/Events/*.cs`（领域事件类本身也是空的，`AggregateRoot` 也还没有收集事件的机制）
- `src/DeepLearning.Application/Interfaces/` 下除 `IUnitOfWork` 外的所有接口（`IQuestionRepository`、`ISubmissionRepository`、`IWeakPointRepository`、`ILlmClient`、`IExamConfigLoader`、`IGradingResultInterpreter`、`IProgressRepository`、`IStandardOverrideRepository`）
- `src/DeepLearning.Application/Common/{PagedRequest,PagedResult}.cs`
- `src/DeepLearning.Application/Features/**/*` 里除了 `Behaviors`/`DependencyInjection.cs` 之外的**所有**业务代码——包括看起来已经有目录结构的 `Features/Questions/Commands/GenerateQuestion/`（Command/Handler/Result/Validator 四个文件全是空类）。**这套目录形状可以照抄，但里面没有可用的实现，不要当参考代码抄逻辑。**
- `src/DeepLearning.Infrastructure/Persistence/Repositories/*.cs`（`QuestionRepository`、`SubmissionRepository`、`WeakPointRepository`）
- `src/DeepLearning.Api/Controllers/*.cs`（除自动生成的 `WeatherForecastController` 外，`QuestionsController` 等 5 个都是空类）
- `src/DeepLearning.Api/Middleware/*.cs`、`src/DeepLearning.Api/Constants/*.cs`
- `tests/DeepLearning.UnitTests/**` 里除 `.csproj` 外全部是空类（含 `UnitTest1.cs`）

## 目录结构与依赖方向

```
DeepLearning.Domain          <- 没有任何依赖，纯 C#
    ↑
DeepLearning.Application     <- 依赖 Domain
    ↑
DeepLearning.Infrastructure  <- 依赖 Application + Domain
    ↑
DeepLearning.Api             <- 依赖 Infrastructure + Application
```

- **Domain**：实体（`Entities/`）、枚举（`Enums/`）、领域事件（`Events/`）、领域异常（`Exceptions/`）、基类和共享值对象（`Common/`）。不引用任何其他项目，不引用 EF/MediatR 等框架。
- **Application**：业务用例。`Features/<业务域>/Commands|Queries/<用例名>/` 放 CQRS 四件套；`Behaviors/` 放 MediatR pipeline；`Interfaces/` 放仓储和外部服务（LLM 等）的抽象；`Common/` 放跨用例的公共类型（分页等）。只依赖 Domain，不引用 EF Core、Npgsql 这些具体实现。
- **Infrastructure**：Application 定义的接口在这里落地。`Persistence/` 是 EF Core + Npgsql（DbContext、Configurations、Migrations、Repositories、UnitOfWork）。以后如果接 LLM API、邮件等外部服务，也应该在这一层新建对应文件夹实现 Application 的接口。
- **Api**：ASP.NET Core 入口。`Controllers/` 只做参数绑定和调 `IMediator.Send(...)`，不写业务逻辑。`Middleware/` 放全局异常处理等横切逻辑。

## 数据库：Supabase (Postgres) + EF Core

- 连接串键名统一是 `ConnectionStrings:DefaultConnection`。本地开发填在 [appsettings.Development.json](src/DeepLearning.Api/appsettings.Development.json)——**这个文件已被 `.gitignore`排除、不进 git**，所以换一台机器/新 clone 之后要重新手动填一份（里面有真实的 Supabase 密码）。生产环境走环境变量 `ConnectionStrings__DefaultConnection`。
- 运行时读取入口：[DependencyInjection.cs](src/DeepLearning.Infrastructure/DependencyInjection.cs)。`dotnet ef` 设计期命令读取入口：[AppDbContextFactory.cs](src/DeepLearning.Infrastructure/Persistence/AppDbContextFactory.cs)（只认环境变量，不读 appsettings.json，本地跑 `dotnet ef` 系列命令前记得先 `export`/`$env:` 设置环境变量）。
- 全局用了 `EFCore.NamingConventions` 的 snake_case 约定（见 [AppDbContext.cs](src/DeepLearning.Infrastructure/Persistence/AppDbContext.cs) 里的 `.UseSnakeCaseNamingConvention()`）。C# 属性 `CreatedAt` 会自动映射成列 `created_at`，Configuration 里不用手写列名，除非要覆盖默认规则。
- **原生 Postgres enum**：每加一个新枚举，两处都要注册，缺一处会在运行时/设计时报错：
  1. `AppDbContext.OnModelCreating` 里 `modelBuilder.HasPostgresEnum<T>(name: "..._enum", nameTranslator: ...)`
  2. [NpgsqlEnumConfiguration.cs](src/DeepLearning.Infrastructure/Persistence/NpgsqlEnumConfiguration.cs) 里 `o.MapEnum<T>("..._enum", nameTranslator: ...)`
  多数枚举用 `NpgsqlNullNameTranslator`（C# 成员名跟 SQL 里的 label 逐字一致）；`Visibility`/`MasteryLevel` 因为 label 里有 C# 保留字（`new`/`private`）改用 `NpgsqlSnakeCaseNameTranslator`，C# 成员名写成 PascalCase（`New`/`Private`），靠 snake_case 转换器换回 label。
- [Migrations/schema.sql](src/DeepLearning.Infrastructure/Persistence/Migrations/schema.sql) 是项目最初手工在 Supabase SQL Editor 里跑的建库脚本，**不是**从 EF Migrations 导出的。它自己的注释也写明：以后 schema 变更一律走 `dotnet ef migrations add`，不要再手改这个文件或手动在 Supabase 里改表结构。
- `__EFMigrationsHistory` 表是后来手动补建的（因为表是靠 schema.sql 建的，不是 `dotnet ef database update`），列名要按项目的 snake_case 约定写成 `migration_id`/`product_version`（不是 EF 默认的 PascalCase）。
- 2026-08-29 处理过一次遗留问题：`schema.sql` 里的主键/外键/唯一/CHECK 约束都是内联写的、没有显式命名，Postgres 自动生成的名字和 EF 迁移快照里期望的名字（`pk_*`/`fk_*`/`ix_*`/`ck_*`）对不上，已经手动 `RENAME CONSTRAINT` 对齐过。**以后新迁移如果要按名字操作某个约束，以 [AppDbContextModelSnapshot.cs](src/DeepLearning.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs) 里的名字为准。**

## 开发一个新的 CQRS 功能，照这个顺序做

1. **仓储接口**：需要什么持久化操作，加到 `Application/Interfaces/I<Aggregate>Repository.cs`。一个聚合根一个仓储，别按表拆。接口要是 `public`（不能是 `internal`，Infrastructure 项目要跨程序集实现它）。
2. **仓储实现**：`Infrastructure/Persistence/Repositories/<Aggregate>Repository.cs`，直接用 `AppDbContext` 实现。**仓储自己不调 `SaveChanges`**，只负责查询/标记变更，落库统一交给 `IUnitOfWork`。
3. 在 `Infrastructure/DependencyInjection.cs` 里把新仓储注册成 `AddScoped`。
4. 建 `Application/Features/<业务域>/Commands|Queries/<用例名>/` 四件套：
   - `<用例>Command`/`Query`：`: IRequest<TResult>`，纯数据。
   - `<用例>Validator`：`: AbstractValidator<TCommand>`，`AddValidatorsFromAssembly` 自动扫描到，`ValidationBehavior` 会在 handler 之前自动跑。
   - `<用例>Result`：对外 DTO，不要把 Domain 实体直接跨边界返回。
   - `<用例>CommandHandler`：`: IRequestHandler<TCommand, TResult>`，注入仓储 + `IUnitOfWork`，改完实体后调一次 `await _unitOfWork.SaveChangesAsync(ct)`，映射成 Result 返回。Query 一般不需要 `IUnitOfWork`。
5. Api 里对应 Controller 注入 `IMediator`，`await _mediator.Send(command)`。
6. 如果这个功能要在保存后触发领域事件（比如批改提交后要更新薄弱点/进度）：目前 `AggregateRoot` 还没有收集领域事件的机制，`UnitOfWork.SaveChangesAsync` 也没有 dispatch 逻辑，这部分要单独设计（往 `AggregateRoot` 加 `DomainEvents` 列表 + 在 `UnitOfWork.SaveChangesAsync` 里 publish），别假设它已经能用。

## 遇到问题去哪儿找

| 想知道/想做 | 去哪儿 |
|---|---|
| 有哪些实体、字段是什么 | `Domain/Entities/*.cs`（权威），`Migrations/schema.sql` 是数据库当前实际结构 |
| 某个字段的列名/类型/约束/索引 | `Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` |
| 怎么连数据库、连接串怎么读 | `Infrastructure/DependencyInjection.cs`（运行时）、`AppDbContextFactory.cs`（设计期） |
| MediatR pipeline 顺序/怎么注册 handler | `Application/DependencyInjection.cs` |
| 新增/修改枚举 | `Domain/Enums/` + `AppDbContext.OnModelCreating` + `NpgsqlEnumConfiguration.cs`（两处都要改） |
| 加新的仓储方法 | `Application/Interfaces/I*Repository.cs`（接口）+ `Infrastructure/Persistence/Repositories/*.cs`（实现） |
| 加新的命令/查询 | `Application/Features/<Area>/Commands|Queries/<UseCase>/` |
| Controller 怎么写 | `Api/Controllers/`，注入 `IMediator`，目前都是空壳 |
| 数据库 schema 怎么变更 | `dotnet ef migrations add`，不要手改 `schema.sql` 或去 Supabase 里手动改表 |

## 已知的坑

- `Application/Interfaces` 和 `Infrastructure/Persistence/Repositories` 里现有的空壳类型/接口默认是 `internal`。真正实现跨程序集接口时记得改成 `public`，否则编译不过（`IUnitOfWork` 已经改过，可以参考）。
- 新加一个项目引用前先看 `.csproj`——项目刚建的时候 `Infrastructure` 没引用 `Application`、`Application` 没引用 `Domain`，2026-08-29 才补上，说明这个骨架的项目引用关系不能完全信任，改动前自己确认一下。
- 本地跑 `dotnet ef migrations add/list/database update` 之前，要先设置环境变量 `ConnectionStrings__DefaultConnection`（设计期工厂不读 `appsettings.json`）。
- 没有独立的设计文档文件在仓库里，`schema.sql` 开头的注释是目前对整体设计意图最完整的书面说明，改动 schema 前值得看一眼。

## 常用命令

```bash
# 构建（Api 项目会连带构建 Infrastructure/Application/Domain）
dotnet build src/DeepLearning.Api

# EF 相关命令要先设置环境变量，再在 src/DeepLearning.Api 目录下执行
export ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
dotnet ef migrations add <MigrationName>
dotnet ef migrations list
dotnet ef database update
```
