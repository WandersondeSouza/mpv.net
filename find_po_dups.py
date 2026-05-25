from pathlib import Path
from collections import defaultdict
path = Path('lang/po/zh_CN.po')
lines = path.read_text(encoding='utf-8').splitlines()
entries = []
ctxt = None
mid = None
active = True
for line in lines:
    if line.startswith('#~'):
        active = False
    elif line.strip() == '':
        if mid is not None and active:
            entries.append((ctxt, mid))
        ctxt = None
        mid = None
        active = True
    elif line.startswith('msgctxt ') and active:
        ctxt = line[8:]
    elif line.startswith('msgid ') and active:
        mid = line[6:]
    elif line.startswith('msgstr ') and active:
        pass
if mid is not None and active:
    entries.append((ctxt, mid))
counts = defaultdict(list)
for i,(c,m) in enumerate(entries, start=1):
    counts[(c,m)].append(i)
for key, locs in counts.items():
    if len(locs) > 1:
        print(f'{len(locs)} duplicates for ctxt={key[0]} msgid={key[1]} at record lines {locs}')
