# HR-Auto — Protótipo de Login e Cadastro (Flutter)

Primeiras telas do app: **Login** e **Cadastro (Sign Up)** no mesmo card, com transição animada entre elas,
"Esqueci minha senha" e uma tela provisória pós-login. Layout mobile-first que vira tela dividida no desktop.

## Como rodar

Requisito: Flutter 3.22+ (Dart 3.4+).

```bash
cd hr_auto_login
flutter create --platforms=android,ios,web,windows --project-name hr_auto --org br.senai.hrauto .
flutter pub get
flutter analyze
flutter test
flutter run -d chrome                       # modo protótipo (sem back-end)
```

O `flutter create .` só cria as pastas de plataforma; não sobrescreve `lib/`, `test/` nem o `pubspec.yaml`.

### Modo protótipo x API real

| Modo | Comando | Comportamento |
|---|---|---|
| Protótipo (padrão) | `flutter run -d chrome` | Contas em memória. Conta de teste: `admin@hrauto.local` / `Admin@123`. Cadastro funciona até fechar o app. |
| API .NET (Fase 4) | `flutter run -d chrome --dart-define=PROTOTIPO=false` | Login em `POST /api/v1/auth/login`, recuperação em `POST /api/v1/auth/esqueci-senha` e **registro de ponto** em `/api/v1/ponto` (hoje, espelho e batidas). Usa `http://localhost:5080` (perfil `http` da API). |

Para o perfil `https` da API, acrescente `--dart-define=API_URL=https://localhost:7080` (exige o certificado de desenvolvimento confiável).
No emulador Android use `API_URL=http://10.0.2.2:5080`.

### Integração com a API (Entrega Parcial 2)

1. Suba a API: `cd backend && dotnet run --project src/HRAuto.Api --launch-profile http` (cria o `hrauto.db` e o admin de teste).
2. Rode o app: `flutter run -d chrome --dart-define=PROTOTIPO=false` (chama a API direto, com CORS liberado para localhost)
   ou, pelo proxy do `web_dev_config.yaml` (mesma origem, sem CORS):
   `flutter run -d web-server --dart-define=PROTOTIPO=false --dart-define=API_URL=http://localhost:5173` e abra http://localhost:5173.
   No Windows, `etapa3_subir_app.cmd` (raiz do repositório) faz isso.
3. Entre com `admin@hrauto.local` / `Admin@123` e abra **Registro de ponto** no menu.

| Tela | Com `PROTOTIPO=false` |
|---|---|
| Login e "Esqueci minha senha" | API real (`AuthRepositoryApi`) |
| Registro de ponto: batida, batidas de hoje, espelho da semana, saldo do banco de horas | API real (`PontoRepositoryApi`), horário do servidor (RF07) |
| Solicitar ajuste de ponto | Ainda sem endpoint: mostra o aviso "entra na próxima etapa" |
| Dashboard, Funcionários, Compliance, Advertências, Configurações | Dados de exemplo (`AdminRepositoryPrototipo`) até os endpoints de RH serem ligados |

## O que foi implementado

**Login:** E-mail ou usuário, Senha (mostrar/ocultar), "Esqueci minha senha" (diálogo), "Entrar" com estado de carregamento
e link "Não tem uma conta? Cadastre-se". Erro da API aparece em um banner no card; em erro, a senha é limpa.

**Cadastro:** Nome completo, E-mail, Senha, Confirmação de senha e "Criar conta". A senha mostra barra de força e
checklist (8+ caracteres, maiúscula, minúscula, número, símbolo) com as **mesmas regras da `PoliticaSenha` do back-end**.
Conta criada → volta ao login com o e-mail preenchido.

**Alternância:** `AnimatedSwitcher` (fade + deslize) + `AnimatedSize` (a altura do card se ajusta), sem trocar de rota.

**Responsivo:**

| Largura | Layout |
|---|---|
| < 960 px (celular/tablet) | Faixa em gradiente no topo, logo e card centralizado (até 460 px) |
| ≥ 960 px (desktop/web) | Painel da marca à esquerda (5/11) e card centralizado à direita (6/11) |

## Área do Administrador

Entrar com a conta de teste (`admin@hrauto.local` / `Admin@123`, perfil **Admin**) abre a área do administrador,
no modelo dos protótipos do Figma (*HR-Auto | Protótipos*): **menu lateral só com ícones, uma aba por vez e o mesmo
layout no celular e no computador** (RNF08). O nome de cada aba aparece no tooltip. Os demais perfis continuam na tela provisória.

| Aba | O que tem | Requisitos |
|---|---|---|
| Dashboard | Colaboradores ativos, absenteísmo, horas extras, ajustes de ponto pendentes (aprovar/recusar/desfazer), atrasos x horas extras por setor, semáforo de advertências, termômetro de clima, banco de horas e alertas | RF10, RF21, RN01, RN05, RN08 |
| Registro de ponto | Batida em um toque na sequência obrigatória, batidas do dia, espelho da semana com saldo e pedido de ajuste com justificativa | RF07, RF09, RF10, RF11, RN02, RN04 |
| Funcionários | Busca pelo código FUNC (não pelo nome), filtros por setor e situação, CPF mascarado com consulta registrada, inativar/reativar, paginação | RF04, RF05, RF22, RN09 |
| Novo funcionário | Identificação, acesso (perfil + convite), vínculo (cargo com carga horária, setor/gestor, admissão) e residência; código FUNC gerado e imutável | RF04, RF05, RF06, RN10 |
| Central de Compliance | Leituras obrigatórias com confirmação, testes obrigatórios, perfil DISC, vitrine de benefícios e "Publicar aviso" | RF15, RF16, RF18, RF20 |
| Gestão de advertências | Indicadores, semáforo por colaborador, motivos com gravidade, régua de consequências e aprovação de suspensões | RF13, RF14, RN06, RN07 |
| Configurações | Preferências da conta, níveis de acesso, cargos e setores (adicionar), registro LGPD e canal com o RH (melhoria futura) | RF03, RF06, RF22 |

Os dados vêm de `AdminRepositoryPrototipo`: 248 colaboradores ativos + 4 inativos gerados de forma determinística,
então os números do dashboard batem com a lista de funcionários. Para ligar na API, basta implementar
`AdminRepository` e trocar o `adminRepositoryProvider`.

Fora do escopo desta versão (conforme o documento da Entrega Parcial 1): cálculo da folha, controle de férias,
Kudos e chat com o RH.

## Arquitetura (Clean Architecture + MVVM com Riverpod)

```
lib/
├── main.dart / app.dart                  # ProviderScope, tema e troca Login <-> Área Admin <-> Home pela sessão
├── core/
│   ├── config/app_config.dart            # PROTOTIPO, API_URL (--dart-define)
│   ├── errors/falha.dart                 # erro com mensagem pronta para o usuário
│   ├── formatting/                       # formatadores pt-BR (datas, CPF, horas) e máscaras de digitação
│   ├── network/api_client.dart           # HTTP + leitura de ProblemDetails da API
│   ├── theme/app_colors.dart, app_theme.dart
│   └── validation/validadores.dart       # espelho de EmailUsuario.cs e PoliticaSenha.cs
├── domain/
│   ├── entities/usuario_autenticado.dart # LoginRespostaDto
│   ├── entities/                         # funcionário, estrutura, ponto, advertência, compliance, dashboard
│   └── repositories/                     # contratos: auth_repository, admin_repository, ponto_repository
├── data/repositories/
│   ├── auth_repository_api.dart          # API .NET real
│   ├── auth_repository_prototipo.dart    # em memória
│   ├── admin_repository_prototipo.dart   # dados de exemplo das abas do Admin (e ponto no protótipo)
│   └── ponto_repository_api.dart         # ponto real: /api/v1/ponto/hoje, /espelho e POST /batidas
└── presentation/
    ├── providers.dart                    # injeção (repositórios, sessão)
    ├── viewmodels/                       # Login, Cadastro, RecuperarSenha, Dashboard, Funcionários, Ponto, Advertências, Compliance
    ├── pages/                            # AuthPage, HomePage (provisória) e admin/ (AdminPage + 7 abas)
    └── widgets/                          # formulários, campos, card, painel da marca, diálogo
        └── admin/                        # menu de ícones, barra superior, KPI, tabelas, painéis, campos
```

A View só observa o estado do ViewModel; o ViewModel só conhece o contrato `AuthRepository`.
Trocar protótipo por API é uma linha em `providers.dart` (ou o `--dart-define`).

## Paleta

| Cor | Uso |
|---|---|
| `#FFFFFA` | Fundo geral |
| `#0D5C63` | Primária: botões, links, foco dos campos (texto branco, contraste ≈ 7,6:1) |
| `#247B7B` | Secundária: fim dos gradientes |
| `#44A1A0` | Detalhes e ícone do logo |
| `#78CDD7` | Decoração (círculos, ícones no painel) |

Fonte: Inter (`google_fonts`). Cantos: 12 px nos campos/botões, 20 px nos cards.

## Pendências para o grupo decidir

1. **Cadastro na API:** a Fase 4 não tem endpoint público de autocadastro (no HR-Auto o RH cria a conta).
   Com `PROTOTIPO=false`, "Criar conta" mostra essa mensagem. Opções: convite enviado pelo RH ou `POST /api/v1/auth/cadastro`.
2. **"Usuário" no login:** a API autentica só por e-mail. Com a API ligada, digitar `FUNC-0001` mostra o aviso para usar o e-mail
   (sem gastar tentativa do limite de login). Para aceitar o código, a API precisa de ajuste.
3. **Paleta:** substitui a azul usada na Fase 1 (`#1E3A8A`). Atualizar o `app_colors.dart` do `frontend/` ao integrar.
4. **Token:** fica só em memória neste protótipo; armazenamento seguro e renovação entram com o dashboard.
