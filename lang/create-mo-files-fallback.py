import argparse
import re
import struct
from pathlib import Path

STRING_RE = re.compile(r'^"(.*)"$')
FIELD_RE = re.compile(r'^(msgctxt|msgid_plural|msgid|msgstr(?:\[[0-9]+\])?)\s+(.*)$')


def unescape_po_string(value: str) -> str:
    result = []
    i = 0
    while i < len(value):
        ch = value[i]
        if ch != '\\' or i + 1 >= len(value):
            result.append(ch)
            i += 1
            continue

        nxt = value[i + 1]
        escapes = {'n': '\n', 'r': '\r', 't': '\t', '"': '"', '\\': '\\'}
        result.append(escapes.get(nxt, nxt))
        i += 2

    return ''.join(result)


def escape_mo_string(value: str) -> bytes:
    return value.encode('utf-8')


class PoEntry:
    def __init__(self):
        self.msgctxt = None
        self.msgid = None
        self.msgid_plural = None
        self.msgstrs = []
        self.current_field = None

    def set_field(self, field: str, value: str):
        self.current_field = field
        if field == 'msgctxt':
            self.msgctxt = value
        elif field == 'msgid':
            self.msgid = value
        elif field == 'msgid_plural':
            self.msgid_plural = value
        elif field.startswith('msgstr'):
            if field == 'msgstr':
                idx = 0
            else:
                idx = int(field[field.index('[') + 1:field.index(']')])
            while len(self.msgstrs) <= idx:
                self.msgstrs.append('')
            self.msgstrs[idx] = value

    def append_continuation(self, value: str):
        if self.current_field is None:
            return
        if self.current_field.startswith('msgstr'):
            if not self.msgstrs:
                self.msgstrs.append('')
            self.msgstrs[-1] += value
        elif self.current_field == 'msgid':
            self.msgid = (self.msgid or '') + value
        elif self.current_field == 'msgid_plural':
            self.msgid_plural = (self.msgid_plural or '') + value
        elif self.current_field == 'msgctxt':
            self.msgctxt = (self.msgctxt or '') + value

    def is_empty(self):
        return self.msgid is None and self.msgid_plural is None and not self.msgstrs

    def key(self):
        if self.msgid is None:
            return None
        if self.msgctxt is not None and self.msgid_plural is not None:
            return (self.msgctxt, self.msgid, self.msgid_plural)
        if self.msgctxt is not None:
            return (self.msgctxt, self.msgid)
        if self.msgid_plural is not None:
            return (self.msgid, self.msgid_plural)
        return (self.msgid,)

    def output_key(self):
        parts = []
        if self.msgctxt is not None:
            parts.append(self.msgctxt)
            parts.append('\x04')
        parts.append(self.msgid or '')
        if self.msgid_plural is not None:
            parts.append('\x00')
            parts.append(self.msgid_plural)
        return ''.join(parts)

    def output_value(self):
        if self.msgid_plural is not None:
            return '\x00'.join(self.msgstrs or ['', ''])
        return self.msgstrs[0] if self.msgstrs else ''


def parse_po(path: Path):
    entries = []
    current = PoEntry()
    obsolete = False
    with path.open('r', encoding='utf-8') as f:
        for line in f:
            line = line.rstrip('\n')
            if line.startswith('#~'):
                obsolete = True
                continue
            if obsolete:
                if not line.strip():
                    obsolete = False
                continue
            if not line.strip():
                if not current.is_empty():
                    entries.append(current)
                current = PoEntry()
                continue
            m = FIELD_RE.match(line)
            if m:
                field, raw = m.groups()
                raw = raw.strip()
                s = STRING_RE.match(raw)
                if s:
                    current.set_field(field, unescape_po_string(s.group(1)))
                else:
                    current.set_field(field, '')
                continue
            s = STRING_RE.match(line.strip())
            if s:
                current.append_continuation(unescape_po_string(s.group(1)))
                continue
    if not current.is_empty():
        entries.append(current)
    return entries


def build_mo(entries):
    catalog = {}
    for entry in entries:
        if entry.msgid is None:
            continue
        key = entry.output_key()
        catalog[key] = entry.output_value()
    keys = sorted(catalog.keys())
    ids = b''
    strs = b''
    offsets = []
    for key in keys:
        id_bytes = escape_mo_string(key) + b'\0'
        str_bytes = escape_mo_string(catalog[key]) + b'\0'
        offsets.append((len(id_bytes), len(ids), len(str_bytes), len(strs)))
        ids += id_bytes
        strs += str_bytes
    key_start = 7 * 4 + len(keys) * 8 * 2
    value_start = key_start + len(ids)
    header = struct.pack('<Iiiiiii', 0x950412de, 0, len(keys), 7 * 4, 7 * 4 + len(keys) * 8, 0, 0)
    bodies = [header]
    for length, pos, _, _ in offsets:
        bodies.append(struct.pack('<ii', length, key_start + pos))
    for _, _, length, pos in offsets:
        bodies.append(struct.pack('<ii', length, value_start + pos))
    bodies.append(ids)
    bodies.append(strs)
    return b''.join(bodies)


def main(po_file: Path, mo_file: Path):
    entries = parse_po(po_file)
    data = build_mo(entries)
    mo_file.parent.mkdir(parents=True, exist_ok=True)
    mo_file.write_bytes(data)
    print(f'Compiled {po_file.name} -> {mo_file}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Compile a PO file to MO without msgfmt.')
    parser.add_argument('--po-file', required=True)
    parser.add_argument('--mo-file', required=True)
    args = parser.parse_args()
    main(Path(args.po_file), Path(args.mo_file))
