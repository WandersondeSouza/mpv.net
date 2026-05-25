#!/usr/bin/env python3
"""Remove duplicate msgids from source.pot"""

from pathlib import Path

po_file = Path('lang/source.pot')
content = po_file.read_text(encoding='utf-8')
lines = content.split('\n')

output_lines = []
seen_msgids = set()
current_entry = []
entry_msgid = None
duplicates = 0

i = 0
while i < len(lines):
    line = lines[i]
    
    if line.startswith('msgid '):
        # Save previous entry if any
        if current_entry and entry_msgid is not None:
            if entry_msgid == '""' or entry_msgid not in seen_msgids:
                output_lines.extend(current_entry)
                if entry_msgid != '""':
                    seen_msgids.add(entry_msgid)
            else:
                duplicates += 1
        
        current_entry = [line]
        entry_msgid = line[6:].strip()
        i += 1
        # Collect continuation lines
        while i < len(lines) and lines[i].startswith('"'):
            current_entry.append(lines[i])
            i += 1
        continue
    
    elif line.startswith('msgstr'):
        current_entry.append(line)
        i += 1
        while i < len(lines) and lines[i].startswith('"'):
            current_entry.append(lines[i])
            i += 1
        continue
    
    elif line.strip() == '':
        if current_entry and entry_msgid is not None:
            if entry_msgid == '""' or entry_msgid not in seen_msgids:
                output_lines.extend(current_entry)
                if entry_msgid != '""':
                    seen_msgids.add(entry_msgid)
            else:
                duplicates += 1
        output_lines.append(line)
        current_entry = []
        entry_msgid = None
        i += 1
        continue
    
    else:
        current_entry.append(line)
        i += 1

# Handle last entry
if current_entry and entry_msgid is not None:
    if entry_msgid == '""' or entry_msgid not in seen_msgids:
        output_lines.extend(current_entry)
    else:
        duplicates += 1

output_content = '\n'.join(output_lines)
if not output_content.endswith('\n'):
    output_content += '\n'

po_file.write_text(output_content, encoding='utf-8')
print(f'Removed {duplicates} duplicates from source.pot')
