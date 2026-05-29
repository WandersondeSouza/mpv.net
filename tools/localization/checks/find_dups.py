from pathlib import Path
from collections import defaultdict
path = Path('lang/po/zh_CN.po')
ids = defaultdict(list)
with path.open('r', encoding='utf-8') as f:
    lines = f.readlines()
i = 0
while i < len(lines):
    line = lines[i].rstrip('\n')
    if line.startswith('msgid '):
        mid = line[6:]
        if mid == '""':
            i += 1
            full = ''
            while i < len(lines):
                line = lines[i].rstrip('\n')
                if line.startswith('msgstr '):
                    break
                if line.startswith('"'):
                    full += line.strip('"')
                i += 1
            mid = '""' + full
        ids[mid].append(i+1)
    i += 1
for msg, locs in ids.items():
    if len(locs) > 1:
        print(f'{len(locs)} duplicates at {locs}: {msg}')
