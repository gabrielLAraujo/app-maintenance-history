// Gera src/api/schema.d.ts a partir do OpenAPI da API (npm run gen:api).
// Padrão: API local em http://localhost:5081 (a produção só tem os endpoints novos depois do deploy).
// Outra origem: API_URL=https://... npm run gen:api (no PowerShell: $env:API_URL='https://...'; npm run gen:api).
import { writeFile } from 'node:fs/promises'
import openapiTS, { astToString } from 'openapi-typescript'

const apiUrl = process.env.API_URL ?? 'http://localhost:5081'
const output = new URL('../src/api/schema.d.ts', import.meta.url)

const header = `/**
 * Gerado por "npm run gen:api" a partir de /openapi/v1.json da API.
 * Não edite à mão: rode o comando de novo quando a API mudar.
 */

`

const ast = await openapiTS(new URL('/openapi/v1.json', apiUrl))
await writeFile(output, header + astToString(ast))

console.log(`schema.d.ts gerado a partir de ${apiUrl}`)
