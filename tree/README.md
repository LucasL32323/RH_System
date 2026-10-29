# HR-Auto

Aplicação automatizada de Recursos Humanos — TCC Técnico em Desenvolvimento de Sistemas, SENAI Santos (2026).

## Estrutura

```
HR-Auto/
├── backend/                 # C# / .NET 8 — Clean Architecture
│   ├── HRAuto.sln
│   ├── src/
│   │   ├── HRAuto.Domain/          # entidades (25 tabelas), value objects, regras puras
│   │   ├── HRAuto.Application/     # casos de uso e interfaces (IHashSenha, IRelogio...)
│   │   ├── HRAuto.Infrastructure/  # EF Core + SQLite, migrations, seed, BCrypt, relógio
│   │   └── HRAuto.Api/             # controllers, Swagger, JWT (Fase 4)
│   └── tests/HRAuto.Tests/         # xUnit (unidade, banco e integração)
├── hr_auto_login/           # Flutter (Android, iOS e Web) — MVVM com Riverpod
├── _legado/                 # fundação Flutter de 15/10 (substituída por hr_auto_login), só consulta
├── database/                # modelo_fisico_sqlite.sql (25 tabelas) — referência oficial do banco
└── scripts/
```

Regra de dependência (back-end): `Api → Infrastructure → Application → Domain`. O Domain não conhece ninguém.

## Banco de dados (Fase 2)

O modelo oficial é `database/modelo_fisico_sqlite.sql` (25 tabelas). O back-end o mapeia com EF Core
(`HrAutoDbContext`) e o banco é criado **só pelas migrations** (RNF15).

### Primeira vez: gerar a migration inicial (uma única vez)

```bash
cd backend
dotnet tool install --global dotnet-ef --version 8.0.10   # se ainda não tiver
dotnet restore
dotnet ef migrations add ModeloFisicoInicial --project src/HRAuto.Infrastructure --output-dir Persistencia/Migrations
```

Faça o commit da pasta `Persistencia/Migrations`. Depois disso, a API aplica as migrations sozinha ao subir.
Para recriar o banco do zero: apague `hrauto.db` (em `backend/src/HRAuto.Api`) e suba a API de novo.

Os testes `EsquemaDoBancoTests` comparam o banco gerado pelas migrations com o SQL oficial
(tabelas, colunas, tipos, chaves primárias e estrangeiras). Se algum falhar, o mapeamento divergiu do modelo.

### Dados iniciais (seed)

Rodam a cada subida da API e são idempotentes:

- Perfis fixos: `1 = Colaborador`, `2 = RH`, `3 = Admin` (iguais ao enum `PerfilAcesso`).
- 5 cargos e 5 departamentos de exemplo.
- **Somente em Development**: administrador de TESTE `admin@hrauto.local` / `Admin@123` (código FUNC-0001).
  Controlado por `Seed:CriarAdminDeTeste` (desligado no `appsettings.json`). Nunca ligue em produção.

### Alterações em relação ao SQL oficial

Não há tabelas nem colunas novas. Mudanças de integridade, todas em índices e nulidade:

- Índices únicos novos: `USUARIO.email` (sem diferenciar maiúsculas), `PONTO(id_funcionario, data)` (RNF09; substitui o
  `idx_ponto_func_data`) e `CERTIFICADO.codigo_validacao` (RF39).
- Colunas que o SQL deixava opcionais mas o negócio exige (ex.: `nome`, `email`, `ativo`, datas de admissão) são `NOT NULL` no EF.
- Datas `DateOnly` = `AAAA-MM-DD`; horários do ponto = `HH:MM:SS`; data e hora = `AAAA-MM-DDTHH:MM:SS` (Brasília).

## Regras de negócio (Fase 3)

Ficam em `backend/src/HRAuto.Domain`, sem dependências externas, e são testadas em `backend/tests/HRAuto.Tests/Domain`.

| Pasta (namespace) | O que faz |
|---|---|
| `Entities/Ponto.cs` | Sequência das 4 batidas, bloqueio de batida fora de ordem/repetida, sincronização offline sem duplicar |
| `Jornada/` | Jornada padrão, cálculo de horas/extras/atrasos, feriados e férias, banco de horas, manutenção de ponto |
| `Afastamentos/` | Solicitação e decisão de férias (regras da CLT) |
| `Disciplina/` | Advertências, semáforo (verde/amarelo/vermelho) e régua de consequências |
| `Comunicacao/` | Notificações geradas pelas regras |

Parâmetros iniciais da jornada (alterar em `JornadaPadrao`): segunda a sexta, entrada às 08:00, 8 h de trabalho, tolerância de 10 min por dia, alerta de hora extra acima de 2 h.

## Back-end

Requisito: .NET SDK 8.

```bash
cd backend
dotnet restore
dotnet build
dotnet test
dotnet run --project src/HRAuto.Api --launch-profile https
```

- Swagger: https://localhost:7080/swagger
- Health check: https://localhost:7080/health
- Status: https://localhost:7080/api/v1/sistema/status

Na primeira vez: `dotnet dev-certs https --trust`.

### API e autenticação (Fase 4)

Login com e-mail (ou, desde 28/10, o código `FUNC-0001`, no campo `login`) e senha em `POST /api/v1/auth/login`; a resposta traz o token JWT (8 h) que o app envia em
`Authorization: Bearer <token>`. Todas as rotas exigem login, exceto login, esqueci/redefinir senha, `/health` e `/api/v1/sistema/status`.

```bash
# Em Development (admin de teste): pegar o token e consultar quem sou
TOKEN=$(curl -sk https://localhost:7080/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"admin@hrauto.local","senha":"Admin@123"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["token"])')
curl -sk https://localhost:7080/api/v1/eu -H "Authorization: Bearer $TOKEN"
curl -sk -X POST https://localhost:7080/api/v1/ponto/batidas -H "Authorization: Bearer $TOKEN"
curl -sk "https://localhost:7080/api/v1/ponto/espelho?de=2026-10-01&ate=2026-10-21" -H "Authorization: Bearer $TOKEN"
```

Configuração (appsettings ou variáveis de ambiente, com `__` no lugar de `:`):

| Chave | Para quê |
|---|---|
| `Jwt:Chave` | Segredo de assinatura, 32+ caracteres. **Obrigatória**; em produção só por variável de ambiente (`Jwt__Chave`). |
| `App:UrlRedefinicaoSenha` | URL da tela de redefinição de senha do app (vai no e-mail). Obrigatória. |
| `Email:Host`, `Email:Porta`, `Email:Usuario`, `Email:Senha` | SMTP. Sem `Host`, o e-mail só é registrado no log. |
| `RateLimit:AutenticacaoLimite` / `AutenticacaoJanelaSegundos` | Tentativas por IP nos endpoints anônimos (padrão 10 por 60 s). |
| `Cors:Origens` | Origens permitidas em produção. Vazio = só localhost. |

Erros sempre vêm em `application/problem+json` (`title`, `detail`, `status`).

#### Cadastro, acessos e disciplina (Fase 4, parte 2)

```
GET    /api/v1/funcionarios            lista (RH/Admin; gestor vê a equipe) — ?codigo=FUNC-0001
POST   /api/v1/funcionarios            cadastra (RH/Admin) + convite por e-mail
GET    /api/v1/funcionarios/{codigo}   ficha
PUT    /api/v1/funcionarios/{codigo}   atualiza
POST   /api/v1/funcionarios/{codigo}/desligamento | /reativacao
GET|POST|PATCH /api/v1/funcionarios/{codigo}/acesso     POST .../acesso/convite
GET|POST|PUT|DELETE /api/v1/cargos[/{id}]  e  /api/v1/departamentos[/{id}]
GET|POST /api/v1/advertencias    POST /advertencias/{id}/cancelamento
GET /api/v1/advertencias/situacao[/{codigo}]   semáforo + histórico
```

## Front-end

Requisito: Flutter 3.22+ (Dart 3.4+). O app fica em `hr_auto_login/`.

```bash
cd hr_auto_login
flutter pub get
flutter analyze
flutter test
flutter run -d chrome                                  # modo protótipo (dados em memória, sem API)
flutter run -d chrome --dart-define=PROTOTIPO=false    # API real em http://localhost:5080
```

Para usar a API real, suba o back-end com o perfil `http` (porta 5080):
`dotnet run --project src/HRAuto.Api --launch-profile http`. O `web_dev_config.yaml` repassa `/api` para a porta 5080
(app em http://localhost:5173, sem depender de CORS). Com o perfil `https`, use `--dart-define=API_URL=https://localhost:7080`.

No emulador Android use `--dart-define=API_URL=http://10.0.2.2:5080`.

## Convenções

- Commits por funcionalidade, mensagens em português no imperativo (ex.: `Adiciona registro de ponto`).
- Código do funcionário: `FUNC-0001` (derivado de `id_funcionario`); busca sempre pelo código.
- Paleta: `#1E3A8A` (principal), `#3B82F6` (destaque), `#93C5FD` (apoio), fundo `#F8FAFC`, texto `#0F172A`; fonte Inter.
- Perfis: Colaborador, RH, Admin. "Gestor" é quem aparece como `id_gestor` de outros funcionários.
