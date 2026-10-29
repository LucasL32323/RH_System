"""Conferência estática do Flutter (sem SDK aqui): argumentos nomeados x construtores do projeto e membros usados de UsuarioAutenticado."""
import re, glob, sys
arqs = glob.glob('lib/**/*.dart', recursive=True) + glob.glob('test/**/*.dart', recursive=True)
def limpa(t):
    t = re.sub(r"'''.*?'''|\"\"\".*?\"\"\"", "''", t, flags=re.S)
    t = re.sub(r"r?'(?:\\.|[^'\\\n])*'|r?\"(?:\\.|[^\"\\\n])*\"", "''", t)
    return re.sub(r'//.*', '', t)
L = {a: limpa(open(a, encoding='utf-8').read()) for a in arqs}
def bloco(t, i):
    n = 0
    for j in range(i, len(t)):
        if t[j] in '([{': n += 1
        elif t[j] in ')]}':
            n -= 1
            if n == 0: return t[i+1:j]
    return ''
def nivel0(s):
    partes, n, cur = [], 0, ''
    for ch in s:
        if ch in '([{': n += 1
        elif ch in ')]}': n -= 1
        if ch == ',' and n == 0: partes.append(cur); cur = ''
        else: cur += ch
    partes.append(cur); return [p.strip() for p in partes if p.strip()]
params = {}
for a, t in L.items():
    for m in re.finditer(r'\bclass\s+(\w+)', t):
        cls = m.group(1)
        for c in re.finditer(rf'(?:const\s+|factory\s+)\b{cls}(\.\w+)?\s*\(|^\s+{cls}(\.\w+)?\s*\(', t, re.M):
            inner = bloco(t, c.end()-1)
            nome = c.group(1) or c.group(2) or ''
            ps = params.setdefault((cls, nome), set())
            if '{' in inner:
                for p in nivel0(inner[inner.index('{')+1: inner.rindex('}')]):
                    p = re.sub(r'=.*', '', p, flags=re.S).strip()
                    mm = re.search(r'(?:this|super)\.(\w+)|(\w+)\s*$', p)
                    if mm: ps.add(mm.group(1) or mm.group(2))
probs = []
for a, t in L.items():
    for c in re.finditer(r'\b([A-Z]\w*)(\.\w+)?\s*\(', t):
        chave = (c.group(1), c.group(2) or '')
        if chave not in params: continue
        for p in nivel0(bloco(t, c.end()-1)):
            m = re.match(r'^(\w+)\s*:(?!:)', p)
            if m and m.group(1) not in params[chave] | {'key'}:
                probs.append(f'{a}: {chave[0]}{chave[1]}(… {m.group(1)}: …) não existe no construtor')
# membros usados de objetos UsuarioAutenticado (usuario/sessao) existem na entidade
ent = L['lib/domain/entities/usuario_autenticado.dart']
membros = set(re.findall(r'\bfinal\s+[\w<>?]+\s+(\w+)\s*;', ent)) | set(re.findall(r'\bget\s+(\w+)', ent)) | set(re.findall(r'^\s+[\w<>?]+\s+(\w+)\s*\(', ent, re.M))
for a, t in L.items():
    if 'perfil/' not in a and not a.endswith('app.dart'): continue
    for obj, mbr in re.findall(r'\b(widget\.usuario|usuario|sessao)\.(\w+)', t):
        if mbr not in membros and mbr not in ('toString', 'hashCode'):
            probs.append(f'{a}: {obj}.{mbr} não existe em UsuarioAutenticado')
print('\n'.join(sorted(set(probs))) or 'OK: argumentos nomeados e membros de UsuarioAutenticado conferem.')
