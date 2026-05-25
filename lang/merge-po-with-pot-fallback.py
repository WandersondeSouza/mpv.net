import argparse
import re
from pathlib import Path

STRING_RE = re.compile(r'^"(.*)"$')
ENTRY_KEY_SEPARATOR = '\u0000'


def unescape_po_string(text):
    return bytes(text, 'utf-8').decode('unicode_escape')


def escape_po_string(text):
    return text.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n')


def quote_po_string(text):
    return f'"{escape_po_string(text)}"'


class PoEntry:
    def __init__(self):
        self.comments = []
        self.refs = []
        self.msgctxt = None
        self.msgid = None
        self.msgid_plural = None
        self.msgstrs = []
        self.is_header = False

    def key(self):
        if self.is_header:
            return ('header',)
        if self.msgctxt is not None and self.msgid is not None and self.msgid_plural is not None:
            return ('npgettext', self.msgctxt, self.msgid, self.msgid_plural)
        if self.msgctxt is not None and self.msgid is not None:
            return ('pgettext', self.msgctxt, self.msgid)
        if self.msgid_plural is not None:
            return ('plural', self.msgid, self.msgid_plural)
        if self.msgid is not None:
            return ('gettext', self.msgid)
        return ('unknown',)

    def set_field(self, field, value):
        if field == 'msgctxt':
            self.msgctxt = value
        elif field == 'msgid':
            self.msgid = value
        elif field == 'msgid_plural':
            self.msgid_plural = value
        elif field.startswith('msgstr'):
            if field == 'msgstr':
                if not self.msgstrs:
                    self.msgstrs = ['']
                self.msgstrs[0] = value
            else:
                index = int(field[field.find('[')+1:field.find(']')])
                while len(self.msgstrs) <= index:
                    self.msgstrs.append('')
                self.msgstrs[index] = value

    def append_to_last_field(self, text):
        if self.msgstrs:
            self.msgstrs[-1] += text
        elif self.msgid_plural is not None:
            self.msgid_plural += text
        elif self.msgid is not None:
            self.msgid += text
        elif self.msgctxt is not None:
            self.msgctxt += text

    def ensure_plural_length(self):
        if self.msgid_plural is not None and len(self.msgstrs) < 2:
            while len(self.msgstrs) < 2:
                self.msgstrs.append('')


def parse_po(path):
    lines = path.read_text(encoding='utf-8').splitlines()
    entries = []
    current = PoEntry()
    pending_comments = []
    skip_obsolete = False

    def flush_current():
        nonlocal current, pending_comments, skip_obsolete
        if current.msgid is not None or current.is_header:
            current.comments = pending_comments
            current.ensure_plural_length()
            if current.msgid == '':
                current.is_header = True
            entries.append(current)
        current = PoEntry()
        pending_comments = []
        skip_obsolete = False

    for line in lines:
        if line.startswith('#~'):
            skip_obsolete = True
            continue
        if skip_obsolete:
            continue
        if line.startswith('#'):
            pending_comments.append(line)
            continue
        if not line.strip():
            if current.msgid is not None or current.comments:
                flush_current()
            continue
        if line.startswith('msgctxt'):
            value = STRING_RE.match(line[len('msgctxt'):].strip()).group(1)
            current.set_field('msgctxt', unescape_po_string(value))
            continue
        if line.startswith('msgid_plural'):
            value = STRING_RE.match(line[len('msgid_plural'):].strip()).group(1)
            current.set_field('msgid_plural', unescape_po_string(value))
            continue
        if line.startswith('msgid'):
            value = STRING_RE.match(line[len('msgid'):].strip()).group(1)
            current.set_field('msgid', unescape_po_string(value))
            continue
        if line.startswith('msgstr['):
            field, rest = line.split(' ', 1)
            value = STRING_RE.match(rest.strip()).group(1)
            current.set_field(field, unescape_po_string(value))
            continue
        if line.startswith('msgstr'):
            value = STRING_RE.match(line[len('msgstr'):].strip()).group(1)
            current.set_field('msgstr', unescape_po_string(value))
            continue
        m = STRING_RE.match(line.strip())
        if m:
            current.append_to_last_field(unescape_po_string(m.group(1)))
            continue
        # Unknown line, preserve as comment if no current msgid
        pending_comments.append(line)
    if current.msgid is not None or current.comments:
        flush_current()
    return entries


def pot_entries(path):
    raw = parse_po(path)
    return [entry for entry in raw if not entry.is_header]


def header_entry(entries):
    for entry in entries:
        if entry.is_header:
            return entry
    return None


def key_order(entry):
    key = entry.key()
    if key[0] == 'npgettext':
        return (0, key[1], key[2], key[3])
    if key[0] == 'pgettext':
        return (1, key[1], key[2])
    if key[0] == 'plural':
        return (2, key[1], key[2])
    if key[0] == 'gettext':
        return (3, key[1])
    return (4,)


def entry_to_string(entry):
    lines = []
    for comment in entry.comments:
        lines.append(comment)
    for ref in sorted(entry.refs):
        lines.append(f'#: {ref}')
    if entry.msgctxt is not None:
        lines.append(f'msgctxt {quote_po_string(entry.msgctxt)}')
    lines.append(f'msgid {quote_po_string(entry.msgid if entry.msgid is not None else "")}')
    if entry.msgid_plural is not None:
        lines.append(f'msgid_plural {quote_po_string(entry.msgid_plural)}')
    if entry.msgid_plural is not None:
        for idx, value in enumerate(entry.msgstrs):
            lines.append(f'msgstr[{idx}] {quote_po_string(value)}')
    else:
        lines.append(f'msgstr {quote_po_string(entry.msgstrs[0] if entry.msgstrs else "")}')
    lines.append('')
    return lines


def merge_entries(pot_entries, po_entries):
    po_map = {}
    duplicates = []
    for entry in po_entries:
        key = entry.key()
        if key in po_map:
            existing = po_map[key]
            if not existing.msgstrs or all(not s for s in existing.msgstrs):
                po_map[key] = entry
            duplicates.append(key)
        else:
            po_map[key] = entry
    merged = []
    for pot_entry in sorted(pot_entries, key=key_order):
        key = pot_entry.key()
        if key in po_map:
            entry = po_map[key]
            entry.refs = sorted(set(entry.refs + pot_entry.refs))
            if entry.msgid_plural is not None:
                entry.ensure_plural_length()
            merged.append(entry)
        else:
            new_entry = PoEntry()
            new_entry.comments = []
            new_entry.refs = sorted(set(pot_entry.refs))
            new_entry.msgctxt = pot_entry.msgctxt
            new_entry.msgid = pot_entry.msgid
            new_entry.msgid_plural = pot_entry.msgid_plural
            if pot_entry.msgid_plural is not None:
                new_entry.msgstrs = ['', '']
            else:
                new_entry.msgstrs = ['']
            merged.append(new_entry)
    return merged, duplicates


def write_po(path, header, entries):
    lines = []
    if header is not None:
        lines.extend(entry_to_string(header))
    else:
        lines.extend([
            '# Generated PO file',
            'msgid ""',
            'msgstr ""',
            '"Content-Type: text/plain; charset=UTF-8\\n"',
            '"Content-Transfer-Encoding: 8bit\\n"',
            '',
        ])
    for entry in entries:
        if entry.is_header:
            continue
        lines.extend(entry_to_string(entry))
    path.write_text('\n'.join(lines).rstrip() + '\n', encoding='utf-8')


def main(po_directory, pot_path):
    pot_path = Path(pot_path)
    po_directory = Path(po_directory)
    if not pot_path.exists():
        raise SystemExit(f'POT file not found: {pot_path}')
    if not po_directory.exists():
        raise SystemExit(f'PO directory not found: {po_directory}')

    pot = pot_entries(pot_path)
    if not pot:
        raise SystemExit('No entries found in POT file.')

    for po_file in sorted(po_directory.glob('*.po')):
        po = parse_po(po_file)
        header = header_entry(po)
        entries = [e for e in po if not e.is_header]
        merged, duplicates = merge_entries(pot, entries)
        write_po(po_file, header, merged)
        print(f'Updated {po_file.name}: {len(merged)} entries, duplicates ignored: {len(duplicates)}')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Merge PO files with POT when gettext msgmerge is unavailable.')
    parser.add_argument('--po-directory', required=True)
    parser.add_argument('--pot-path', required=True)
    args = parser.parse_args()
    main(args.po_directory, args.pot_path)
