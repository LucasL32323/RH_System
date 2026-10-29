# HR-Auto — pacote v3 (28/10/2026)

Montado a partir do `HR_AUTO.zip` (v2) e dos documentos diários do Project. Não substitui o projeto do seu PC (`D:\TCC\rh_System`, com os 699 testes passando desde 23/10): serve para levar ao PC tudo o que foi gerado de 24 a 28/10 de uma vez, com os patches já aplicados.

## O que mudou em relação à v2

**Back-end**
- Remontado em ordem cronológica com os 12 documentos (15, 16, 20, 21, 22, 23 completo, 24, 25, 26, 27, 27-perfis e 28/10). Na v2 faltavam os dias 21, 22 e 24, e o 23 vinha sem os testes; por isso o back-end não compilava.
- Patches aplicados:
  - `Cenario.cs`: os 2 trechos de 24/10 e o B1 de 28/10. Os 4 trechos de 22/10 já vinham no `Cenario.cs` completo de 23/10.
  - B2: comentário do `AuthController`.
  - `.gitignore`: entrou `dados/`, a pasta dos PDFs de holerite.
  - README: entraram as seções de 20, 21 e 22/10, e a parte de front-end agora aponta para `hr_auto_login`.
- Igual ao que foi feito no seu PC em 23/10: perfil `http` (porta 5080) no `launchSettings.json` e `global.json` com `rollForward: latestMajor`. Essa opção permite compilar com o SDK .NET 10.

**Flutter**
- Entraram as áreas do Colaborador e do RH (27/10): `lib/presentation/pages/perfil/` (3 arquivos), `lib/presentation/widgets/perfil/` (3 arquivos), `test/area_perfil_test.dart` e o `lib/app.dart` novo.
- `providers.dart`: só uma quebra de linha.

**Estrutura**
- `database/` agora fica dentro de `tree/`. O projeto de testes procura `../../../database/modelo_fisico_sqlite.sql`.
- Ficaram fora do zip: `hr_auto_login/build`, `.dart_tool` e `.flutter-plugins-dependencies`. São arquivos gerados (cerca de 136 MB) e o Flutter recria tudo.

## Como levar para o PC

1. Faça antes uma cópia de `D:\TCC\rh_System` (ou um commit local).
2. Copie `tree/backend`, `tree/hr_auto_login`, `tree/README.md` e `tree/.gitignore` por cima das pastas do PC. A pasta `Persistencia/Migrations` e o `hrauto.db` do PC não estão no pacote, então continuam lá. Se você mudou algum `appsettings*.json` no PC, mescle esses arquivos em vez de substituir.
3. Não precisa de migration nova: o esquema do banco não mudou desde 16/10.

```bat
cd backend
dotnet build
dotnet test
dotnet run --project src/HRAuto.Api --launch-profile http

cd ..\hr_auto_login
flutter pub get
flutter analyze
flutter test
flutter run -d chrome --dart-define=PROTOTIPO=false
```

O `flutter pub get` baixa `file_picker`, `file_saver` e `http_parser`, que entraram em 27/10. As versões foram escolhidas sem acesso ao pub.dev; se alguma não resolver, ajuste só o `pubspec.yaml`.

## O que foi verificado aqui

- **Compilado com .NET 8, 0 erros:** Domain, Application, controllers e middleware da Api, e as partes da Infrastructure que não usam pacotes (armazenamento de holerites, relógio, e-mail).
- **742 testes passaram:** Domain, Application, armazenamento de holerites e relógio, num executor próprio que imita o xUnit (`docs/ferramentas/verificacao_dotnet`). Uma mutação proposital foi detectada, o que confirma que o executor acusa falhas.
- **Conferido por script, sem compilar:**
  - Back-end (`checar_cs.py`): namespaces dos `using`, tipos registrados na injeção de dependência e interfaces da Application implementadas na Infrastructure e nos dublês de teste.
  - Flutter (`checar_dart.py`): imports, pacotes do `pubspec`, providers, contratos dos repositórios e argumentos nomeados dos construtores.
- **NÃO executado aqui:** EF Core (repositórios e migrations), JWT e BCrypt, `Program.cs`, testes de API e de integração, e nada do Flutter (`analyze` e `test`). O NuGet e o pub.dev estão bloqueados neste ambiente. Esse é o teste que falta, e só roda no seu PC.

## Pastas

- `tree/`: a raiz do repositório.
  - `backend/`: .NET 8, Clean Architecture, com testes xUnit.
  - `hr_auto_login/`: o app Flutter.
  - `database/`: modelo físico SQLite oficial e o MySQL original.
  - `_legado/`: fundação Flutter de 15/10, só para consulta.
  - `scripts/`, `README.md` e `.gitignore`.
- `docs/dias/`: os 12 documentos diários, com explicações, suposições e os patches originais.
- `docs/progresso.md`: o registro diário.
- `docs/DIVERGENCIAS_PARCIAL1.md`: as divergências em relação à Parcial 1.
- `docs/extracao.log`: de qual dia veio cada arquivo.
- `docs/ferramentas/`: os scripts de extração, de patch e de conferência, e o harness de verificação .NET.
