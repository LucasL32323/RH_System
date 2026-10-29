"""Extrai os arquivos de código dos documentos diários (### caminho + bloco cercado), em ordem cronológica.
Dias posteriores sobrescrevem os anteriores. Seções de patch / trechos (título com ' — ' ou 'PATCH') não são aplicadas:
são listadas em PATCHES_NAO_APLICADOS.md."""
import re, sys, os, json
raw_dir, out_dir, log_path = sys.argv[1], sys.argv[2], sys.argv[3]
dias = sorted(f for f in os.listdir(raw_dir) if f.endswith('.md'))
PATH = re.compile(r'^[A-Za-z0-9_./\-]+\.[A-Za-z0-9]+$|^scripts/[\w./-]+$|^\.gitignore$')
FENCE = re.compile(r'^(`{3,})([\w+#-]*)\s*$')
log, patches = [], []
for dia in dias:
    lines = open(os.path.join(raw_dir, dia), encoding='utf-8').read().split('\n')
    i, pend = 0, None
    n_ok = 0
    while i < len(lines):
        ln = lines[i]
        m = FENCE.match(ln)
        if m:
            ticks = m.group(1); j = i + 1; body = []
            while j < len(lines) and not (lines[j].startswith(ticks) and lines[j].strip() == ticks):
                body.append(lines[j]); j += 1
            if pend:
                if PATH.match(pend):
                    dest = pend
                    if dest.startswith('frontend/'): dest = '_legado/frontend_fundacao_15-10/' + dest[len('frontend/'):]
                    full = os.path.join(out_dir, dest)
                    os.makedirs(os.path.dirname(full) or out_dir, exist_ok=True)
                    existed = os.path.exists(full)
                    with open(full, 'w', encoding='utf-8', newline='\n') as f:
                        f.write('\n'.join(body) + '\n')
                    log.append((dia, pend, 'SOBRESCRITO' if existed else 'novo', len(body)))
                    n_ok += 1
                else:
                    patches.append((dia, pend))
                pend = None
            i = j + 1; continue
        if ln.startswith('### '):
            pend = ln[4:].strip()
            if re.match(r'^(PATCH|[BF]\d)\b', pend) or ' — ' in pend: patches.append((dia, pend)); pend = None
        elif ln.startswith('#'):
            pend = None
        i += 1
with open(log_path, 'w', encoding='utf-8') as f:
    for d, p, s, n in log: f.write(f'{d}\t{s}\t{n}\t{p}\n')
    f.write('\n## SEÇÕES DE PATCH (não aplicadas)\n')
    for d, p in sorted(set(patches)): f.write(f'{d}\t{p}\n')
print(len(log), 'arquivos;', len(set(patches)), 'patches')
