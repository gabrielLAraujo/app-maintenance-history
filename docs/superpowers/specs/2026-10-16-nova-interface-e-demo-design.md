# Nova interface e demo por visitante — Design

- **Data:** 2026-10-16
- **Status:** implementada na branch `feat/nova-interface` (PR aguardando a revisão do Gabriel)
- **Relação com o roadmap:** melhoria transversal entre a Fase 1 (concluída) e a Fase 2. Não implementa nada da Fase 2 do roadmap (roadmap de modificações, colaboradores, PDF).
- **Referência visual:** canvas "Garagem — nova interface" (artefato privado no claude.ai), aprovado em 16/10/2026: landing, painel, página do veículo, link público e direção visual.

---

## 1. Contexto e objetivo

O Garagem funciona, mas parece um projeto interno: coluna estreita no desktop, sem identidade visual, sem fotos nos veículos e sem nenhum resumo de gastos. E quem chega pelo LinkedIn cai num login com cadastro fechado, sem ver nada.

Objetivo: o app passa a ter cara de produto, e um visitante entra numa demonstração sem cadastro.

### 1.1 O que foi decidido pelo Gabriel
- Direção visual "Ficha técnica" e as telas do canvas de referência.
- Demo com uma conta por visitante, apagada depois de 24 horas (escolhida entre: conta por visitante, conta compartilhada resetada todo dia, conta compartilhada só leitura).
- Gastos calculados por consultas no backend, não no front.
- Foto de capa escolhida entre as fotos já anexadas ao veículo, sem fluxo de upload próprio.
- Menu lateral com Painel, Veículos e Lembretes; sem página global de gastos.
- Fontes servidas pelo próprio app; sem dark mode por enquanto.
- Entrega por branch e PR; nada vai para a `main` nem para o Coolify sem a aprovação dele.

### 1.2 Critérios de sucesso
1. Na landing, "Ver demo" leva ao painel já preenchido com os dados de exemplo, sem cadastro.
2. Duas demos abertas ao mesmo tempo não enxergam os dados uma da outra.
3. Uma conta demo e os arquivos dela deixam de existir no máximo 25 horas depois de criados (24 h de validade + 1 h do intervalo da limpeza).
4. As telas seguem o canvas de referência (seção 2), em desktop e celular.
5. O link do app compartilhado no LinkedIn mostra título, descrição e imagem de preview.
6. Todos os testes atuais continuam verdes e os comportamentos novos têm testes (seção 10).
7. Lighthouse da landing no celular: acessibilidade ≥ 95 e performance ≥ 90.
8. O cadastro continua fechado em produção.

### 1.3 Fora do escopo
Dark mode; página global de gastos; cadastro aberto; dossiê em PDF; upload dedicado de capa; idiomas além do pt-BR; tudo das fases 2 e 3 do roadmap.

---

## 2. Direção visual

Grafite e sinal âmbar, números em mono: um painel de instrumentos com a clareza de uma ficha de oficina. Os valores abaixo são os tokens do tema (`@theme` do Tailwind v4 em `frontend/src/index.css`).

### 2.1 Cores

| Token | Valor | Uso |
|---|---|---|
| `ink` | `#14161B` | Texto principal, barra lateral, botão escuro |
| `ink-2` | `#3A3F4B` | Texto secundário |
| `muted` | `#5D6472` | Legendas (contraste ≥ 4,5:1 sobre `ground`) |
| `line` | `#E3E5EA` | Bordas e divisórias |
| `ground` | `#F3F4F6` | Fundo das telas |
| `surface` | `#FFFFFF` | Cartões e cabeçalhos |
| `amber` | `#F5A524` | Ação principal, sempre com texto `ink` |
| `amber-ink` | `#8A5100` | Âmbar em texto; estado "próximo" |
| `amber-soft` | `#FFF4DC` | Avisos, faixa da demo, ícones de recurso |
| `danger` | `#B42318` (fundo `#FEF3F2`, borda `#FECDCA`) | Lembrete vencido |
| `ok` | `#067647` (fundo `#ECFDF3`, borda `#ABEFC6`) | Lembrete em dia, confirmações |
| `plate` | `#1E3A8A` (fundo `#E8EEFF`) | Modificações e faixa da placa |

Estados que precisam ser distinguidos (vencido, próximo, em dia) diferem também em claridade e sempre trazem o texto do estado, nunca só a cor.

### 2.2 Tipografia

| Estilo | Fonte | Uso |
|---|---|---|
| Display | Archivo 800, largura 112–118% | Títulos de página e da landing |
| Título | Archivo 700, 17–20 px | Títulos de cartão e seção |
| Corpo | Archivo 400, 15–16 px | Texto corrido |
| Números | IBM Plex Mono 600 | Km, valores, datas, placa |
| Rótulo | IBM Plex Mono 500, 13 px, caixa alta | Sobretítulos |

Fontes empacotadas pelo build (pacotes Fontsource: Archivo variável com os eixos de peso e largura; IBM Plex Mono 400/500/600). Nenhuma requisição ao Google Fonts.

### 2.3 Forma
Raios 6 (placa, badges), 10 (controles), 16 (cartões), 20 (capas). Espaços 4 · 8 · 12 · 16 · 24 · 32 · 48 · 72. Alvos de toque ≥ 44 px. Placa no padrão Mercosul (faixa azul "BRASIL" decorativa, `aria-hidden`).

---

## 3. Demo por visitante

### 3.1 Fluxo

1. A landing (e a tela de login) mostram "Ver demo" quando `GET /auth/config` devolve `demoEnabled: true`.
2. O front busca o token de antiforgery (fluxo atual) e chama `POST /api/auth/demo`, sem sessão.
3. A API:
   1. aplica o rate limit `Demo` (seção 9.3);
   2. conta as contas demo ainda válidas; com `Demo:Visitors:MaxActive` atingido, responde 503 `demo.unavailable`;
   3. cria um usuário com `IsDemo = true`, `DemoExpiresAt = agora + Demo:Visitors:TtlHours`, e-mail `demo-{id}@demo.garagem.invalid` e **sem senha** (ninguém consegue entrar nessa conta por login);
   4. semeia os dados de exemplo atuais (Civic com capa, Gol) para esse usuário, na mesma transação lógica do seed de hoje;
   5. emite o cookie de sessão com a claim `garage:demo=true` e validade absoluta igual a `DemoExpiresAt` (sem renovação deslizante além disso);
   6. responde 201 `{ expiresAt }`.
4. O front recarrega a sessão (`/auth/me`) e navega para `/`.

Se a semeadura falhar, a conta criada é apagada antes da resposta (nenhuma conta demo vazia fica para trás).

### 3.2 Restrições da conta demo

| Ação | Comportamento |
|---|---|
| Upload de anexo | 403 `demo.uploads_disabled` (as fotos de exemplo já estão lá) |
| Criar o 6º veículo | 422 `demo.vehicle_limit_reached` (limite `Demo:Visitors:MaxVehicles`, padrão 5) |
| Criar link público | Permitido; o link deixa de funcionar quando a conta é apagada |
| Sair (`POST /auth/logout`) | A conta é marcada como vencida (`DemoExpiresAt = agora`) e sai na próxima limpeza |
| Resetar senha pela CLI | Recusado para conta demo |

`ICurrentUser` ganha `IsDemo`, lido da claim. `GET /auth/me` passa a devolver `isDemo` e `demoExpiresAt`.

### 3.3 Limpeza

Serviço hospedado `DemoCleanupService` (`BackgroundService`), a cada `Demo:Visitors:CleanupIntervalMinutes` (padrão 60):

1. busca até 50 contas demo com `DemoExpiresAt <= agora`;
2. para cada conta: lê as chaves dos arquivos dos anexos dos veículos dela, apaga o usuário (veículos, registros, peças, lembretes, anexos e links saem em cascata pelo banco) e, **depois do commit**, apaga os arquivos, um por vez (a falha de um é registrada em log e não impede os outros);
3. repete enquanto houver lote cheio; registra em log quantas contas e arquivos saíram.

É idempotente: rodar duas vezes ou cair no meio não deixa linhas órfãs; no pior caso sobra arquivo sem linha, como já acontece hoje se a exclusão de arquivo de um veículo falhar.

### 3.4 Configuração

| Chave | Padrão | Produção |
|---|---|---|
| `Demo:Visitors:Enabled` | `false` | `true` |
| `Demo:Visitors:TtlHours` | `24` | |
| `Demo:Visitors:MaxActive` | `100` | |
| `Demo:Visitors:MaxVehicles` | `5` | |
| `Demo:Visitors:CleanupIntervalMinutes` | `60` | |
| `RateLimits:DemoPerHour` | `5` (por IP) | |

Com `Enabled=false`, `POST /auth/demo` nem é mapeado (404) e o serviço de limpeza não sobe. `Demo:Enabled` (o botão "Carregar dados de exemplo" de contas reais) continua independente e inalterado.

### 3.5 Onde fica cada parte
- **Domain:** nada novo (a conta é da identidade, não do domínio).
- **Application:** o seed atual é extraído para um `DemoDataSeeder` interno que recebe o `ownerId`; o comando existente (`SeedDemoDataCommand`) e o novo fluxo usam o mesmo seeder. A limpeza não passa pela Application: é infraestrutura.
- **Infrastructure:** `AppUser` ganha `IsDemo` e `DemoExpiresAt` (migração); `DemoAccountService` (criar, contar ativas, listar e apagar vencidas); `DemoCleanupService`.
- **Api:** `DemoSessionEndpoint` (`POST /auth/demo`), política de rate limit `Demo`, `demoEnabled` no `/auth/config`, `isDemo`/`demoExpiresAt` no `/auth/me`, filtro que aplica as restrições da seção 3.2.

---

## 4. Foto de capa

### 4.1 Regra
- `Vehicle.CoverPhotoId` (`Guid?`).
- **C1:** a capa precisa ser um anexo do tipo Foto do próprio veículo; senão 422 `vehicle.cover_must_be_vehicle_photo`.
- Apagar o anexo que é capa limpa a capa (chave estrangeira com `ON DELETE SET NULL`).
- Trocar a visibilidade da foto não muda a capa do dono; muda só o que o link público mostra (4.3).

### 4.2 API
`PUT /vehicles/{id}/cover-photo` com `{ attachmentId: Guid | null }` → 204. `null` remove a capa. Passa por `IVehicleAccess` antes de tudo (sem acesso = 404).

### 4.3 Leitura
- `VehicleDto` e `VehicleSummaryDto` ganham `coverPhotoId`; o front monta a URL com o endpoint de conteúdo que já existe.
- `PublicVehicleDto` ganha `coverPhoto: PublicPhotoDto | null`, preenchido **só se a foto for pública**. Mantém a regra de allowlist da visão pública.
- O seed de exemplo define a foto do Civic (`civic-lx-frente.jpg`) como capa.

### 4.4 Front
No detalhe do registro, cada foto ganha a ação "Usar como capa" (e "Remover capa" na foto que é a capa). Sem capa, o card e a página do veículo mostram "Adicionar foto de capa", que leva ao histórico.

---

## 5. Resumos de gastos

### 5.1 Regras
- Custo de um registro = `TotalCost` (mão de obra + soma das peças com preço). Registro sem custo conta 0 nos totais e conta normalmente nas quantidades.
- "Ano" e "mês" seguem o fuso `App:TimeZone`, a partir de `IAppClock.Today`.
- Custo por km = custo total ÷ km acompanhada, em que km acompanhada = km atual − menor km entre os registros do veículo. Só é calculado com km acompanhada ≥ 1.000; abaixo disso volta `null` (o número seria enganoso).

### 5.2 `GET /vehicles/{id}/summary`

| Campo | Conteúdo |
|---|---|
| `totalCost` | Soma de todos os registros |
| `maintenance` | `{ cost, count }` dos registros de Manutenção |
| `modification` | `{ cost, count }` dos registros de Modificação |
| `firstRecordDate` | Data do registro mais antigo, ou `null` |
| `kmTracked` | Conforme 5.1, ou `null` sem registro com km |
| `costPerKm` | Conforme 5.1, ou `null` |
| `byCategory` | `[{ category, cost }]` das categorias com custo > 0, da maior para a menor |
| `byMonth` | Últimos 12 meses, incluindo o atual: `[{ month: "2026-10", cost }]`, meses sem gasto com 0 |
| `lastRecord` | `{ id, title, date }` do registro mais recente, ou `null` |

### 5.3 `GET /garage/summary`

| Campo | Conteúdo |
|---|---|
| `year` | Ano corrente |
| `yearCost`, `yearRecordCount`, `yearVehicleCount` | Gasto, registros e veículos com registro no ano |
| `byMonth` | Janeiro a dezembro do ano corrente, `[{ month, cost }]` |
| `recentRecords` | Os 4 registros mais recentes de todos os veículos: `{ id, vehicleId, vehicleName, title, date, totalCost }` |
| `recordCount`, `firstRecordDate` | Total de registros e data do mais antigo |

Considera só os veículos do usuário (`OwnerId`), como `GET /vehicles`.

### 5.4 Lista de veículos
`VehicleSummaryDto` ganha `coverPhotoId`, `totalCost` e `lastRecordDate`, para os cards.

### 5.5 Implementação
Consultas na Application sobre `IReadDbContext`: carregam os registros com peças dos veículos em questão e agregam em memória. O volume é de uso pessoal (centenas de registros por usuário). **Trade-off aceito:** se o volume crescer, a agregação vai para SQL sem mudar o contrato.

---

## 6. Front-end

### 6.1 Rotas

| Rota | Tela |
|---|---|
| `/` | Visitante: **landing**. Logado: **painel** |
| `/login` | Login no visual novo, com "Ver demo" |
| `/register` | Inalterada no comportamento (só com cadastro aberto) |
| `/vehicles` | Lista de veículos em cards com capa |
| `/reminders` | Todos os lembretes de todos os veículos, agrupados em Vencidos, Próximos e Em dia (usa o novo `GET /reminders`; `GET /reminders/due` continua só com vencidos e próximos, para o painel) |
| `/vehicles/new`, `/vehicles/:id/edit` | Visual novo, mesmo fluxo |
| `/vehicles/:id` | Capa, km, indicadores e abas **Histórico**, **Lembretes**, **Gastos**, **Compartilhar** (`?tab=` como hoje) |
| `/vehicles/:id/records/...` | Visual novo; "Usar como capa" no detalhe |
| `/p/:token` | Dossiê público (capa, destaques, o que o link não mostra, histórico) |
| `*` | Não encontrado, no visual novo |

### 6.2 Estrutura
- **AppShell:** barra lateral grafite a partir de 1024 px (Painel, Veículos, Lembretes com contador de vencidos; e-mail e Sair no rodapé); abaixo disso, barra superior com menu em gaveta.
- **DemoBanner:** faixa âmbar no topo das telas logadas quando `isDemo`: "Você está na demonstração. Pode mexer à vontade: esta conta é apagada em X h." com link para o código.
- **Landing:** seções do canvas (hero com "Ver demo", recursos, link para venda, "Por dentro" com números do projeto e links para o código e a arquitetura, chamada final, rodapé com créditos das fotos).

### 6.3 Componentes
Refeitos com os tokens: `Button` (primário, secundário, escuro, fantasma), `Card`, `Badge` (estado e tipo), `PlateChip`, `Kpi`, `ProgressBar`, `MonthBars` (gráfico de barras simples com texto equivalente para leitor de tela), `CategoryBars`, `Timeline`, `EmptyState`, `Skeleton`. Os componentes atuais (`Field`, `Modal`, `Tabs`, `Alert`…) mantêm a API e mudam o visual.

### 6.4 Erros do "Ver demo"
- 429: "Muitas demonstrações criadas a partir deste endereço. Tente de novo em alguns minutos."
- 503 `demo.unavailable`: "A demonstração está cheia agora. Tente de novo mais tarde."
- Outros: mensagem do ProblemDetails, como no resto do app.

### 6.5 Preview e metadados
`index.html`: título "Garagem — o histórico do seu carro num só lugar", `meta description`, Open Graph (`og:title`, `og:description`, `og:image` em `/og-image.png`, 1200×630, gerada a partir do hero do canvas) e `twitter:card=summary_large_image`. A página pública continua com `noindex`.

---

## 7. API — o que muda

| Método e rota | Mudança |
|---|---|
| `GET /auth/config` | + `demoEnabled` |
| `GET /auth/me` | + `isDemo`, `demoExpiresAt` |
| `POST /auth/demo` | Novo (seção 3); só mapeado com `Demo:Visitors:Enabled=true` |
| `GET /vehicles` | + `coverPhotoId`, `totalCost`, `lastRecordDate` nos itens |
| `GET /vehicles/{id}` | + `coverPhotoId` |
| `PUT /vehicles/{id}/cover-photo` | Novo (seção 4) |
| `GET /vehicles/{id}/summary` | Novo (seção 5.2) |
| `GET /garage/summary` | Novo (seção 5.3) |
| `GET /reminders` | Novo: todos os lembretes dos veículos do usuário, com status, na mesma forma de `GET /reminders/due` (que segue sem os "em dia") |
| `GET /public/{token}` | + `coverPhoto` (só foto pública) |
| `POST .../attachments` | Conta demo: 403 `demo.uploads_disabled` |
| `POST /vehicles` | Conta demo: limite de veículos |

Tipos do front regenerados do OpenAPI (`npm run gen:api`).

---

## 8. Erros novos

| Código | Tipo | HTTP |
|---|---|---|
| `demo.unavailable` | Unavailable | 503 |
| `demo.uploads_disabled` | Forbidden (novo tipo) | 403 |
| `demo.vehicle_limit_reached` | DomainRule | 422 |
| `vehicle.cover_must_be_vehicle_photo` | DomainRule | 422 |

`Forbidden` entra no mapeamento de tipos (seção 9.2 da spec da Fase 1). Continua valendo "sem acesso = 404": 403 é só para ação proibida a uma conta que tem acesso ao recurso.

---

## 9. Segurança

### 9.1 Conta demo
- Sem senha: a sessão é a única forma de entrar, e expira junto com a conta.
- Teto de contas ativas e rate limit por IP limitam abuso e uso de disco (cada demo copia cerca de 4 MB de fotos de exemplo; com o teto padrão, no máximo cerca de 400 MB).
- Upload bloqueado: a demo não serve de hospedagem de arquivos.
- Links públicos criados na demo morrem com a conta.
- O e-mail da conta usa o domínio reservado `.invalid`: nunca é endereço real.

### 9.2 Capa
`PUT /cover-photo` passa por `IVehicleAccess`; o anexo é buscado por `vehicle_id` **e** `id` (IDOR). A visão pública só mostra a capa se a foto for pública.

### 9.3 Rate limiting

| Alvo | Limite |
|---|---|
| `POST /auth/demo` | 5/hora por IP (`RateLimits:DemoPerHour`) |
| `GET /garage/summary`, `GET /vehicles/{id}/summary` | Sem limite próprio (leitura autenticada, como as demais) |

---

## 10. Testes

| Projeto | O que cobre |
|---|---|
| `Garage.Domain.Tests` | C1 (capa de outro veículo, anexo que não é foto, `null` remove) |
| `Garage.Api.IntegrationTests` | Resumos: totais, manutenção x modificação, categorias, meses com zero, custo/km nulo abaixo de 1.000 km, fuso do ano, usuário B não vê o resumo de A (404). `GET /reminders` traz também os "em dia". `POST /auth/demo`: cria conta isolada e já logada; duas demos não se veem; sem senha (login falha); teto ativo → 503; rate limit → 429; desligado → 404. Conta demo: upload → 403, 6º veículo → 422, logout marca vencida. Limpeza: conta vencida some com veículos, registros e arquivos; conta válida fica. Capa: dono define e remove; usuário B recebe 404; anexo de outro veículo → 422; apagar a foto limpa a capa; link público só mostra capa pública. `/auth/config` e `/auth/me` com os campos novos |
| `Garage.ArchitectureTests` | Inalterado (as regras atuais cobrem as peças novas) |
| Front (Vitest + Testing Library) | Testes atuais adaptados ao markup novo; landing com e sem `demoEnabled`; "Ver demo" (sucesso, 429, 503); `/` alterna landing e painel; painel e aba Gastos com os resumos; `/reminders` agrupado; "Usar como capa"; faixa da demo |

---

## 11. Entrega

1. Branch `feat/nova-interface`; um commit por tarefa do plano.
2. README em inglês (`README.md`) com prints das telas novas; versão em português em `README.pt-BR.md`. A seção "Como este projeto foi construído" ganha esta etapa.
3. PR para a `main` com o resumo, os prints e a lista de variáveis novas do Coolify.
4. Depois da aprovação do Gabriel: merge, `Demo__Visitors__Enabled=true` no Coolify, deploy, conferência em produção (criar demo, checar limpeza no log no dia seguinte).

---

## 12. Decisões e trade-offs

| Decisão | Alternativa descartada | Motivo |
|---|---|---|
| Conta por visitante | Conta compartilhada resetada | Um visitante não vê o que outro escreveu |
| Conta demo sem senha | Senha gerada | Não há o que vazar nem como reaproveitar a conta |
| Agregação em memória | SQL agregado | Volume pessoal; contrato permite trocar depois |
| Capa entre fotos existentes | Upload próprio de capa | Reaproveita o fluxo de anexos e a regra de privacidade |
| Fontes empacotadas | Google Fonts | Sem requisição a terceiros; combina com `Referrer-Policy: no-referrer` |
| Seed copiado por conta | Arquivos compartilhados entre demos | Simples e isolado; o teto limita o disco |
