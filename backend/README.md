# Garage — back-end (.NET 10)

API do Garage (histórico de manutenção do carro): Clean Architecture em .NET 10, Minimal APIs, EF Core + PostgreSQL 18.
Em produção ela também serve o front React (mesma origem). Como o código se organiza, as regras de negócio e a
segurança estão em [`docs/ARQUITETURA.md`](../docs/ARQUITETURA.md); este arquivo cobre como rodar, testar e operar.

## Pré-requisitos

- **.NET 10 SDK** (`global.json` pede 10.0.100 ou mais novo da mesma faixa).
- **Docker** ligado: o Postgres 18 local e os testes de integração (Testcontainers) dependem dele.
- **Node 24**, só para rodar o front (`frontend/`).
- O `dotnet ef` é uma **ferramenta local** (`backend/.config/dotnet-tools.json`): `dotnet tool restore` uma vez.

Os comandos abaixo partem da pasta `backend/`.

## Testes

```powershell
dotnet build            # 0 avisos: o build trata aviso como erro (Directory.Build.props)
dotnet test --nologo    # precisa do Docker ligado
```

São quatro projetos em `tests/`:

| Projeto | O que cobre |
|---|---|
| `Garage.Domain.Tests` | Regras puras do domínio |
| `Garage.Api.IntegrationTests` | A API inteira contra um `postgres:18` descartável (Testcontainers), incluindo o contrato OpenAPI |
| `Garage.Infrastructure.Tests` | Relógio, disco, cliente e tradutor da FIPE |
| `Garage.ArchitectureTests` | Fronteiras entre as camadas (NetArchTest) |

Na primeira execução o Docker baixa a imagem `postgres:18`. Para rodar só um grupo:
`dotnet test --nologo --filter OpenApiContractTests`. Os testes não usam o banco local nem a FIPE real: sobem o
próprio container e trocam a FIPE por uma falsa.

## Rodar front + back localmente

### 1. Banco

```powershell
docker run -d --name garage-db -e POSTGRES_USER=garage -e POSTGRES_PASSWORD=garage_dev -e POSTGRES_DB=garage -p 5432:5432 postgres:18
```

### 2. Configuração da API (uma vez)

O `appsettings.Development.json` já aponta para `localhost:5432` e liga os dados de exemplo (`Demo:Enabled`), mas não traz
a senha do banco, a pasta dos anexos nem libera o cadastro de conta. Guarde isso nos user-secrets (ficam fora do git):

```powershell
dotnet user-secrets set "ConnectionStrings:Garage" "Host=localhost;Port=5432;Database=garage;Username=garage;Password=garage_dev" --project src/Garage.Api
dotnet user-secrets set "Storage:RootPath" "C:\temp\garage-attachments" --project src/Garage.Api   # ex.: /tmp/garage-attachments no Linux/macOS
dotnet user-secrets set "Auth:AllowRegistration" "true" --project src/Garage.Api
```

Todas as chaves aceitas estão em [Configurações](#configurações).

### 3. Migrations

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Garage.Infrastructure --startup-project src/Garage.Api
```

Em desenvolvimento a API **não** aplica migrations sozinha (na imagem publicada, o entrypoint aplica). Sem este passo o
primeiro cadastro dá 500. Num banco novo o EF registra um erro `Failed executing DbCommand ... __EFMigrationsHistory`
antes de criar as tabelas: é esperado, a tabela de histórico ainda não existe, e o comando termina com `Done.`.

### 4. API (terminal 1)

```powershell
dotnet run --project src/Garage.Api --launch-profile http
```

Sobe em <http://localhost:5081> (ambiente Development):

| Endereço | O que é |
|---|---|
| `/scalar` | Documentação interativa (Scalar) |
| `/openapi/v1.json` | Contrato OpenAPI; não precisa de banco para responder |
| `/api/health`, `/api/health/ready` | Liveness e verificação do banco |

O `/scalar` e o `/openapi` só existem fora de produção.

Para chamar rotas de escrita pelo Scalar ou por qualquer cliente HTTP:

1. `GET /api/auth/antiforgery` grava o cookie `XSRF-TOKEN`; mande o valor dele no cabeçalho `X-XSRF-TOKEN` em todo
   `POST`, `PUT`, `PATCH` e `DELETE`. Depois de login ou cadastro, busque o token de novo (ele é vinculado ao usuário).
2. Os cookies `garage_auth` (sessão) e `XSRF-TOKEN` são `Secure` e `SameSite=Strict`. Os navegadores os aceitam em
   `http://localhost`, mas um cliente que guarda cookies sozinho pode não devolvê-los por HTTP (o `CookieContainer` do
   .NET, por exemplo): nesse caso mande o cabeçalho `Cookie` você mesmo.
3. A senha precisa de 10 caracteres ou mais. O login é limitado a 5 tentativas por minuto por IP (429
   `rate_limit.exceeded`) e 5 senhas erradas bloqueiam a conta por 15 minutos.

### 5. Front (terminal 2)

```powershell
cd frontend
npm ci
npm run dev
```

Abre em <http://localhost:5173>. O Vite faz proxy de `/api`, `/openapi` e `/scalar` para a API, então o navegador fala
com uma origem só e o cookie de sessão e o antiforgery funcionam como em produção. Crie a conta na tela de cadastro e,
se quiser, carregue os dados de exemplo (`POST /api/demo/seed`: só para contas sem veículos; com veículos responde 409
`demo.already_has_data`).

### Tipos da API

Mudou um endpoint? Com a API rodando (passo 4, com o cadastro liberado), `npm run gen:api` em `frontend/` regenera
`src/api/schema.d.ts`. Para outra origem, defina `API_URL` (veja `frontend/.env.example`).

### Limpeza

```powershell
docker rm -f garage-db     # apaga o banco local junto
```

## Reset de senha

Não há e-mail no MVP: o comando gera uma senha temporária e a imprime. Ele roda e sai, sem subir o servidor.

```powershell
dotnet run --project src/Garage.Api -- reset-password <email>
```

A saída é `Senha temporária: ...` (código de saída 0); e-mail desconhecido imprime `Usuário não encontrado.` e sai com
código 1. Em produção (imagem publicada), pelo terminal do contêiner no Coolify ou de fora dele:

```bash
docker exec <container> /app/docker-entrypoint.sh reset-password <email>
```

## Configurações

Vêm do `appsettings*.json`, dos user-secrets (desenvolvimento) ou de variáveis de ambiente, onde `:` vira `__`
(`Fipe:BaseUrl` → `Fipe__BaseUrl`). Sem a chave, vale o padrão da tabela.

| Chave | Padrão | Para que serve |
|---|---|---|
| `ConnectionStrings:Garage` | — (o `appsettings.Development.json` traz host, banco e usuário, sem senha) | Conexão com o PostgreSQL |
| `App:TimeZone` | `America/Sao_Paulo` | Fuso (id IANA) que define o "hoje" da aplicação |
| `Auth:AllowRegistration` | `false` | `true` cria a rota `POST /api/auth/register`; desligada, ela nem existe (404). Ligue só para criar a conta |
| `Demo:Enabled` | `false` (`true` no `appsettings.Development.json`) | `true` cria a rota `POST /api/demo/seed` (dados de exemplo) |
| `DataProtection:KeysPath` | vazio | Pasta das chaves do Data Protection. Vazia, as chaves não são persistidas e cada deploy derruba as sessões. Na imagem: `/data/keys` |
| `Storage:RootPath` | vazio | Pasta dos arquivos dos anexos. Vazia, gravar ou ler anexo falha (o resto da API segue). Na imagem: `/data/attachments` |
| `Fipe:BaseUrl` | `https://fipe.parallelum.com.br/api/v2/` | URL base da API da FIPE |
| `Fipe:Token` | vazio | Token de assinatura da FIPE (cabeçalho `X-Subscription-Token`): sem ele a cota é de 500 consultas por dia; com o token gratuito, 1.000 |
| `RateLimits:LoginPerMinute` | `5` | `POST /api/auth/login`, por IP |
| `RateLimits:PublicPerMinute` | `60` | `/api/public/*`, por IP |
| `RateLimits:CatalogPerMinute` | `30` | `/api/catalog/*`, por usuário |
| `RateLimits:UploadsPerMinute` | `30` | Upload de anexos, por usuário |

Só no contêiner: `MIGRATE_ON_START` (`true` por padrão; `false` pula as migrations do entrypoint).

## Contrato OpenAPI

`/openapi/v1.json` é a fonte dos tipos do front (`npm run gen:api`). Todo endpoint declara `.WithName`, `.WithTags`,
`.Produces<T>()` do sucesso e `.ProducesProblem()` de cada erro possível. `OpenApiContractTests` garante, a cada
`dotnet test`, que:

- toda operação tem resposta 2xx; as 200/201 trazem `application/json` com schema e as 204 não têm corpo (os endpoints
  que entregam o arquivo de um anexo, `…/content` e `/api/public/{token}/attachments/{id}`, declaram só o tipo de mídia);
- todo `operationId` existe e é único, e toda operação tem tag;
- as rotas de cada funcionalidade estão no documento;
- o fallback da SPA não aparece nele: todas as rotas do documento ficam sob `/api`.

Endpoint novo sem metadado reprova esse teste. Os erros são `application/problem+json` (RFC 9457) com `code`, `detail`
e `traceId`; a validação traz também `errors` por campo.

## Imagem única (front dentro da API)

É a mesma imagem que o Coolify constrói: um estágio Node compila o front e a API o serve de `wwwroot`. O contexto de
build é a **raiz do repositório**:

```powershell
docker build -f backend/Dockerfile -t garage-local .
```

Variáveis de ambiente do contêiner: `ConnectionStrings__Garage`, `DataProtection__KeysPath` (`/data/keys`),
`Storage__RootPath` (`/data/attachments`), `Demo__Enabled` e `Auth__AllowRegistration`. Os dois diretórios `/data/...`
existem na imagem e pertencem ao usuário não-root; monte volumes neles para que chaves (sessão) e anexos sobrevivam aos
deploys. O entrypoint aplica as migrations antes de subir a API (`MIGRATE_ON_START=false` desliga). Detalhes de
publicação: seção 12 da spec em `docs/superpowers/specs/`.
