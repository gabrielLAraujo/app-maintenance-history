# Nova interface e demo por visitante — Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** dar cara de produto ao Garagem (landing, painel, veículo e link público no visual "Ficha técnica") e deixar um visitante entrar numa demo própria, sem cadastro.

**Architecture:** o backend ganha capa do veículo (regra no agregado `Vehicle`), consultas de resumo de gastos na Application, a lista de todos os lembretes e contas demo por visitante (Identity + serviço de infraestrutura + limpeza em `BackgroundService`). O front troca o `Layout` por um `AppShell` com menu lateral, cria a landing, as páginas de Veículos e Lembretes e refaz painel, veículo, registros e link público sobre tokens do Tailwind.

**Tech Stack:** .NET 10 (Minimal APIs, EF Core + PostgreSQL, ASP.NET Identity), xUnit + Testcontainers; React 19, TypeScript, Vite, Tailwind v4, TanStack Query, openapi-fetch, Vitest + Testing Library.

**Spec:** `docs/superpowers/specs/2026-10-16-nova-interface-e-demo-design.md`. Referência visual: `docs/superpowers/specs/2026-10-16-nova-interface-mockups/`.

## Global Constraints

- Ambiente: PC do Gabriel, PowerShell, pasta `C:\Repositories\APP_MAINETENCE`, branch `feat/nova-interface` (já no GitHub). O workspace na nuvem não tem .NET nem acesso ao NuGet.
- Backend: `dotnet test backend/Garage.slnx` precisa do Docker rodando (Testcontainers, `postgres:18`). `TreatWarningsAsErrors` está ligado.
- Handlers `internal sealed`; um `IEndpoint` por endpoint, com `WithName`, `WithTags` e `Produces`; falhas por `Result`/`Error`; "sem acesso = 404".
- Códigos de erro novos, exatamente: `demo.unavailable` (Unavailable, 503), `demo.uploads_disabled` (Forbidden, 403), `demo.vehicle_limit_reached` (DomainRule, 422), `vehicle.cover_must_be_vehicle_photo` (DomainRule, 422).
- Configuração `Demo:Visitors`: `Enabled=false`, `TtlHours=24`, `MaxActive=100`, `MaxVehicles=5`, `CleanupIntervalMinutes=60` (0 desliga a limpeza automática); `RateLimits:DemoPerHour=5` por IP.
- Custo por km só com km acompanhada ≥ 1.000; "ano" e "mês" no fuso `App:TimeZone` (America/Sao_Paulo).
- Textos da interface e mensagens de erro em pt-BR; moeda BRL (`formatMoney`), km com `formatKm`.
- Visual: tokens da seção 2 da spec; fontes `@fontsource-variable/archivo@5.3.0` e `@fontsource/ibm-plex-mono@5.3.0`, sem Google Fonts; alvos de toque ≥ 44 px; estado de lembrete sempre com texto, nunca só cor.
- Commits em pt-BR no padrão do repo (`feat:`, `docs:`, `test:`...), terminando com as linhas `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` e `Claude-Session: https://claude.ai/code/session_01B1mAtavo3AkExawBsmSYGK`. Push só da branch; nunca na `main`.

## Review Focus

1. **Sessão demo depois da renovação do cookie:** o Identity reconstrói o principal de tempos em tempos; a claim `garage:demo` precisa sobreviver. Teste no Task 6 (a fábrica de principal adiciona a claim).
2. **E-mail técnico da conta demo na tela:** `demo-…@demo.garagem.invalid` não pode aparecer; o menu mostra "Visitante". Teste no Task 13.
3. **Veículo sem registros, ou só com registros sem km nem custo:** os resumos devolvem zeros, `null` e os 12 meses zerados, sem erro. Teste no Task 3.
4. **Capa que vira privada depois:** o dono continua vendo a capa; o link público deixa de mostrar. Teste no Task 2.
5. **Demo que saiu (logout) com o cookie antigo:** `/auth/me` responde 401 e a API não aceita mais a sessão. Teste no Task 7.

---

## Parte A — Backend

### Task 1: Capa do veículo no domínio (regra C1)

**Files:**
- Modify: `backend/src/Garage.Domain/Vehicles/Vehicle.cs`, `backend/src/Garage.Domain/Vehicles/VehicleErrors.cs`
- Test: `backend/tests/Garage.Domain.Tests/Vehicles/VehicleTests.cs`

**Interfaces:**
- Produces: `Guid? Vehicle.CoverPhotoId { get; private set; }`; `Result Vehicle.SetCoverPhoto(Attachment photo)`; `void Vehicle.ClearCoverPhoto()`; `VehicleErrors.CoverMustBeVehiclePhoto` (`"vehicle.cover_must_be_vehicle_photo"`, "A capa precisa ser uma foto deste veículo.", `ErrorType.DomainRule`).

- [ ] **Step 1: Linha de base.** No PC: `git fetch; git checkout feat/nova-interface; dotnet test backend/Garage.slnx; cd frontend; npm ci; npm test`. Esperado: tudo verde (anote os totais de testes para comparar no fim).
- [ ] **Step 2: Testes que falham** em `VehicleTests.cs` (use `NewVehicle()` e `Attachment.Create(...)` como os testes de anexo):

```csharp
[Fact] public void SetCoverPhoto_accepts_a_photo_of_the_same_vehicle()
{   var v = NewVehicle(); var photo = Attachment.Create(v.Id, Guid.NewGuid(), AttachmentKind.Photo, DetectedFileType.Jpeg, "a.jpg", "image/jpeg", 10, Now);
    Assert.True(v.SetCoverPhoto(photo).IsSuccess); Assert.Equal(photo.Id, v.CoverPhotoId); }

[Fact] public void SetCoverPhoto_rejects_a_photo_of_another_vehicle()  // VehicleId diferente → Error == VehicleErrors.CoverMustBeVehiclePhoto, CoverPhotoId continua null

[Fact] public void SetCoverPhoto_rejects_a_document()  // DetectedFileType.Pdf → Kind Document → mesmo erro

[Fact] public void ClearCoverPhoto_removes_the_cover()  // depois de definir, ClearCoverPhoto() → CoverPhotoId null
```

- [ ] **Step 3:** `dotnet test backend/tests/Garage.Domain.Tests --filter "FullyQualifiedName~VehicleTests"` → FAIL (membros não existem).
- [ ] **Step 4: Implementar** os membros em `Vehicle.cs` e o erro em `VehicleErrors.cs`. A regra: `photo.VehicleId == Id && photo.Kind == AttachmentKind.Photo`.
- [ ] **Step 5:** mesmo comando → PASS.
- [ ] **Step 6: Commit** `feat(domain): capa do veículo (regra C1)`.

### Task 2: Capa — persistência, endpoint e visões

**Files:**
- Modify: `backend/src/Garage.Infrastructure/Persistence/Configurations/VehicleConfiguration.cs`
- Create: migração `AddVehicleCoverPhoto` em `backend/src/Garage.Infrastructure/Persistence/Migrations/`
- Create: `backend/src/Garage.Application/Vehicles/SetCoverPhoto/SetCoverPhotoCommand.cs`, `SetCoverPhotoHandler.cs`
- Create: `backend/src/Garage.Api/Vehicles/SetCoverPhotoEndpoint.cs`; Modify: `backend/src/Garage.Api/Vehicles/VehicleRequests.cs`
- Modify: `backend/src/Garage.Application/Vehicles/VehicleDtos.cs` (`VehicleDto`), o mapeamento do `GetVehicle`, `backend/src/Garage.Application/Sharing/PublicDtos.cs` e o handler de `GetPublicVehicle`
- Test: `backend/tests/Garage.Api.IntegrationTests/Vehicles/CoverPhotoTests.cs`, `backend/tests/Garage.Api.IntegrationTests/Sharing/PublicViewTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: `public sealed record SetCoverPhotoCommand(Guid VehicleId, Guid? AttachmentId) : ICommand<Unit>`; `PUT /api/vehicles/{vehicleId}/cover-photo` com `SetCoverPhotoRequest(Guid? AttachmentId)` → 204, nome `SetVehicleCoverPhoto`; `VehicleDto.CoverPhotoId` (`Guid?`, último campo antes de `RowVersion`); `PublicVehicleDto.CoverPhoto` (`PublicPhotoDto?`, depois de `IncludesCosts`).

- [ ] **Step 1: Testes que falham** em `CoverPhotoTests.cs` (`[Collection("api")]`, padrão de `DemoSeedTests`), com `UploadAttachmentAsync(vehicleId, recordId, TestImages.TinyPng(), "a.png")`:
  - `Owner_sets_and_clears_the_cover_photo`: PUT com o id da foto → 204; `GetVehicleAsync` → `coverPhotoId` igual; PUT com `attachmentId: null` → 204; `coverPhotoId` nulo.
  - `Another_user_gets_404`: usuário B, PUT no veículo de A → `AssertProblemAsync(NotFound, "vehicle.not_found")`.
  - `Photo_of_another_vehicle_is_422`: foto do veículo 2 como capa do veículo 1 → 422 `vehicle.cover_must_be_vehicle_photo`.
  - `Document_is_422`: anexo `TestImages.Pdf()` → 422 com o mesmo código.
  - `Deleting_the_cover_photo_clears_the_cover`: define capa, `DELETE .../attachments/{id}` → `coverPhotoId` nulo.
- [ ] **Step 2: Testes que falham** em `PublicViewTests.cs`:
  - `Public_view_shows_the_cover_only_when_the_photo_is_public`: capa pública → `coverPhoto.id` igual e `path` termina em `/attachments/{id}`; `PATCH .../attachments/{id}` com `isPublic: false` → `coverPhoto` nulo; `GetVehicleAsync` do dono ainda traz `coverPhotoId` (Review Focus 4).
- [ ] **Step 3:** `dotnet test backend/tests/Garage.Api.IntegrationTests --filter "FullyQualifiedName~CoverPhoto|FullyQualifiedName~PublicViewTests"` → FAIL.
- [ ] **Step 4: Persistência.** Em `VehicleConfiguration`: `builder.HasOne<Attachment>().WithMany().HasForeignKey(v => v.CoverPhotoId).OnDelete(DeleteBehavior.SetNull);`. Gere a migração: `$env:ConnectionStrings__Garage='Host=localhost;Database=design'; dotnet ef migrations add AddVehicleCoverPhoto --project backend/src/Garage.Infrastructure --startup-project backend/src/Garage.Api --output-dir Persistence/Migrations`. Confira no `Up` só a coluna `cover_photo_id`, o índice e a FK com `ON DELETE SET NULL`.
- [ ] **Step 5: Comando e endpoint.** Handler: `IVehicleAccess.CanEditAsync` primeiro (falha → `VehicleErrors.NotFound`); `null` → `ClearCoverPhoto`; senão `IAttachmentRepository.GetAsync(vehicleId, attachmentId)`, e anexo inexistente nesse veículo também vira `VehicleErrors.CoverMustBeVehiclePhoto` (não revela anexo alheio). Endpoint no padrão de `UpdateOdometerEndpoint`.
- [ ] **Step 6: Leitura.** `VehicleDto.CoverPhotoId`. Visão pública: `CoverPhoto` só se o anexo da capa for `IsPublic` (mesma montagem de caminho de `PublicPhotoDto` das fotos dos registros).
- [ ] **Step 7:** comando do Step 3 → PASS; depois `dotnet test backend/Garage.slnx` → tudo verde (o `OpenApiContractTests` cobre o endpoint novo).
- [ ] **Step 8: Commit** `feat: capa do veículo (PUT /cover-photo, visão pública só com foto pública)`.

### Task 3: Resumo de gastos do veículo e lista enriquecida

**Files:**
- Create: `backend/src/Garage.Application/Spending/SpendingMath.cs` (agregações compartilhadas)
- Create: `backend/src/Garage.Application/Vehicles/GetVehicleSummary/GetVehicleSummaryQuery.cs`, `GetVehicleSummaryHandler.cs`; Modify: `VehicleDtos.cs`
- Create: `backend/src/Garage.Api/Vehicles/GetVehicleSummaryEndpoint.cs`
- Modify: `backend/src/Garage.Application/Vehicles/GetVehicles/GetVehiclesHandler.cs`
- Test: `backend/tests/Garage.Api.IntegrationTests/Vehicles/VehicleSummaryTests.cs`

**Interfaces:**
- Produces:
  - `internal static class SpendingMath` com `decimal Total(IEnumerable<ServiceRecord>)`, `IReadOnlyList<MonthCostDto> Months(IEnumerable<ServiceRecord>, DateOnly firstMonth, int count)` (meses consecutivos a partir de `firstMonth`, zerados quando não há gasto) e `IReadOnlyList<CategoryCostDto> ByCategory(IEnumerable<ServiceRecord>)` (só custo > 0, decrescente).
  - DTOs: `MonthCostDto(string Month, decimal Cost)` com `Month` no formato `"yyyy-MM"`; `CategoryCostDto(Category Category, decimal Cost)`; `TypeSpendingDto(decimal Cost, int Count)`; `RecordRefDto(Guid Id, string Title, DateOnly Date)`; `VehicleSpendingDto(decimal TotalCost, TypeSpendingDto Maintenance, TypeSpendingDto Modification, DateOnly? FirstRecordDate, int? KmTracked, decimal? CostPerKm, IReadOnlyList<CategoryCostDto> ByCategory, IReadOnlyList<MonthCostDto> ByMonth, RecordRefDto? LastRecord)`.
  - `public sealed record GetVehicleSummaryQuery(Guid VehicleId) : IQuery<VehicleSpendingDto>`; `GET /api/vehicles/{vehicleId}/summary`, nome `GetVehicleSummary`.
  - `VehicleSummaryDto` ganha, nesta ordem no fim: `Guid? CoverPhotoId, decimal TotalCost, DateOnly? LastRecordDate`.

- [ ] **Step 1: Testes que falham** em `VehicleSummaryTests.cs`. Relógio em 2026-10-10 15:00 UTC. Veículo com km atual 152.300 e registros: manutenção Motor 11/11/2024, 128.900 km, mão de obra 450 + peças 389,90 e 165 (= 1.004,90); manutenção Freios 31/03/2025, 133.600 km, 268; modificação Suspensão 16/12/2025, 142.200 km, 4.490; manutenção Motor 04/06/2026, 147.800 km, 270,90; manutenção Carroceria 02/10/2026, 151.800 km, 850. Asserções em `Summary_totals_types_categories_months_and_cost_per_km`:
  - `totalCost` 6883.80; `maintenance` {cost 2393.80, count 4}; `modification` {cost 4490, count 1};
  - `firstRecordDate` "2024-11-11"; `kmTracked` 23400; `costPerKm` 0.29 (arredondado a 2 casas, `MidpointRounding.AwayFromZero`);
  - `byCategory[0]` = suspension 4490, depois engine 1275.80, bodyPaint 850, brakes 268;
  - `byMonth` com 12 itens de "2025-11" a "2026-10"; "2025-12" = 4490; "2026-06" = 270.90; "2026-10" = 850; "2026-01" = 0;
  - `lastRecord.title` = "Repintura do para-choque dianteiro".
  - `Summary_of_a_vehicle_without_records_has_zeros_and_nulls` (Review Focus 3): `totalCost` 0, contagens 0, `firstRecordDate`/`kmTracked`/`costPerKm`/`lastRecord` nulos, `byCategory` vazio, 12 meses com 0.
  - `Cost_per_km_is_null_below_1000_km`: um registro a 151.500 km com km atual 152.300 → `kmTracked` 800, `costPerKm` nulo.
  - `Another_user_gets_404`.
  - `Vehicle_list_brings_cover_total_and_last_record_date`: `GET /api/vehicles` → item com `totalCost` igual ao do resumo, `lastRecordDate` "2026-10-02", `coverPhotoId` nulo.
- [ ] **Step 2:** `dotnet test backend/tests/Garage.Api.IntegrationTests --filter "FullyQualifiedName~VehicleSummaryTests"` → FAIL.
- [ ] **Step 3: Implementar.** Handler: `IVehicleAccess` (sem acesso → `VehicleErrors.NotFound`), carrega o veículo e `db.Query<ServiceRecord>().Where(r => r.VehicleId == id)` (as peças vêm junto, são owned) e agrega em memória com `SpendingMath` e `ServiceRecord.TotalCost`. `byMonth`: 12 meses terminando no mês de `IAppClock.Today`. A lista de veículos faz uma única consulta extra de registros dos veículos do usuário (nada de uma consulta por veículo).
- [ ] **Step 4:** comando do Step 2 → PASS; `dotnet test backend/Garage.slnx` → verde.
- [ ] **Step 5: Commit** `feat: resumo de gastos do veículo e totais na lista`.

### Task 4: Resumo da garagem

**Files:**
- Create: `backend/src/Garage.Application/Garage/GetGarageSummary/GetGarageSummaryQuery.cs`, `GetGarageSummaryHandler.cs`, `backend/src/Garage.Application/Garage/GarageDtos.cs`
- Create: `backend/src/Garage.Api/Garage/GetGarageSummaryEndpoint.cs`
- Test: `backend/tests/Garage.Api.IntegrationTests/Garage/GarageSummaryTests.cs`

**Interfaces:**
- Consumes: `SpendingMath`, `MonthCostDto` (Task 3).
- Produces: `RecentRecordDto(Guid Id, Guid VehicleId, string VehicleName, string Title, DateOnly Date, decimal TotalCost)`; `GarageSummaryDto(int Year, decimal YearCost, int YearRecordCount, int YearVehicleCount, IReadOnlyList<MonthCostDto> ByMonth, IReadOnlyList<RecentRecordDto> RecentRecords, int RecordCount, DateOnly? FirstRecordDate)`; `public sealed record GetGarageSummaryQuery : IQuery<GarageSummaryDto>`; `GET /api/garage/summary`, nome `GetGarageSummary`, tag `Garage`.

- [ ] **Step 1: Testes que falham:**
  - `Summary_of_the_year_across_vehicles`: carregue os dados de exemplo (`POST /api/demo/seed` com o relógio em 2026-10-12 15:00 UTC) → `year` 2026, `yearCost` 2586.60, `yearRecordCount` 4, `yearVehicleCount` 2, `byMonth` com 12 itens de "2026-01" a "2026-12" e "2026-07" = 1259.60, `recentRecords` com 4 itens e o primeiro "Repintura do para-choque dianteiro" (`vehicleName` "Civic EK"), `recordCount` 8.
  - `Year_follows_the_Sao_Paulo_time_zone`: relógio em 2027-01-01 02:00 UTC (31/12/2026 em São Paulo) → `year` 2026.
  - `Only_the_callers_vehicles_count`: dados do usuário A não aparecem para B (B vê `recordCount` 0 e `byMonth` zerado).
- [ ] **Step 2:** `--filter "FullyQualifiedName~GarageSummaryTests"` → FAIL.
- [ ] **Step 3: Implementar** com uma consulta de registros dos veículos do usuário (junto com o nome de exibição pela regra de `Vehicle.DisplayName`) e `SpendingMath`.
- [ ] **Step 4:** PASS; suíte completa verde.
- [ ] **Step 5: Commit** `feat: resumo de gastos da garagem`.

### Task 5: Todos os lembretes

**Files:**
- Create: `backend/src/Garage.Application/Reminders/UserReminderStatuses.cs`
- Modify: `backend/src/Garage.Application/Reminders/GetDueReminders/GetDueRemindersHandler.cs`
- Create: `backend/src/Garage.Application/Reminders/GetAllReminders/GetAllRemindersQuery.cs`, `GetAllRemindersHandler.cs`; `backend/src/Garage.Api/Reminders/ListAllRemindersEndpoint.cs`
- Test: `backend/tests/Garage.Api.IntegrationTests/Reminders/ReminderTests.cs`

**Interfaces:**
- Produces: `internal sealed class UserReminderStatuses(IReadDbContext db, IAppClock clock)` com `Task<IReadOnlyList<DueReminderDto>> LoadAsync(Guid userId, CancellationToken ct)` (todos, com status, ordenados como hoje: pior estado primeiro); registrado como scoped no `DependencyInjection` da Application. `public sealed record GetAllRemindersQuery : IQuery<IReadOnlyList<DueReminderDto>>`; `GET /api/reminders`, nome `ListAllReminders`.

- [ ] **Step 1: Teste que falha** `All_reminders_include_the_ones_that_are_ok`: um lembrete em dia e um vencido → `GET /api/reminders` traz os 2 (o vencido primeiro); `GET /api/reminders/due` continua só com o vencido.
- [ ] **Step 2:** `--filter "FullyQualifiedName~ReminderTests"` → FAIL.
- [ ] **Step 3: Implementar.** Mova a projeção e a ordenação do `GetDueRemindersHandler` para `UserReminderStatuses`; o handler de vencidos filtra `State != Ok`.
- [ ] **Step 4:** PASS (os testes antigos de `/reminders/due` continuam verdes).
- [ ] **Step 5: Commit** `feat: lista de todos os lembretes`.

### Task 6: Contas demo — modelo, sessão e seed

**Files:**
- Modify: `backend/src/Garage.Infrastructure/Identity/AppUser.cs`, `backend/src/Garage.Infrastructure/Persistence/GarageDbContext.cs`
- Create: migração `AddDemoAccounts`
- Create: `backend/src/Garage.Infrastructure/Demo/DemoVisitorOptions.cs`, `DemoAccountService.cs`, `DemoLimits.cs`
- Create: `backend/src/Garage.Infrastructure/Identity/AppUserClaimsPrincipalFactory.cs`
- Modify: `backend/src/Garage.Application/Abstractions/Ports/ICurrentUser.cs`, `backend/src/Garage.Infrastructure/Identity/CurrentUser.cs`
- Create: `backend/src/Garage.Application/Abstractions/Ports/IDemoLimits.cs`
- Create: `backend/src/Garage.Application/Demo/DemoDataSeeder.cs`, `backend/src/Garage.Application/Demo/SeedVisitorDemo/SeedVisitorDemoCommand.cs`, `SeedVisitorDemoHandler.cs`
- Modify: `backend/src/Garage.Application/Demo/SeedDemoData/SeedDemoDataHandler.cs`, `DemoDataCatalog.cs`
- Modify: `backend/src/Garage.Infrastructure/DependencyInjection.cs`, `backend/src/Garage.Api/Auth/AuthSetup.cs`
- Test: `backend/tests/Garage.Api.IntegrationTests/Demo/DemoAccountTests.cs`, `backend/tests/Garage.Api.IntegrationTests/Demo/DemoSeedTests.cs`

**Interfaces:**
- Produces:
  - `AppUser.IsDemo` (`bool`) e `AppUser.DemoExpiresAt` (`DateTimeOffset?`); índice `ix_asp_net_users_demo_expires_at` em `demo_expires_at` filtrado por `is_demo`.
  - `public sealed class DemoVisitorOptions` (`SectionName = "Demo:Visitors"`; `Enabled`, `TtlHours`, `MaxActive`, `MaxVehicles`, `CleanupIntervalMinutes`, padrões da seção Global Constraints).
  - `public sealed class DemoAccountService` (scoped): `Task<int> CountActiveAsync(CancellationToken)`, `Task<AppUser> CreateAsync(CancellationToken)` (e-mail `demo-{id:N}@demo.garagem.invalid`, sem senha, `DemoExpiresAt = agora + TtlHours`), `Task ExpireNowAsync(Guid userId, CancellationToken)`, `Task DeleteAsync(Guid userId, CancellationToken)`.
  - Claim `"garage:demo"` = `"true"` para conta demo, emitida por `AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser>` (registrada com `AddClaimsPrincipalFactory`).
  - `bool ICurrentUser.IsDemo { get; }`; `public interface IDemoLimits { int MaxVehicles { get; } }`.
  - `internal sealed class DemoDataSeeder` com `Task<Result<DemoSeedResult>> SeedAsync(Guid ownerId, CancellationToken ct)`; define a capa do Civic com a foto `civic-lx-frente.jpg` (flag `IsCover` no item de foto do catálogo).
  - `public sealed record SeedVisitorDemoCommand(Guid OwnerId) : ICommand<DemoSeedResult>`.

- [ ] **Step 1: Testes que falham** em `DemoAccountTests.cs` (resolvendo serviços por `factory.Services.CreateAsyncScope()`):
  - `Created_demo_account_has_no_password_and_expires_in_24_hours`: `CreateAsync` → `IsDemo`, `DemoExpiresAt == Clock.Now + 24h`, `PasswordHash` nulo, e-mail termina em `@demo.garagem.invalid`.
  - `Count_active_ignores_expired_and_real_accounts`.
  - `Principal_of_a_demo_user_carries_the_demo_claim` (Review Focus 1): `IUserClaimsPrincipalFactory<AppUser>.CreateAsync(demo)` tem `garage:demo=true`; para usuário comum, não tem.
  - Em `DemoSeedTests`: `Demo_seed_sets_the_civic_cover_photo` (o Civic volta com `coverPhotoId` de uma foto do próprio veículo).
- [ ] **Step 2:** `--filter "FullyQualifiedName~Demo"` → FAIL.
- [ ] **Step 3: Implementar** os membros acima. Migração `AddDemoAccounts` como no Task 2 (confira: duas colunas e o índice filtrado). `SeedDemoDataHandler` mantém a checagem `AlreadyHasData` e delega ao `DemoDataSeeder`; `SeedVisitorDemoHandler` só delega. `DemoDataSeeder` registrado como scoped. `IDemoLimits` implementado por `DemoLimits` lendo `DemoVisitorOptions`.
- [ ] **Step 4:** PASS; suíte completa verde.
- [ ] **Step 5: Commit** `feat(demo): contas demo sem senha e seed por dono`.

### Task 7: `POST /auth/demo`, `/auth/config`, `/auth/me` e logout

**Files:**
- Create: `backend/src/Garage.Api/Auth/DemoSessionEndpoint.cs`
- Modify: `AuthConfigEndpoint.cs`, `MeEndpoint.cs`, `LogoutEndpoint.cs` (em `backend/src/Garage.Api/Auth/`), `backend/src/Garage.Api/Http/RateLimitingSetup.cs`, `RateLimitOptions.cs`, `backend/src/Garage.Application/Demo/DemoErrors.cs`
- Modify: `backend/tests/Garage.Api.IntegrationTests/Infrastructure/GarageApiFactory.cs` (`Demo:Visitors:Enabled=true`, `MaxActive=10000`, `CleanupIntervalMinutes=0`, `DemoPerHour` na lista de limites altos)
- Test: `backend/tests/Garage.Api.IntegrationTests/Demo/DemoSessionTests.cs`, `backend/tests/Garage.Api.IntegrationTests/Auth/AuthTests.cs`

**Interfaces:**
- Consumes: Task 6.
- Produces: `POST /api/auth/demo` (anônimo, nome `StartDemoSession`, política `RateLimitPolicies.Demo = "demo"`) → 201 `DemoSessionResponse(DateTimeOffset ExpiresAt)`; `AuthConfigResponse(bool RegistrationEnabled, bool DemoEnabled)`; `MeResponse(Guid Id, string Email, bool IsDemo, DateTimeOffset? DemoExpiresAt)`; `DemoErrors.Unavailable` ("A demonstração está cheia agora. Tente de novo mais tarde.").

- [ ] **Step 1: Testes que falham** em `DemoSessionTests.cs` (cliente de `CreateAnonymousClientAsync`):
  - `Demo_session_logs_in_with_the_example_data`: 201 com `expiresAt` = relógio + 24h; `/api/auth/me` → `isDemo` true; `/api/vehicles` → 2 veículos.
  - `Two_demo_sessions_do_not_see_each_other`: a demo 1 cria um veículo; a demo 2 continua com 2.
  - `Demo_account_cannot_log_in_with_a_password`: login com o e-mail da demo e qualquer senha → 401 `auth.invalid_credentials`.
  - `Demo_is_503_when_max_active_is_reached`: fábrica com `Demo:Visitors:MaxActive=0` → 503 `demo.unavailable`.
  - `Demo_is_rate_limited_per_ip`: fábrica com `RateLimits:DemoPerHour=1` → segunda chamada 429.
  - `Demo_is_404_when_disabled`: `Demo:Visitors:Enabled=false` → 404.
  - `Logout_expires_the_demo_and_the_old_cookie_stops_working` (Review Focus 5): guarde o cookie antes do logout, faça logout, mande o cookie antigo em `/api/auth/me` → 401.
  - Em `AuthTests`: `/api/auth/config` traz `demoEnabled` true; `/api/auth/me` de conta comum traz `isDemo` false e `demoExpiresAt` nulo.
- [ ] **Step 2:** `--filter "FullyQualifiedName~DemoSessionTests|FullyQualifiedName~AuthTests"` → FAIL.
- [ ] **Step 3: Implementar.** Endpoint: conta ativas ≥ `MaxActive` → `DemoErrors.Unavailable`; `CreateAsync`; `SeedVisitorDemoCommand(user.Id)` (falha → `DeleteAsync` e devolve o erro); `SignInManager.SignInAsync(user, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = user.DemoExpiresAt, AllowRefresh = false })`. Só mapeado com `Enabled`. Política `Demo` por IP com janela de 1 hora (`ByIp` ganha o parâmetro de janela). `MeEndpoint`: demo com `DemoExpiresAt <= agora` → 401. `LogoutEndpoint`: demo → `ExpireNowAsync` antes de sair.
- [ ] **Step 4:** PASS; suíte completa verde.
- [ ] **Step 5: Commit** `feat(demo): sessão de demonstração por visitante`.

### Task 8: Restrições da conta demo

**Files:**
- Modify: `backend/src/Garage.Domain/Common/ErrorType.cs` (+ `Forbidden`), `backend/src/Garage.Api/Http/ResultHttpExtensions.cs` (`Forbidden` → 403)
- Modify: `backend/src/Garage.Application/Demo/DemoErrors.cs`, `backend/src/Garage.Application/Attachments/UploadAttachment/UploadAttachmentHandler.cs`, `backend/src/Garage.Application/Vehicles/CreateVehicle/CreateVehicleHandler.cs`, `backend/src/Garage.Api/Cli/ResetPasswordCli.cs`, os `Produces` de `UploadAttachmentEndpoint` e `CreateVehicleEndpoint`
- Test: `backend/tests/Garage.Api.IntegrationTests/Demo/DemoRestrictionTests.cs`

**Interfaces:**
- Produces: `DemoErrors.UploadsDisabled` ("Na demonstração não dá para enviar arquivos.", Forbidden); `DemoErrors.VehicleLimitReached(int max)` ("A demonstração permite até {max} veículos.", DomainRule, metadado `max`).

- [ ] **Step 1: Testes que falham:**
  - `Demo_upload_is_403`: anexo num registro da demo → 403 `demo.uploads_disabled`.
  - `Demo_vehicle_limit_is_enforced`: com `MaxVehicles=3` numa fábrica derivada, a demo (já com 2) cria o 3º → 201; o 4º → 422 `demo.vehicle_limit_reached` com `max` 3.
  - `Real_accounts_are_not_limited`: conta comum cria 6 veículos e envia anexo normalmente.
- [ ] **Step 2:** `--filter "FullyQualifiedName~DemoRestrictionTests"` → FAIL.
- [ ] **Step 3: Implementar.** Upload: depois do `IVehicleAccess`, `currentUser.IsDemo` → `UploadsDisabled`. Criação de veículo: demo com `count(OwnerId) >= IDemoLimits.MaxVehicles` → `VehicleLimitReached`. CLI: conta demo → mensagem "Conta de demonstração não tem senha." e código 1.
- [ ] **Step 4:** PASS; suíte completa verde.
- [ ] **Step 5: Commit** `feat(demo): sem upload e com limite de veículos na demonstração`.

### Task 9: Limpeza das contas vencidas

**Files:**
- Modify: `backend/src/Garage.Infrastructure/Demo/DemoAccountService.cs`
- Create: `backend/src/Garage.Infrastructure/Demo/DemoCleanupService.cs`; Modify: `backend/src/Garage.Infrastructure/DependencyInjection.cs`
- Test: `backend/tests/Garage.Api.IntegrationTests/Demo/DemoCleanupTests.cs`

**Interfaces:**
- Produces: `DemoPurgeResult(int Accounts, int Files)`; `Task<DemoPurgeResult> DemoAccountService.PurgeExpiredAsync(int batchSize, CancellationToken ct)`; `DemoCleanupService : BackgroundService`, registrado só com `Enabled` e `CleanupIntervalMinutes > 0`, usando `PeriodicTimer` com o `TimeProvider` do contêiner, lote de 50, repetindo enquanto o lote vier cheio.

- [ ] **Step 1: Testes que falham:**
  - `Expired_demo_is_removed_with_its_data_and_files`: crie uma demo (endpoint), avance o relógio 25 h, `PurgeExpiredAsync(50)` → `Accounts` 1, `Files` 10 (as 9 do Civic, nota fiscal inclusa, e a do Gol); o usuário não existe; nenhum veículo com aquele `OwnerId`; os arquivos sumiram de `factory.StoragePath`.
  - `Valid_demo_and_real_accounts_stay`: demo criada há 1 h e conta comum continuam.
  - `Demo_expired_by_logout_is_removed_on_the_next_run`.
- [ ] **Step 2:** `--filter "FullyQualifiedName~DemoCleanupTests"` → FAIL.
- [ ] **Step 3: Implementar.** Por lote: ids vencidos; chaves dos anexos dos veículos desses donos; `ExecuteDeleteAsync` nos usuários (o banco apaga o resto em cascata); depois apaga cada arquivo com `IFileStorage.DeleteAsync`, registrando em log a falha de um sem parar os outros; log final com contas e arquivos.
- [ ] **Step 4:** PASS; suíte completa verde.
- [ ] **Step 5: Commit** `feat(demo): limpeza das contas de demonstração vencidas`. Depois: `git push`.

---

## Parte B — Front-end

Comandos do front, em `C:\Repositories\APP_MAINETENCE\frontend`: `npm test -- <arquivo>` para um arquivo, `npm test` para a suíte, `npm run lint` (tsc + oxlint), `npm run build`.

### Task 10: Tipos da API

**Files:**
- Modify: `frontend/src/api/schema.d.ts` (gerado)
- Modify: `frontend/src/test/signedIn.ts`

- [ ] **Step 1: Subir a API local:** `docker run -d --name garagem-pg -e POSTGRES_PASSWORD=dev -p 5432:5432 postgres:18`; em `backend/src/Garage.Api`: `$env:ConnectionStrings__Garage='Host=localhost;Username=postgres;Password=dev;Database=garagem'; $env:Auth__AllowRegistration='true'; $env:Demo__Visitors__Enabled='true'; dotnet run --launch-profile http`.
- [ ] **Step 2:** em `frontend`: `npm run gen:api` → "schema.d.ts gerado". Confira no arquivo `CoverPhotoId`, `VehicleSpendingDto`, `GarageSummaryDto`, `/api/auth/demo` e `/api/reminders`.
- [ ] **Step 3:** `signedInRoutes()` passa a responder `demoEnabled: false` no config e `isDemo: false, demoExpiresAt: null` no `account`.
- [ ] **Step 4:** `npm run lint` e `npm test` → verdes.
- [ ] **Step 5: Commit** `chore(front): tipos da API com capa, resumos e demo`.

### Task 11: Tokens, fontes e componentes base

**Files:**
- Modify: `frontend/package.json` (+ as duas fontes), `frontend/src/main.tsx` (imports das fontes), `frontend/src/index.css` (`@theme`)
- Modify: `frontend/src/components/buttonStyles.ts`, `Card.tsx`, `Alert.tsx`, `Tabs.tsx`, `Field.tsx`, `FieldFrame.tsx`, `SelectField.tsx`, `TextAreaField.tsx`, `Modal.tsx`, `Empty.tsx`, `PageHeader.tsx`, `Spinner.tsx`
- Create: `frontend/src/components/Badge.tsx`, `PlateChip.tsx`, `Kpi.tsx`, `ProgressBar.tsx`, `Skeleton.tsx`
- Test: `frontend/src/components/components.test.tsx`

**Interfaces:**
- Produces:
  - Tokens de cor (seção 2.1 da spec) como `--color-ink`, `--color-ink-2`, `--color-muted`, `--color-line`, `--color-ground`, `--color-surface`, `--color-amber`, `--color-amber-ink`, `--color-amber-soft`, `--color-danger`(+`-soft`, `-line`), `--color-ok`(+`-soft`, `-line`), `--color-plate`(+`-soft`); `--font-sans: "Archivo Variable"`, `--font-mono: "IBM Plex Mono"`; utilitário `.font-display` (Archivo 800, `font-stretch: 112%`).
  - `ButtonVariant = 'primary' | 'secondary' | 'dark' | 'ghost' | 'danger' | 'inverse'` (os existentes continuam).
  - `Badge({ tone: 'overdue' | 'dueSoon' | 'ok' | 'maintenance' | 'modification' | 'neutral', children })`; `PlateChip({ plate: string })` (faixa "BRASIL" com `aria-hidden`, texto `Placa {plate}` para leitor de tela); `Kpi({ label, value, hint?, tone? })` (valor em mono); `ProgressBar({ value: number /*0..1*/, tone, label })` com `role="progressbar"` e `aria-valuenow` 0–100; `Skeleton({ className })`.

- [ ] **Step 1: Testes que falham** em `components.test.tsx`: `PlateChip` tem nome acessível "Placa ABC1D23" e o texto "BRASIL" fica fora da árvore de acessibilidade; `ProgressBar` com `value 0.9` → `aria-valuenow="90"`; `Badge tone="overdue"` mostra o texto passado (estado nunca só por cor); `Kpi` renderiza rótulo e valor.
- [ ] **Step 2:** `npm test -- src/components/components.test.tsx` → FAIL.
- [ ] **Step 3: Implementar.** `npm install @fontsource-variable/archivo@5.3.0 @fontsource/ibm-plex-mono@5.3.0` (pesos 400, 500, 600) e importe em `main.tsx`; o variável precisa do eixo de largura (`@fontsource-variable/archivo/wdth.css`). Restilize os componentes existentes com os tokens, sem mudar props. Referência: `docs/superpowers/specs/2026-10-16-nova-interface-mockups/direcao-visual.html`.
- [ ] **Step 4:** o arquivo → PASS; `npm test` e `npm run lint` → verdes.
- [ ] **Step 5: Commit** `feat(front): tokens da Ficha técnica, fontes e componentes base`.

### Task 12: Landing e "Ver demo"

**Files:**
- Create: `frontend/src/pages/Landing.tsx`, `frontend/src/auth/RootRoute.tsx`
- Modify: `frontend/src/auth/AuthProvider.tsx`, `frontend/src/routes.tsx`, `frontend/src/pages/Login.tsx`
- Test: `frontend/src/pages/Landing.test.tsx`, `frontend/src/auth/auth.test.tsx`

**Interfaces:**
- Produces: no contexto de auth, `demoEnabled: boolean` e `startDemo(): Promise<Problem | null>` (mesma mecânica de `startSession`); `RootRoute` em `/`: carregando → spinner; sem usuário → `<Landing />`; com usuário → painel dentro do shell (as rotas logadas continuam sob `RequireAuth`); `DemoButton` (exportado de `Landing.tsx`), usado também no `Login`.
- Mensagens: 429 → "Muitas demonstrações criadas a partir deste endereço. Tente de novo em alguns minutos."; 503 `demo.unavailable` → "A demonstração está cheia agora. Tente de novo mais tarde."

- [ ] **Step 1: Testes que falham** em `Landing.test.tsx`:
  - `visitante em / vê a landing`: `/api/auth/me` 401 e config com `demoEnabled: true` → `heading` nível 1 "Tudo o que já foi feito no seu carro, num só lugar." e botões "Ver demo".
  - `sem demo ligada não há "Ver demo"`: só o link "Entrar".
  - `Ver demo abre a sessão e leva ao painel`: `POST /api/auth/demo` → 201; o `/me` seguinte devolve conta demo; uma chamada ao POST, com `X-XSRF-TOKEN`; a landing some e a rota fica `/`.
  - `429 e 503 mostram as mensagens` (as duas frases acima).
  - Em `auth.test.tsx`, troque "visitante sem sessão é levado ao login" por: `/` mostra a landing; `/vehicles/x` sem sessão ainda leva ao `/login`. O login mostra "Ver demo" quando `demoEnabled`.
- [ ] **Step 2:** `npm test -- src/pages/Landing.test.tsx src/auth/auth.test.tsx` → FAIL.
- [ ] **Step 3: Implementar** a landing pelo mockup `landing.html` (hero com a foto do Civic servida de `frontend/public/landing/civic.jpg`, recursos, link para venda, "Por dentro" com 773 testes, .NET 10, DDD e Coolify e links para o repositório e `docs/ARQUITETURA.md`, chamada final, rodapé com créditos das fotos). A foto vem de `backend/src/Garage.Infrastructure/Demo/Assets/civic-lx-frente.jpg`, reduzida a 1600 px de largura.
- [ ] **Step 4:** PASS; `npm test` verde.
- [ ] **Step 5: Commit** `feat(front): landing e botão Ver demo`.

### Task 13: AppShell e faixa da demo

**Files:**
- Create: `frontend/src/components/AppShell.tsx`, `frontend/src/components/DemoBanner.tsx`
- Delete: `frontend/src/components/Layout.tsx` (substituído); Modify: `frontend/src/routes.tsx`
- Test: `frontend/src/components/AppShell.test.tsx`

**Interfaces:**
- Produces: `AppShell` (layout das rotas logadas, com `<Outlet />`): `nav` "Principal" com links Painel (`/`), Veículos (`/vehicles`), Lembretes (`/reminders`, contador de vencidos de `GET /api/reminders/due` com nome acessível "{n} vencido(s)"); rodapé com o e-mail, ou "Visitante" em conta demo, e "Sair"; abaixo de 1024 px, barra superior com botão "Menu" (`aria-expanded`) que abre a navegação. `DemoBanner({ expiresAt })`: `role="status"`, "Você está na demonstração. Pode mexer à vontade: esta conta é apagada em {h} h." (horas arredondadas para cima) e link "Ver o código" para o repositório.

- [ ] **Step 1: Testes que falham:** links e destinos do menu; contador "1 vencido" com um lembrete vencido; conta demo com `demoExpiresAt` = agora + 22h59 → "apagada em 23 h" e "Visitante" no lugar do e-mail (Review Focus 2); conta comum sem faixa e com e-mail; botão "Menu" alterna `aria-expanded`.
- [ ] **Step 2:** `npm test -- src/components/AppShell.test.tsx` → FAIL.
- [ ] **Step 3: Implementar** pelo mockup `painel.html` (barra lateral grafite, item ativo com `aria-current="page"`).
- [ ] **Step 4:** PASS; `npm test` verde (os testes de "Sair" do `auth.test.tsx` seguem passando pelo botão novo).
- [ ] **Step 5: Commit** `feat(front): menu lateral e faixa da demonstração`.

### Task 14: Painel

**Files:**
- Modify: `frontend/src/pages/Dashboard.tsx`; Create: `frontend/src/api/hooks/summary.ts`, `frontend/src/components/MonthBars.tsx`, `frontend/src/components/VehicleCard.tsx`
- Test: `frontend/src/pages/Dashboard.test.tsx`

**Interfaces:**
- Consumes: `GET /api/garage/summary`, `GET /api/vehicles` (com capa e totais), `GET /api/reminders/due`.
- Produces: `useGarageSummary()`, `useVehicleSummary(vehicleId)` (chave `[...vehicleKeys.one(id), 'summary']`, invalidada junto com o veículo); `coverUrl(vehicleId, attachmentId)` → `/api/vehicles/{vehicleId}/attachments/{attachmentId}/content`; `MonthBars({ months: MonthCostDto[], highlight?: string, label: string })` como `figure` com `aria-label` que lista cada mês com gasto ("julho: R$ 1.259,60") e diz "demais meses sem gastos"; `VehicleCard({ vehicle })` (capa ou "Adicionar foto de capa", `PlateChip`, km, gasto total, último serviço).

- [ ] **Step 1: Testes que falham** (reescreva os de layout; mantenha os de comportamento, como "Carregar dados de exemplo"):
  - título "Sua garagem"; KPI "Gasto em 2026" com "R$ 2.586,60" e "4 serviços em 2 veículos"; "Registros" 8 e "desde nov/2024";
  - "Vencidos" e "Vencem em breve" contados de `/reminders/due`;
  - card do Civic com `img` de nome "Civic EK" e `src` da capa; card do Gol com "Adicionar foto de capa";
  - gráfico com `aria-label` contendo "julho: R$ 1.259,60"; "Últimos serviços" com 4 itens, o primeiro "Repintura do para-choque dianteiro".
- [ ] **Step 2:** `npm test -- src/pages/Dashboard.test.tsx` → FAIL.
- [ ] **Step 3: Implementar** pelo mockup `painel.html`.
- [ ] **Step 4:** PASS; `npm test` verde.
- [ ] **Step 5: Commit** `feat(front): painel com gastos do ano e cards com capa`.

### Task 15: Páginas Veículos e Lembretes

**Files:**
- Create: `frontend/src/pages/Vehicles.tsx`, `frontend/src/pages/Reminders.tsx`; Modify: `frontend/src/api/hooks/reminders.ts` (`useAllReminders`), `frontend/src/routes.tsx`
- Test: `frontend/src/pages/Vehicles.test.tsx`, `frontend/src/pages/Reminders.test.tsx`

- [ ] **Step 1: Testes que falham:** `/vehicles` lista os `VehicleCard` em ordem de nome e tem "Novo veículo"; sem veículos mostra o estado vazio com "Cadastrar veículo". `/reminders` mostra os grupos "Vencidos", "Próximos" e "Em dia" (só os grupos com itens), cada item com o nome do veículo, o texto do status e link para `/vehicles/{id}?tab=reminders`; sem lembretes, estado vazio.
- [ ] **Step 2:** `npm test -- src/pages/Vehicles.test.tsx src/pages/Reminders.test.tsx` → FAIL.
- [ ] **Step 3: Implementar.** O texto de status reaproveita o do painel (`statusText` passa para `frontend/src/pages/vehicle/reminderStatus.ts`).
- [ ] **Step 4:** PASS; `npm test` verde.
- [ ] **Step 5: Commit** `feat(front): páginas de veículos e de lembretes`.

### Task 16: Página do veículo

**Files:**
- Modify: `frontend/src/pages/VehicleDetail.tsx`, `frontend/src/pages/vehicle/HistoryTab.tsx`
- Create: `frontend/src/pages/vehicle/SpendingTab.tsx`, `frontend/src/components/CategoryBars.tsx`
- Test: `frontend/src/pages/VehicleDetail.test.tsx`, `frontend/src/pages/vehicle/SpendingTab.test.tsx`

**Interfaces:**
- Consumes: `useVehicleSummary`, `MonthBars`, `PlateChip`, `Kpi` (Tasks 11 e 14).
- Produces: aba `spending` ("Gastos") entre Lembretes e Compartilhar (`?tab=spending`); `CategoryBars({ items: CategoryCostDto[] })` com o rótulo da categoria (`categoryLabels`) e o valor em texto em cada barra.

- [ ] **Step 1: Testes que falham:**
  - capa com `img` "Civic EK" quando há `coverPhotoId`; sem capa, "Adicionar foto de capa" levando à aba Histórico;
  - indicadores "Gasto total", "Manutenção", "Modificações" e "Custo por km" com os valores do resumo; `costPerKm` nulo mostra "—" e a dica "a partir de 1.000 km acompanhados";
  - aba Gastos: categorias na ordem do resumo e o gráfico dos 12 meses;
  - histórico em linha do tempo, com data e km em mono, badge de tipo e categoria (os testes atuais de filtro e rolagem continuam).
- [ ] **Step 2:** `npm test -- src/pages/VehicleDetail.test.tsx src/pages/vehicle/SpendingTab.test.tsx` → FAIL.
- [ ] **Step 3: Implementar** pelo mockup `veiculo.html`.
- [ ] **Step 4:** PASS; `npm test` verde.
- [ ] **Step 5: Commit** `feat(front): página do veículo com capa, indicadores e aba Gastos`.

### Task 17: Capa nas fotos, demo sem upload e telas restantes

**Files:**
- Modify: `frontend/src/pages/record/Attachments.tsx`, `frontend/src/api/hooks/attachments.ts` (ou `vehicles.ts`: `useSetCoverPhoto(vehicleId)`), `frontend/src/pages/RecordDetail.tsx`, `RecordForm.tsx`, `VehicleNew.tsx`, `VehicleEdit.tsx`, `Login.tsx`, `Register.tsx`, `NotFound.tsx`, `frontend/src/auth/RouteGuards.tsx` (moldura do `GuestOnly`)
- Test: `frontend/src/pages/RecordDetail.test.tsx`

- [ ] **Step 1: Testes que falham** em `RecordDetail.test.tsx`:
  - foto mostra "Usar como capa" → `PUT /api/vehicles/{id}/cover-photo` com `{"attachmentId":"<id>"}`; a foto que é capa mostra "Capa do veículo" e "Remover capa" (corpo `{"attachmentId":null}`); documento não tem a ação;
  - conta demo: os botões "Tirar foto" e "Escolher arquivo" não aparecem e o texto "Na demonstração não dá para enviar arquivos." aparece.
- [ ] **Step 2:** `npm test -- src/pages/RecordDetail.test.tsx` → FAIL.
- [ ] **Step 3: Implementar** e restilizar as demais telas da lista com os componentes do Task 11 (sem mudar comportamento; os testes delas devem continuar verdes).
- [ ] **Step 4:** PASS; `npm test` e `npm run lint` verdes.
- [ ] **Step 5: Commit** `feat(front): capa a partir das fotos e telas restantes no visual novo`.

### Task 18: Dossiê público

**Files:**
- Modify: `frontend/src/pages/PublicVehicle.tsx`
- Test: `frontend/src/pages/PublicVehicle.test.tsx`

**Interfaces:**
- Destaques (decisão deste plano, a spec só cita a seção): até 3 registros, primeiro as modificações (mais recentes antes), completando com as manutenções mais recentes; cada um com título, mês/ano e km.

- [ ] **Step 1: Testes que falham:** capa quando `coverPhoto` vem, sem capa quando é nulo; "Destaques do histórico" segue a regra acima com os dados de exemplo (Coilover, Repintura do para-choque dianteiro, Troca de óleo e filtro); bloco "O que este link não mostra" com custos e placa conforme `includesCosts` e a presença de `plate`; os testes atuais de privacidade continuam.
- [ ] **Step 2:** `npm test -- src/pages/PublicVehicle.test.tsx` → FAIL.
- [ ] **Step 3: Implementar** pelo mockup `publico.html`.
- [ ] **Step 4:** PASS; `npm test` verde.
- [ ] **Step 5: Commit** `feat(front): link público como dossiê para comprador`.

### Task 19: Preview, README e verificação final

**Files:**
- Modify: `frontend/index.html`; Create: `frontend/public/og-image.png` (1200×630)
- Modify: `README.md` (em inglês), Create: `README.pt-BR.md`; Create: `docs/screenshots/*.png`
- Modify: `docs/superpowers/specs/2026-10-16-nova-interface-e-demo-design.md` (status "implementada")

- [ ] **Step 1: Metadados.** Título "Garagem — o histórico do seu carro num só lugar", `meta description`, `og:title`, `og:description`, `og:image` (`/og-image.png`), `og:type=website`, `twitter:card=summary_large_image`. A imagem é um print do hero da landing em 1200×630. Verifique: `npm run build` e confira as tags em `dist/index.html`.
- [ ] **Step 2: Prints** com a API local do Task 10 e `npm run dev`: landing, painel, veículo, aba Gastos, dossiê público e celular (390 px), em `docs/screenshots/`.
- [ ] **Step 3: README.** `README.md` em inglês com os prints e a seção "How this project was built" incluindo esta etapa; o texto atual vai para `README.pt-BR.md` com os mesmos prints. Atualize a contagem de testes com os números reais da suíte.
- [ ] **Step 4: Verificação.** `dotnet test backend/Garage.slnx`, `npm test`, `npm run lint`, `npm run build` → verdes. Lighthouse como em produção (a API servindo o build): copie `frontend/dist/*` para `backend/src/Garage.Api/wwwroot/` (não commitar), reinicie a API do Task 10 e rode `npx lighthouse http://localhost:5081/ --form-factor=mobile --only-categories=performance,accessibility --chrome-flags="--headless" --output=json --output-path=lighthouse.json` → acessibilidade ≥ 95 e performance ≥ 90 (critério 7 da spec). Apague o `wwwroot` copiado no fim.
- [ ] **Step 5: Commit e push** `docs: README em inglês, prints e preview para redes`; `git push`.
- [ ] **Step 6: PR** para a `main` com o resumo, os prints, os números dos testes e as variáveis novas do Coolify (`Demo__Visitors__Enabled=true`). Não fazer merge: o Gabriel aprova.
