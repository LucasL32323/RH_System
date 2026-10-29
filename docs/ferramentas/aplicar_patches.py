import re, sys, os
raw, root = sys.argv[1], sys.argv[2]   # root = pasta hr_auto_login
FENCE = re.compile(r'^(`{3,})([\w+#-]*)\s*$')

def secoes(dia):
    """{titulo: [(rotulo, conteudo), ...]} para cada '### ' fora de cercas."""
    L = open(f'{raw}/{dia}.md', encoding='utf-8').read().split('\n')
    out, atual, rotulo, i = {}, None, '', 0
    while i < len(L):
        ln = L[i]; m = FENCE.match(ln)
        if m:
            t = m.group(1); j = i + 1; corpo = []
            while j < len(L) and L[j].strip() != t: corpo.append(L[j]); j += 1
            if atual is not None: out[atual].append((rotulo, '\n'.join(corpo)))
            i = j + 1; rotulo = ''; continue
        if ln.startswith('### '): atual = ln[4:].strip(); out[atual] = []; rotulo = ''
        elif ln.startswith('## '): atual = None
        elif ln.strip(): rotulo = ln.strip()
        i += 1
    return out

relatorio = []
def trocar(rel, antigo, novo, nome):
    p = os.path.join(root, rel)
    s = open(p, encoding='utf-8', newline='').read()
    crlf = '\r\n' in s
    a, n = antigo, novo
    if crlf: s = s.replace('\r\n', '\n')
    c = s.count(a)
    if c != 1:
        relatorio.append(f'FALHOU ({c} ocorrências) {nome}: {rel}'); return False
    s = s.replace(a, n)
    if crlf: s = s.replace('\n', '\r\n')
    open(p, 'w', encoding='utf-8', newline='').write(s)
    relatorio.append(f'ok  {nome}: {rel}'); return True

# ---- 26/10: pares "Trecho antigo" / "Trecho novo"
s26 = secoes('2026-10-26')
for t, fences in s26.items():
    if ' — ' not in t: continue
    rel = t.split(' — ')[0].strip().removeprefix('hr_auto_login/')
    if rel.endswith('aba_registro_ponto.dart'):
        p = os.path.join(root, rel); s = open(p, encoding='utf-8', newline='').read()
        a = s.index('class _MinhasSolicitacoes extends StatelessWidget {'); b = s.index('class _DialogoAjuste')
        s = s[:a] + fences[0][1] + '\n\n' + s[b:]
        open(p, 'w', encoding='utf-8', newline='').write(s); relatorio.append(f'ok  26/10 classe _MinhasSolicitacoes: {rel}'); continue
    antigo = None
    for rot, corpo in fences:
        if 'antigo' in rot.lower(): antigo = corpo
        elif 'novo' in rot.lower() and antigo is not None:
            trocar(rel, antigo, corpo, f'26/10 {rot[:50]}'); antigo = None
        else: relatorio.append(f'?? rótulo não reconhecido em {t}: {rot[:60]}')

# ---- 27/10
s27 = secoes('2026-10-27')
ped = s27['PATCH hr_auto_login/pubspec.yaml'][0][1]
trocar('pubspec.yaml', re.search(r'^  google_fonts:.*$', open(f'{root}/pubspec.yaml').read(), re.M).group(0),
       re.search(r'^  google_fonts:.*$', open(f'{root}/pubspec.yaml').read(), re.M).group(0) + '\n' + ped, '27/10 pubspec')
m = s27['PATCH hr_auto_login/lib/presentation/widgets/admin/menu_icones_admin.dart']
trocar('lib/presentation/widgets/admin/menu_icones_admin.dart', m[0][1], m[1][1], '27/10 menu')
a = s27['PATCH hr_auto_login/lib/presentation/pages/admin/admin_page.dart']
trocar('lib/presentation/pages/admin/admin_page.dart', "import 'aba_novo_funcionario.dart';", a[0][1] + "\nimport 'aba_novo_funcionario.dart';", '27/10 admin_page import')
trocar('lib/presentation/pages/admin/admin_page.dart', a[1][1], a[2][1], '27/10 admin_page switch')

# ---- 28/10 (Flutter)
s28 = secoes('2026-10-28')
f1 = s28['F1 — lib/core/validation/validadores.dart (import e método loginUsuario)']
trocar('lib/core/validation/validadores.dart', f1[0][1], f1[1][1], '28/10 F1 import')
trocar('lib/core/validation/validadores.dart', f1[2][1], f1[3][1], '28/10 F1 método')
trocar('lib/presentation/widgets/login_form.dart', "dica: 'nome@empresa.com.br',", "dica: 'nome@empresa.com.br ou FUNC-0001',", '28/10 F2')
f3 = s28['F3 — test/validadores_test.dart (grupo "Login")']
trocar('test/validadores_test.dart', f3[0][1], f3[1][1], '28/10 F3')
f4 = s28['F4 — test/ponto_repository_api_test.dart (grupo "AuthRepositoryApi")']
trocar('test/ponto_repository_api_test.dart', '\n' + f4[0][1] + '\n', '', '28/10 F4')
print('\n'.join(relatorio))
