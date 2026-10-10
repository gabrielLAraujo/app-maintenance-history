#!/bin/sh
set -e

# Com argumentos (ex.: reset-password <email>), roda o comando administrativo e sai.
if [ "$#" -gt 0 ]; then
  exec dotnet /app/Garage.Api.dll "$@"
fi

if [ "${MIGRATE_ON_START:-true}" = "true" ]; then
  echo "Aplicando migrations..."
  /app/efbundle --connection "$ConnectionStrings__Garage"
fi

exec dotnet /app/Garage.Api.dll