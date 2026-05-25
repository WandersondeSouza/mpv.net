import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE_POT = ROOT / "lang" / "source.pot"
CS_PATTERNS = [
    r'_\(\s*(?P<msgid>@?"(?:[^"\\]|\\.)*")\s*\)',
    r'ngettext\(\s*(?P<msgid>@?"(?:[^"\\]|\\.)*")\s*,\s*(?P<msgid_plural>@?"(?:[^"\\]|\\.)*")\s*,',
    r'pgettext\(\s*(?P<context>@?"(?:[^"\\]|\\.)*")\s*,\s*(?P<msgid>@?"(?:[^"\\]|\\.)*")\s*\)',
    r'npgettext\(\s*(?P<context>@?"(?:[^"\\]|\\.)*")\s*,\s*(?P<msgid>@?"(?:[^"\\]|\\.)*")\s*,\s*(?P<msgid_plural>@?"(?:[^"\\]|\\.)*")\s*,',
]
XAML_PATTERNS = [
    (re.compile(r'\{ngettext:Gettext\s+([^}]+)\}'), False),
    (re.compile(r'\{ngettext:PluralGettext\s+([^,}]+)\s*,\s*([^}]+)\}'), True),
]
EDITOR_CONF_KEYS = {"name", "directory", "help", "option"}


def unescape_csharp_string(value: str) -> str:
    if value.startswith('@"') and value.endswith('"'):
        content = value[2:-1]
        return content.replace('""', '"')
    assert value.startswith('"') and value.endswith('"'), value
    body = value[1:-1]
    return bytes(body, 'utf-8').decode('unicode_escape')


def escape_po_string(value: str) -> str:
    return value.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n')


def parse_cs_files(root: Path):
    results = {}
    for path in root.rglob('*.cs'):
        if 'obj' in path.parts:
            continue
        text = path.read_text(encoding='utf-8', errors='ignore')
        for pattern in CS_PATTERNS:
            for match in re.finditer(pattern, text):
                if match.lastgroup == 'msgid_plural':
                    msgid = unescape_csharp_string(match.group('msgid'))
                    msgid_plural = unescape_csharp_string(match.group('msgid_plural'))
                    key = ('plural', msgid, msgid_plural)
                    results.setdefault(key, {'references': set()})['references'].add(f'{path}:{text[:match.start()].count("\n") + 1}')
                elif match.lastgroup == 'msgid':
                    msgid = unescape_csharp_string(match.group('msgid'))
                    key = ('gettext', msgid)
                    results.setdefault(key, {'references': set()})['references'].add(f'{path}:{text[:match.start()].count("\n") + 1}')
    return results


def parse_xaml_files(root: Path):
    results = {}
    for path in root.rglob('*.xaml'):
        if 'obj' in path.parts:
            continue
        text = path.read_text(encoding='utf-8', errors='ignore')
        for regex, is_plural in XAML_PATTERNS:
            for match in regex.finditer(text):
                if is_plural:
                    msgid = match.group(1).strip()
                    msgid_plural = match.group(2).strip()
                    key = ('plural', msgid, msgid_plural)
                else:
                    msgid = match.group(1).strip()
                    key = ('gettext', msgid)
                results.setdefault(key, {'references': set()})['references'].add(f'{path}:{text[:match.start()].count("\n") + 1}')
    return results


def parse_editor_conf(path: Path) -> dict[str, dict]:
    results: dict[str, dict] = {}
    for line_no, line in enumerate(path.read_text(encoding='utf-8').splitlines(), start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith('#'):
            continue
        parts = stripped.split('=', 1)
        if len(parts) != 2:
            continue
        key = parts[0].strip().lower()
        value = parts[1].strip()
        if not value:
            continue
        if key == 'name' or key == 'help':
            msgid = value
            results.setdefault(('gettext', msgid), {'references': set()})['references'].add(f'{path}:{line_no}')
        elif key == 'directory':
            for part in [p.strip() for p in value.split('/') if p.strip()]:
                results.setdefault(('gettext', part), {'references': set()})['references'].add(f'{path}:{line_no}')
        elif key == 'option':
            if ' ' in value:
                name, help_text = value.split(' ', 1)
                results.setdefault(('gettext', name), {'references': set()})['references'].add(f'{path}:{line_no}')
                if help_text:
                    results.setdefault(('gettext', help_text), {'references': set()})['references'].add(f'{path}:{line_no}')
            else:
                results.setdefault(('gettext', value), {'references': set()})['references'].add(f'{path}:{line_no}')
    return results


def generate_pot(entries: dict[tuple, dict], output_path: Path) -> None:
    lines = [
        'msgid ""',
        'msgstr ""',
        '"Content-Type: text/plain; charset=UTF-8\\n"',
        '"Content-Transfer-Encoding: 8bit\\n"',
        '',
    ]
    def sort_key(item):
        key = item[0]
        if key[0] == 'npgettext':
            return (key[0], key[1], key[2], key[3])
        if key[0] == 'pgettext':
            return (key[0], key[1], key[2])
        return (key[0], key[1], key[2] if len(key) > 2 else '')

    for key, data in sorted(entries.items(), key=sort_key):
        for ref in sorted(data['references']):
            lines.append(f'#: {ref}')
        if key[0] == 'npgettext':
            lines.append(f'msgctxt "{escape_po_string(key[1])}"')
            lines.append(f'msgid "{escape_po_string(key[2])}"')
            lines.append(f'msgid_plural "{escape_po_string(key[3])}"')
            lines.append('msgstr[0] ""')
            lines.append('msgstr[1] ""')
        elif key[0] == 'pgettext':
            lines.append(f'msgctxt "{escape_po_string(key[1])}"')
            lines.append(f'msgid "{escape_po_string(key[2])}"')
            lines.append('msgstr ""')
        elif key[0] == 'plural':
            lines.append(f'msgid "{escape_po_string(key[1])}"')
            lines.append(f'msgid_plural "{escape_po_string(key[2])}"')
            lines.append('msgstr[0] ""')
            lines.append('msgstr[1] ""')
        else:
            lines.append(f'msgid "{escape_po_string(key[1])}"')
            lines.append('msgstr ""')
        lines.append('')
    output_path.write_text('\n'.join(lines), encoding='utf-8')


def merge_entries(destination, source):
    for key, value in source.items():
        if key not in destination:
            destination[key] = {'references': set(value['references'])}
        else:
            destination[key]['references'].update(value['references'])


if __name__ == '__main__':
    entries: dict[tuple, dict] = {}
    merge_entries(entries, parse_cs_files(ROOT))
    merge_entries(entries, parse_xaml_files(ROOT))
    editor_conf_path = ROOT / 'src' / 'MpvNet.Windows' / 'Resources' / 'editor_conf.txt'
    if editor_conf_path.exists():
        merge_entries(entries, parse_editor_conf(editor_conf_path))
    if not entries:
        raise SystemExit('No gettext strings found for fallback POT generation.')
    generate_pot(entries, SOURCE_POT)
    print(f'Generated fallback POT with {len(entries)} entries: {SOURCE_POT}')
