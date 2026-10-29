"""Conferência estática do back-end nas partes que não compilam aqui (EF Core, JWT, BCrypt, Program.cs, testes de API):
namespaces dos using, tipos registrados na DI, interfaces da Application implementadas na Infrastructure."""
import re, sys, os, glob
B = sys.argv[1]
arqs = glob.glob(f'{B}/**/*.cs', recursive=True)
src = {a: open(a, encoding='utf-8').read() for a in arqs}
def sem_coment(t): return re.sub(r'//.*', '', re.sub(r'/\*.*?\*/', '', t, flags=re.S))
ns_decl, tipos = set(), {}
for a, t in src.items():
    m = re.search(r'^namespace\s+([\w.]+)', t, re.M)
    ns = m.group(1) if m else ''
    ns_decl.add(ns)
    for k in re.finditer(r'\b(class|interface|record|enum|struct)\s+(?:struct\s+|class\s+)?([A-Z]\w*)', sem_coment(t)):
        tipos.setdefault(k.group(2), []).append(a)
probs = []
# 1) using HRAuto.* precisa existir
for a, t in src.items():
    for u in re.findall(r'^using\s+(?:static\s+)?(HRAuto[\w.]*)\s*;', t, re.M):
        if u not in ns_decl and not (u.rsplit('.',1)[0] in ns_decl and u.rsplit('.',1)[1] in tipos):
            probs.append(f'using inexistente: {u}  em {os.path.relpath(a,B)}')
# 2) tipos citados nos registros de DI
for a in [x for x in arqs if x.endswith('DependencyInjection.cs') or x.endswith('Program.cs') or 'Extensoes' in x]:
    t = sem_coment(src[a])
    for g in re.findall(r'Add(?:Scoped|Singleton|Transient|DbContext|HostedService)<([^>()]+)>', t):
        for nome in re.findall(r'[A-Z]\w*', g):
            if nome not in tipos and nome not in ('IServiceProvider',):
                probs.append(f'tipo da DI não encontrado: {nome}  em {os.path.relpath(a,B)}')
    for g in re.findall(r'Configure<(\w+)>', t):
        if g not in tipos: probs.append(f'opções não encontradas: {g} em {os.path.relpath(a,B)}')
# 3) interfaces I* da Application: cada membro precisa aparecer na classe que a implementa (Infrastructure e fakes de teste)
ifaces = {}
for a, t in src.items():
    if '/src/HRAuto.Application/' not in a: continue
    for m in re.finditer(r'public\s+interface\s+(I\w+)[^{]*\{(.*?)\n\}', sem_coment(t), re.S):
        ifaces[m.group(1)] = set(re.findall(r'\b(\w+)\s*(?:<[^>]*>)?\s*\(', m.group(2))) - {'Task','ValueTask'}
impl = {}
for a, t in src.items():
    for m in re.finditer(r'class\s+(\w+)(?:\([^)]*\))?\s*:\s*([^{\n]+)', sem_coment(t)):
        for base in re.findall(r'\b(I[A-Z]\w*)', m.group(2)):
            if base in ifaces:
                impl.setdefault(base, []).append((m.group(1), a))
for i, membros in sorted(ifaces.items()):
    for cls, a in impl.get(i, []):
        t = sem_coment(src[a])
        ini = t.find(f'class {cls}')
        prox = re.search(r'\n(?:public |internal )?(?:sealed )?(?:partial )?(?:static )?class\s', t[ini+6:])
        corpo = t[ini: ini+6+prox.start()] if prox else t[ini:]
        for mbr in membros:
            if not re.search(rf'\b{mbr}\s*(?:<[^>]*>)?\s*\(', corpo):
                probs.append(f'{cls} ({os.path.relpath(a,B)}) não implementa {i}.{mbr}')
sem_impl = sorted(i for i in ifaces if i not in impl)
print(f'{len(arqs)} arquivos .cs, {len(tipos)} tipos, {len(ifaces)} interfaces da Application, {sum(len(v) for v in impl.values())} implementações')
print('interfaces sem nenhuma implementação:', ', '.join(sem_impl) or 'nenhuma')
print('\n'.join(probs) if probs else 'Nenhum problema encontrado.')
