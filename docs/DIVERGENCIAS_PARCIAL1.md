# Divergências: documento da Entrega Parcial 1 x projeto atual

Levantado em 23/10/2026 para alinhar o documento antes da Entrega Parcial 2 (04/11).
Fontes: `claude/hr-auto/RHSystem_modelo_fisico_mysql.sql` (Parcial 1), `database/modelo_fisico_sqlite.sql` e a migration
`ModeloFisicoInicial` (projeto atual, 25 tabelas, conferida pelo teste `EsquemaDoBancoTests`).

## 1. Banco de dados

| # | Parcial 1 (documento) | Projeto atual | Ajuste no documento |
|---|---|---|---|
| 1 | MySQL 8 (InnoDB, utf8mb4), script `RHSystem_Criacao_Tabelas_MySQL.sql` + triggers | SQLite (`hrauto.db`), criado pelas migrations do EF Core 8; sem triggers (regras no Domain em C#) | Trocar o SGBD, o script de criação e retirar a seção de triggers |
| 2 | 22 tabelas | 25 tabelas | Atualizar a contagem e o DER |
| 3 | Nomes no plural/maiúsculas (`FUNCIONARIOS`, `SETORES`, `AVISOS`) | Singular, colunas em `snake_case` (`FUNCIONARIO`, `DEPARTAMENTO`, `AVISO`) | Renomear no DER e no dicionário de dados |
| 4 | Tabela `GESTORES` (e `SETORES.ID_GESTOR`) | Removida: gestor é o funcionário apontado por `FUNCIONARIO.id_gestor` (autorrelacionamento) | Tirar `GESTORES` do DER e desenhar o autorrelacionamento |
| 5 | `NIVEIS_DE_ACESSO`, ligado a `FUNCIONARIOS.ID_NV_ACESSO` | `PERFIL`, ligado a `USUARIO.id_perfil` (o perfil é da conta, não do funcionário) | Mover a FK no DER |
| 6 | `PONTO` com `HORA_ENTRADA`, `HORA_SAIDA`, `HORAS_TRABALHADAS`, `CARGA_HORARIA` | `PONTO` com 4 batidas (`entrada`, `saida_intervalo`, `retorno_intervalo`, `saida`); horas, extras e atrasos são calculados, não gravados | Atualizar colunas e explicar o cálculo (jornada 8 h, tolerância 10 min) |
| 7 | `FUNCIONARIOS_DETALHES` repetia colunas de `FUNCIONARIOS` | `FUNCIONARIO_DETALHES` 1:1 (PK = `id_funcionario`) com CPF, RG, PIS, CTPS e endereço | Atualizar colunas e citar a LGPD (dados pessoais separados) |
| 8 | `USUARIO` com `NOME` e `SENHA` | Sem `nome` (vem de `FUNCIONARIO`); `senha` guarda hash BCrypt | Atualizar colunas e citar o hash |
| 9 | `MANUTENCAO_PONTO.ID_GESTOR` | `id_responsavel` (FK para `FUNCIONARIO`) | Renomear |
| 10 | `AVISOS_VISUALIZACOES`, `TREINAMENTO_CERTIFICADO`, `FORMULARIO_PERGUNTAS`, `FORMULARIO_RESPOSTAS`, `BENEFICIOS`, `BENEFICIOS_FUNCIONARIOS` | `AVISO_FUNCIONARIO`, `CERTIFICADO`, `PERGUNTA`, `RESPOSTA`, `BENEFICIO`, `FUNCIONARIO_BENEFICIO` | Renomear; o script MySQL ainda tinha colunas com espaço/acento (`ID_FUNCIONARIO (AUTOR)`, `OBRIGATÓRIA`) e `ID_PERGUNTA_PERGUNTA` |
| 11 | Não existiam | 4 tabelas novas: `LOG_AUDITORIA` (RF08/LGPD), `FERIADO`, `FERIAS`, `NOTIFICACAO` | Incluir no DER e no dicionário |
| 12 | Só os índices auxiliares | + 3 índices únicos (e-mail do usuário, ponto por funcionário/dia, código do certificado) e colunas essenciais `NOT NULL` no EF | Citar como restrições de integridade |
| 13 | Matrícula/código não definido | Código `FUNC-0001` derivado de `id_funcionario` (sem coluna própria) | Explicar a regra (RN10) |

Conta: 22 − 1 (`GESTORES`) + 4 novas = **25 tabelas**.

## 2. Requisitos, perfis e regras

| # | Parcial 1 | Projeto atual | Ajuste no documento |
|---|---|---|---|
| 14 | 21 requisitos funcionais | 51 RF e 17 RNF | Atualizar a lista e a matriz de rastreabilidade |
| 15 | 4 perfis: Colaborador, Gestor, RH, Admin | 3 perfis na API (Colaborador, RH, Admin); "gestor" é quem tem subordinados (`id_gestor`) | Confirmar com o grupo. O protótipo Flutter ainda mostra 4 níveis (inclui Gestor) |
| 16 | Login por e-mail ou usuário | API autentica só por e-mail; o app avisa se digitarem `FUNC-0001` | Confirmar se o login por código entra na Parcial 3 |
| 17 | Cadastro público na tela de login | Não há autocadastro: o RH cria a conta e a pessoa recebe convite por e-mail (30 min) | Ajustar o fluxo e a tela de cadastro |
| 18 | Recuperação de senha sem detalhe | Token assinado de 30 min, uso único, sem tabela nova | Descrever o fluxo |

## 3. Arquitetura e interface

| # | Parcial 1 | Projeto atual | Ajuste no documento |
|---|---|---|---|
| 19 | Back-end .NET sem camadas definidas | .NET 8, Clean Architecture (Domain, Application, Infrastructure, Api), JWT HS256 de 8 h, Swagger, ProblemDetails, limite de tentativas | Atualizar o diagrama de arquitetura |
| 20 | Paleta azul (`#1E3A8A`) | Paleta teal do Figma: `#0D5C63`, `#247B7B`, `#44A1A0`, `#78CDD7`, fundo `#FFFFFA` (RNF11) | Atualizar prints e guia visual |
| 21 | Front-end sem integração | Flutter (Riverpod/MVVM) com login e registro de ponto ligados à API; demais abas do Admin com dados de exemplo | Descrever o que é real e o que é protótipo na demo |

## 4. Repositório Git

- O `.gitignore` da raiz e o de `hr_auto_login/` ignoram `build/`, `.dart_tool/`, `bin/`, `obj/`, `*.db` e segredos; nenhuma saída de build entra no Git.
- O repositório remoto `xmiguelps/RH-System` hoje só tem a pasta de documentação (DER, SQL MySQL, requisitos, Figma); nenhum código nem `build/` versionado.
- O Project do Claude ainda guarda `hr_auto_login/build/` (saída do `flutter build web`); pode ser apagado de lá sem perder código.
