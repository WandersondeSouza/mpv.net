#!/usr/bin/env python3
"""Remove duplicate msgid entries from PO files while preserving formatting."""

from pathlib import Path

def clean_po_file(input_path, output_path):
    """Remove duplicate msgids while preserving PO file structure and formatting."""
    content = Path(input_path).read_text(encoding='utf-8')
    lines = content.split('\n')
    
    output_lines = []
    current_entry = []
    entry_msgid = None
    seen_msgids = set()
    duplicates_removed = 0
    in_header = True
    header_done = False
    
    i = 0
    while i < len(lines):
        line = lines[i]
        
        # Check if this is a comment or special line
        if line.startswith('#'):
            current_entry.append(line)
            i += 1
            continue
        
        # Parse msgid/msgstr/msgctxt
        if line.startswith('msgid '):
            # If we have a previous entry, save it
            if current_entry and entry_msgid is not None:
                if entry_msgid == '""' or entry_msgid not in seen_msgids:
                    # Header or new message - keep it
                    if entry_msgid == '""':
                        in_header = False
                        header_done = True
                    if entry_msgid != '""':
                        seen_msgids.add(entry_msgid)
                    output_lines.extend(current_entry)
                else:
                    # Duplicate - skip
                    duplicates_removed += 1
                    print(f"Removing duplicate msgid: {entry_msgid[:60]}...")
            
            current_entry = [line]
            entry_msgid = line[6:].strip()
            i += 1
            
            # Collect continuation lines
            while i < len(lines) and lines[i].startswith('"') and not lines[i].startswith('msgctxt ') and not lines[i].startswith('msgid ') and not lines[i].startswith('msgstr '):
                current_entry.append(lines[i])
                i += 1
            continue
        
        elif line.startswith('msgctxt '):
            current_entry.append(line)
            i += 1
            while i < len(lines) and lines[i].startswith('"') and not lines[i].startswith('msgid '):
                current_entry.append(lines[i])
                i += 1
            continue
            
        elif line.startswith('msgstr'):
            current_entry.append(line)
            i += 1
            # Collect msgstr continuation lines
            while i < len(lines) and lines[i].startswith('"'):
                current_entry.append(lines[i])
                i += 1
            continue
        
        elif line.strip() == '':
            # Empty line marks end of entry
            if current_entry and entry_msgid is not None:
                if entry_msgid == '""' or entry_msgid not in seen_msgids:
                    if entry_msgid == '""':
                        seen_msgids.add(entry_msgid)
                    else:
                        seen_msgids.add(entry_msgid)
                    output_lines.extend(current_entry)
                else:
                    duplicates_removed += 1
                    print(f"Removing duplicate msgid: {entry_msgid[:60]}...")
            
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
            duplicates_removed += 1
            print(f"Removing duplicate msgid: {entry_msgid[:60]}...")
    
    # Write output
    output_content = '\n'.join(output_lines)
    if not output_content.endswith('\n'):
        output_content += '\n'
    
    Path(output_path).write_text(output_content, encoding='utf-8')
    
    print(f"\n[OK] Removed {duplicates_removed} duplicate entries")
    print(f"[OK] Cleaned file saved to {output_path}")
    return duplicates_removed


if __name__ == '__main__':
    po_file = Path('lang/po/bg.po')
    
    clean_po_file(po_file, po_file)
    print("\nDone! File cleaned.")
