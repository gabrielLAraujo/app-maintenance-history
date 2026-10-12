# Fase 1 — Front básico, dados de exemplo e publicação Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Colocar no ar, em `https://garagem.frauchesgabriel.work`, um front React básico que usa toda a API da Fase 1, com um botão de "carregar dados de exemplo" para ver o app funcionando sem digitar nada.

**Architecture:** O front (Vite + React + TypeScript) é compilado num estágio Node do Dockerfile e servido pela própria API ASP.NET (`UseStaticFiles` + fallback para `index.html`), no mesmo domínio. Assim o cookie de sessão e o antiforgery funcionam sem CORS. A API ganha um endpoint de dados de exemplo (ligado por configuração) e passa a guardar as chaves do Data Protection e os anexos em volumes persistentes do Coolify.

**Tech Stack:** React 19 + Vite + TypeScript, React Router, TanStack Query, React Hook Form + Zod, Tailwind CSS v4, `openapi-typescript` + `openapi-fetch`; ASP.NET Core static files; Coolify (volumes e variáveis via API).

**Spec:** `docs/superpowers/specs/2026-10-10-fase1-historico-manutencao-design.md` (§8 front, §10 segurança, §12 publicação — ver rulings abaixo)

**Pré-requisito:** Plano 1 (`2026-10-10-fase1-backend-api.md`) concluído — todos os endpoints existem e o OpenAPI está completo.

## Rulings que este plano assume (decididos com o Gabriel em 2026-10-10)

- Publicação pelo **Coolify** (já roda na VPS): Traefik com HTTPS, deploy a cada push, Postgres gerenciado e backup agendado. Isso substitui Caddy, `deploy.sh` e parte do `backup.sh` da spec §12.
- O front é servido pela API (mesma origem), não por um container separado.
- "Front básico": telas funcionais e responsivas com Tailwind, sem biblioteca de componentes (shadcn/ui fica para uma iteração de UI). Sem teste E2E nesta rodada (fica registrado como pendência da spec §11).
- Dados de exemplo só existem quando `Demo:Enabled=true` (ligado no ambiente publicado nesta fase, desligado por padrão).

## Global Constraints

- Tudo das Global Constraints do Plano 1 continua valendo no back-end.
- Front em `frontend/` na raiz do repositório; Node 24; `npm` (sem yarn/pnpm).
- Textos da interface em pt-BR; números e moeda formatados com `Intl` em `pt-BR` (`R$ 1.234,56`, `150.000 km`); datas `dd/MM/yyyy`.
- Todas as chamadas à API passam por um único cliente (`src/api/client.ts`) que: usa `credentials: 'include'`; busca `GET /api/auth/antiforgery` na inicialização e depois de login/registro/logout; envia `X-XSRF-TOKEN` (lido do cookie `XSRF-TOKEN`) em todo POST/PUT/PATCH/DELETE.
- Tipos da API **gerados** do OpenAPI (`npm run gen:api` → `src/api/schema.d.ts`), nunca escritos à mão.
- Erros: ProblemDetails `validation.failed` → `setError` nos campos do formulário (chaves camelCase, ex.: `parts.0.name` ← `parts[0].name`); demais erros → mensagem de `detail` num aviso visível; 409 → aviso "Este item foi alterado em outra tela" + recarregar dados; 401 → ir para `/login`.
- Botões de envio ficam desabilitados enquanto a requisição está em andamento.
- Mobile-first: layout de uma coluna até 640 px.

## Review Focus

1. **Sessão expirada no meio do uso** (cookie vencido ou deploy novo antes da persistência de chaves): qualquer 401 leva ao login sem tela branca nem loop. → Task F2, teste de unidade do cliente (`client.test.ts`: resposta 401 dispara `onUnauthorized`).
2. **Token antiforgery desatualizado após login/logout** (o token é vinculado ao usuário): a próxima mutação depois de logar não pode dar 400. → Task F2 (`client.test.ts`: login renova o token antes da próxima mutação).
3. **Rotas profundas recarregadas no navegador** (`/vehicles/<id>` com F5) devem servir o `index.html`, mas `/api/...` inexistente deve continuar devolvendo 404 da API. → Task F1, teste de integração `Spa_fallback_serves_index_but_not_for_api`.
4. **Dados de exemplo chamados duas vezes** ou por usuário que já tem veículos: 409, sem duplicar. → Task F1 (`Demo_seed_twice_returns_409`).
5. **Deploy novo derrubando sessões**: com as chaves do Data Protection em volume, um cookie emitido antes do restart continua válido. → Task F1 (`Data_protection_keys_are_persisted_to_configured_path`) e verificação manual na Task F6.

---

## Estrutura de arquivos

```
backend/src/Garage.Api/
  Http/SpaHosting.cs                   UseSpaStaticFiles / MapSpaFallback (exclui /api, /openapi, /scalar)
  Demo/DemoSeedEndpoint.cs             POST /api/demo/seed
backend/src/Garage.Application/Demo/
  SeedDemoData/SeedDemoDataCommand.cs, SeedDemoDataHandler.cs, DemoErrors.cs, DemoDataCatalog.cs
backend/src/Garage.Infrastructure/
  DataProtectionSetup.cs               PersistKeysToFileSystem(DataProtection:KeysPath)
backend/tests/Garage.Api.IntegrationTests/Demo/DemoSeedTests.cs, Hosting/SpaHostingTests.cs
frontend/
  package.json  vite.config.ts  tsconfig*.json  index.html  .env.example
  src/main.tsx  src/App.tsx  src/routes.tsx  src/index.css
  src/api/client.ts  src/api/schema.d.ts (gerado)  src/api/hooks/*.ts  src/api/problem.ts
  src/auth/AuthProvider.tsx  src/auth/RequireAuth.tsx
  src/lib/format.ts  src/lib/forms.ts
  src/components/{Layout,Button,Field,Card,Tabs,Alert,Empty,Spinner,Money,Km}.tsx
  src/pages/{Login,Register,Dashboard,VehicleNew,VehicleEdit,VehicleDetail,RecordForm,RecordDetail,PublicVehicle,NotFound}.tsx
  src/pages/vehicle/{HistoryTab,RemindersTab,ShareTab,FipePicker,OdometerDialog}.tsx
  src/**/*.test.ts(x)  (Vitest)
backend/Dockerfile                     + estágio Node que gera o build do front em wwwroot
```

---

### Task F1: API — dados de exemplo, SPA no mesmo domínio e chaves persistidas

**Files:**
- Create: `backend/src/Garage.Api/Http/SpaHosting.cs`, `backend/src/Garage.Api/Demo/DemoSeedEndpoint.cs`, `backend/src/Garage.Application/Demo/**`, `backend/src/Garage.Infrastructure/DataProtectionSetup.cs`
- Modify: `backend/src/Garage.Api/Program.cs` (trocar o `MapGet("/")` → `/scalar` pelo hosting da SPA), `backend/src/Garage.Infrastructure/DependencyInjection.cs`
- Test: `backend/tests/Garage.Api.IntegrationTests/Demo/DemoSeedTests.cs`, `backend/tests/Garage.Api.IntegrationTests/Hosting/SpaHostingTests.cs`

**Interfaces:**
- Consumes: casos de uso e repositórios das Tasks 9–13 do Plano 1 (`Vehicle.Create`, `ServiceRecord.Create`, `Reminder.Create`, `ShareLink.Create`, `IUnitOfWork`, `IAppClock`, `ICurrentUser`).
- Produces:
```
POST /api/demo/seed   → 201 { vehicles: int, records: int, reminders: int }
                      → 409 demo.already_has_data   (usuário já tem veículo)
                      → 404 (rota não mapeada) quando Demo:Enabled=false
```
```csharp
public sealed record SeedDemoDataCommand : ICommand<DemoSeedResult>;
public sealed record DemoSeedResult(int Vehicles, int Records, int Reminders);
public static class DemoErrors { public static readonly Error AlreadyHasData; } // demo.already_has_data, Conflict, "Você já tem veículos cadastrados; os dados de exemplo são só para contas vazias."
public static class SpaHosting { IApplicationBuilder UseSpaStaticFiles(this WebApplication app); void MapSpaFallback(this WebApplication app); }
// config: Demo:Enabled (bool, padrão false); DataProtection:KeysPath (string, opcional; quando vazio não persiste — testes)
```
- Dados de exemplo (`DemoDataCatalog`), todos relativos a `clock.Today` e criados pelo domínio (as regras valem):
  - Veículo 1: Honda Civic, versão "Sedan LX 1.6 16V Mec. 4p", geração "EK", 1997/1998, gasolina, apelido "Civic EK", placa "ABC1D23", km atual 152.300.
    - 6 registros entre hoje−700 dias e hoje−10 dias, km crescente: troca de óleo e filtro (manutenção, motor, 2 peças com preço), pastilhas de freio (manutenção, freios), alinhamento e balanceamento (manutenção, rodas/pneus), coilover (modificação, suspensão, "Amortecedores originais guardados"), repintura do para-choque dianteiro (manutenção, carroceria/pintura), troca de correia dentada (manutenção, motor).
    - 3 lembretes: óleo a cada 5.000 km/6 meses (base 147.800 km → **próximo**), correia a cada 50.000 km/48 meses (em dia), fluido de freio a cada 24 meses (base hoje−760 dias → **vencido**).
    - 1 link público ativo sem custos.
  - Veículo 2: Volkswagen Gol, "1.0 MPI Flex 12V 5p", 2018/2019, flex, km 48.000, 2 registros e 1 lembrete.
- `SpaHosting`: arquivos de `wwwroot` com cache longo para `/assets/*`; fallback para `index.html` só quando o caminho **não** começa com `/api`, `/openapi` ou `/scalar` e não tem extensão de arquivo. Sem `wwwroot/index.html` (testes, dev) o fallback não é mapeado.
- `DataProtectionSetup`: `AddDataProtection().SetApplicationName("garage").PersistKeysToFileSystem(new DirectoryInfo(KeysPath))` quando `KeysPath` informado.

- [ ] **Step 1: Escrever os testes que falham**
```csharp
[Fact] Demo_seed_creates_two_vehicles_with_records_and_reminders()   // Demo:Enabled=true; 201 {vehicles:2, records:8, reminders:4}; GET /api/reminders/due tem 1 overdue e 1 dueSoon do Civic
[Fact] Demo_seed_twice_returns_409()                                  // 2ª chamada → 409 demo.already_has_data; ainda 2 veículos
[Fact] Demo_seed_is_404_when_disabled()                               // WithWebHostBuilder(Demo:Enabled=false)
[Fact] Demo_seed_requires_login_and_antiforgery()                     // anônimo → 401; sem header → 400 auth.antiforgery_invalid
[Fact] Spa_fallback_serves_index_but_not_for_api()                    // factory com wwwroot temporário contendo index.html: /vehicles/abc → 200 text/html; /api/naoexiste → 404 sem html
[Fact] Data_protection_keys_are_persisted_to_configured_path()        // KeysPath temporário: após GET /api/auth/antiforgery existe ao menos 1 arquivo key-*.xml
```
- [ ] **Step 2: Rodar e ver falhar** — `dotnet test --filter "DemoSeedTests|SpaHostingTests"` → FAIL.
- [ ] **Step 3: Implementar** catálogo/handler de exemplo, endpoint (`.RequireRateLimiting` não precisa), `SpaHosting`, `DataProtectionSetup`; em `Program.cs` remover o redirect `/` → `/scalar` e chamar `UseSpaStaticFiles()` antes da autenticação e `MapSpaFallback()` depois de `MapEndpoints`.
- [ ] **Step 4: Rodar e ver passar** — suíte inteira PASS, 0 warnings.
- [ ] **Step 5: Commit** — `git commit -m "feat(api): dados de exemplo, SPA no mesmo domínio e chaves do Data Protection persistidas"`

---

### Task F2: Front — base do projeto, cliente da API e autenticação

**Files:**
- Create: `frontend/**` base (Vite React TS), `src/api/client.ts`, `src/api/problem.ts`, `src/api/schema.d.ts` (gerado), `src/auth/*`, `src/components/{Layout,Button,Field,Card,Alert,Spinner}.tsx`, `src/pages/{Login,Register,NotFound}.tsx`, `src/routes.tsx`, `src/lib/format.ts`
- Test: `src/api/client.test.ts`, `src/lib/format.test.ts`

**Interfaces:**
- Consumes: OpenAPI do back (`GET /openapi/v1.json`), endpoints `/api/auth/*`.
- Produces:
```ts
// src/api/client.ts
export const api: Client<paths>;                      // openapi-fetch, baseUrl '' (mesma origem)
export async function ensureAntiforgery(force?: boolean): Promise<void>;
export function onUnauthorized(handler: () => void): void;
// src/api/problem.ts
export type Problem = { status: number; code?: string; detail?: string; errors?: Record<string, string[]>; [k: string]: unknown };
export function applyProblemToForm<T>(problem: Problem, setError: UseFormSetError<T>): string | null; // devolve mensagem geral quando não é de campo
// src/auth/AuthProvider.tsx
export function useAuth(): { user: { id: string; email: string } | null; loading: boolean; login(email: string, password: string): Promise<Problem | null>; register(...): Promise<Problem | null>; logout(): Promise<void>; registrationEnabled: boolean };
// src/lib/format.ts
export const formatKm: (km: number | null | undefined) => string;      // 150000 → "150.000 km"; null → "—"
export const formatMoney: (v: number | null | undefined) => string;    // 291.8 → "R$ 291,80"
export const formatDate: (iso: string) => string;                      // "2026-10-10" → "10/10/2026"
```
- Scripts `package.json`: `dev` (Vite com proxy `/api`, `/openapi`, `/scalar` → `http://localhost:5080`), `build`, `test` (Vitest), `gen:api` (`openapi-typescript ${API_URL:-https://garagem.frauchesgabriel.work}/openapi/v1.json -o src/api/schema.d.ts`), `lint` (tsc --noEmit).
- Rotas: `/login`, `/register` (só se `registrationEnabled`), `/` (protegida), `/p/:token` (pública), `*` → NotFound.

- [ ] **Step 1:** scaffold (`npm create vite@latest frontend -- --template react-ts`), instalar dependências, Tailwind v4 (`@tailwindcss/vite`), gerar `schema.d.ts`.
- [ ] **Step 2: testes que falham**
```ts
test('401 dispara onUnauthorized')                     // fetch mock devolve 401 em GET /api/vehicles → handler chamado 1x
test('mutação envia X-XSRF-TOKEN do cookie')            // document.cookie = 'XSRF-TOKEN=abc' → POST tem header 'abc'
test('login renova o token antes da próxima mutação')   // após login, ensureAntiforgery(true) foi chamado antes do próximo POST
test('formatKm/formatMoney/formatDate em pt-BR')        // valores do bloco acima
```
- [ ] **Step 3:** implementar cliente, provider, páginas de login/cadastro (RHF + Zod: e-mail válido, senha ≥ 10) e layout (cabeçalho com nome do app, e-mail e "Sair").
- [ ] **Step 4:** `npm test` PASS; `npm run lint` e `npm run build` sem erros.
- [ ] **Step 5: Commit** — `git commit -m "feat(front): base do app, cliente da API com antiforgery e autenticação"`

---

### Task F3: Front — painel e veículos (com FIPE)

**Files:**
- Create: `src/api/hooks/{vehicles,reminders,catalog,demo}.ts`, `src/pages/{Dashboard,VehicleNew,VehicleEdit}.tsx`, `src/pages/vehicle/{FipePicker,OdometerDialog}.tsx`, `src/components/{Empty,Money,Km}.tsx`
- Test: `src/pages/vehicle/FipePicker.test.tsx`

**Interfaces:**
- Consumes: `GET/POST /api/vehicles`, `GET/PUT/DELETE /api/vehicles/{id}`, `POST /api/vehicles/{id}/odometer[/correction]`, `GET /api/reminders/due`, `GET /api/catalog/{type}/...`, `POST /api/demo/seed`.
- Produces: hooks TanStack Query `useVehicles()`, `useVehicle(id)`, `useDueReminders()`, `useCreateVehicle()`, `useUpdateVehicle()`, `useDeleteVehicle()`, `useUpdateOdometer()`, `useCorrectOdometer()`, `useCatalog*()`, `useSeedDemo()`; chave de cache por recurso (`['vehicles']`, `['vehicle', id]`, `['reminders','due']`).
- **Painel:** cards dos veículos (apelido ou marca/modelo/ano, placa, km) + lista "Atenção" com lembretes vencidos (vermelho) e próximos (âmbar). Conta sem veículos: estado vazio com "Cadastrar veículo" e "Carregar dados de exemplo".
- **Novo veículo:** passo a passo tipo → marca → modelo → ano, cada passo com busca por texto; ao escolher o ano, preenche marca, modelo sugerido, versão sugerida, ano-modelo e combustível (editáveis) e guarda `catalog {type, fipeCode, fipeYearCode}`. Botão "Não achei, preencher manualmente" em todos os passos; resposta 503 do catálogo abre direto o modo manual com aviso.
- **Editar veículo:** mesmo formulário sem o passo da FIPE; envia `rowVersion`.

- [ ] **Step 1: teste que falha** — `FipePicker`: com catálogo mockado, escolher Honda → "Civic Sedan LX 1.6 16V Mec. 4p" → "1998 Gasolina" chama `onPick({ make:'Honda', model:'Civic', version:'Sedan LX 1.6 16V Mec. 4p', modelYear:1998, fuel:'gasoline', catalog:{type:'car', fipeCode:'005340-6', fipeYearCode:'1998-1'} })`; catálogo com 503 chama `onManual()`.
- [ ] **Step 2:** implementar hooks e páginas.
- [ ] **Step 3:** testes, lint e build OK.
- [ ] **Step 4: Commit** — `git commit -m "feat(front): painel, cadastro de veículos com FIPE e km"`

---

### Task F4: Front — histórico, registros e anexos

**Files:**
- Create: `src/api/hooks/{records,attachments}.ts`, `src/pages/VehicleDetail.tsx`, `src/pages/vehicle/HistoryTab.tsx`, `src/pages/{RecordForm,RecordDetail}.tsx`, `src/components/Tabs.tsx`
- Test: `src/pages/RecordForm.test.tsx`

**Interfaces:**
- Consumes: `/api/vehicles/{id}/records*`, `/api/vehicles/{id}/records/{rid}/attachments`, `/api/vehicles/{id}/attachments/{aid}[/content]`, `GET /api/vehicles/{id}/reminders`, `GET /api/vehicles/{id}/fipe-price`.
- **Detalhe do veículo:** cabeçalho com km atual + "Atualizar km" (dialog, e link "corrigir km digitada errado" para a correção), valor FIPE quando vinculado; abas Histórico / Lembretes / Compartilhar (as duas últimas na Task F5).
- **Histórico:** filtros tipo e categoria; lista por data desc com título, data, km, categoria, total e contador de anexos; "Carregar mais" usando `nextCursor`.
- **Formulário de registro:** tipo, data (padrão hoje), km, categoria, título, descrição, quem fez, mão de obra, peças em linhas dinâmicas (nome, marca, part number, qtd, unidade, preço unitário), "peça original substituída" só para modificação, "cumpre lembretes" (checkbox por lembrete do veículo, só na criação). Erro `record.odometer_out_of_order` mostra a faixa válida no campo km usando `maxKmBefore`/`minKmAfter`.
- **Detalhe do registro:** dados, peças com subtotal, total; galeria de anexos (fotos em miniatura, documentos como link); upload com escolha Foto/Documento e dois botões ("Tirar foto" com `capture="environment"`, "Escolher arquivo"); alternar "visível no link público" nas fotos; excluir anexo e registro com confirmação. O front recusa arquivos acima de 10 MB antes de enviar e envia vários anexos **em sequência**, nunca em paralelo (rate limit `uploads` e limite de 20 por registro).

- [ ] **Step 1: teste que falha** — `RecordForm`: adicionar 2 peças e enviar monta `parts` na ordem com números (não strings); erro 422 `record.odometer_out_of_order` com `{maxKmBefore:80000, minKmAfter:90000}` exibe "Entre 80.000 e 90.000 km" no campo km; "peça original substituída" some quando o tipo é manutenção.
- [ ] **Step 2:** implementar.
- [ ] **Step 3:** testes, lint e build OK.
- [ ] **Step 4: Commit** — `git commit -m "feat(front): histórico, registros com peças e anexos"`

---

### Task F5: Front — lembretes, links e página pública

**Files:**
- Create: `src/api/hooks/{sharing,public}.ts`, `src/pages/vehicle/{RemindersTab,ShareTab}.tsx`, `src/pages/PublicVehicle.tsx`
- Test: `src/pages/vehicle/RemindersTab.test.tsx`

**Interfaces:**
- Consumes: `/api/vehicles/{id}/reminders*`, `/api/vehicles/{id}/share-links*`, `GET /api/public/{token}`, `GET /api/public/{token}/attachments/{aid}`.
- **Lembretes:** lista com selo de estado (Em dia/Próximo/Vencido), "faltam X km / Y dias" e aviso "km atualizada em dd/MM" quando `odometerUpdatedAt` tem mais de 30 dias; criar/editar (título, intervalo km e/ou meses, base data/km), excluir.
- **Compartilhar:** criar link (rótulo, mostrar custos, mostrar placa, validade opcional), lista com status, "Copiar link" (`navigator.clipboard`, URL absoluta `location.origin + path`), revogar com confirmação.
- **Página pública `/p/:token`:** sem layout autenticado; veículo, registros com peças, fotos públicas; custos/placa só quando vierem; "Carregar mais" por `nextCursor`; 404 → "Link não encontrado ou expirado"; `<meta name="robots" content="noindex">`.

- [ ] **Step 1: teste que falha** — `RemindersTab`: lembrete `overdue` mostra "Vencido" e "há 10 dias" para `daysRemaining:-10`; `dueSoon` com `kmRemaining:500` mostra "Faltam 500 km".
- [ ] **Step 2:** implementar.
- [ ] **Step 3:** testes, lint e build OK.
- [ ] **Step 4: Commit** — `git commit -m "feat(front): lembretes, links de compartilhamento e página pública"`

---

### Task F6: Publicação — imagem única, volumes e configuração no Coolify

**Files:**
- Modify: `backend/Dockerfile` (estágio `frontend` com `node:24-alpine`: `npm ci` + `npm run build`; copiar `dist/` para `/app/wwwroot`); o contexto de build do Coolify passa a ser a raiz do repositório (`base_directory=/`, `dockerfile_location=/backend/Dockerfile`), então os `COPY` do back ganham o prefixo `backend/`.
- Create: `.dockerignore` na raiz (substitui o de `backend/`), `frontend/.env.example`
- Modify: `backend/README.md` (como rodar front + back juntos localmente), spec §12 (trocar Caddy/deploy.sh por Coolify)

**Configuração no Coolify (via API, feita pelo controlador):**
- App `garage-api`: `base_directory=/`, `dockerfile_location=/backend/Dockerfile`.
- Volumes persistentes: `/data/keys` (Data Protection) e `/data/attachments` (anexos), graváveis pelo usuário não-root do container (`$APP_UID`, 1654) — criar os diretórios no Dockerfile com `chown` antes do `USER`.
- Variáveis: `DataProtection__KeysPath=/data/keys`, `Storage__RootPath=/data/attachments`, `Demo__Enabled=true`, `Auth__AllowRegistration=true` (desligar depois que o Gabriel criar a conta).

- [ ] **Step 1:** ajustar Dockerfile/.dockerignore e validar com `docker build -f backend/Dockerfile .` + smoke local (Postgres 18 + imagem): `/` devolve o HTML do app, `/vehicles/x` devolve o HTML, `/api/health` 200, `/scalar` 200.
- [ ] **Step 2:** commit — `git commit -m "build: imagem única com o front servido pela API"`
- [ ] **Step 3 (controlador):** atualizar o app no Coolify, criar volumes e variáveis, fazer push e acompanhar o deploy.
- [ ] **Step 4 (controlador):** verificação no domínio: cadastro → carregar exemplos → painel com 2 veículos e alertas → abrir o Civic → registro com foto → link público em janela anônima; redeploy e conferir que a sessão continua válida (chaves persistidas).
