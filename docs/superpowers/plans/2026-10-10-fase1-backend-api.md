# Fase 1 — Back-end (API) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar a API .NET da Fase 1 completa e testada (auth, veículos com FIPE, registros, lembretes, anexos e links públicos), pronta para o front consumir via OpenAPI.

**Architecture:** Clean Architecture em 4 projetos (Domain → Application → Infrastructure → Api). DDD tático no Domain; CQRS com dispatcher próprio e decorators na Application; EF Core/Postgres, armazenamento de arquivos e FIPE (camada anticorrupção) na Infrastructure; Minimal APIs organizadas por feature na Api.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core + Npgsql, PostgreSQL 18, ASP.NET Core Identity (cookie), FluentValidation, Scrutor, Magick.NET, Microsoft.Extensions.Http.Resilience, Serilog, OpenAPI + Scalar, xUnit, Testcontainers, NetArchTest.

**Spec:** `docs/superpowers/specs/2026-10-10-fase1-historico-manutencao-design.md`

**Fora deste plano (vão para o Plano 2):** front-end React (spec §8), teste E2E Playwright (§11), Docker Compose/Caddy/deploy/backup (§12).

**Ambiente verificado em 2026-10-10:** Windows; repositório em `C:\Repositories\APP_MAINETENCE` (branch `main`); .NET SDK 10.0.400; Docker 29.2.1 (necessário para Testcontainers); Node 24.11.1. Comandos rodam em PowerShell a partir de `C:\Repositories\APP_MAINETENCE\backend`, salvo indicação.

## Global Constraints

- Todos os projetos `net10.0`; `Nullable` e `ImplicitUsings` habilitados; `TreatWarningsAsErrors=true` (via `backend/Directory.Build.props`). Pacotes na última versão estável compatível no momento do `dotnet add package`.
- Testes com banco usam a imagem `postgres:18`.
- Identificadores, rotas e códigos de erro em inglês; `Error.Message` e mensagens de validação em pt-BR.
- Ids de agregados: `Guid.CreateVersion7()`, gerados no domínio.
- Dinheiro: `decimal`, coluna `numeric(12,2)`, BRL.
- "Hoje" = data no fuso `America/Sao_Paulo` (config `App:TimeZone`), obtida só via `IAppClock`. O domínio recebe `today`/`now` por parâmetro e nunca lê relógio.
- Proibido: MediatR, AutoMapper, FluentAssertions (licença comercial). Asserções com `Xunit.Assert`.
- Dependências: Domain não referencia nenhum projeto nem pacote. Application referencia só Domain, FluentValidation, Scrutor, `Microsoft.EntityFrameworkCore` (núcleo, apenas para LINQ assíncrono) e abstrações `Microsoft.Extensions.*` — nunca Npgsql. Api usa Infrastructure só para composição (DI) e Identity.
- Handlers e decorators são `internal sealed`; commands, queries e DTOs são `public sealed record`.
- Todo handler de rota `/vehicles/{vehicleId}/...` chama `IVehicleAccess` antes de qualquer outra leitura; sem acesso → `VehicleErrors.NotFound` (404 `vehicle.not_found`). Recursos filhos sempre buscados por `vehicleId` **e** `id`.
- Versão de concorrência: propriedade sombra `RowVersion` (`uint`, `.IsRowVersion()`, mapeada pelo Npgsql para `xmin`); DTOs expõem `RowVersion`, comandos de update recebem `RowVersion`. Nunca usar o nome `Version` para isso (colide com a versão do veículo).
- JSON camelCase; enums como string camelCase (`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`); `DateOnly` como `yyyy-MM-dd`.
- Todo endpoint: `.WithName(...)`, `.WithTags(...)`, `.Produces<T>(status)` do sucesso e `.ProducesProblem(status)` de cada erro possível — o Plano 2 gera os tipos do front a partir do OpenAPI.
- Grupo `/api` exige autenticação por padrão; endpoints anônimos chamam `.AllowAnonymous()` explicitamente.
- Códigos de erro: exatamente os da spec §9.3, mais três adicionados aqui: `auth.antiforgery_invalid` (400), `rate_limit.exceeded` (429), `vehicle.catalog_not_linked` (404).
- Chaves de erro de validação em camelCase por segmento (`parts[0].name`).
- Commits criados por agente terminam com o rodapé:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01WNr91bmuDTKJ6WvJtSgqwK
  ```

## Review Focus

1. **Lembrete de outro veículo em `fulfilledReminderIds`** (IDOR pelo evento de domínio): a requisição deve falhar com 404 `reminder.not_found`, sem criar o registro e sem tocar no lembrete alheio. → Task 11, `AddRecord_with_reminder_of_other_vehicle_returns_404_and_changes_nothing`.
2. **Virada do dia em São Paulo:** às 23:30 BRT (02:30Z do dia seguinte), registro com a data local de hoje é aceito; com a data local de amanhã → 422 `record.date_in_future`. → Task 7 (`AppClock`) e Task 11, `Record_dated_local_today_late_night_is_accepted_and_local_tomorrow_rejected`.
3. **Foto HEIC de Android ou arquivo renomeado para `.jpg`:** 415 `attachment.unsupported_type` com mensagem clara, nada gravado no disco. → Task 12, `Upload_heic_or_renamed_file_returns_415_and_stores_nothing`.
4. **Entrada malformada em parâmetros abertos:** cursor adulterado em `/records?cursor=` → 400 `validation.failed`; token malformado em `/public/{token}` → 404. Nunca 500. → Task 11, `Invalid_cursor_returns_400_validation`; Task 13, `Malformed_token_returns_404`.
5. **Mudar a data de um registro na linha do tempo e corrigir km digitada errado:** a edição revalida R3 excluindo o próprio registro; excluir o registro de maior km mantém a km atual (V4) e a correção (V3) funciona depois. → Task 11, `Moving_record_date_revalidates_bounds_excluding_itself` e `Deleting_highest_km_record_keeps_current_km_and_correction_fixes_typo`.

---

## Estrutura de arquivos

```
backend/
  Garage.slnx  global.json  Directory.Build.props  .gitignore  .config/dotnet-tools.json  README.md
  src/
    Garage.Domain/
      AssemblyMarker.cs
      Common/        Error.cs ErrorType.cs Result.cs Unit.cs Entity.cs AggregateRoot.cs IDomainEvent.cs Odometer.cs Money.cs
      Vehicles/      Vehicle.cs VehicleData.cs VehicleType.cs Fuel.cs Plate.cs VehicleYear.cs CatalogRef.cs VehicleErrors.cs
      Records/       ServiceRecord.cs Part.cs PartData.cs RecordDetails.cs OdometerBounds.cs RecordType.cs Category.cs PartUnit.cs ServiceRecordAdded.cs ServiceRecordErrors.cs
      Reminders/     Reminder.cs ReminderInterval.cs ReminderSchedule.cs ReminderStatus.cs ReminderState.cs ReminderErrors.cs
      Attachments/   Attachment.cs AttachmentKind.cs DetectedFileType.cs FileSignature.cs AttachmentErrors.cs
      Sharing/       ShareLink.cs ShareLinkErrors.cs
    Garage.Application/
      AssemblyMarker.cs  DependencyInjection.cs
      Abstractions/
        Messaging/   ICommand.cs IQuery.cs ICommandHandler.cs IQueryHandler.cs ISender.cs Sender.cs
        Behaviors/   LoggingCommandDecorator.cs LoggingQueryDecorator.cs ValidationCommandDecorator.cs ValidationQueryDecorator.cs TransactionCommandDecorator.cs ValidationErrors.cs
        Events/      IDomainEventHandler.cs IDomainEventDispatcher.cs DomainEventDispatcher.cs
        Ports/       IUnitOfWork.cs IReadDbContext.cs ICurrentUser.cs IAppClock.cs IVehicleAccess.cs IVehicleRepository.cs IServiceRecordRepository.cs IReminderRepository.cs IAttachmentRepository.cs IShareLinkRepository.cs IFileStorage.cs IImageProcessor.cs IVehicleCatalog.cs
        CommonErrors.cs  PagedResult.cs
      Vehicles/      <Caso>/{<Caso>Command|Query.cs, <Caso>Handler.cs, <Caso>Validator.cs}  VehicleDtos.cs VehicleInputValidator.cs
      Records/       idem + RecordDtos.cs RecordInputValidator.cs RecordCursor.cs FulfillRemindersOnRecordAdded.cs
      Reminders/     idem + ReminderDtos.cs ReminderInputValidator.cs
      Attachments/   idem + AttachmentDtos.cs
      Sharing/       idem + ShareLinkDtos.cs PublicDtos.cs ShareTokens.cs
      Catalog/       idem + CatalogModels.cs CatalogErrors.cs
    Garage.Infrastructure/
      AssemblyMarker.cs  DependencyInjection.cs
      Persistence/   GarageDbContext.cs UnitOfWork.cs Configurations/*.cs Repositories/*.cs Migrations/
      Identity/      AppUser.cs CurrentUser.cs PasswordResetService.cs
      Access/        VehicleAccess.cs
      Time/          AppClock.cs AppOptions.cs
      Storage/       StorageOptions.cs LocalDiskFileStorage.cs MagickImageProcessor.cs
      Catalog/       FipeOptions.cs FipeClient.cs FipeDtos.cs FipeTranslator.cs CatalogCacheEntry.cs FipeVehicleCatalog.cs
    Garage.Api/
      Program.cs  appsettings.json  appsettings.Development.json
      Endpoints/     IEndpoint.cs EndpointRegistration.cs
      Http/          ResultHttpExtensions.cs ApiErrors.cs GlobalExceptionHandler.cs AntiforgeryValidationMiddleware.cs SecurityHeadersMiddleware.cs RateLimitingSetup.cs RateLimitOptions.cs CreatedIdResponse.cs
      Auth/          AuthSetup.cs AuthRequests.cs <Caso>Endpoint.cs
      Cli/           ResetPasswordCli.cs
      Vehicles/ Records/ Reminders/ Attachments/ Sharing/ Public/ Catalog/   <Caso>Endpoint.cs + <Feature>Requests.cs
  tests/
    Garage.Domain.Tests/          Common/ Vehicles/ Records/ Reminders/ Attachments/ Sharing/
    Garage.Infrastructure.Tests/  AppClockTests.cs LocalDiskFileStorageTests.cs Catalog/FipeTranslatorTests.cs Catalog/Fixtures/*.json
    Garage.Api.IntegrationTests/  Infrastructure/{GarageApiFactory.cs ApiCollection.cs TestClients.cs TestHttpExtensions.cs TestData.cs FakeFipeHandler.cs TestImages.cs}  <Feature>/*Tests.cs
    Garage.ArchitectureTests/     LayerDependencyTests.cs HandlerConventionTests.cs
```

Padrão por caso de uso (ex.: `Application/Vehicles/CreateVehicle/`): `CreateVehicleCommand.cs`, `CreateVehicleHandler.cs`, `CreateVehicleValidator.cs` (validator só quando houver entrada). Endpoint correspondente: `Api/Vehicles/CreateVehicleEndpoint.cs`.

---

### Task 1: Solução, projetos e testes de arquitetura

**Files:**
- Create: `backend/Garage.slnx`, `backend/global.json`, `backend/Directory.Build.props`, `backend/.gitignore`, `backend/.config/dotnet-tools.json`
- Create: os 4 projetos de `src/` e os 4 de `tests/`, cada `src` com `AssemblyMarker.cs`
- Test: `backend/tests/Garage.ArchitectureTests/LayerDependencyTests.cs`

**Interfaces:**
- Produces: `public sealed class AssemblyMarker;` em `Garage.Domain`, `Garage.Application`, `Garage.Infrastructure` (namespaces iguais ao projeto). `Garage.Api` expõe `public partial class Program;`.

- [ ] **Step 1: Criar solução e projetos**

```powershell
mkdir backend; cd backend
dotnet new sln -n Garage --format slnx
dotnet new gitignore
dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature
dotnet new tool-manifest; dotnet tool install dotnet-ef
dotnet new classlib -f net10.0 -n Garage.Domain -o src/Garage.Domain
dotnet new classlib -f net10.0 -n Garage.Application -o src/Garage.Application
dotnet new classlib -f net10.0 -n Garage.Infrastructure -o src/Garage.Infrastructure
dotnet new web      -f net10.0 -n Garage.Api -o src/Garage.Api
dotnet new xunit -f net10.0 -n Garage.Domain.Tests -o tests/Garage.Domain.Tests
dotnet new xunit -f net10.0 -n Garage.Infrastructure.Tests -o tests/Garage.Infrastructure.Tests
dotnet new xunit -f net10.0 -n Garage.Api.IntegrationTests -o tests/Garage.Api.IntegrationTests
dotnet new xunit -f net10.0 -n Garage.ArchitectureTests -o tests/Garage.ArchitectureTests
Get-ChildItem -Recurse -Filter Class1.cs | Remove-Item
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName }
dotnet add src/Garage.Application reference src/Garage.Domain
dotnet add src/Garage.Infrastructure reference src/Garage.Application
dotnet add src/Garage.Api reference src/Garage.Infrastructure
dotnet add tests/Garage.Domain.Tests reference src/Garage.Domain
dotnet add tests/Garage.Infrastructure.Tests reference src/Garage.Infrastructure
dotnet add tests/Garage.Api.IntegrationTests reference src/Garage.Api
dotnet add tests/Garage.ArchitectureTests reference src/Garage.Api
dotnet add tests/Garage.ArchitectureTests package NetArchTest.Rules
```

`Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`.

- [ ] **Step 2: Escrever o teste de arquitetura que falha**

```csharp
public class LayerDependencyTests
{
    [Fact] public void Domain_depends_on_nothing() => AssertNoDependency(
        typeof(Garage.Domain.AssemblyMarker).Assembly,
        "Garage.Application", "Garage.Infrastructure", "Garage.Api", "Microsoft.EntityFrameworkCore");

    [Fact] public void Application_does_not_depend_on_outer_layers() => AssertNoDependency(
        typeof(Garage.Application.AssemblyMarker).Assembly,
        "Garage.Infrastructure", "Garage.Api", "Npgsql");

    [Fact] public void Infrastructure_does_not_depend_on_api() => AssertNoDependency(
        typeof(Garage.Infrastructure.AssemblyMarker).Assembly, "Garage.Api");

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test tests/Garage.ArchitectureTests`
Expected: FAIL — erro de compilação, `AssemblyMarker` não existe.

- [ ] **Step 4: Criar os três `AssemblyMarker.cs` e `public partial class Program;` no fim de `Program.cs`**

- [ ] **Step 5: Rodar build e testes**

Run: `dotnet build` → 0 warnings, 0 errors. `dotnet test tests/Garage.ArchitectureTests` → PASS (3 testes).

- [ ] **Step 6: Commit**

```powershell
git add .
git commit -m "chore(backend): solução .NET 10 em camadas com testes de arquitetura"
```

---

### Task 2: Primitivas do domínio e value objects

**Files:**
- Create: `src/Garage.Domain/Common/{Error,ErrorType,Result,Unit,Entity,AggregateRoot,IDomainEvent,Odometer,Money}.cs`, `src/Garage.Domain/Vehicles/{Plate,VehicleYear}.cs`
- Test: `tests/Garage.Domain.Tests/Common/ResultTests.cs`, `tests/Garage.Domain.Tests/Common/ValueObjectTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum ErrorType { Validation, Unauthorized, NotFound, Conflict, PayloadTooLarge, UnsupportedMedia, DomainRule, Unavailable, TooManyRequests }
public sealed record Error(string Code, string Message, ErrorType Type, IReadOnlyDictionary<string, object?>? Metadata = null)
{   // Code "validation.failed", Message "Dados inválidos.", Metadata["errors"] = fieldErrors
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors); }
public class Result
{   public bool IsSuccess { get; } public bool IsFailure => !IsSuccess; public Error? Error { get; }
    public static Result Success(); public static Result Failure(Error error);
    public static implicit operator Result(Error error); }
public sealed class Result<T> : Result
{   public T Value { get; }                       // InvalidOperationException se IsFailure
    public static Result<T> Success(T value); public static new Result<T> Failure(Error error);
    public static implicit operator Result<T>(T value); public static implicit operator Result<T>(Error error); }
public readonly record struct Unit { public static readonly Unit Value; }
public abstract class Entity { public Guid Id { get; protected init; } }
public interface IDomainEvent;
public abstract class AggregateRoot : Entity
{   public IReadOnlyList<IDomainEvent> DomainEvents { get; } protected void Raise(IDomainEvent e); public void ClearDomainEvents(); }
public readonly record struct Odometer      // ctor lança ArgumentOutOfRangeException se !IsValid
{   public const int Max = 2_000_000; public int Km { get; } public Odometer(int km); public static bool IsValid(int km); }
public readonly record struct Money          // ctor lança ArgumentOutOfRangeException se !IsValid
{   public decimal Amount { get; } public Money(decimal amount); public static Money Zero { get; }
    public static bool IsValid(decimal amount);          // 0 ≤ amount ≤ 9_999_999_999.99 e no máximo 2 casas
    public Money Times(decimal quantity);                // arredonda 2 casas, MidpointRounding.AwayFromZero
    public static Money operator +(Money a, Money b); }
public sealed record Plate                    // armazenada normalizada
{   public string Value { get; } public static string Normalize(string raw);   // maiúsculas, sem '-' e espaços
    public static bool IsValid(string raw);   // ^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$ sobre o normalizado
    public static Plate From(string raw); }   // lança ArgumentException se inválida
public readonly record struct VehicleYear(int Manufacture, int Model)
{   public static bool IsValid(int manufacture, int model, int currentYear);  // 1900 ≤ fab ≤ currentYear+1; model ∈ {fab, fab+1}
    public static VehicleYear From(int manufacture, int model, int currentYear); }  // lança ArgumentException se inválido
```
Regra do plano: VOs lançam exceção (erro de programação); validators da Application chamam `IsValid` antes, então entrada inválida vira 400.
Atenção: C# não aplica conversão implícita definida pelo usuário quando `T` é interface. Para `Result<IReadOnlyList<X>>`, use `Result<IReadOnlyList<X>>.Success(lista)`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Theory] [InlineData("abc-1234","ABC1234")] [InlineData("ABC1D23","ABC1D23")] [InlineData(" abc 1d23 ","ABC1D23")]
public void Plate_normalizes_valid_formats(string raw, string expected) => Assert.Equal(expected, Plate.From(raw).Value);
[Theory] [InlineData("AB12345")] [InlineData("ABC12345")] [InlineData("")] [InlineData("ABCD123")]
public void Plate_rejects_invalid(string raw) => Assert.False(Plate.IsValid(raw));

[Theory] [InlineData(1997,1998,2026,true)] [InlineData(1998,1998,2026,true)] [InlineData(2027,2028,2026,true)]
         [InlineData(1998,2000,2026,false)] [InlineData(1899,1899,2026,false)] [InlineData(2028,2028,2026,false)] [InlineData(1998,1997,2026,false)]
public void VehicleYear_rules(int fab, int model, int current, bool valid) => Assert.Equal(valid, VehicleYear.IsValid(fab, model, current));

[Theory] [InlineData(-1,false)] [InlineData(0,true)] [InlineData(2_000_000,true)] [InlineData(2_000_001,false)]
public void Odometer_range(int km, bool valid) => Assert.Equal(valid, Odometer.IsValid(km));

[Theory] [InlineData("10.005",false)] [InlineData("-1",false)] [InlineData("0",true)] [InlineData("9999999999.99",true)]
public void Money_validity(string amount, bool valid) => Assert.Equal(valid, Money.IsValid(decimal.Parse(amount, CultureInfo.InvariantCulture)));
[Fact] public void Money_times_rounds_to_cents() { Assert.Equal(99.99m, new Money(33.33m).Times(3).Amount); Assert.Equal(5.00m, new Money(10m).Times(0.5m).Amount); }

[Fact] public void Result_value_of_failure_throws() =>
    Assert.Throws<InvalidOperationException>(() => Result<int>.Failure(new Error("x","y",ErrorType.DomainRule)).Value);
[Fact] public void Validation_error_carries_field_errors()
{   var e = Error.Validation(new Dictionary<string,string[]> { ["make"] = ["Obrigatório."] });
    Assert.Equal("validation.failed", e.Code); Assert.Equal(ErrorType.Validation, e.Type); Assert.NotNull(e.Metadata!["errors"]); }
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Garage.Domain.Tests` → FAIL (tipos não existem).

- [ ] **Step 3: Implementar os tipos acima nos arquivos listados**

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Garage.Domain.Tests` → PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(domain): Result, Error e value objects"`

---

### Task 3: Agregado Vehicle (V1–V3)

**Files:**
- Create: `src/Garage.Domain/Vehicles/{Vehicle,VehicleData,VehicleType,Fuel,CatalogRef,VehicleErrors}.cs`
- Test: `tests/Garage.Domain.Tests/Vehicles/VehicleTests.cs`

**Interfaces:**
- Consumes: Task 2 (`Odometer`, `Plate`, `VehicleYear`, `Result`, `Error`, `AggregateRoot`).
- Produces:
```csharp
public enum VehicleType { Car, Motorcycle, Truck }
public enum Fuel { Gasoline, Ethanol, Flex, Diesel, Hybrid, Electric, Other }
public sealed record CatalogRef(VehicleType Type, string FipeCode, string FipeYearCode);
public sealed record VehicleData(VehicleType Type, string Make, string Model, string? Version, string? Generation,
    VehicleYear Year, Fuel Fuel, string? Nickname, Plate? Plate, string? Vin, string? Notes, CatalogRef? Catalog);
public sealed class Vehicle : AggregateRoot
{   public Guid OwnerId { get; } /* + uma propriedade por campo de VehicleData */ 
    public Odometer CurrentOdometer { get; } public DateTimeOffset OdometerUpdatedAt { get; }
    public string DisplayName => Nickname ?? $"{Make} {Model} {Year.Model}";
    public static Vehicle Create(Guid ownerId, VehicleData data, Odometer initialOdometer, DateTimeOffset now);
    public void Update(VehicleData data);
    public void RegisterOdometer(Odometer km, DateTimeOffset now);                         // V1
    public Result UpdateOdometer(Odometer km, DateTimeOffset now);                         // V2
    public Result CorrectOdometer(Odometer km, Odometer? maxRecordKm, DateTimeOffset now); // V3 }
public static class VehicleErrors
{   public static readonly Error NotFound;               // vehicle.not_found, NotFound, "Veículo não encontrado."
    public static readonly Error OdometerCannotDecrease; // vehicle.odometer_cannot_decrease, DomainRule, "A quilometragem não pode diminuir. Se digitou errado, use a correção de km."
    public static Error OdometerBelowRecords(int maxRecordKm); // vehicle.odometer_below_records, DomainRule, Metadata["maxRecordKm"], "A km corrigida não pode ficar abaixo de {max:N0} km, a maior km do histórico."
    public static readonly Error CatalogNotLinked; }     // vehicle.catalog_not_linked, NotFound, "Este veículo não está vinculado à FIPE."
```

- [ ] **Step 1: Escrever os testes que falham** (veículo de teste: km atual 90.000, `now` = 2026-10-10T15:00Z)

```csharp
[Fact] public void Create_generates_version7_id()                                  // Assert.Equal(7, v.Id.Version)
[Fact] public void RegisterOdometer_higher_updates_km_and_timestamp()            // 95_000 → CurrentOdometer.Km == 95_000, OdometerUpdatedAt == now2
[Fact] public void RegisterOdometer_lower_or_equal_is_ignored()                  // 80_000 e 90_000 → km 90_000, timestamp inalterado
[Fact] public void UpdateOdometer_lower_fails_with_cannot_decrease()             // Error.Code == "vehicle.odometer_cannot_decrease", km inalterada
[Fact] public void UpdateOdometer_equal_succeeds()
[Fact] public void CorrectOdometer_below_max_record_fails()                      // km atual 150_000, max 15_200, corrigir p/ 15_000 → "vehicle.odometer_below_records", Metadata["maxRecordKm"] == 15_200
[Fact] public void CorrectOdometer_to_max_record_decreases()                     // corrigir p/ 15_200 → km 15_200
[Fact] public void CorrectOdometer_without_records_accepts_any_value()           // maxRecordKm null, corrigir p/ 10 → km 10
[Fact] public void DisplayName_uses_nickname_or_make_model_year()               // null → "Honda Civic 1998"; "Roxinho" → "Roxinho"
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test tests/Garage.Domain.Tests --filter VehicleTests` → FAIL (tipos não existem).
- [ ] **Step 3: Implementar os tipos acima**
- [ ] **Step 4: Rodar e ver passar** — mesmo comando → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(domain): agregado Vehicle com regras de km"`

---

### Task 4: Agregado ServiceRecord (R1–R3, R5)

**Files:**
- Create: `src/Garage.Domain/Records/{ServiceRecord,Part,PartData,RecordDetails,OdometerBounds,RecordType,Category,PartUnit,ServiceRecordAdded,ServiceRecordErrors}.cs`
- Test: `tests/Garage.Domain.Tests/Records/ServiceRecordTests.cs`

**Interfaces:**
- Consumes: Task 2.
- Produces:
```csharp
public enum RecordType { Maintenance, Modification }
public enum Category { Engine, Transmission, Suspension, Brakes, Electrical, WheelsTires, BodyPaint, Interior, Other }
public enum PartUnit { Unit, Liter, Kilogram, Meter }
public sealed record PartData(string Name, string? Brand, string? PartNumber, decimal Quantity, PartUnit Unit, Money? UnitPrice);
public sealed record RecordDetails(RecordType Type, DateOnly Date, Odometer? Odometer, string Title, string? Description,
    Category Category, string? PerformedBy, Money? LaborCost, string? ReplacedOriginalPart, IReadOnlyList<PartData> Parts);
public readonly record struct OdometerBounds(int? MaxKmBefore, int? MinKmAfter) { public static OdometerBounds None { get; } }
public sealed class Part : Entity { /* campos de PartData */ public Money LineTotal { get; } } // UnitPrice null → Money.Zero
public sealed class ServiceRecord : AggregateRoot
{   public Guid VehicleId { get; } /* campos de RecordDetails */ public IReadOnlyList<Part> Parts { get; } public DateTimeOffset CreatedAt { get; }
    public Money TotalCost { get; }  // Σ LineTotal + LaborCost
    public static Result<ServiceRecord> Create(Guid vehicleId, RecordDetails details, OdometerBounds bounds,
        IReadOnlyList<Guid> fulfilledReminderIds, DateOnly today, DateTimeOffset now);   // valida R1, R2, R3 nesta ordem; emite ServiceRecordAdded
    public Result Update(RecordDetails details, OdometerBounds bounds, DateOnly today); } // mesmas regras; substitui as peças; não emite evento; falha não altera estado
public sealed record ServiceRecordAdded(Guid RecordId, Guid VehicleId, DateOnly Date, Odometer? Odometer, IReadOnlyList<Guid> FulfilledReminderIds) : IDomainEvent;
public static class ServiceRecordErrors
{   public static readonly Error NotFound;                         // record.not_found, "Registro não encontrado."
    public static readonly Error DateInFuture;                     // record.date_in_future, DomainRule, "A data do registro não pode estar no futuro."
    public static readonly Error ReplacedPartOnlyForModification;  // record.replaced_part_only_for_modification, DomainRule, "Peça original substituída só vale para modificações."
    public static Error OdometerOutOfOrder(int? maxKmBefore, int? minKmAfter); } // record.odometer_out_of_order, Metadata["maxKmBefore"], ["minKmAfter"]
```
Mensagem de `OdometerOutOfOrder` (pt-BR, números com `N0` pt-BR): os dois limites → "A km deve ficar entre 80.000 e 90.000 km para manter a ordem do histórico."; só antes → "A km deve ser de pelo menos 80.000 km…"; só depois → "A km deve ser de no máximo 90.000 km…".

- [ ] **Step 1: Escrever os testes que falham** (`today` = 2026-10-10)

```csharp
[Fact] public void Create_with_future_date_fails()                    // date 2026-10-11 → "record.date_in_future"
[Fact] public void Create_dated_today_succeeds()
[Fact] public void ReplacedOriginalPart_on_maintenance_fails()        // "record.replaced_part_only_for_modification"
[Fact] public void ReplacedOriginalPart_on_modification_succeeds()
[Theory] [InlineData(85_000, 80_000, 90_000)] [InlineData(80_000, 80_000, 90_000)] [InlineData(90_000, 80_000, 90_000)]
         [InlineData(50_000, null, null)] [InlineData(85_000, 80_000, null)]
public void Odometer_within_bounds_succeeds(int km, int? before, int? after)
[Theory] [InlineData(79_999, 80_000, 90_000)] [InlineData(90_001, 80_000, 90_000)]
public void Odometer_out_of_bounds_fails_with_metadata(int km, int? before, int? after) // Metadata["maxKmBefore"] == before, ["minKmAfter"] == after
[Fact] public void Record_without_odometer_ignores_bounds()           // Odometer null, bounds (80_000, 90_000) → sucesso
[Fact] public void Create_raises_ServiceRecordAdded_with_fulfilled_ids()
[Fact] public void TotalCost_sums_parts_and_labor()                   // 2 × 45,90 + 1 × 120,00 + mão de obra 80,00 → 291,80; LineTotal da 1ª peça 91,80
[Fact] public void Update_with_invalid_data_keeps_state()             // update com data futura falha; Title e Parts inalterados
[Fact] public void Update_does_not_raise_events()
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test tests/Garage.Domain.Tests --filter ServiceRecordTests` → FAIL.
- [ ] **Step 3: Implementar os tipos acima**
- [ ] **Step 4: Rodar e ver passar** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(domain): agregado ServiceRecord com regra km x cronologia"`

---

### Task 5: Agregado Reminder e cálculo de status (M1–M5)

**Files:**
- Create: `src/Garage.Domain/Reminders/{Reminder,ReminderInterval,ReminderSchedule,ReminderStatus,ReminderState,ReminderErrors}.cs`
- Test: `tests/Garage.Domain.Tests/Reminders/ReminderScheduleTests.cs`, `tests/Garage.Domain.Tests/Reminders/ReminderTests.cs`

**Interfaces:**
- Consumes: Task 2.
- Produces:
```csharp
public enum ReminderState { Ok, DueSoon, Overdue }
public sealed record ReminderStatus(ReminderState State, int? KmRemaining, int? DaysRemaining);
public static class ReminderSchedule
{   public const int DueSoonKm = 1_000; public const int DueSoonDays = 30;
    public static ReminderStatus Evaluate(int? intervalKm, int? intervalMonths, DateOnly lastDoneDate,
        int? lastDoneOdometer, int currentOdometer, DateOnly today); }
public sealed record ReminderInterval(int? Km, int? Months);
public sealed class Reminder : AggregateRoot
{   public Guid VehicleId { get; } public string Title { get; } public int? IntervalKm { get; } public int? IntervalMonths { get; }
    public DateOnly LastDoneDate { get; } public Odometer? LastDoneOdometer { get; }
    public static Result<Reminder> Create(Guid vehicleId, string title, ReminderInterval interval, DateOnly lastDoneDate, Odometer? lastDoneOdometer, DateOnly today); // M1, M5
    public Result Update(string title, ReminderInterval interval, DateOnly lastDoneDate, Odometer? lastDoneOdometer, DateOnly today);           // M1, M5; base pode voltar
    public Result MarkDone(DateOnly date, Odometer? odometer);                 // M3, M4; sucesso → LastDoneDate = date, LastDoneOdometer = odometer
    public ReminderStatus Evaluate(Odometer current, DateOnly today); }      // delega para ReminderSchedule
public static class ReminderErrors
{   NotFound /* reminder.not_found, "Lembrete não encontrado." */, IntervalRequired /* reminder.interval_required, "Informe o intervalo em km, em meses ou ambos." */,
    FulfillmentRequiresOdometer /* reminder.fulfillment_requires_odometer, "Este lembrete é por km: informe a km do registro." */,
    FulfillmentOlderThanBase /* reminder.fulfillment_older_than_base, "O registro é anterior à última vez que este lembrete foi feito." */,
    BaseDateInFuture /* reminder.base_date_in_future, "A data da última vez não pode estar no futuro." */ }  // todos static readonly Error, DomainRule exceto NotFound
```
`Evaluate`: `kmRemaining = lastDoneOdometer + intervalKm − currentOdometer`; `daysRemaining = lastDoneDate.AddMonths(intervalMonths).DayNumber − today.DayNumber`. Por eixo: ≤ 0 → Overdue; ≤ limite → DueSoon; senão Ok. Estado final = o pior eixo presente. `Create`/`Update` lançam `ArgumentException` se `IntervalKm` existe e `lastDoneOdometer` é null (o validator garante antes).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// km: intervalo 5_000, base 80_000
[Theory] [InlineData(83_999, ReminderState.Ok, 1_001)] [InlineData(84_000, ReminderState.DueSoon, 1_000)]
         [InlineData(84_999, ReminderState.DueSoon, 1)] [InlineData(85_000, ReminderState.Overdue, 0)] [InlineData(86_000, ReminderState.Overdue, -1_000)]
public void Km_axis_boundaries(int current, ReminderState state, int kmRemaining)
// meses: intervalo 6, base 2026-04-10 (vence 2026-10-10)
[Theory] [InlineData("2026-09-09", ReminderState.Ok, 31)] [InlineData("2026-09-10", ReminderState.DueSoon, 30)]
         [InlineData("2026-10-09", ReminderState.DueSoon, 1)] [InlineData("2026-10-10", ReminderState.Overdue, 0)]
public void Months_axis_boundaries(string today, ReminderState state, int daysRemaining)
[Fact] public void Whichever_comes_first_by_time()  // 5_000 km/6 meses, base 80_000/2026-04-10; atual 81_000, hoje 2026-10-10 → Overdue, km 4_000, dias 0
[Fact] public void Whichever_comes_first_by_km()    // atual 84_500, hoje 2026-05-01 → DueSoon, km 500

[Fact] public void Create_without_interval_fails()               // "reminder.interval_required"
[Fact] public void Create_with_base_date_in_future_fails()       // today 2026-10-10, base 2026-10-11 → "reminder.base_date_in_future"
[Fact] public void MarkDone_without_odometer_on_km_reminder_fails()   // "reminder.fulfillment_requires_odometer"
[Fact] public void MarkDone_older_than_base_fails()                   // "reminder.fulfillment_older_than_base"
[Fact] public void MarkDone_same_date_succeeds()
[Fact] public void MarkDone_moves_base()                              // base vira (2026-10-01, 85_000)
[Fact] public void MarkDone_without_odometer_on_months_only_succeeds()
[Fact] public void Update_can_move_base_backwards()
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test tests/Garage.Domain.Tests --filter Reminder` → FAIL.
- [ ] **Step 3: Implementar os tipos acima**
- [ ] **Step 4: Rodar e ver passar** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(domain): agregado Reminder e cálculo de status"`

---

### Task 6: Anexos, links e detecção de tipo de arquivo (A1–A4, S1–S2)

**Files:**
- Create: `src/Garage.Domain/Attachments/{Attachment,AttachmentKind,DetectedFileType,FileSignature,AttachmentErrors}.cs`, `src/Garage.Domain/Sharing/{ShareLink,ShareLinkErrors}.cs`
- Test: `tests/Garage.Domain.Tests/Attachments/{FileSignatureTests,AttachmentTests}.cs`, `tests/Garage.Domain.Tests/Sharing/ShareLinkTests.cs`

**Interfaces:**
- Consumes: Task 2.
- Produces:
```csharp
public enum AttachmentKind { Photo, Document }
public enum DetectedFileType { Jpeg, Png, Webp, Pdf }
public static class FileSignature
{   public const int HeaderLength = 12;
    public static DetectedFileType? Detect(ReadOnlySpan<byte> header); } // JPEG FF D8 FF; PNG 89 50 4E 47 0D 0A 1A 0A; WebP "RIFF"(0–3)+"WEBP"(8–11); PDF "%PDF-"
public sealed class Attachment : AggregateRoot
{   public const long MaxSizeBytes = 10 * 1024 * 1024; public const int MaxPerRecord = 20;
    public Guid VehicleId { get; } public Guid RecordId { get; } public AttachmentKind Kind { get; } public bool IsPublic { get; }
    public string FileName { get; } public string ContentType { get; } public long SizeBytes { get; }
    public string StorageKey { get; }    // $"{VehicleId}/{Id}"
    public DateTimeOffset UploadedAt { get; }
    public static Attachment Create(Guid vehicleId, Guid recordId, AttachmentKind requestedKind, DetectedFileType type,
        string fileName, string contentType, long sizeBytes, DateTimeOffset now); // PDF ⇒ Document; IsPublic = (Kind == Photo)
    public Result SetVisibility(bool isPublic); }                                  // A4
public static class AttachmentErrors
{   NotFound /* attachment.not_found, "Anexo não encontrado." */, TooLarge /* attachment.too_large, PayloadTooLarge, "Arquivo maior que 10 MB." */,
    UnsupportedType /* attachment.unsupported_type, UnsupportedMedia, "Formato não suportado. Envie JPG, PNG, WebP ou PDF." */,
    LimitReached /* attachment.limit_reached, DomainRule, "Limite de 20 anexos por registro atingido." */,
    DocumentCannotBePublic /* attachment.document_cannot_be_public, DomainRule, "Documentos não podem ficar visíveis no link público." */ } // todos static readonly Error; NotFound com ErrorType.NotFound
public sealed class ShareLink : AggregateRoot
{   public Guid VehicleId { get; } public string Token { get; } public string? Label { get; } public bool IncludeCosts { get; } public bool IncludePlate { get; }
    public DateTimeOffset? ExpiresAt { get; } public DateTimeOffset? RevokedAt { get; } public DateTimeOffset CreatedAt { get; }
    public static ShareLink Create(Guid vehicleId, string token, string? label, bool includeCosts, bool includePlate, DateTimeOffset? expiresAt, DateTimeOffset now);
    public bool IsValidAt(DateTimeOffset now);   // RevokedAt null && (ExpiresAt null || ExpiresAt > now)
    public void Revoke(DateTimeOffset now); }    // idempotente: mantém o primeiro RevokedAt
public static class ShareLinkErrors { public static readonly Error NotFound; } // share_link.not_found, "Link não encontrado ou expirado."
```

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Theory] [MemberData(nameof(Signatures))] public void Detects_supported_types(byte[] header, DetectedFileType? expected)
// FF D8 FF E0 … → Jpeg; 89 50 4E 47 0D 0A 1A 0A … → Png; "RIFF"+00 00 00 00+"WEBP" → Webp; "%PDF-1.7" → Pdf;
// 00 00 00 18 "ftypheic" → null; "<svg" → null; [] → null; FF D8 → null
[Fact] public void Pdf_is_always_private_document()          // requested Photo + Pdf → Kind Document, IsPublic false
[Fact] public void Photo_is_public_by_default()               // Jpeg + Photo → IsPublic true
[Fact] public void Image_marked_as_document_is_private()      // Jpeg + Document → IsPublic false
[Fact] public void Document_cannot_be_made_public()           // SetVisibility(true) → "attachment.document_cannot_be_public"
[Fact] public void StorageKey_is_vehicle_slash_id()
[Fact] public void Link_without_expiry_is_valid()
[Fact] public void Link_is_invalid_at_or_after_expiry()       // ExpiresAt == now → inválido
[Fact] public void Revoked_link_is_invalid_and_revoke_is_idempotent()
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test tests/Garage.Domain.Tests` → FAIL.
- [ ] **Step 3: Implementar os tipos acima**
- [ ] **Step 4: Rodar e ver passar** → PASS (suíte de domínio inteira).
- [ ] **Step 5: Commit** — `git commit -m "feat(domain): anexos, links públicos e detecção de tipo"`

---

### Task 7: Pipeline da Application, persistência base e host da API

**Files:**
- Create: `src/Garage.Application/Abstractions/**` (Messaging, Behaviors, Events, Ports `IUnitOfWork` `IReadDbContext` `ICurrentUser` `IAppClock` `IVehicleAccess`, `CommonErrors.cs`, `PagedResult.cs`), `src/Garage.Application/DependencyInjection.cs`
- Create: `src/Garage.Infrastructure/{DependencyInjection.cs, Persistence/GarageDbContext.cs, Persistence/UnitOfWork.cs, Identity/AppUser.cs, Identity/CurrentUser.cs, Time/AppClock.cs, Time/AppOptions.cs}`, migration `Initial`
- Create: `src/Garage.Api/{Program.cs, appsettings.json, appsettings.Development.json, Endpoints/IEndpoint.cs, Endpoints/EndpointRegistration.cs, Http/ResultHttpExtensions.cs, Http/GlobalExceptionHandler.cs, Http/CreatedIdResponse.cs}`
- Test: `tests/Garage.Api.IntegrationTests/Infrastructure/{GarageApiFactory,ApiCollection,TestHttpExtensions}.cs`, `tests/Garage.Api.IntegrationTests/HostTests.cs`, `tests/Garage.Infrastructure.Tests/AppClockTests.cs`, `tests/Garage.ArchitectureTests/HandlerConventionTests.cs`

**Interfaces:**
- Consumes: Task 2 (`Result`, `Error`, `Unit`, `AggregateRoot`, `IDomainEvent`).
- Produces:
```csharp
public interface ICommand<TResult>; public interface IQuery<TResult>;
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult> { Task<Result<TResult>> Handle(TCommand command, CancellationToken ct); }
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult> { Task<Result<TResult>> Handle(TQuery query, CancellationToken ct); }
public interface ISender
{   Task<Result<TResult>> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task<Result<TResult>> Send<TResult>(IQuery<TResult> query, CancellationToken ct = default); }
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent { Task<Result> Handle(TEvent e, CancellationToken ct); }
public interface IDomainEventDispatcher { Task<Result> Dispatch(IReadOnlyList<IDomainEvent> events, CancellationToken ct); } // para no 1º Result de falha
public interface IUnitOfWork
{   Task<Result> SaveChangesAsync(CancellationToken ct);
    Task DiscardAsync(CancellationToken ct);                 // roda ações OnRollback e limpa o ChangeTracker
    void AfterCommit(Func<CancellationToken, Task> action);  // falhas só geram log de warning
    void OnRollback(Func<CancellationToken, Task> action); }
public interface IReadDbContext { IQueryable<T> Query<T>() where T : class; }   // sempre AsNoTracking
public interface ICurrentUser { Guid UserId { get; } }                            // InvalidOperationException se anônimo
public interface IAppClock { DateTimeOffset Now { get; } DateOnly Today { get; } int CurrentYear { get; } }
public interface IVehicleAccess
{   Task<bool> CanReadAsync(Guid vehicleId, Guid userId, CancellationToken ct);
    Task<bool> CanEditAsync(Guid vehicleId, Guid userId, CancellationToken ct); }  // implementação na Task 9
public static class CommonErrors { public static readonly Error ConcurrencyConflict; } // concurrency.conflict, Conflict, "Este item foi alterado em outra tela. Recarregue e tente de novo."
public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor);
public static IServiceCollection AddApplication(this IServiceCollection services);
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config);
public sealed class AppUser : IdentityUser<Guid>;
public sealed class GarageDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IReadDbContext;
public interface IEndpoint { void Map(RouteGroupBuilder api); }        // api = app.MapGroup("/api").RequireAuthorization()
public static void MapEndpoints(this WebApplication app, RouteGroupBuilder api); // instancia todo IEndpoint concreto do assembly Api
public static IResult ToProblem(this Error error);   // ProblemDetails: status pelo ErrorType (spec §9.2 + TooManyRequests→429), extensions "code", metadata e "traceId";
                                                     // Validation com Metadata["errors"] → ValidationProblem com "errors"; Validation sem "errors" → 400 ProblemDetails simples
public sealed record CreatedIdResponse(Guid Id);
// testes
public sealed class GarageApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{   public FakeTimeProvider Clock { get; }      // início 2026-10-10T15:00:00Z
    public string StoragePath { get; } }        // pasta temporária, usada a partir da Task 12
[CollectionDefinition("api")] public sealed class ApiCollection : ICollectionFixture<GarageApiFactory>;
public static Task<JsonElement> AssertProblemAsync(this HttpResponseMessage r, HttpStatusCode status, string code); // devolve o corpo
public static class TestJson { public static JsonSerializerOptions Options { get; } } // web defaults + enum camelCase
```

Detalhes que o implementador não deduz sozinho:
- `Sender` resolve `ICommandHandler<,>`/`IQueryHandler<,>` com `MakeGenericType(request.GetType(), typeof(TResult))`; cache por tipo de request.
- Registro (Scrutor) em `AddApplication`: scan de handlers, validators e `IDomainEventHandler<>`; decorators registrados nesta ordem para que Logging fique por fora: `TransactionCommandDecorator` → `ValidationCommandDecorator` → `LoggingCommandDecorator`; para queries: `ValidationQueryDecorator` → `LoggingQueryDecorator`.
- `ValidationErrors.FromFailures(...)` monta o dicionário com chaves camelCase por segmento (`Parts[0].Name` → `parts[0].name`).
- `TransactionCommandDecorator`: handler falhou → `uow.DiscardAsync`; sucesso → `uow.SaveChangesAsync`, e se este falhar devolve o erro dele.
- `UnitOfWork.SaveChangesAsync`:
  ```
  repetir até 5 rodadas:
      eventos = agregados rastreados → DomainEvents; limpar eventos
      se vazio: sair
      r = dispatcher.Dispatch(eventos); se falha: DiscardAsync; retornar r
  se ainda houver eventos após 5 rodadas: lançar InvalidOperationException
  try SaveChangesAsync do DbContext
  catch DbUpdateConcurrencyException → DiscardAsync; retornar CommonErrors.ConcurrencyConflict
  catch qualquer outra → DiscardAsync; relançar
  rodar AfterCommit (cada uma em try/catch com log); retornar Success
  ```
- `GarageDbContext`: `UseNpgsql(ConnectionStrings:Garage).UseSnakeCaseNamingConvention()`; `ApplyConfigurationsFromAssembly`; `Query<T>() => Set<T>().AsNoTracking()`.
- `AppClock(TimeProvider, IOptions<AppOptions>)`: `Today` = data de `TimeZoneInfo.ConvertTime(time.GetUtcNow(), tz)`; `AppOptions.TimeZone` padrão `"America/Sao_Paulo"`. Infrastructure registra `TimeProvider.System` com `TryAddSingleton`.
- `Program.cs` (nesta task): Serilog (console JSON compacto, `UseSerilogRequestLogging`), `AddProblemDetails` com `traceId`, `AddExceptionHandler<GlobalExceptionHandler>`, JSON com enum camelCase, `AddApplication`, `AddInfrastructure`, `AddOpenApi`, health checks (`AddDbContextCheck<GarageDbContext>`). Rotas: `/api/health` (sem checks) e `/api/health/ready` (todos), ambas anônimas; `MapOpenApi()` + `MapScalarApiReference()` fora de Production.
- Pacotes: Application → `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `Scrutor`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Logging.Abstractions`; Infrastructure → `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (+ `<FrameworkReference Include="Microsoft.AspNetCore.App" />`); Api → `Serilog.AspNetCore`, `Serilog.Formatting.Compact`, `Scalar.AspNetCore`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`; testes → `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Microsoft.Extensions.TimeProvider.Testing`.
- `GarageApiFactory`: container `postgres:18`; ambiente `Testing`; settings `ConnectionStrings:Garage`, `Auth:AllowRegistration=true`, `Storage:RootPath=<temp>`, `RateLimits:*=10000`; troca `TimeProvider` por `Clock`; `InitializeAsync` aplica `Database.MigrateAsync()`. Testes que dependem de horário definem `Clock.SetUtcNow(...)` no início. Todas as classes de teste de integração usam `[Collection("api")]` (execução sequencial).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// HostTests
[Fact] public async Task Health_ready_returns_200_when_database_is_up()
[Fact] public async Task OpenApi_document_is_served()                     // GET /openapi/v1.json → 200, corpo contém "\"openapi\""
[Fact] public async Task Exception_handler_hides_details()                // GlobalExceptionHandler.TryHandleAsync com DefaultHttpContext e
                                                                           // new InvalidOperationException("segredo") → 500, corpo sem "segredo" e sem "StackTrace", com "traceId"
// AppClockTests
[Theory] [InlineData("2026-10-11T02:30:00Z","2026-10-10")] [InlineData("2026-10-11T03:00:00Z","2026-10-11")]
public void Today_uses_sao_paulo(string utcNow, string expected)
// HandlerConventionTests: todo tipo do assembly Application que implementa ICommandHandler<,>, IQueryHandler<,> ou IDomainEventHandler<> é sealed e não público
[Fact] public void Handlers_and_decorators_are_internal_sealed()
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test` → FAIL (tipos e Program incompletos).
- [ ] **Step 3: Implementar as abstrações da Application e o `AddApplication`**
- [ ] **Step 4: Implementar `GarageDbContext`, `UnitOfWork`, `AppUser`, `CurrentUser` (claim `NameIdentifier`), `AppClock` e `AddInfrastructure`**
- [ ] **Step 5: Implementar `Program.cs`, `IEndpoint`/`MapEndpoints`, `ToProblem`, `GlobalExceptionHandler` e appsettings** (`appsettings.Development.json`: `Host=localhost;Port=5432;Database=garage;Username=garage;Password=garage_dev`)
- [ ] **Step 6: Criar a migration inicial**

Run: `dotnet ef migrations add Initial -p src/Garage.Infrastructure -s src/Garage.Api -o Persistence/Migrations`
Expected: migration com as tabelas do Identity em snake_case (`asp_net_users` etc.).

- [ ] **Step 7: Rodar e ver passar** — `dotnet test` → PASS (todas as suítes).
- [ ] **Step 8: Commit** — `git commit -m "feat: pipeline CQRS, unit of work com eventos e host da API"`

---

### Task 8: Autenticação (Identity, cookie, antiforgery, rate limit, reset de senha)

**Files:**
- Create: `src/Garage.Api/Auth/{AuthSetup.cs, AuthRequests.cs, AuthConfigEndpoint.cs, AntiforgeryTokenEndpoint.cs, RegisterEndpoint.cs, LoginEndpoint.cs, LogoutEndpoint.cs, MeEndpoint.cs}`, `src/Garage.Api/Http/{ApiErrors.cs, AntiforgeryValidationMiddleware.cs, SecurityHeadersMiddleware.cs, RateLimitingSetup.cs, RateLimitOptions.cs}`, `src/Garage.Api/Cli/ResetPasswordCli.cs`, `src/Garage.Infrastructure/Identity/PasswordResetService.cs`
- Modify: `src/Garage.Api/Program.cs`
- Test: `tests/Garage.Api.IntegrationTests/Infrastructure/TestClients.cs`, `tests/Garage.Api.IntegrationTests/Auth/AuthTests.cs`

**Interfaces:**
- Consumes: Task 7 (`GarageDbContext`, `AppUser`, `ToProblem`, `IEndpoint`).
- Produces:
```
GET  /api/auth/config        anônimo → 200 { registrationEnabled: bool }
GET  /api/auth/antiforgery   anônimo → 204 + cookie XSRF-TOKEN (não HttpOnly, Secure, SameSite=Strict, Path=/)
POST /api/auth/register      anônimo, só mapeado se Auth:AllowRegistration=true; { email, password } → 201 e já autenticado
POST /api/auth/login         anônimo, rate limit "login"; { email, password } → 204
POST /api/auth/logout        → 204
GET  /api/auth/me            → 200 { id, email } | 401
```
```csharp
public static class ApiErrors
{   InvalidCredentials /* auth.invalid_credentials, Unauthorized, "E-mail ou senha inválidos." */, LockedOut /* auth.locked_out, Unauthorized, "Conta bloqueada por 15 minutos após várias tentativas." */,
    AntiforgeryInvalid /* auth.antiforgery_invalid, Validation→400, "Sessão expirada. Recarregue a página." */,
    RateLimitExceeded /* rate_limit.exceeded, TooManyRequests, "Muitas tentativas. Aguarde um minuto." */ }
public sealed class RateLimitOptions { int LoginPerMinute = 5; int PublicPerMinute = 60; int CatalogPerMinute = 30; int UploadsPerMinute = 30; } // seção "RateLimits"
// políticas: "login" e "public" particionadas por IP; "catalog" e "uploads" por usuário (NameIdentifier)
public sealed class PasswordResetService { public Task<string?> ResetAsync(string email, CancellationToken ct); } // null se não existe; senha temporária = 16 chars base64url; destrava a conta
// testes
// extensões sobre WebApplicationFactory<Program> para funcionar também nas factories derivadas por WithWebHostBuilder
public static Task<HttpClient> CreateUserClientAsync(this WebApplicationFactory<Program> f, string? email = null); // registra, autentica e configura X-XSRF-TOKEN já vinculado ao usuário
public static Task<HttpClient> CreateAnonymousClientAsync(this WebApplicationFactory<Program> f);                  // com X-XSRF-TOKEN anônimo
// clientes: HandleCookies = true, BaseAddress = https://localhost (cookies Secure)
```

Detalhes:
- Identity: `AddIdentityCore<AppUser>` + `AddEntityFrameworkStores<GarageDbContext>` + `AddSignInManager` + `AddDefaultTokenProviders`. Senha: `RequiredLength=10`, sem regras de dígito, maiúscula, minúscula ou símbolo. `User.RequireUniqueEmail=true` (username = e-mail). Lockout: 5 tentativas, 15 min. Novo usuário com `Id = Guid.CreateVersion7()`.
- Cookie (`AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`): nome `garage_auth`, `HttpOnly`, `SecurePolicy=Always`, `SameSite=Strict`, 14 dias com renovação deslizante. `OnRedirectToLogin` → 401 e `OnRedirectToAccessDenied` → 403, sem redirecionar.
- Erros do Identity no registro viram `Error.Validation`: `PasswordTooShort` → `password`: "A senha deve ter pelo menos 10 caracteres."; `DuplicateUserName`/`DuplicateEmail` → `email`: "E-mail já cadastrado."; `InvalidEmail`/`InvalidUserName` → `email`: "E-mail inválido."; outros → `password`: "Senha inválida."
- `AntiforgeryValidationMiddleware`: para POST, PUT, PATCH e DELETE em `/api/*`, chama `IAntiforgery.ValidateRequestAsync`. Falha → `ApiErrors.AntiforgeryInvalid.ToProblem()`. Header: `X-XSRF-TOKEN`. O token é vinculado ao usuário, então o cliente busca `/auth/antiforgery` de novo depois de login ou registro.
- `SecurityHeadersMiddleware`: `X-Content-Type-Options: nosniff` em todas as respostas.
- Ordem no `Program`: `UseForwardedHeaders` (XFor|XProto; `KnownNetworks`/`KnownProxies` limpos, porque só o Caddy alcança a API) → `UseExceptionHandler` → `UseSerilogRequestLogging` → `SecurityHeadersMiddleware` → `UseAuthentication` → `UseAuthorization` → `AntiforgeryValidationMiddleware` → `UseRateLimiter` → endpoints. `OnRejected` do rate limiter → `ApiErrors.RateLimitExceeded`.
- CLI: se `args is ["reset-password", var email]`, o `Program` constrói o app, resolve `PasswordResetService`, imprime `"Senha temporária: {senha}"` ou `"Usuário não encontrado."` e encerra sem subir o servidor.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] public async Task Config_reports_registration_enabled()
[Fact] public async Task Register_then_me_returns_user()
[Fact] public async Task Register_with_short_password_returns_400_with_password_error()      // 9 chars → errors.password
[Fact] public async Task Register_duplicate_email_returns_400_with_email_error()
[Fact] public async Task Login_with_wrong_password_returns_401_invalid_credentials()
[Fact] public async Task Five_wrong_passwords_lock_the_account()                            // 6ª tentativa, mesmo com senha certa → 401 "auth.locked_out"
[Fact] public async Task Mutating_request_without_antiforgery_header_returns_400()           // POST /api/auth/logout sem header → "auth.antiforgery_invalid"
[Fact] public async Task Me_without_cookie_returns_401_not_redirect()
[Fact] public async Task Auth_cookie_is_httponly_secure_strict()                             // Set-Cookie de garage_auth contém httponly, secure, samesite=strict
[Fact] public async Task Register_is_404_when_registration_disabled()                        // factory.WithWebHostBuilder(Auth:AllowRegistration=false)
[Fact] public async Task Login_is_rate_limited()                                             // WithWebHostBuilder(RateLimits:LoginPerMinute=2): 3ª → 429 "rate_limit.exceeded"
[Fact] public async Task Password_reset_issues_working_temporary_password_and_unlocks()      // bloquear conta, ResetAsync, login com a temporária → 204
[Fact] public async Task Every_api_endpoint_requires_authorization_except_allowlist()
// percorre EndpointDataSource: rotas /api/* sem IAuthorizeData ou com IAllowAnonymous devem estar em
// { /api/health, /api/health/ready, /api/auth/config, /api/auth/antiforgery, /api/auth/register, /api/auth/login, /api/public/* }
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test tests/Garage.Api.IntegrationTests --filter AuthTests` → FAIL.
- [ ] **Step 3: Implementar `AuthSetup`, endpoints, middlewares, rate limiting, `PasswordResetService` e CLI**
- [ ] **Step 4: Rodar e ver passar** → PASS; e `dotnet test` inteiro continua PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(auth): Identity com cookie, antiforgery, rate limit e reset de senha"`

---

### Task 9: Veículos (pipeline ponta a ponta, acesso e concorrência)

**Files:**
- Create: `src/Garage.Application/Vehicles/{VehicleDtos.cs, VehicleInputValidator.cs, CreateVehicle/*, UpdateVehicle/*, DeleteVehicle/*, UpdateOdometer/*, GetVehicles/*, GetVehicle/*}`, `src/Garage.Application/Abstractions/Ports/IVehicleRepository.cs`
- Create: `src/Garage.Infrastructure/Persistence/Configurations/VehicleConfiguration.cs`, `Persistence/Repositories/VehicleRepository.cs`, `Access/VehicleAccess.cs`, migration `AddVehicles`
- Create: `src/Garage.Api/Vehicles/{VehicleRequests.cs, CreateVehicleEndpoint.cs, ListVehiclesEndpoint.cs, GetVehicleEndpoint.cs, UpdateVehicleEndpoint.cs, DeleteVehicleEndpoint.cs, UpdateOdometerEndpoint.cs}`
- Test: `tests/Garage.Api.IntegrationTests/Infrastructure/TestData.cs`, `tests/Garage.Api.IntegrationTests/Vehicles/VehicleTests.cs`

**Interfaces:**
- Consumes: Task 3 (`Vehicle`, `VehicleData`, `VehicleErrors`), Task 7 (pipeline, portas, `ToProblem`), Task 8 (`CreateUserClientAsync`).
- Produces:
```csharp
public sealed record CatalogRefInput(VehicleType Type, string FipeCode, string FipeYearCode);
public sealed record VehicleInput(VehicleType Type, string Make, string Model, string? Version, string? Generation, int ManufactureYear, int ModelYear,
    Fuel Fuel, string? Nickname, string? Plate, string? Vin, string? Notes, CatalogRefInput? Catalog);
public sealed record CreateVehicleCommand(VehicleInput Vehicle, int CurrentOdometer) : ICommand<Guid>;
public sealed record UpdateVehicleCommand(Guid VehicleId, VehicleInput Vehicle, uint RowVersion) : ICommand<Unit>;
public sealed record DeleteVehicleCommand(Guid VehicleId) : ICommand<Unit>;
public sealed record UpdateOdometerCommand(Guid VehicleId, int Km) : ICommand<Unit>;
public sealed record GetVehiclesQuery : IQuery<IReadOnlyList<VehicleSummaryDto>>;
public sealed record GetVehicleQuery(Guid VehicleId) : IQuery<VehicleDto>;
public sealed record VehicleSummaryDto(Guid Id, string DisplayName, string Make, string Model, int ModelYear, string? Plate, int CurrentOdometer);
public sealed record VehicleDto(Guid Id, VehicleType Type, string Make, string Model, string? Version, string? Generation, int ManufactureYear, int ModelYear,
    Fuel Fuel, string? Nickname, string? Plate, string? Vin, string? Notes, int CurrentOdometer, DateTimeOffset OdometerUpdatedAt, CatalogRefInput? Catalog, uint RowVersion);
public interface IVehicleRepository
{   Task<Vehicle?> GetAsync(Guid id, CancellationToken ct); void Add(Vehicle v); void Remove(Vehicle v); void SetExpectedVersion(Vehicle v, uint rowVersion); }
```
```
POST   /api/vehicles                    body = VehicleInput + currentOdometer (plano)  → 201 CreatedIdResponse, Location /api/vehicles/{id}
GET    /api/vehicles                    → 200 VehicleSummaryDto[]
GET    /api/vehicles/{vehicleId}        → 200 VehicleDto
PUT    /api/vehicles/{vehicleId}        body = VehicleInput + rowVersion → 204
DELETE /api/vehicles/{vehicleId}        → 204
POST   /api/vehicles/{vehicleId}/odometer   { km } → 204
```
- `VehicleInputValidator` (recebe `IAppClock`): `Make`/`Model` obrigatórios, ≤ 60; `Version` ≤ 120; `Generation` ≤ 30; `Nickname` ≤ 60; `Vin` ≤ 17; `Notes` ≤ 2000; `Plate` → `Plate.IsValid` ("Placa inválida."); anos → `VehicleYear.IsValid(..., clock.CurrentYear)`, erro na chave `modelYear` ("Ano inválido."); `CurrentOdometer`/`Km` → `Odometer.IsValid`; enums `IsInEnum`; `Catalog.FipeCode` e `FipeYearCode` obrigatórios, ≤ 20, quando `Catalog` existe.
- `VehicleConfiguration`: `Year` como `ComplexProperty` (`manufacture_year`, `model_year`); `Plate` e `Odometer` com value converter; `Catalog` com `OwnsOne` opcional (`catalog_type`, `catalog_fipe_code`, `catalog_fipe_year_code`); shadow `RowVersion` (`.IsRowVersion()`); índice `owner_id`; FK `owner_id` → `asp_net_users` com cascade.
- `VehicleAccess`: `AnyAsync(v => v.Id == vehicleId && v.OwnerId == userId)` para ler e para editar.
- Consultas projetam `EF.Property<uint>(v, "RowVersion")`.
- `DeleteVehicleHandler` remove o agregado (a limpeza de arquivos entra na Task 12).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// TestData.Vehicle(int currentOdometer = 150_000, object? catalog = null) → objeto do request:
// car, Honda, Civic, "Sedan LX 1.6 16V Mec. 4p", "EK", 1997, 1998, gasoline, nickname null, plate "abc-1234", vin "1HGEJ6678WL000001", notes "segredo-notas"
[Fact] public async Task Create_and_get_roundtrip()                     // GET devolve plate "ABC1234", currentOdometer 150000, generation "EK", rowVersion > 0
[Fact] public async Task Create_with_invalid_fields_returns_400_with_field_errors() // make "", plate "XX", 1997/2000 → errors com chaves make, plate, modelYear
[Fact] public async Task List_returns_only_my_vehicles()
[Fact] public async Task Other_user_gets_404_on_get_update_delete_and_odometer()   // code "vehicle.not_found" e traceId no corpo
[Fact] public async Task Update_with_stale_row_version_returns_409()                // 2 PUTs com o mesmo rowVersion → 2º = "concurrency.conflict"
[Fact] public async Task UpdateOdometer_lower_returns_422()                         // "vehicle.odometer_cannot_decrease"
[Fact] public async Task Delete_then_get_returns_404()
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test tests/Garage.Api.IntegrationTests --filter VehicleTests` → FAIL.
- [ ] **Step 3: Implementar Application (casos de uso, validator, DTOs)**
- [ ] **Step 4: Implementar Infrastructure (configuração, repositório, `VehicleAccess`) e criar a migration**

Run: `dotnet ef migrations add AddVehicles -p src/Garage.Infrastructure -s src/Garage.Api -o Persistence/Migrations`

- [ ] **Step 5: Implementar endpoints**
- [ ] **Step 6: Rodar e ver passar** — `dotnet test` → PASS.
- [ ] **Step 7: Commit** — `git commit -m "feat(vehicles): CRUD de veículos com controle de acesso e concorrência"`

---

### Task 10: Lembretes (M1–M6, painel)

**Files:**
- Create: `src/Garage.Application/Reminders/{ReminderDtos.cs, ReminderInputValidator.cs, CreateReminder/*, UpdateReminder/*, DeleteReminder/*, GetVehicleReminders/*, GetDueReminders/*}`, `Abstractions/Ports/IReminderRepository.cs`
- Create: `src/Garage.Infrastructure/Persistence/Configurations/ReminderConfiguration.cs`, `Repositories/ReminderRepository.cs`, migration `AddReminders`
- Create: `src/Garage.Api/Reminders/{ReminderRequests.cs, ListRemindersEndpoint.cs, CreateReminderEndpoint.cs, UpdateReminderEndpoint.cs, DeleteReminderEndpoint.cs, DueRemindersEndpoint.cs}`
- Test: `tests/Garage.Api.IntegrationTests/Reminders/ReminderTests.cs`

**Interfaces:**
- Consumes: Task 5 (`Reminder`, `ReminderSchedule`, `ReminderErrors`), Task 9 (`IVehicleRepository`, `IVehicleAccess`, `TestData.Vehicle`).
- Produces:
```csharp
public sealed record ReminderInput(string Title, int? IntervalKm, int? IntervalMonths, DateOnly LastDoneDate, int? LastDoneOdometer);
public sealed record CreateReminderCommand(Guid VehicleId, ReminderInput Reminder) : ICommand<Guid>;
public sealed record UpdateReminderCommand(Guid VehicleId, Guid ReminderId, ReminderInput Reminder, uint RowVersion) : ICommand<Unit>;
public sealed record DeleteReminderCommand(Guid VehicleId, Guid ReminderId) : ICommand<Unit>;
public sealed record GetVehicleRemindersQuery(Guid VehicleId) : IQuery<IReadOnlyList<ReminderDto>>;
public sealed record GetDueRemindersQuery : IQuery<IReadOnlyList<DueReminderDto>>;
public sealed record ReminderStatusDto(ReminderState State, int? KmRemaining, int? DaysRemaining, DateTimeOffset OdometerUpdatedAt);
public sealed record ReminderDto(Guid Id, string Title, int? IntervalKm, int? IntervalMonths, DateOnly LastDoneDate, int? LastDoneOdometer, ReminderStatusDto Status, uint RowVersion);
public sealed record DueReminderDto(Guid VehicleId, string VehicleName, Guid ReminderId, string Title, ReminderStatusDto Status);
public interface IReminderRepository
{   Task<Reminder?> GetAsync(Guid vehicleId, Guid reminderId, CancellationToken ct);
    Task<IReadOnlyList<Reminder>> GetManyAsync(Guid vehicleId, IReadOnlyCollection<Guid> ids, CancellationToken ct); // só do veículo informado
    void Add(Reminder r); void Remove(Reminder r); void SetExpectedVersion(Reminder r, uint rowVersion); }
```
```
GET    /api/vehicles/{vehicleId}/reminders               → 200 ReminderDto[]
POST   /api/vehicles/{vehicleId}/reminders               body = ReminderInput → 201 CreatedIdResponse
PUT    /api/vehicles/{vehicleId}/reminders/{reminderId}  body = ReminderInput + rowVersion → 204
DELETE /api/vehicles/{vehicleId}/reminders/{reminderId}  → 204
GET    /api/reminders/due                                → 200 DueReminderDto[] (só DueSoon/Overdue dos veículos do usuário)
```
- Validator: `Title` obrigatório, ≤ 120; `IntervalKm` 1..1.000.000; `IntervalMonths` 1..240; `LastDoneOdometer` obrigatório quando houver `IntervalKm` ("Informe a km da última vez para lembretes por km."); `Odometer.IsValid`.
- Create/Update: acesso → veículo → `Reminder.Create/Update(..., clock.Today)` → com `LastDoneOdometer`, `vehicle.RegisterOdometer` (M6).
- Status nas consultas: projetar os campos e calcular com `ReminderSchedule.Evaluate` em memória. Painel ordenado por Overdue primeiro, depois `DaysRemaining ?? int.MaxValue`, depois `KmRemaining ?? int.MaxValue`.
- FK `vehicle_id` com cascade; índice `vehicle_id`; shadow `RowVersion`.

- [ ] **Step 1: Escrever os testes que falham** (cada teste começa com `Clock.SetUtcNow(2026-10-10T15:00Z)`)

```csharp
[Fact] public async Task Create_and_list_with_computed_status()          // veículo 84_500 km; lembrete 5000 km, base 80_000 → dueSoon, kmRemaining 500
[Fact] public async Task Create_without_interval_returns_422()           // "reminder.interval_required"
[Fact] public async Task Km_interval_without_base_km_returns_400()       // errors.lastDoneOdometer
[Fact] public async Task Base_date_in_future_returns_422()               // base 2026-10-11 → "reminder.base_date_in_future"
[Fact] public async Task Base_km_above_current_updates_vehicle_odometer() // veículo 80_000, base 82_000 → GET veículo currentOdometer 82000
[Fact] public async Task Due_lists_overdue_first_and_only_my_vehicles()   // meus: 1 vencido, 1 próximo, 1 em dia; outro usuário: 1 vencido → 2 itens, vencido primeiro
[Fact] public async Task Update_with_stale_row_version_returns_409()
[Fact] public async Task Other_user_gets_404()
```

- [ ] **Step 2: Rodar e ver falhar** — `--filter ReminderTests` → FAIL.
- [ ] **Step 3: Implementar Application, Infrastructure (+ `dotnet ef migrations add AddReminders ...`) e endpoints**
- [ ] **Step 4: Rodar e ver passar** — `dotnet test` → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(reminders): lembretes com status calculado e painel"`

---

### Task 11: Registros (R1–R6, paginação, cumprimento de lembretes, correção de km)

**Files:**
- Create: `src/Garage.Application/Records/{RecordDtos.cs, RecordInputValidator.cs, RecordCursor.cs, FulfillRemindersOnRecordAdded.cs, AddServiceRecord/*, UpdateServiceRecord/*, DeleteServiceRecord/*, GetServiceRecords/*, GetServiceRecord/*}`, `src/Garage.Application/Vehicles/CorrectOdometer/*`, `Abstractions/Ports/IServiceRecordRepository.cs`
- Create: `src/Garage.Infrastructure/Persistence/Configurations/ServiceRecordConfiguration.cs`, `Repositories/ServiceRecordRepository.cs`, migration `AddServiceRecords`
- Create: `src/Garage.Api/Records/{RecordRequests.cs, ListRecordsEndpoint.cs, GetRecordEndpoint.cs, AddRecordEndpoint.cs, UpdateRecordEndpoint.cs, DeleteRecordEndpoint.cs}`, `src/Garage.Api/Vehicles/CorrectOdometerEndpoint.cs`
- Test: `tests/Garage.Api.IntegrationTests/Records/RecordTests.cs`

**Interfaces:**
- Consumes: Task 4 (`ServiceRecord`, `RecordDetails`, `OdometerBounds`, `ServiceRecordAdded`), Task 5 (`Reminder.MarkDone`), Task 9, Task 10 (`IReminderRepository.GetManyAsync`).
- Produces:
```csharp
public sealed record PartInput(string Name, string? Brand, string? PartNumber, decimal Quantity, PartUnit Unit, decimal? UnitPrice);
public sealed record RecordInput(RecordType Type, DateOnly Date, int? Odometer, string Title, string? Description, Category Category,
    string? PerformedBy, decimal? LaborCost, string? ReplacedOriginalPart, IReadOnlyList<PartInput> Parts);
public sealed record AddServiceRecordCommand(Guid VehicleId, RecordInput Record, IReadOnlyList<Guid> FulfilledReminderIds) : ICommand<Guid>;
public sealed record UpdateServiceRecordCommand(Guid VehicleId, Guid RecordId, RecordInput Record, uint RowVersion) : ICommand<Unit>;
public sealed record DeleteServiceRecordCommand(Guid VehicleId, Guid RecordId) : ICommand<Unit>;
public sealed record CorrectOdometerCommand(Guid VehicleId, int Km) : ICommand<Unit>;
public sealed record GetServiceRecordsQuery(Guid VehicleId, RecordType? Type, Category? Category, string? Cursor, int PageSize = 30) : IQuery<PagedResult<RecordListItemDto>>;
public sealed record GetServiceRecordQuery(Guid VehicleId, Guid RecordId) : IQuery<RecordDto>;
public sealed record RecordListItemDto(Guid Id, RecordType Type, DateOnly Date, int? Odometer, string Title, Category Category, decimal TotalCost);
public sealed record PartDto(string Name, string? Brand, string? PartNumber, decimal Quantity, PartUnit Unit, decimal? UnitPrice, decimal LineTotal);
public sealed record RecordDto(Guid Id, RecordType Type, DateOnly Date, int? Odometer, string Title, string? Description, Category Category, string? PerformedBy,
    decimal? LaborCost, string? ReplacedOriginalPart, IReadOnlyList<PartDto> Parts, decimal TotalCost, uint RowVersion);
public static class RecordCursor
{   public static string Encode(DateOnly date, DateTimeOffset createdAt, Guid id);   // base64url de "yyyy-MM-dd|O|guid"
    public static bool TryDecode(string? cursor, out (DateOnly Date, DateTimeOffset CreatedAt, Guid Id) value); }
public interface IServiceRecordRepository
{   Task<ServiceRecord?> GetAsync(Guid vehicleId, Guid recordId, CancellationToken ct);
    Task<OdometerBounds> GetOdometerBoundsAsync(Guid vehicleId, DateOnly date, Guid? excludeRecordId, CancellationToken ct);
    Task<int?> GetMaxOdometerAsync(Guid vehicleId, CancellationToken ct);
    void Add(ServiceRecord r); void Remove(ServiceRecord r); void SetExpectedVersion(ServiceRecord r, uint rowVersion); }
internal sealed class FulfillRemindersOnRecordAdded : IDomainEventHandler<ServiceRecordAdded>;
```
```
GET    /api/vehicles/{vehicleId}/records?type&category&cursor&pageSize → 200 PagedResult<RecordListItemDto>
POST   /api/vehicles/{vehicleId}/records       body = RecordInput + fulfilledReminderIds → 201 CreatedIdResponse
GET    /api/vehicles/{vehicleId}/records/{recordId}  → 200 RecordDto
PUT    /api/vehicles/{vehicleId}/records/{recordId}  body = RecordInput + rowVersion → 204
DELETE /api/vehicles/{vehicleId}/records/{recordId}  → 204
POST   /api/vehicles/{vehicleId}/odometer/correction { km } → 204
```
- Validator: `Title` obrigatório, ≤ 120; `Description` ≤ 4000; `PerformedBy` ≤ 120; `ReplacedOriginalPart` ≤ 500; até 50 `Parts`, cada uma com `Name` obrigatório (≤ 120), `Brand` ≤ 60, `PartNumber` ≤ 60, `Quantity` > 0 com até 3 casas e ≤ 99.999,999, `UnitPrice` válido por `Money.IsValid`; `LaborCost` válido por `Money.IsValid`; `Odometer.IsValid`; até 20 `FulfilledReminderIds`; na query, `PageSize` 1..100 e `Cursor` decodificável por `RecordCursor.TryDecode` (senão erro em `cursor`: "Cursor inválido.").
- `GetOdometerBoundsAsync`: `MaxKmBefore` = maior km com `date < d`; `MinKmAfter` = menor km com `date > d`. Ignora registros sem km e o `excludeRecordId`.
- Add: acesso → veículo → limites → `ServiceRecord.Create(..., clock.Today, clock.Now)` → com km, `vehicle.RegisterOdometer` → `Add`. Update: acesso → registro (filtrado por veículo) → `SetExpectedVersion` → limites excluindo ele mesmo → `Update` → `RegisterOdometer`. Delete: só remove (V4). CorrectOdometer: acesso → veículo → `GetMaxOdometerAsync` → `vehicle.CorrectOdometer`.
- `FulfillRemindersOnRecordAdded`: lista vazia → sucesso. Caso contrário, `GetManyAsync(e.VehicleId, ids)`; se a quantidade encontrada ≠ quantidade de ids distintos → `ReminderErrors.NotFound`. Depois `MarkDone(e.Date, e.Odometer)` em cada um, parando na primeira falha.
- Listagem: ordem `date DESC, created_at DESC, id DESC`, busca `pageSize + 1` itens. O predicado do cursor `(d, c, i)` é `date < d OR (date = d AND (created_at < c OR (created_at = c AND id < i)))`.
- Configuração: `Parts` como coleção owned na tabela `service_record_parts`; colunas de dinheiro `numeric(12,2)`; quantidade `numeric(8,3)`; índice `(vehicle_id, date DESC, id DESC)`; FK do veículo com cascade; shadow `RowVersion`.

- [ ] **Step 1: Escrever os testes que falham** (relógio em 2026-10-10T15:00Z, salvo indicação)

```csharp
[Fact] public async Task Add_record_with_parts_returns_201_and_detail_has_totals()     // 2 × 45,90 L + 1 × 120,00 + mão de obra 80,00 → totalCost 291.80, lineTotal 91.80
[Fact] public async Task Add_record_with_higher_km_updates_vehicle_odometer()
[Fact] public async Task Add_record_out_of_order_returns_422_with_bounds()            // 2026-01-10@80_000 e 2026-06-10@90_000; novo 2026-03-10@95_000 → maxKmBefore 80000, minKmAfter 90000
[Fact] public async Task Add_old_record_without_km_is_accepted()
[Fact] public async Task Add_record_fulfilling_reminder_moves_its_base()              // lembrete base 2026-01-01/75_000; registro 2026-10-01@85_000 → base 2026-10-01/85000
[Fact] public async Task Fulfilling_km_reminder_without_record_km_returns_422_and_creates_nothing() // "reminder.fulfillment_requires_odometer"; lista de registros vazia
[Fact] public async Task AddRecord_with_reminder_of_other_vehicle_returns_404_and_changes_nothing()  // Review Focus 1: lembrete do veículo de outro usuário → "reminder.not_found"; sem registros; lembrete alheio intacto
[Fact] public async Task Record_dated_local_today_late_night_is_accepted_and_local_tomorrow_rejected() // Review Focus 2: relógio 2026-10-11T02:30Z; 2026-10-10 → 201; 2026-10-11 → "record.date_in_future"
[Fact] public async Task Moving_record_date_revalidates_bounds_excluding_itself()     // Review Focus 5: A 01-10@80k, B 06-10@90k, C 08-10@95k; B → 09-10@90k = 422 (maxKmBefore 95000); B só muda o título = 204
[Fact] public async Task Deleting_highest_km_record_keeps_current_km_and_correction_fixes_typo() // Review Focus 5: registros @15_200 e @150_000; apagar o 150k → veículo continua 150000; corrigir p/ 14_000 → 422 "vehicle.odometer_below_records"; p/ 15_200 → 204
[Fact] public async Task Invalid_cursor_returns_400_validation()                     // Review Focus 4: cursor "@@@" → errors.cursor
[Fact] public async Task Pagination_returns_every_record_once_in_date_desc_order()   // 5 registros (2 na mesma data), pageSize 2 → 3 páginas; ordem esperada; última nextCursor null
[Fact] public async Task Filters_by_type_and_category()
[Fact] public async Task Replaced_part_on_maintenance_returns_422()
[Fact] public async Task Title_longer_than_120_returns_400()
[Fact] public async Task Update_with_stale_row_version_returns_409()
[Fact] public async Task Other_user_gets_404_on_records_and_correction()
```

- [ ] **Step 2: Rodar e ver falhar** — `--filter RecordTests` → FAIL.
- [ ] **Step 3: Implementar Application (casos de uso, validator, cursor, handler do evento, `CorrectOdometer`)**
- [ ] **Step 4: Implementar Infrastructure** (+ `dotnet ef migrations add AddServiceRecords ...`)
- [ ] **Step 5: Implementar endpoints**
- [ ] **Step 6: Rodar e ver passar** — `dotnet test` → PASS.
- [ ] **Step 7: Commit** — `git commit -m "feat(records): registros com peças, regra de km, paginação e cumprimento de lembretes"`

---

### Task 12: Anexos (upload, EXIF, limites e limpeza de arquivos)

**Files:**
- Create: `src/Garage.Application/Attachments/{AttachmentDtos.cs, UploadAttachment/*, GetAttachmentContent/*, SetAttachmentVisibility/*, DeleteAttachment/*}`, `Abstractions/Ports/{IFileStorage,IImageProcessor,IAttachmentRepository}.cs`
- Create: `src/Garage.Infrastructure/Storage/{StorageOptions,LocalDiskFileStorage,MagickImageProcessor}.cs`, `Persistence/Configurations/AttachmentConfiguration.cs`, `Repositories/AttachmentRepository.cs`, migration `AddAttachments`
- Create: `src/Garage.Api/Attachments/{UploadAttachmentEndpoint,GetAttachmentContentEndpoint,SetAttachmentVisibilityEndpoint,DeleteAttachmentEndpoint}.cs`
- Modify: `src/Garage.Application/Records/RecordDtos.cs` (+`AttachmentCount` em `RecordListItemDto`, +`Attachments` em `RecordDto`) e as consultas de registros; `DeleteVehicleHandler` e `DeleteServiceRecordHandler` (limpeza de arquivos)
- Test: `tests/Garage.Api.IntegrationTests/Infrastructure/TestImages.cs`, `tests/Garage.Api.IntegrationTests/Attachments/AttachmentTests.cs`, `tests/Garage.Infrastructure.Tests/LocalDiskFileStorageTests.cs`

**Interfaces:**
- Consumes: Task 6 (`Attachment`, `FileSignature`, `AttachmentErrors`), Task 7 (`IUnitOfWork.AfterCommit/OnRollback`), Task 9, Task 11.
- Produces:
```csharp
public interface IFileStorage
{   Task SaveAsync(string key, Stream content, CancellationToken ct); Task<Stream?> OpenReadAsync(string key, CancellationToken ct); Task DeleteAsync(string key, CancellationToken ct); }
public sealed record ProcessedFile(Stream Content, string ContentType, long Length);
public interface IImageProcessor { Task<ProcessedFile> NormalizeAsync(Stream input, DetectedFileType type, CancellationToken ct); }
public sealed record AttachmentDto(Guid Id, AttachmentKind Kind, bool IsPublic, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);
public sealed record FileContent(Stream Content, string ContentType, string FileName);
public sealed record UploadAttachmentCommand(Guid VehicleId, Guid RecordId, Stream Content, long Length, string FileName, AttachmentKind Kind) : ICommand<AttachmentDto>;
public sealed record GetAttachmentContentQuery(Guid VehicleId, Guid AttachmentId) : IQuery<FileContent>;
public sealed record SetAttachmentVisibilityCommand(Guid VehicleId, Guid AttachmentId, bool IsPublic) : ICommand<Unit>;
public sealed record DeleteAttachmentCommand(Guid VehicleId, Guid AttachmentId) : ICommand<Unit>;
public interface IAttachmentRepository
{   Task<Attachment?> GetAsync(Guid vehicleId, Guid attachmentId, CancellationToken ct); Task<int> CountByRecordAsync(Guid recordId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetStorageKeysByVehicleAsync(Guid vehicleId, CancellationToken ct);
    Task<IReadOnlyList<string>> GetStorageKeysByRecordAsync(Guid recordId, CancellationToken ct);
    void Add(Attachment a); void Remove(Attachment a); }
// RecordListItemDto ganha int AttachmentCount (último parâmetro); RecordDto ganha IReadOnlyList<AttachmentDto> Attachments (antes de RowVersion)
```
```
POST   /api/vehicles/{vehicleId}/records/{recordId}/attachments   multipart: file + kind → 201 AttachmentDto   (rate limit "uploads"; .DisableAntiforgery(), porque o middleware global já valida)
GET    /api/vehicles/{vehicleId}/attachments/{attachmentId}/content → stream (imagem inline; PDF como anexo, com nome do arquivo)
PATCH  /api/vehicles/{vehicleId}/attachments/{attachmentId}         { isPublic } → 204
DELETE /api/vehicles/{vehicleId}/attachments/{attachmentId}         → 204
```
- Upload, nesta ordem: acesso → registro do veículo (`record.not_found`) → `Length > MaxSizeBytes` → `TooLarge` → `CountByRecordAsync ≥ 20` → `LimitReached` → copiar para `MemoryStream` → `FileSignature.Detect` nulo → `UnsupportedType` → imagem passa por `NormalizeAsync` (PDF segue como veio) → `Attachment.Create` → `storage.SaveAsync(StorageKey)` → `uow.OnRollback(apagar o arquivo)` → `Add`.
- `MagickImageProcessor`: `AutoOrient()`, `Strip()`, redimensiona para no máximo 2560 px no maior lado (só reduz); PNG continua `image/png`, JPEG e WebP viram `image/jpeg` com qualidade 85.
- `LocalDiskFileStorage`: raiz em `Storage:RootPath`; o caminho final precisa estar dentro da raiz (`Path.GetFullPath`), senão `InvalidOperationException`; grava em arquivo temporário e depois move.
- Exclusões: `DeleteServiceRecordHandler` e `DeleteVehicleHandler` buscam as chaves antes de remover e registram `uow.AfterCommit` para apagar os arquivos. `DeleteAttachmentHandler` faz o mesmo para um arquivo.
- FKs `record_id` → `service_records` e `vehicle_id` → `vehicles`, ambas com cascade; índices `record_id` e `vehicle_id`.
- Pacotes: Infrastructure e IntegrationTests → `Magick.NET-Q8-AnyCPU`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// TestImages: Jpeg(int w, int h, bool withGps) (Magick.NET, ExifProfile com GPSLatitude/GPSLatitudeRef), TinyPng(), Pdf() = "%PDF-1.4\n%âãÏÓ\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF", Heic() = 00 00 00 18 66 74 79 70 68 65 69 63 + zeros
[Fact] public async Task Upload_jpeg_photo_strips_exif_resizes_and_is_public()   // 3000×2000 com GPS → 201 isPublic true, image/jpeg; download sem ExifProfile, lados ≤ 2560
[Fact] public async Task Upload_pdf_is_private_document_downloaded_as_attachment() // kind document, isPublic false; Content-Disposition: attachment
[Fact] public async Task Upload_heic_or_renamed_file_returns_415_and_stores_nothing() // Review Focus 3: Heic() com nome "foto.jpg" → "attachment.unsupported_type"; pasta {StoragePath}/{vehicleId} vazia ou inexistente
[Fact] public async Task Upload_over_10mb_returns_413()
[Fact] public async Task Twenty_first_attachment_returns_422()                    // 20 × TinyPng ok; 21º → "attachment.limit_reached"
[Fact] public async Task Making_document_public_returns_422()
[Fact] public async Task Attachment_of_other_vehicle_via_my_vehicle_url_returns_404() // usuário com veículos A e B: /vehicles/A/attachments/{anexoDeB}/content → 404
[Fact] public async Task Other_user_cannot_download_or_delete()
[Fact] public async Task Record_detail_lists_attachments_and_list_counts_them()
[Fact] public async Task Deleting_record_removes_files_from_disk()
[Fact] public async Task Deleting_vehicle_removes_files_from_disk()
[Fact] public async Task Responses_have_nosniff_header()
// LocalDiskFileStorageTests
[Fact] public async Task Save_then_open_roundtrip()
[Fact] public async Task Rejects_keys_outside_root()                             // "../fora.txt" → InvalidOperationException
```

- [ ] **Step 2: Rodar e ver falhar** — `--filter "AttachmentTests|LocalDiskFileStorageTests"` → FAIL.
- [ ] **Step 3: Implementar portas, storage, processador de imagem e casos de uso** (+ `dotnet ef migrations add AddAttachments ...`)
- [ ] **Step 4: Atualizar DTOs e consultas de registros e os handlers de exclusão**
- [ ] **Step 5: Implementar endpoints**
- [ ] **Step 6: Rodar e ver passar** — `dotnet test` → PASS.
- [ ] **Step 7: Commit** — `git commit -m "feat(attachments): upload com remoção de EXIF, limites e limpeza de arquivos"`

---

### Task 13: Links públicos e visão pública

**Files:**
- Create: `src/Garage.Application/Sharing/{ShareLinkDtos.cs, PublicDtos.cs, ShareTokens.cs, CreateShareLink/*, RevokeShareLink/*, GetShareLinks/*, GetPublicVehicle/*, GetPublicAttachment/*}`, `Abstractions/Ports/IShareLinkRepository.cs`
- Create: `src/Garage.Infrastructure/Persistence/Configurations/ShareLinkConfiguration.cs`, `Repositories/ShareLinkRepository.cs`, migration `AddShareLinks`
- Create: `src/Garage.Api/Sharing/{ShareLinkRequests,ListShareLinksEndpoint,CreateShareLinkEndpoint,RevokeShareLinkEndpoint}.cs`, `src/Garage.Api/Public/{PublicHeadersFilter,GetPublicVehicleEndpoint,GetPublicAttachmentEndpoint}.cs`
- Test: `tests/Garage.Api.IntegrationTests/Sharing/{ShareLinkTests,PublicViewTests}.cs`

**Interfaces:**
- Consumes: Task 6 (`ShareLink`), Task 11 (`RecordCursor`), Task 12 (`FileContent`, `IFileStorage`).
- Produces:
```csharp
public static class ShareTokens
{   public static string New();                     // Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)) → 43 chars
    public static bool LooksValid(string token); }  // 43 chars, só [A-Za-z0-9_-]
public sealed record CreateShareLinkCommand(Guid VehicleId, string? Label, bool IncludeCosts, bool IncludePlate, DateTimeOffset? ExpiresAt) : ICommand<ShareLinkDto>;
public sealed record RevokeShareLinkCommand(Guid VehicleId, Guid LinkId) : ICommand<Unit>;
public sealed record GetShareLinksQuery(Guid VehicleId) : IQuery<IReadOnlyList<ShareLinkDto>>;
public sealed record ShareLinkDto(Guid Id, string Token, string Path, string? Label, bool IncludeCosts, bool IncludePlate,
    DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt, DateTimeOffset CreatedAt, bool IsActive);   // Path = "/p/{token}"
public sealed record GetPublicVehicleQuery(string Token, string? Cursor) : IQuery<PublicVehicleDto>;
public sealed record PublicVehicleDto(string Make, string Model, string? Version, string? Generation, int ManufactureYear, int ModelYear, Fuel Fuel,
    string? Nickname, string? Plate, int CurrentOdometer, bool IncludesCosts, IReadOnlyList<PublicRecordDto> Records, string? NextCursor);
public sealed record PublicRecordDto(RecordType Type, DateOnly Date, int? Odometer, Category Category, string Title, string? Description, string? PerformedBy,
    string? ReplacedOriginalPart, IReadOnlyList<PublicPartDto> Parts, decimal? LaborCost, decimal? TotalCost, IReadOnlyList<PublicPhotoDto> Photos);
public sealed record PublicPartDto(string Name, string? Brand, string? PartNumber, decimal Quantity, PartUnit Unit, decimal? UnitPrice);
public sealed record PublicPhotoDto(Guid Id, string Path);   // "/api/public/{token}/attachments/{id}"
public sealed record GetPublicAttachmentQuery(string Token, Guid AttachmentId) : IQuery<FileContent>;
public interface IShareLinkRepository { Task<ShareLink?> GetAsync(Guid vehicleId, Guid linkId, CancellationToken ct); void Add(ShareLink l); }
```
```
GET    /api/vehicles/{vehicleId}/share-links            → 200 ShareLinkDto[]
POST   /api/vehicles/{vehicleId}/share-links            { label?, includeCosts, includePlate, expiresAt? } → 201 ShareLinkDto
DELETE /api/vehicles/{vehicleId}/share-links/{linkId}   → 204 (revoga)
GET    /api/public/{token}?cursor                       anônimo, rate limit "public" → 200 PublicVehicleDto
GET    /api/public/{token}/attachments/{attachmentId}   anônimo, rate limit "public" → stream inline
```
- Validator: `Label` ≤ 60; `ExpiresAt` > `clock.Now` quando informado.
- Handlers públicos: `!ShareTokens.LooksValid` → `ShareLinkErrors.NotFound`, sem consultar o banco. Link inexistente ou `!IsValidAt(clock.Now)` → também `NotFound`. A projeção segue a allowlist da spec §4.8: preço das peças, mão de obra e total só com `IncludeCosts`; placa só com `IncludePlate`; fotos só `Kind == Photo && IsPublic`. Página de 30 registros por `RecordCursor`.
- Anexo público: link válido + anexo do mesmo veículo + `Photo` + `IsPublic`; qualquer outro caso → `AttachmentErrors.NotFound`.
- `PublicHeadersFilter` (endpoint filter): define `X-Robots-Tag: noindex` e `Referrer-Policy: no-referrer` antes de chamar `next`, então os headers vão também nas respostas de erro.
- Índice único em `token`; índice em `vehicle_id`; FK do veículo com cascade.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// ShareLinkTests
[Fact] public async Task Create_returns_43_char_token_and_path()
[Fact] public async Task Revoke_marks_link_inactive()
[Fact] public async Task Expires_at_in_past_returns_400()
[Fact] public async Task Other_user_gets_404()
// PublicViewTests — cenário: veículo com vin, notes "segredo-notas", registro com peça de preço 45,90 e mão de obra 80,00,
// foto pública, foto privada, documento e um lembrete
[Fact] public async Task Public_view_exposes_only_allowlisted_fields()
// JSON bruto: não contém o VIN, "segredo-notas", os ids da foto privada e do documento, nem "reminder"; o conjunto de propriedades da raiz é exatamente
// {make, model, version, generation, manufactureYear, modelYear, fuel, nickname, plate, currentOdometer, includesCosts, records, nextCursor}; photos.Count == 1
[Fact] public async Task Costs_appear_only_with_include_costs()     // false → unitPrice, laborCost e totalCost null; true → 45.9, 80, 171.8
[Fact] public async Task Plate_appears_only_with_include_plate()
[Fact] public async Task Revoked_link_returns_404()
[Fact] public async Task Expired_link_returns_404()                 // avança o Clock além de ExpiresAt
[Fact] public async Task Malformed_token_returns_404()              // Review Focus 4: "x", 43 chars com "!", 200 chars → 404 "share_link.not_found"
[Fact] public async Task Public_photo_downloads_and_private_or_document_returns_404()
[Fact] public async Task Public_responses_have_noindex_and_no_referrer_even_on_404()
[Fact] public async Task Public_endpoint_is_rate_limited()          // WithWebHostBuilder(RateLimits:PublicPerMinute=2): 3ª → 429
```

- [ ] **Step 2: Rodar e ver falhar** — `--filter "ShareLinkTests|PublicViewTests"` → FAIL.
- [ ] **Step 3: Implementar Application, Infrastructure (+ `dotnet ef migrations add AddShareLinks ...`) e endpoints**
- [ ] **Step 4: Rodar e ver passar** — `dotnet test` → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(sharing): links públicos com visão por allowlist"`

---

### Task 14: Catálogo FIPE (camada anticorrupção)

**Files:**
- Create: `src/Garage.Application/Catalog/{CatalogModels.cs, CatalogErrors.cs, GetCatalogMakes/*, GetCatalogModels/*, GetCatalogYears/*, GetCatalogDetails/*}`, `src/Garage.Application/Vehicles/GetVehicleFipePrice/*`, `Abstractions/Ports/IVehicleCatalog.cs`
- Create: `src/Garage.Infrastructure/Catalog/{FipeOptions,FipeClient,FipeDtos,FipeTranslator,CatalogCacheEntry,FipeVehicleCatalog}.cs`, `Persistence/Configurations/CatalogCacheConfiguration.cs`, migration `AddCatalogCache`; `InternalsVisibleTo("Garage.Infrastructure.Tests")`
- Create: `src/Garage.Api/Catalog/{CatalogMakesEndpoint,CatalogModelsEndpoint,CatalogYearsEndpoint,CatalogDetailsEndpoint}.cs`, `src/Garage.Api/Vehicles/GetVehicleFipePriceEndpoint.cs`
- Test: `tests/Garage.Infrastructure.Tests/Catalog/{FipeTranslatorTests.cs, Fixtures/models-honda.json, Fixtures/years-sample.json, Fixtures/detail-sample.json}`, `tests/Garage.Api.IntegrationTests/Infrastructure/FakeFipeHandler.cs`, `tests/Garage.Api.IntegrationTests/Catalog/CatalogTests.cs`

**Interfaces:**
- Consumes: Task 3 (`VehicleType`, `Fuel`, `CatalogRef`, `VehicleErrors.CatalogNotLinked`), Task 8 (rate limit "catalog"), Task 9.
- Produces:
```csharp
public sealed record CatalogMake(string Id, string Name);
public sealed record CatalogModel(string Id, string FullName, string SuggestedModel, string SuggestedVersion);
public sealed record CatalogYear(string Id, int? ModelYear, Fuel Fuel, string Label);
public sealed record CatalogDetails(string FipeCode, string Make, string FullName, int? ModelYear, Fuel Fuel, decimal Price, string ReferenceMonth);
public sealed record CatalogPrice(decimal Price, string ReferenceMonth);
public interface IVehicleCatalog
{   Task<Result<IReadOnlyList<CatalogMake>>> GetMakesAsync(VehicleType type, CancellationToken ct);
    Task<Result<IReadOnlyList<CatalogModel>>> GetModelsAsync(VehicleType type, string makeId, CancellationToken ct);
    Task<Result<IReadOnlyList<CatalogYear>>> GetYearsAsync(VehicleType type, string makeId, string modelId, CancellationToken ct);
    Task<Result<CatalogDetails>> GetDetailsAsync(VehicleType type, string makeId, string modelId, string yearId, CancellationToken ct);
    Task<Result<CatalogPrice>> GetPriceAsync(VehicleType type, string fipeCode, string yearId, CancellationToken ct); }
public static class CatalogErrors { public static readonly Error Unavailable; } // catalog.unavailable, Unavailable, "Catálogo FIPE indisponível no momento. Preencha manualmente."
internal sealed record FipeItem(string Code, string Name);
internal sealed record FipeVehicleDetail(int VehicleType, string Price, string Brand, string Model, int ModelYear, string Fuel, string CodeFipe, string FuelAcronym, string ReferenceMonth);
internal static class FipeTranslator
{   CatalogMake ToMake(FipeItem i); CatalogModel ToModel(FipeItem i); CatalogYear ToYear(FipeItem i);
    CatalogDetails ToDetails(FipeVehicleDetail d); decimal ParsePrice(string brl); Fuel ParseFuel(string text); }
public sealed class FipeClient   // typed HttpClient; devolve o JSON bruto
{   Task<string> GetAsync(string relativePath, CancellationToken ct); }
public sealed class FakeFipeHandler : HttpMessageHandler   // testes
{   public Dictionary<string, string> Responses { get; } public bool IsDown { get; set; } public int Calls { get; } } // IsDown → 503; caminho desconhecido → 404
```
```
GET /api/catalog/{type}/makes                                              (type = car | motorcycle | truck)
GET /api/catalog/{type}/makes/{makeId}/models
GET /api/catalog/{type}/makes/{makeId}/models/{modelId}/years
GET /api/catalog/{type}/makes/{makeId}/models/{modelId}/years/{yearId}    → CatalogDetails
GET /api/vehicles/{vehicleId}/fipe-price                                   → CatalogPrice
todos autenticados, rate limit "catalog"; type inválido → 400 validation (errors.type)
```
- Tradução, conforme spec §6.3:
  - `ToModel`: `FullName` = nome sem espaços nas pontas; `SuggestedModel` = primeira palavra; `SuggestedVersion` = o resto, ou `""`.
  - `ToYear`: o primeiro token do **nome** vira `ModelYear` se for inteiro entre 1900 e 2100; senão `null` (cobre `32000` e "Zero KM").
  - `ParseFuel` procura no texto inteiro, sem acento e sem diferenciar maiúsculas: gasolina → Gasoline, alcool/etanol → Ethanol, diesel → Diesel, flex → Flex, hibrido → Hybrid, eletrico → Electric; nada encontrado → Other.
  - `ParsePrice`: remove "R$" e espaços (incluindo `\u00A0`) e faz parse com `pt-BR`.
- Caminhos na FIPE: `Car` → `cars`, `Motorcycle` → `motorcycles`, `Truck` → `trucks`. `brands`, `brands/{m}/models`, `brands/{m}/models/{mo}/years`, `brands/{m}/models/{mo}/years/{y}`; preço por `{fipeType}/{fipeCode}/years/{yearId}`.
- `FipeOptions` (seção `Fipe`): `BaseUrl` = `https://fipe.parallelum.com.br/api/v2/`; `Token` opcional, enviado no header `X-Subscription-Token`.
- Resiliência com `AddStandardResilienceHandler`: timeout de 5 s por tentativa, 10 s no total; até 2 retries só em 5xx, `HttpRequestException` e timeout (nunca em 429).
- `FipeVehicleCatalog`, para cada consulta:
  - chave `fipe:{caminho}`; validade de 30 dias para listas e 7 dias para detalhes e preço;
  - cache dentro da validade → traduz e devolve;
  - senão chama o `FipeClient`, grava o cache (`INSERT … ON CONFLICT (key) DO UPDATE` via `ExecuteSqlInterpolatedAsync`) e devolve;
  - em `HttpRequestException`, `TimeoutRejectedException`, `BrokenCircuitException` ou status ≠ 2xx: log de warning; se houver cache vencido, devolve ele; senão `CatalogErrors.Unavailable`.
- Tabela `catalog_cache(key text PK, payload jsonb, fetched_at timestamptz)`.
- `GetVehicleFipePrice`: acesso → veículo → sem `Catalog` → `VehicleErrors.CatalogNotLinked` → `GetPriceAsync`.
- No `GarageApiFactory` (alterar), registrar `FakeFipeHandler` como `ConfigurePrimaryHttpMessageHandler` do `FipeClient` e expor como `Factory.Fipe`.

- [ ] **Step 1: Criar as fixtures**

`models-honda.json`, com nomes reais da FIPE:
`[{"code":"1263","name":"Civic Sedan LX 1.6 16V Mec. 4p"},{"code":"10727","name":"Civic Type-R 2.0 Turbo 16V"},{"code":"1249","name":"Civic CRX/ Targa VTi"}]`
`years-sample.json`: `[{"code":"1998-1","name":"1998 Gasolina"},{"code":"2022-3","name":"2022 Diesel"},{"code":"32000-1","name":"32000 Gasolina"}]`
`detail-sample.json`: `{"vehicleType":1,"price":"R$ 10.000,00","brand":"Honda","model":"Civic Sedan LX 1.6 16V Mec. 4p","modelYear":1998,"fuel":"Gasolina","codeFipe":"005340-6","fuelAcronym":"G","referenceMonth":"outubro de 2026","priceHistory":[]}`
Se a API responder durante a implementação, salvar também a resposta real de `cars/brands/25/models/1263/years` como `years-civic-real.json`.

- [ ] **Step 2: Escrever os testes que falham**

```csharp
// FipeTranslatorTests
[Theory] [InlineData("Civic Sedan LX 1.6 16V Mec. 4p","Civic","Sedan LX 1.6 16V Mec. 4p")] [InlineData("Civic CRX/ Targa VTi","Civic","CRX/ Targa VTi")] [InlineData("Fusca","Fusca","")]
public void ToModel_splits_family_and_version(string name, string model, string version)
[Theory] [InlineData("1998 Gasolina",1998,Fuel.Gasoline)] [InlineData("2022 Diesel",2022,Fuel.Diesel)] [InlineData("32000 Gasolina",null,Fuel.Gasoline)]
         [InlineData("2010 Álcool",2010,Fuel.Ethanol)] [InlineData("Zero KM Flex",null,Fuel.Flex)] [InlineData("2020 Elétrico",2020,Fuel.Electric)]
         [InlineData("2020 Híbrido",2020,Fuel.Hybrid)] [InlineData("2020 GNV",2020,Fuel.Other)]
public void ToYear_reads_year_and_fuel_from_name(string name, int? year, Fuel fuel)
[Theory] [InlineData("R$ 10.000,00","10000.00")] [InlineData("R$ 1.234.567,89","1234567.89")] [InlineData("R$\u00a099,90","99.90")]
public void ParsePrice_handles_brl(string text, string expected)
[Fact] public void All_fixtures_translate() // todo arquivo em Fixtures/ desserializa e traduz sem exceção
// CatalogTests (fake: brands → [{"code":"25","name":"Honda"}], models/years/detail com as fixtures)
[Fact] public async Task Makes_are_translated_and_cached()              // 2 GETs → Fipe.Calls == 1
[Fact] public async Task Models_include_suggested_model_and_version()
[Fact] public async Task Years_parse_model_year_and_fuel()
[Fact] public async Task Fipe_down_without_cache_returns_503()          // "catalog.unavailable"
[Fact] public async Task Fipe_down_with_expired_cache_serves_stale()    // popula, UPDATE catalog_cache SET fetched_at = now() - interval '31 days', IsDown = true → 200
[Fact] public async Task Catalog_requires_login()                       // anônimo → 401
[Fact] public async Task Invalid_vehicle_type_returns_400()             // /api/catalog/boat/makes
[Fact] public async Task Vehicle_fipe_price_for_linked_vehicle()        // veículo com catalog {car,"005340-6","1998-1"} → price 10000.00
[Fact] public async Task Vehicle_without_catalog_returns_404()          // "vehicle.catalog_not_linked"
[Fact] public async Task Catalog_is_rate_limited_per_user()             // WithWebHostBuilder(RateLimits:CatalogPerMinute=2): 3ª → 429
```

- [ ] **Step 3: Rodar e ver falhar** — `--filter "FipeTranslatorTests|CatalogTests"` → FAIL.
- [ ] **Step 4: Implementar tradutor, client, cache e `FipeVehicleCatalog`** (+ `dotnet ef migrations add AddCatalogCache ...`)
- [ ] **Step 5: Implementar casos de uso e endpoints; registrar `FakeFipeHandler` na factory**
- [ ] **Step 6: Rodar e ver passar** — `dotnet test` → PASS.
- [ ] **Step 7: Commit** — `git commit -m "feat(catalog): integração FIPE com camada anticorrupção, cache e fallback"`

---

### Task 15: Contrato OpenAPI, execução local e verificação final

**Files:**
- Create: `backend/README.md`
- Test: `tests/Garage.Api.IntegrationTests/OpenApiContractTests.cs`

**Interfaces:**
- Consumes: todas as tasks anteriores.
- Produces: documento `/openapi/v1.json` completo, base do Plano 2.

- [ ] **Step 1: Escrever o teste de contrato que falha**

```csharp
[Fact] public async Task Every_endpoint_declares_success_response_with_schema()
// para cada operação em paths: existe resposta 2xx; respostas 200/201 têm content application/json com schema
// (exceções: rotas de stream /content e /public/{token}/attachments/{id}; 204 sem corpo)
[Fact] public async Task Document_contains_all_feature_routes()
// paths incluem /api/vehicles, /api/vehicles/{vehicleId}/records, /api/vehicles/{vehicleId}/reminders, /api/reminders/due,
// /api/vehicles/{vehicleId}/share-links, /api/public/{token}, /api/catalog/{type}/makes, /api/auth/login
```

- [ ] **Step 2: Rodar** — `--filter OpenApiContractTests`. O esperado é FAIL nos endpoints a que falta `.Produces<T>()`; se já passar, seguir.
- [ ] **Step 3: Completar os metadados que faltarem**
- [ ] **Step 4: Escrever o `README.md`**: pré-requisitos (.NET 10 SDK, Docker); `dotnet test`; rodar localmente com `docker run -d --name garage-db -e POSTGRES_USER=garage -e POSTGRES_PASSWORD=garage_dev -e POSTGRES_DB=garage -p 5432:5432 postgres:18` → `dotnet ef database update -p src/Garage.Infrastructure -s src/Garage.Api` → `dotnet run --project src/Garage.Api`; Scalar em `/scalar`; reset de senha com `dotnet run --project src/Garage.Api -- reset-password <email>`.
- [ ] **Step 5: Verificação manual**: seguir o README. Abrir `/scalar`, registrar um usuário, criar um veículo pela FIPE real e um registro com foto. Rodar o reset de senha pela CLI e logar com a senha temporária.
- [ ] **Step 6: Suíte completa** — `dotnet build` (0 warnings) e `dotnet test` → PASS em todos os projetos.
- [ ] **Step 7: Commit** — `git commit -m "docs(backend): contrato OpenAPI verificado e README de execução"`

---

## Notas para o Plano 2 (front, E2E e deploy)

- O front busca `GET /api/auth/antiforgery` ao carregar o app e de novo depois de login ou registro.
- A imagem Docker da API precisa de ICU e tzdata, por causa de `America/Sao_Paulo`. Use uma variante com essas bibliotecas (ex.: `-noble`), não a `chiseled` básica.
- O `migrator` usa `dotnet ef migrations bundle`.
- `Storage:RootPath` aponta para o volume `attachments`.
