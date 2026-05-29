#!/usr/bin/env python3
"""Remove duplicate message definitions from PO files."""

from pathlib import Path
from collections import OrderedDict

def parse_po_file(path):
    """Parse a PO file and return entries with their line numbers."""
    lines = Path(path).read_text(encoding='utf-8').splitlines()
    
    entries = []
    current_entry = []
    entry_start = 0
    
    for line_num, line in enumerate(lines, 1):
        # Empty line marks end of entry
        if line.strip() == '':
            if current_entry:
                entries.append({
                    'lines': current_entry,
                    'start': entry_start,
                    'end': line_num - 1
                })
                current_entry = []
            continue
        
        if not current_entry:
            entry_start = line_num
        current_entry.append(line)
    
    # Don't forget last entry if file doesn't end with empty line
    if current_entry:
        entries.append({
            'lines': current_entry,
            'start': entry_start,
            'end': len(lines)
        })
    
    return entries, lines


def get_msgid(entry_lines):
    """Extract msgid from entry lines."""
    for line in entry_lines:
        if line.startswith('msgid '):
            return line[6:].strip().strip('"')
    return None


def remove_duplicate_entries(input_path, output_path):
    """Remove duplicate msgid entries, keeping the first occurrence."""
    entries, all_lines = parse_po_file(input_path)
    
    seen_msgids = set()
    lines_to_keep = set()
    duplicates_removed = 0
    
    # Header is always kept
    if entries and entries[0]['lines'][0].startswith('msgid ""'):
        for line_num in range(entries[0]['start'], entries[0]['end'] + 2):
            lines_to_keep.add(line_num)
    
    for entry in entries[1:]:  # Skip header
        msgid = get_msgid(entry['lines'])
        
        if msgid in seen_msgids:
            # Duplicate - skip it
            duplicates_removed += 1
            print(f"Removing duplicate msgid: {msgid[:60]}...")
        else:
            # First occurrence - keep it
            seen_msgids.add(msgid)
            for line_num in range(entry['start'], entry['end'] + 2):
                lines_to_keep.add(line_num)
    
    # Write output file with kept lines
    with open(output_path, 'w', encoding='utf-8') as f:
        for line_num, line in enumerate(all_lines, 1):
            if line_num in lines_to_keep:
                f.write(line + '\n')
            elif line_num == len(all_lines):  # Last line
                if line_num in lines_to_keep:
                    f.write(line)
    
    print(f"\n✓ Removed {duplicates_removed} duplicate entries")
    print(f"✓ Output saved to {output_path}")
    return duplicates_removed


if __name__ == '__main__':
    po_file = Path('lang/po/bg.po')
    backup_file = Path('lang/po/bg.po.backup')
    
    # Create backup
    import shutil
    if po_file.exists():
        shutil.copy2(po_file, backup_file)
        print(f"Created backup: {backup_file}")
    
    # Clean duplicates
    removed = remove_duplicate_entries(po_file, po_file)
    print(f"\nFile cleaned and saved to: {po_file}")
