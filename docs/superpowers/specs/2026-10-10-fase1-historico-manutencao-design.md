# Fase 1 — Histórico de Manutenção de Veículos — Design

- **Data:** 2026-10-10
- **Status:** aguardando revisão do Gabriel
- **Escopo deste documento:** somente a Fase 1 (MVP). Fases 2 e 3 terão specs próprias.

---

## 1. Contexto e objetivo

Aplicação web para manter o histórico de **manutenções** e **modificações** de veículos, com as peças usadas, anexos (fotos, notas fiscais) e lembretes de manutenção preventiva, com possibilidade de compartilhar o histórico por link.

O projeto tem **dois objetivos simultâneos**:

1. **Uso real** — o Gabriel usa nos próprios carros, inclusive pelo celular na oficina.
2. **Estudo de arquitetura** — Clean Architecture, DDD tático e CQRS em .NET. Infra, auth e front seguem o caminho mais simples ("boring"), pois não são foco de estudo.

### 1.1 O que foi decidido pelo Gabriel
- Stack: .NET no back-end, PostgreSQL, React no front.
- Histórico de manutenções e modificações por veículo, com modelos e peças.
- Compartilhamento: link público só leitura, colaboradores (mecânico e coproprietário) e perfil público do build — distribuídos entre as fases (seção 2).
- Hospedagem em VPS própria na Hostinger.
- Lembretes avisam somente dentro do app.
- Catálogo de veículos via FIPE já no MVP.
- Minimal APIs.

### 1.2 Premissas
- Web app responsivo, pensado primeiro para celular (não há app nativo).
- Multiusuário com login desde a Fase 1 (necessário para as fases seguintes), mas com cadastro controlado por configuração.
- Interface somente em pt-BR; moeda somente BRL.

### 1.3 Critérios de sucesso da Fase 1
1. Cadastrar um veículo escolhendo marca/modelo/ano na FIPE, ou manualmente se a FIPE estiver fora.
2. Registrar manutenções e modificações pelo celular, com peças e fotos tiradas na hora.
3. Ver no painel quais lembretes estão vencidos ou próximos.
4. Enviar um link público que mostra o histórico sem expor chassi, documentos, custos (salvo opção) ou localização das fotos.
5. Regras de domínio cobertas por testes; regras de dependência entre camadas garantidas por testes de arquitetura.
6. Rodando na VPS com HTTPS, backup diário fora da VPS e restauração testada.
7. Pelo menos um veículo real cadastrado com histórico.

---

## 2. Roadmap de fases (contexto)

| Fase | Conteúdo |
|---|---|
| **1 — MVP** (este doc) | Login; veículos com catálogo FIPE; registros de manutenção/modificação com peças; anexos; lembretes in-app; link público só leitura |
| 2 — Planejar e colaborar | Roadmap de modificações com orçamento; linha do tempo / antes-e-depois; dossiê em PDF; colaboradores (coproprietário, mecânico) |
| 3 — Dados e comunidade | Planos de revisão por modelo; perfil público do build e comunidade; custos e relatórios; abastecimento; catálogo de peças; oficinas; IA lendo nota fiscal |

### 2.1 Fora do escopo da Fase 1
Tudo das fases 2 e 3, e também: notificações por e-mail/push, troca de painel/hodômetro, recuperação de senha por e-mail, soft delete, CI/CD, multi-idioma, multi-moeda.

---

## 3. Stack

| Camada | Escolha |
|---|---|
| Back-end | .NET 10 (LTS), ASP.NET Core Minimal APIs |
| Persistência | PostgreSQL 18, EF Core + Npgsql, `EFCore.NamingConventions` (snake_case) |
| Auth | ASP.NET Core Identity, cookie |
| Validação | FluentValidation |
| DI/scan | Scrutor (scan de handlers e decorators) |
| Resiliência HTTP | `Microsoft.Extensions.Http.Resilience` |
| Imagens | Magick.NET |
| Logs | Serilog (console JSON) |
| API docs | OpenAPI nativo + Scalar |
| Front-end | React + Vite + TypeScript, React Router, TanStack Query, React Hook Form + Zod, shadcn/ui + Tailwind |
| Cliente da API | `openapi-typescript` + `openapi-fetch` (tipos gerados do OpenAPI) |
| Testes | xUnit, Testcontainers (Postgres), WebApplicationFactory, NetArchTest, Playwright |
| Infra | Docker Compose, Caddy (HTTPS automático), VPS Hostinger |
| Backup | `pg_dump` + `tar` + rclone para armazenamento externo |

---

## 4. Modelo de domínio

Cada agregado tem sua raiz e referencia outros agregados **somente por Id**. Ids são `Guid` v7 (`Guid.CreateVersion7()`), gerados no domínio.

### 4.1 Value objects

| VO | Regras |
|---|---|
| `Odometer` | inteiro em km, 0 ≤ km ≤ 2.000.000 |
| `Money` | `decimal` ≥ 0, 2 casas, moeda fixa BRL |
| `Plate` | normalizada em maiúsculas sem hífen; aceita padrão antigo (`ABC1234`) e Mercosul (`ABC1D23`): `^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$` |
| `VehicleYear` | `ManufactureYear` e `ModelYear`; 1900 ≤ fabricação ≤ ano atual + 1; modelo = fabricação ou fabricação + 1 |
| `CatalogRef` | `VehicleType` + `FipeCode` + `FipeYearCode` (ex.: `1998-1`); opcional no veículo |

### 4.2 Enumerações

**Convenção de nomes:** identificadores de código (classes, enums, rotas, códigos de erro) em inglês; textos da interface em pt-BR. Na API, enums trafegam como string camelCase (ex.: `dueSoon`). Neste documento os termos em português se referem aos valores abaixo.

| Enum | Valores no código → rótulo na UI |
|---|---|
| `VehicleType` | `Car` Carro, `Motorcycle` Moto, `Truck` Caminhão (espelha os tipos da FIPE) |
| `Fuel` | `Gasoline` Gasolina, `Ethanol` Álcool, `Flex` Flex, `Diesel` Diesel, `Hybrid` Híbrido, `Electric` Elétrico, `Other` Outro |
| `RecordType` | `Maintenance` Manutenção, `Modification` Modificação |
| `Category` | `Engine` Motor, `Transmission` Transmissão, `Suspension` Suspensão, `Brakes` Freios, `Electrical` Elétrica, `WheelsTires` Rodas/Pneus, `BodyPaint` Carroceria/Pintura, `Interior` Interior, `Other` Outros |
| `PartUnit` | `Unit` un, `Liter` L, `Kilogram` kg, `Meter` m |
| `AttachmentKind` | `Photo` Foto, `Document` Documento |
| `ReminderState` | `Ok` Em dia, `DueSoon` Próximo, `Overdue` Vencido |

### 4.2.1 "Hoje"

Toda regra que usa "hoje" (R1, M5, cálculo de lembretes) usa a data atual no fuso `America/Sao_Paulo`, configurável em `App:TimeZone`, obtida via `TimeProvider`.

### 4.3 Agregado `Vehicle`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| OwnerId | Guid | sim | usuário dono |
| Type | VehicleType | sim | |
| Make | string ≤ 60 | sim | marca, ex.: Honda |
| Model | string ≤ 60 | sim | família, ex.: Civic |
| Version | string ≤ 120 | não | ex.: Sedan LX 1.6 16V Mec. 4p |
| Generation | string ≤ 30 | não | ex.: EK — não existe na FIPE |
| Year | VehicleYear | sim | |
| Fuel | Fuel | sim | |
| Nickname | string ≤ 60 | não | |
| Plate | Plate | não | |
| Vin | string ≤ 17 | não | chassi; **nunca público** |
| CurrentOdometer | Odometer | sim | |
| OdometerUpdatedAt | DateTimeOffset | sim | |
| Notes | string ≤ 2000 | não | **nunca público** |
| Catalog | CatalogRef | não | preenchido quando escolhido pela FIPE |

**Regras**
- **V1** — `RegisterOdometer(km)`: se `km > CurrentOdometer`, atualiza km e data; caso contrário, não faz nada (leituras antigas não alteram a km atual).
- **V2** — `UpdateOdometer(km)` (atualização manual): `km < CurrentOdometer` → erro `vehicle.odometer_cannot_decrease`.
- **V3** — `CorrectOdometer(km, maxRecordKm)` (correção explícita de digitação): permite diminuir, mas exige `km ≥ maior km entre os registros` → senão `vehicle.odometer_below_records`.
- **V4** — excluir um registro **não** diminui a km atual (é uma leitura já feita); para corrigir, usa-se V3.

### 4.4 Agregado `ServiceRecord`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| VehicleId | Guid | sim | |
| Type | RecordType | sim | |
| Date | DateOnly | sim | |
| Odometer | Odometer | não | registros antigos podem não ter km |
| Title | string ≤ 120 | sim | |
| Description | string ≤ 4000 | não | |
| Category | Category | sim | |
| PerformedBy | string ≤ 120 | não | texto livre ("Eu", nome da oficina) |
| LaborCost | Money | não | |
| ReplacedOriginalPart | string ≤ 500 | não | só para Modificação |
| Parts | lista de `Part` (≤ 50) | — | entidades filhas |
| CreatedAt | DateTimeOffset | sim | |

`Part`: `Name` (≤ 120, obrigatório), `Brand` (≤ 60), `PartNumber` (≤ 60), `Quantity` (decimal > 0, até 3 casas), `Unit` (PartUnit), `UnitPrice` (Money, opcional).

`TotalCost` (calculado) = Σ(`Quantity × UnitPrice`) + `LaborCost`.

**Regras**
- **R1** — `Date ≤ hoje` → senão `record.date_in_future`.
- **R2** — `ReplacedOriginalPart` só pode ser preenchido quando `Type = Modificação` → senão `record.replaced_part_only_for_modification`.
- **R3 (km × cronologia)** — se o registro tem km, então `maxKmAntes ≤ km ≤ minKmDepois`, onde `maxKmAntes` = maior km entre registros do mesmo veículo com data **estritamente anterior** e `minKmDepois` = menor km entre registros com data **estritamente posterior** (registros sem km são ignorados; registros na mesma data não se restringem entre si; na edição o próprio registro é excluído do cálculo). Violação → `record.odometer_out_of_order` com metadados `maxKmBefore` e `minKmAfter`.
  - Implementação: a Application busca os dois valores via repositório (`GetOdometerBounds(vehicleId, date, excludeRecordId)`) e passa ao domínio, que decide.
- **R4** — ao criar ou editar um registro com km, chama `vehicle.RegisterOdometer(km)` (V1).
- **R5** — ao criar, o comando pode trazer `FulfilledReminderIds`; o agregado emite `ServiceRecordAdded(recordId, vehicleId, date, odometer, fulfilledReminderIds)`.
- **R6** — editar ou excluir um registro **não** altera lembretes (a base do lembrete é editável manualmente).

### 4.5 Agregado `Reminder`

| Campo | Tipo | Obrigatório | Observação |
|---|---|---|---|
| VehicleId | Guid | sim | |
| Title | string ≤ 120 | sim | ex.: Troca de óleo |
| IntervalKm | int > 0 | não* | |
| IntervalMonths | int > 0 | não* | |
| LastDoneDate | DateOnly | sim | base de tempo |
| LastDoneOdometer | Odometer | se `IntervalKm` | base de km |

\* **M1** — pelo menos um intervalo é obrigatório → `reminder.interval_required`.

**Cálculo de status** (puro, no domínio: `Evaluate(currentOdometer, today)`):
- Se `IntervalKm`: `kmRestantes = LastDoneOdometer + IntervalKm − CurrentOdometer`.
- Se `IntervalMonths`: `diasRestantes = (LastDoneDate + IntervalMonths meses) − hoje`.
- Estado de cada eixo: **Vencido** se restante ≤ 0; **Próximo** se km restantes ≤ 1.000 ou dias restantes ≤ 30; **Em dia** caso contrário.
- **M2** — estado final = o pior dos eixos presentes ("o que vier primeiro").
- O resultado inclui `kmRestantes`, `diasRestantes` e a data de atualização da km do veículo (para a UI avisar quando a km estiver desatualizada).

**Cumprimento** (`MarkDone(date, odometer)`):
- **M3** — se o lembrete tem `IntervalKm` e o registro não tem km → `reminder.fulfillment_requires_odometer`.
- **M4** — `date < LastDoneDate` → `reminder.fulfillment_older_than_base` (a base só anda para frente).
- Caso válido: `LastDoneDate = date`, `LastDoneOdometer = odometer`.

**Base informada pelo usuário** (criação e edição do lembrete):
- **M5** — `LastDoneDate ≤ hoje` → senão `reminder.base_date_in_future`.
- **M6** — `LastDoneOdometer`, quando informado, é registrado como leitura no veículo via `vehicle.RegisterOdometer` (V1).
- Edição manual da base pode movê-la para trás (M4 vale só para `MarkDone`).

### 4.6 Agregado `Attachment`

| Campo | Tipo | Observação |
|---|---|---|
| VehicleId, RecordId | Guid | sempre ligado a um registro |
| Kind | AttachmentKind | escolhido no upload; PDF é sempre Documento |
| IsPublic | bool | padrão: Foto = true, Documento = false |
| FileName | string ≤ 200 | nome original, só exibição |
| ContentType | string | `image/jpeg`, `image/png` ou `application/pdf` após processamento |
| SizeBytes | long | |
| StorageKey | string | `{vehicleId}/{attachmentId}` — gerado pelo sistema |
| UploadedAt | DateTimeOffset | |

**Regras**
- **A1** — até 10 MB por arquivo → `attachment.too_large`.
- **A2** — tipos aceitos, verificados por magic bytes: JPEG, PNG, WebP, PDF → senão `attachment.unsupported_type`. SVG e HEIC não são aceitos (navegadores iOS convertem HEIC para JPEG no upload).
- **A3** — até 20 anexos por registro → `attachment.limit_reached`.
- **A4** — Documento nunca pode ser público → `attachment.document_cannot_be_public`.
- **A5** — imagens são reprocessadas no servidor antes de gravar: corrige orientação, **remove todos os metadados (EXIF/GPS)**, limita o maior lado a 2560 px e salva como JPEG qualidade 85 (PNG continua PNG). PDFs são gravados como recebidos.

### 4.7 Agregado `ShareLink`

| Campo | Tipo | Observação |
|---|---|---|
| VehicleId | Guid | |
| Token | string (43) | 32 bytes de `RandomNumberGenerator`, base64url |
| Label | string ≤ 60 | opcional, ex.: "Comprador" |
| IncludeCosts | bool | padrão false |
| IncludePlate | bool | padrão false |
| ExpiresAt | DateTimeOffset? | opcional |
| RevokedAt | DateTimeOffset? | |
| CreatedAt | DateTimeOffset | |

**Regras**
- **S1** — link é válido se `RevokedAt` nulo e (`ExpiresAt` nulo ou futuro). Inválido é tratado exatamente como inexistente (404).
- **S2** — revogar é irreversível; para voltar a compartilhar, cria-se outro link.

### 4.8 Visão pública (read model próprio, campos por allowlist)

| Inclui | Condição |
|---|---|
| Marca, modelo, versão, geração, anos, combustível, apelido, km atual | sempre |
| Placa | `IncludePlate` |
| Registros: tipo, data, km, categoria, título, descrição, quem fez, peça original substituída | sempre |
| Peças: nome, marca, part number, quantidade, unidade | sempre |
| Preço das peças, mão de obra, total | `IncludeCosts` |
| Anexos | somente `Kind = Foto` e `IsPublic = true` |

**Nunca inclui:** chassi, observações do veículo, documentos, fotos privadas, lembretes, valor FIPE, dados do dono.

### 4.9 Permissões

- Porta única `IVehicleAccess` com `CanRead(vehicleId, userId)` e `CanEdit(vehicleId, userId)`.
- Fase 1: só o dono pode ler e editar. Na Fase 2 a implementação passa a considerar colaboradores, sem alterar handlers.

### 4.10 Eventos de domínio

| Evento | Emitido por | Tratado por | Efeito |
|---|---|---|---|
| `ServiceRecordAdded` | `ServiceRecord.Create` | `FulfillRemindersOnRecordAdded` | chama `MarkDone` nos lembretes indicados, na mesma transação |

---

## 5. Arquitetura do back-end

### 5.1 Projetos e dependências

```
backend/
  Garage.slnx
  src/
    Garage.Domain/           sem dependências externas
    Garage.Application/      → Domain
    Garage.Infrastructure/   → Application, Domain
    Garage.Api/              → Application, Infrastructure (só para composição/DI)
  tests/
    Garage.Domain.Tests/
    Garage.Infrastructure.Tests/   (tradutor FIPE)
    Garage.Api.IntegrationTests/
    Garage.ArchitectureTests/
```

Pastas por feature dentro de Domain, Application e Api: `Vehicles/`, `Records/`, `Reminders/`, `Attachments/`, `Sharing/`, `Catalog/`, `Auth/`.

### 5.2 Dispatcher próprio (sem MediatR)

- Interfaces: `ICommand<TResult>`, `IQuery<TResult>`, `ICommandHandler<TCommand,TResult>`, `IQueryHandler<TQuery,TResult>`, `ISender`.
- Handlers registrados por scan (Scrutor). Pipeline via decorators, de fora para dentro:
  1. `LoggingDecorator` — nome do caso de uso, duração, resultado (sucesso / código do erro).
  2. `ValidationDecorator` — roda os validators FluentValidation do request; falha → `Error` do tipo Validation.
  3. `TransactionDecorator` — **só comandos**; abre transação, chama o handler, `IUnitOfWork.SaveChangesAsync`, commit.
- Validators checam formato (obrigatórios, tamanhos). Invariantes de negócio ficam no domínio.

### 5.3 CQRS no mesmo banco

- **Comandos:** repositório por agregado (`IVehicleRepository`, `IServiceRecordRepository`, `IReminderRepository`, `IAttachmentRepository`, `IShareLinkRepository`) + modelo de domínio.
- **Consultas:** handlers na Application usando `IReadDbContext` (expõe `IQueryable` somente leitura, `AsNoTracking`) e projetando direto para DTOs. Não passam pelo domínio.

### 5.4 Portas da Application (implementadas na Infrastructure)

`IVehicleAccess`, `IUnitOfWork`, `IReadDbContext`, repositórios, `IFileStorage`, `IImageProcessor`, `IVehicleCatalog`, `ICurrentUser`. Datas via `TimeProvider`.

### 5.5 Persistência

- EF Core + Npgsql, nomes snake_case, migrations em Infrastructure.
- `Part` como coleção owned em tabela própria (`service_record_parts`).
- Concorrência otimista com `xmin` em `vehicles`, `service_records` e `reminders` → conflito vira `concurrency.conflict` (409).
- Eventos de domínio: o `UnitOfWork` coleta eventos dos agregados rastreados, despacha os handlers in-process, repete enquanto surgirem novos eventos (máx. 5 rodadas) e então executa um único `SaveChanges` dentro da transação. Não há outbox: nenhum evento sai do processo.
- Índices: `vehicles(owner_id)`; `service_records(vehicle_id, date DESC, id DESC)` (lista e cálculo de R3); `reminders(vehicle_id)`; `attachments(record_id)`; `attachments(vehicle_id)`; `share_links(token)` único; `share_links(vehicle_id)`.
- Exclusão física (sem soft delete):
  - excluir veículo → remove registros, peças, lembretes, links e anexos; arquivos removidos do disco **após** o commit (falha só gera log).
  - excluir registro → remove peças e anexos (arquivos idem).

### 5.6 Fluxo de exemplo — registrar uma troca de óleo

```
POST /api/vehicles/{id}/records
 → LoggingDecorator → ValidationDecorator → TransactionDecorator
 → AddServiceRecordHandler
     IVehicleAccess.CanEdit(vehicle, user)        → senão vehicle.not_found (404)
     vehicleRepo.Get(id)
     recordRepo.GetOdometerBounds(id, date)        → (maxKmBefore, minKmAfter)
     ServiceRecord.Create(..., bounds)             → R1, R2, R3; emite ServiceRecordAdded
     vehicle.RegisterOdometer(km)                  → V1
     recordRepo.Add(record)
 → UnitOfWork: despacha ServiceRecordAdded → lembretes.MarkDone (M3, M4)
 → SaveChanges → commit → 201 Created { id }
```

---

## 6. Integração FIPE (camada anticorrupção)

### 6.1 Contrato interno (`IVehicleCatalog`, na Application)

```
GetMakes(type)                         → CatalogMake { Id, Name }
GetModels(type, makeId)                → CatalogModel { Id, FullName, SuggestedModel, SuggestedVersion }
GetYears(type, makeId, modelId)        → CatalogYear { Id, ModelYear?, Fuel, Label }
GetDetails(type, makeId, modelId, yearId) → CatalogDetails { FipeCode, Make, FullName, ModelYear?, Fuel, Price, ReferenceMonth }
GetPrice(type, fipeCode, yearId)       → CatalogPrice { Price, ReferenceMonth }
```

O domínio e o front **não conhecem** o formato da FIPE.

### 6.2 Implementação (`FipeVehicleCatalog`, na Infrastructure)

- Base URL configurável, padrão `https://fipe.parallelum.com.br/api/v2`.
- Endpoints usados:
  - `/{type}/brands`
  - `/{type}/brands/{brandId}/models`
  - `/{type}/brands/{brandId}/models/{modelId}/years`
  - `/{type}/brands/{brandId}/models/{modelId}/years/{yearId}`
  - `/{type}/{fipeCode}/years/{yearId}`
- Mapeamento de tipo: `Car` → `cars`, `Motorcycle` → `motorcycles`, `Truck` → `trucks`. A rota da nossa API usa os nossos valores (`/catalog/car/...`), nunca os da FIPE.
- Token opcional por configuração (`Fipe:Token`), enviado no header `X-Subscription-Token`. Sem token: 500 consultas/dia; com token gratuito: 1.000/dia.

### 6.3 Regras de tradução

- **Modelo:** a FIPE junta modelo e versão (`Civic Sedan LX 1.6 16V Mec. 4p`). `SuggestedModel` = primeira palavra (`Civic`); `SuggestedVersion` = restante. A UI mostra os dois **editáveis**, pois a heurística falha em modelos com mais de uma palavra.
- **Ano:** lido do **nome** do ano (ex.: `1998 Gasolina`), não do sufixo do código. Primeiro token numérico → `ModelYear`; se não for numérico (ex.: veículo zero km), `ModelYear` = null e o usuário informa. Restante do nome → `Fuel` por correspondência sem acento/caixa (Gasolina, Álcool, Diesel, Flex, Híbrido, Elétrico); sem correspondência → `Outro`. O ano de fabricação não existe na FIPE: o formulário sugere `ModelYear` e o usuário ajusta.
- **Preço:** texto `R$ 10.000,00` → `decimal` (cultura pt-BR).

### 6.4 Cache, resiliência e falhas

- Cache em tabela `catalog_cache(key text PK, payload jsonb, fetched_at timestamptz)`.
  - Listas (marcas, modelos, anos): validade 30 dias.
  - Preço/detalhes: validade 7 dias.
  - **Cache vencido continua servindo se a FIPE falhar** (stale-on-error).
- Resiliência: timeout de 5 s por tentativa e 10 s no total; 2 retries com backoff só para 5xx e timeout (**nunca** para 429); circuit breaker.
- Sem resposta e sem cache → `catalog.unavailable` (503). O front muda para preenchimento manual. **Cadastrar veículo nunca depende da FIPE.**
- O endpoint de catálogo exige login e tem rate limit por usuário, para proteger a cota.

---

## 7. API

### 7.1 Convenções

- Prefixo `/api`. JSON camelCase. Datas ISO-8601.
- Endpoints organizados por feature: uma classe `IEndpoint` por endpoint, registradas por scan, agrupadas com `MapGroup`.
- Requests e responses são DTOs explícitos; entidades nunca são expostas nem recebidas por bind.
- Erros em ProblemDetails (RFC 9457) com extensão `code` (seção 9).
- Listagem de registros com paginação por cursor: ordem `date DESC, created_at DESC, id DESC`; página padrão 30, máximo 100; cursor opaco (base64 de data + id).

### 7.2 Endpoints

| Grupo | Método e rota | Observação |
|---|---|---|
| Auth | `GET /auth/config` | `{ registrationEnabled }` |
| | `GET /auth/antiforgery` | define cookie `XSRF-TOKEN` |
| | `POST /auth/register` | só mapeado se `Auth:AllowRegistration=true` |
| | `POST /auth/login`, `POST /auth/logout`, `GET /auth/me` | |
| Catálogo | `GET /catalog/{type}/makes` | |
| | `GET /catalog/{type}/makes/{makeId}/models` | |
| | `GET /catalog/{type}/makes/{makeId}/models/{modelId}/years` | |
| | `GET /catalog/{type}/makes/{makeId}/models/{modelId}/years/{yearId}` | detalhes + código FIPE |
| Veículos | `GET /vehicles`, `POST /vehicles` | |
| | `GET /vehicles/{id}`, `PUT /vehicles/{id}`, `DELETE /vehicles/{id}` | |
| | `POST /vehicles/{id}/odometer` | atualização (V2) |
| | `POST /vehicles/{id}/odometer/correction` | correção (V3) |
| | `GET /vehicles/{id}/fipe-price` | 404 se o veículo não tem `CatalogRef` |
| Registros | `GET /vehicles/{id}/records?type&category&cursor&pageSize` | |
| | `POST /vehicles/{id}/records` | aceita `fulfilledReminderIds` |
| | `GET`, `PUT`, `DELETE /vehicles/{id}/records/{recordId}` | |
| Anexos | `POST /vehicles/{id}/records/{recordId}/attachments` | multipart, 1 arquivo + `kind` |
| | `GET /vehicles/{id}/attachments/{attachmentId}/content` | |
| | `PATCH /vehicles/{id}/attachments/{attachmentId}` | `isPublic` |
| | `DELETE /vehicles/{id}/attachments/{attachmentId}` | |
| Lembretes | `GET /vehicles/{id}/reminders` | com status calculado |
| | `POST /vehicles/{id}/reminders` | |
| | `PUT`, `DELETE /vehicles/{id}/reminders/{reminderId}` | `PUT` permite editar a base |
| | `GET /reminders/due` | Próximos e Vencidos de todos os veículos do usuário |
| Links | `GET /vehicles/{id}/share-links`, `POST /vehicles/{id}/share-links` | |
| | `DELETE /vehicles/{id}/share-links/{linkId}` | revoga (S2) |
| Público | `GET /public/{token}` | visão pública (4.8), sem login |
| | `GET /public/{token}/attachments/{attachmentId}` | só foto pública do mesmo veículo |
| Saúde | `GET /health`, `GET /health/ready` | ready verifica o Postgres |

### 7.3 Administração

Comando de linha no próprio executável da API para resetar senha (não há e-mail no MVP):
`docker compose exec api dotnet Garage.Api.dll reset-password <email>` → gera uma senha temporária e a imprime no console.

---

## 8. Front-end

### 8.1 Rotas

| Rota | Tela |
|---|---|
| `/login`, `/register` | `/register` só aparece se `registrationEnabled` |
| `/` | Painel: cards dos veículos + lista de lembretes Vencidos/Próximos |
| `/vehicles/new` | Assistente FIPE: tipo → marca → modelo → ano (com busca em cada passo) → confirmar/editar modelo, versão, geração, anos, combustível → apelido, placa, km. Botão "Não achei, preencher manualmente" em todos os passos; em 503 entra direto no modo manual |
| `/vehicles/:id` | Cabeçalho com km atual (+ atualizar), valor FIPE; abas **Histórico** (filtros tipo/categoria, rolagem infinita), **Lembretes**, **Compartilhar** |
| `/vehicles/:id/edit` | Edição do veículo |
| `/vehicles/:id/records/new`, `/vehicles/:id/records/:recordId/edit` | Formulário: tipo, data, km, categoria, título, descrição, quem fez, mão de obra, peças (linhas dinâmicas), peça original substituída (só Modificação), "cumpre lembretes" (só na criação), anexos |
| `/vehicles/:id/records/:recordId` | Detalhe do registro com galeria |
| `/p/:token` | Página pública só leitura, sem login e sem o layout do app |

### 8.2 Comportamentos

- Mobile-first.
- Anexos: dois botões — "Tirar foto" (`capture="environment"`) e "Escolher arquivo"; seleção Foto/Documento antes do envio; upload um arquivo por vez com progresso.
- Erros `validation.failed` são mapeados para os campos do formulário (`setError`); demais erros mostram a mensagem do ProblemDetails.
- Em `record.odometer_out_of_order`, a mensagem mostra a faixa válida ("entre 85.000 e 92.300 km").
- Ao receber 409, o front avisa que o dado mudou e recarrega.
- Envio de formulário desabilita o botão até a resposta (evita registro duplicado).
- Tipos do cliente gerados do OpenAPI (`npm run gen:api`); quebra de contrato quebra o build do front.

---

## 9. Tratamento de erros

### 9.1 Mecanismo

- Falhas de negócio esperadas → `Result` / `Result<T>` com `Error(Code, Message, Type, Metadata?)`.
- Exceções só para bugs e falhas de infraestrutura.
- `IExceptionHandler` global → 500 ProblemDetails com `traceId`, sem stack trace.
- Todo ProblemDetails inclui `traceId`.

### 9.2 Tipos → HTTP

| ErrorType | HTTP |
|---|---|
| Validation | 400 (`ValidationProblemDetails` com `errors` por campo) |
| Unauthorized | 401 |
| NotFound (inclui "sem acesso") | 404 |
| Conflict | 409 |
| PayloadTooLarge | 413 |
| UnsupportedMedia | 415 |
| DomainRule | 422 |
| Unavailable | 503 |

### 9.3 Catálogo de códigos

| Código | Tipo |
|---|---|
| `validation.failed` | Validation |
| `auth.invalid_credentials`, `auth.locked_out` | Unauthorized |
| `vehicle.not_found`, `record.not_found`, `reminder.not_found`, `attachment.not_found`, `share_link.not_found` | NotFound |
| `concurrency.conflict` | Conflict |
| `attachment.too_large` | PayloadTooLarge |
| `attachment.unsupported_type` | UnsupportedMedia |
| `vehicle.odometer_cannot_decrease`, `vehicle.odometer_below_records` | DomainRule |
| `record.date_in_future`, `record.odometer_out_of_order`, `record.replaced_part_only_for_modification` | DomainRule |
| `reminder.interval_required`, `reminder.fulfillment_requires_odometer`, `reminder.fulfillment_older_than_base`, `reminder.base_date_in_future` | DomainRule |
| `attachment.limit_reached`, `attachment.document_cannot_be_public` | DomainRule |
| `catalog.unavailable` | Unavailable |

---

## 10. Segurança

### 10.1 Autenticação e sessão
- Identity com e-mail + senha; senha mínima de 10 caracteres, sem regras de complexidade forçadas.
- Bloqueio de 15 min após 5 tentativas erradas.
- Cookie `HttpOnly`, `Secure`, `SameSite=Strict`, validade de 14 dias com renovação deslizante.
- Antiforgery: front busca `GET /auth/antiforgery` e envia `X-XSRF-TOKEN` em todo `POST/PUT/PATCH/DELETE`; a API valida.
- Cadastro só existe com `Auth:AllowRegistration=true` (desligar após criar a conta).

### 10.2 Autorização (IDOR/BOLA)
- Todo handler de `/vehicles/{id}/...` passa por `IVehicleAccess` antes de qualquer outra coisa.
- Recursos filhos sempre buscados por `vehicle_id` **e** `id`.
- Sem acesso = 404, igual a inexistente.

### 10.3 Link público
- Token de 32 bytes aleatórios, armazenado em texto no banco para o dono poder copiar o link de novo. **Trade-off aceito:** link é só leitura, filtrado por allowlist e revogável.
- Resposta montada pelo read model público (4.8), nunca a partir do DTO do dono.
- Headers na página e na API pública: `X-Robots-Tag: noindex`, `Referrer-Policy: no-referrer`; a página também tem `<meta name="robots" content="noindex">`.

### 10.4 Anexos
- Nunca servidos como arquivo estático; sempre por endpoint com checagem (dono, ou token válido + foto pública do mesmo veículo).
- Tipo verificado por magic bytes; nome no disco gerado pelo sistema (sem path traversal); limite de tamanho aplicado também no Kestrel/endpoint.
- Metadados removidos (A5).
- Respostas com `X-Content-Type-Options: nosniff`; imagens `inline`, PDFs `attachment`.

### 10.5 Rate limiting (limitador nativo do ASP.NET Core)

| Alvo | Limite |
|---|---|
| `POST /auth/login` | 5/min por IP |
| `/public/*` | 60/min por IP |
| `/catalog/*` | 30/min por usuário |
| Upload de anexos | 30/min por usuário |

### 10.6 Infra
- Postgres sem porta exposta; firewall da VPS libera só 22, 80 e 443.
- Segredos só no `.env` da VPS (fora do git); repositório tem `.env.example`.
- Container da API roda como usuário não-root.

---

## 11. Testes

Sem meta de cobertura: testa-se o que pode quebrar sem ninguém perceber.

| Projeto | O que cobre |
|---|---|
| `Garage.Domain.Tests` | V1–V4, R1–R3, M1–M5, A4, S1; `Evaluate` dos lembretes (limites exatos de 1.000 km / 30 dias, "o que vier primeiro"); VOs (`Plate`, `VehicleYear`, `Odometer`, `Money`) |
| `Garage.Infrastructure.Tests` | Tradutor FIPE com JSONs reais salvos como fixtures (`Civic Sedan LX 1.6 16V Mec. 4p`, ano zero km, combustíveis, preço em pt-BR) |
| `Garage.Api.IntegrationTests` | WebApplicationFactory + Postgres real (Testcontainers) + FIPE simulada por `HttpMessageHandler` falso. Casos obrigatórios: usuário B recebe 404 no veículo de A (e em registro/anexo/lembrete/link de A); link público não expõe chassi, observações, documentos nem fotos privadas, e só mostra custos com `IncludeCosts`; link revogado/expirado → 404; imagem enviada volta sem EXIF; FIPE fora sem cache → 503 e com cache vencido → 200; registrar com `fulfilledReminderIds` reinicia o lembrete; `record.odometer_out_of_order` com metadados; edição concorrente → 409; cadastro desligado → 404 |
| `Garage.ArchitectureTests` | NetArchTest: Domain não referencia Application, Infrastructure, Api nem EF Core; Application não referencia Infrastructure nem Api; handlers são `sealed` e `internal` |
| Front (Playwright) | 1 teste E2E: login → criar veículo manual → registro com peça e foto → criar link → abrir `/p/:token` |

---

## 12. Deploy, operação e backup

### 12.1 Estrutura do repositório

```
APP_MAINETENCE/
  backend/      solução .NET (seção 5.1)
  frontend/     app Vite + e2e/ (Playwright)
  deploy/       docker-compose.yml, Caddyfile, deploy.sh, backup.sh, restore.sh, .env.example
  docs/         specs e planos
```

### 12.2 Docker Compose (VPS Hostinger)

| Serviço | Papel |
|---|---|
| `caddy` | HTTPS automático (Let's Encrypt) no domínio de `APP_DOMAIN`; serve o build do React; `/api/*` → `api` |
| `api` | .NET 10, imagem enxuta, usuário não-root; `depends_on` o `migrator` concluído |
| `migrator` | executa o *migrations bundle* do EF uma vez e sai (migrations não rodam no startup da API) |
| `db` | Postgres 18, sem porta publicada |

Volumes: `pgdata`, `attachments`, `caddy_data`. Logs do Docker com rotação (`max-size`).

Pré-requisito: um domínio ou subdomínio apontando para o IP da VPS.

### 12.3 Deploy
`deploy/deploy.sh` na VPS: `git pull` → `docker compose build` → `docker compose up -d`. CI/CD fica para uma fase futura.

### 12.4 Backup e restauração
- `backup.sh` diário via cron do host às 03:30: `pg_dump -Fc` + `tar` do volume de anexos → `rclone copy` para um remoto externo configurado no `.env` (Backblaze B2, Cloudflare R2 ou Google Drive).
- Retenção: 7 diários + 4 semanais.
- `restore.sh`: baixa um backup, `pg_restore` no banco e extrai os anexos.
- **A restauração deve ser testada uma vez antes de cadastrar dados reais** (faz parte do critério de sucesso 6).

---

## 13. Decisões e trade-offs

| Decisão | Alternativa descartada | Motivo |
|---|---|---|
| Clean Architecture + DDD + CQRS | Vertical Slice; monolito modular | É o foco de estudo escolhido; padrão mais cobrado no mercado .NET; regras de domínio reais justificam |
| Application organizada por feature | Pastas por tipo técnico | Reduz cerimônia e espelha a Api |
| Dispatcher próprio | MediatR | MediatR passou a ter licença comercial; escrever o próprio ensina mais |
| Minimal APIs | Controllers | Recomendação oficial para projetos novos; endpoint é só adaptador do dispatcher; fácil de trocar depois |
| Leituras sem domínio (CQRS no mesmo banco) | Tudo via repositório | Consultas mais simples e rápidas; domínio fica para escrita |
| FIPE no MVP com camada anticorrupção | Texto livre até a Fase 3 | Escolha do Gabriel: dados padronizados desde o início; ACL é estudo de DDD |
| Cache FIPE em tabela Postgres | Redis / só memória | Sobrevive a restart, sem nova infra; respeita a cota |
| Modelo e versão separados | String única da FIPE | É como as pessoas pensam; comunidade/catálogo vão precisar |
| Token do link em texto | Hash do token | Dono pode copiar o link de novo; risco baixo (só leitura, filtrado, revogável) |
| Regra km × cronologia rígida | Só aviso | Protege a confiabilidade do histórico; km é opcional para registros antigos e há correção explícita |
| Lembretes calculados na leitura | Job em background | Sem worker nem SMTP; suficiente para aviso in-app |
| Cookie + mesmo domínio | JWT no SPA | Mais simples e seguro; auth não é foco de estudo |
| Arquivos em volume local | S3/R2 | Menos infra; `IFileStorage` permite trocar depois |
| Exclusão física | Soft delete | Backup cobre recuperação; menos complexidade nas consultas |
