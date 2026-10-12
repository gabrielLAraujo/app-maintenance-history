# syntax=docker/dockerfile:1

# Imagem única: a API ASP.NET serve o front (build do Vite) no mesmo domínio.
# O contexto de build é a RAIZ do repositório (Coolify: base_directory=/, dockerfile_location=/backend/Dockerfile):
#   docker build -f backend/Dockerfile -t garage .

# ---- frontend ----
FROM node:24-alpine AS frontend
WORKDIR /src/frontend

COPY frontend/package.json frontend/package-lock.json ./
# --include=dev: o tsc e o Vite são devDependencies; sem isso um NODE_ENV=production herdado do ambiente
# (o Coolify repassa as variáveis do app como build args) instalaria só as dependências de runtime e o build quebraria.
RUN npm ci --include=dev --no-fund --no-audit

COPY frontend/ ./
RUN npm run build

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/global.json backend/Directory.Build.props backend/Garage.slnx ./
COPY backend/.config/dotnet-tools.json .config/
COPY backend/src/Garage.Domain/Garage.Domain.csproj src/Garage.Domain/
COPY backend/src/Garage.Application/Garage.Application.csproj src/Garage.Application/
COPY backend/src/Garage.Infrastructure/Garage.Infrastructure.csproj src/Garage.Infrastructure/
COPY backend/src/Garage.Api/Garage.Api.csproj src/Garage.Api/
RUN dotnet tool restore && dotnet restore src/Garage.Api/Garage.Api.csproj

COPY backend/src/ src/
RUN dotnet publish src/Garage.Api/Garage.Api.csproj -c Release -o /app/publish --no-restore
# Bundle de migrations do EF Core: aplicado pelo entrypoint antes de subir a API
RUN dotnet ef migrations bundle \
      --project src/Garage.Infrastructure \
      --startup-project src/Garage.Api \
      --configuration Release \
      --output /app/efbundle --force

# ---- runtime ----
# A API precisa de ICU e tzdata: App:TimeZone (America/Sao_Paulo) e validado ao subir, e os precos da FIPE sao lidos com
# a cultura pt-BR. A tag padrao aspnet:10.0 ja traz os dois; nao troque por uma variante alpine ou chiseled sem ICU/tzdata
# (sem tzdata a API nao sobe; sem ICU a traducao da FIPE falha).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# curl para o health check do Coolify
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# Pastas dos volumes do Coolify (DataProtection__KeysPath e Storage__RootPath): criadas e entregues ao usuario nao-root
# ANTES do USER, para que o volume nomeado novo herde o dono (Docker copia dono e permissoes da imagem na 1a montagem)
# e a API consiga gravar as chaves e os anexos ja no primeiro start. Ficam antes dos COPY: mudar o codigo nao refaz
# esta camada.
RUN mkdir -p /data/keys /data/attachments \
 && chown -R "$APP_UID:$APP_UID" /data

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build --chmod=755 /app/efbundle ./efbundle
COPY --chmod=755 backend/docker-entrypoint.sh ./docker-entrypoint.sh
# O front compilado vai para a webroot da API (SpaHosting serve os arquivos e cai no index.html nas rotas do app)
COPY --from=frontend /src/frontend/dist ./wwwroot

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["/app/docker-entrypoint.sh"]
