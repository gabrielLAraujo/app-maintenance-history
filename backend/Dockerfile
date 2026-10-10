# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Garage.slnx ./
COPY .config/dotnet-tools.json .config/
COPY src/Garage.Domain/Garage.Domain.csproj src/Garage.Domain/
COPY src/Garage.Application/Garage.Application.csproj src/Garage.Application/
COPY src/Garage.Infrastructure/Garage.Infrastructure.csproj src/Garage.Infrastructure/
COPY src/Garage.Api/Garage.Api.csproj src/Garage.Api/
RUN dotnet tool restore && dotnet restore src/Garage.Api/Garage.Api.csproj

COPY src/ src/
RUN dotnet publish src/Garage.Api/Garage.Api.csproj -c Release -o /app/publish --no-restore
# Bundle de migrations do EF Core: aplicado pelo entrypoint antes de subir a API
RUN dotnet ef migrations bundle \
      --project src/Garage.Infrastructure \
      --startup-project src/Garage.Api \
      --configuration Release \
      --output /app/efbundle --force

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# curl para o health check do Coolify; ICU e tzdata ja vem na imagem padrao (America/Sao_Paulo)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build --chmod=755 /app/efbundle ./efbundle
COPY --chmod=755 docker-entrypoint.sh ./docker-entrypoint.sh

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["/app/docker-entrypoint.sh"]