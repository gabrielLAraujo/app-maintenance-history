# Garage — back-end (.NET 10)

API do Garage (histórico de manutenção do carro). O README completo (arquitetura, testes e operação) é da Task T15 do
Plano 1; por ora, aqui só como rodar front e back juntos na sua máquina. A arquitetura está em
[`docs/ARQUITETURA.md`](../docs/ARQUITETURA.md).

## Rodar front + back localmente

Pré-requisitos: .NET 10 SDK, Node 24 e Docker (para o Postgres 18 e para os testes de integração).

### 1. Banco

```powershell
docker run -d --name garage-db -e POSTGRES_USER=garage -e POSTGRES_PASSWORD=garage_dev -e POSTGRES_DB=garage -p 5432:5432 postgres:18
```

### 2. Configuração da API (uma vez)

O `appsettings.Development.json` já aponta para `localhost:5432` e liga os dados de exemplo (`Demo:Enabled`), mas não traz
a senha do banco, a pasta dos anexos nem libera o cadastro de conta. Guarde isso nos user-secrets (ficam fora do git):

```powershell
cd backend
dotnet user-secrets set "ConnectionStrings:Garage" "Host=localhost;Port=5432;Database=garage;Username=garage;Password=garage_dev" --project src/Garage.Api
dotnet user-secrets set "Storage:RootPath" "C:\temp\garage-attachments" --project src/Garage.Api   # ex.: /tmp/garage-attachments no Linux/macOS
dotnet user-secrets set "Auth:AllowRegistration" "true" --project src/Garage.Api
```

### 3. Migrations

```powershell
dotnet tool restore
dotnet ef database update --project src/Garage.Infrastructure --startup-project src/Garage.Api
```

### 4. API (terminal 1)

```powershell
dotnet run --project src/Garage.Api --launch-profile http
```

Sobe em <http://localhost:5081>. A documentação interativa fica em `/scalar` e o OpenAPI em `/openapi/v1.json`
(só fora de produção).

### 5. Front (terminal 2)

```powershell
cd frontend
npm ci
npm run dev
```

Abre em <http://localhost:5173>. O Vite faz proxy de `/api`, `/openapi` e `/scalar` para a API, então o navegador fala
com uma origem só e o cookie de sessão e o antiforgery funcionam como em produção. Crie a conta na tela de cadastro e,
se quiser, carregue os dados de exemplo.

### Tipos da API

Mudou um endpoint? Com a API rodando (passo 4, com o cadastro liberado), `npm run gen:api` em `frontend/` regenera
`src/api/schema.d.ts`. Para outra origem, defina `API_URL` (veja `frontend/.env.example`).

### Reset de senha

```powershell
dotnet run --project src/Garage.Api -- reset-password <email>
```

Na imagem publicada: `/app/docker-entrypoint.sh reset-password <email>` (terminal do contêiner no Coolify).

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
