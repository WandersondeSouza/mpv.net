import argparse
from collections import Counter
from pathlib import Path
import re

FIELD_RE = re.compile(r'^(msgctxt|msgid_plural|msgid|msgstr(?:\[\d+\])?)\s+"(.*)"$')
STRING_RE = re.compile(r'^"(.*)"$')


def unescape_po_string(value):
    result = []
    i = 0
    while i < len(value):
        ch = value[i]
        if ch != "\\" or i + 1 >= len(value):
            result.append(ch)
            i += 1
            continue

        nxt = value[i + 1]
        escapes = {"n": "\n", "r": "\r", "t": "\t", '"': '"', "\\": "\\"}
        result.append(escapes.get(nxt, nxt))
        i += 2
    return "".join(result)


def escape_po_string(value):
    return (
        value.replace("\\", "\\\\")
        .replace('"', '\\"')
        .replace("\r", "\\r")
        .replace("\t", "\\t")
        .replace("\n", "\\n")
    )


def quote(value):
    return f'"{escape_po_string(value)}"'


class PoEntry:
    def __init__(self):
        self.comments = []
        self.msgctxt = None
        self.msgid = None
        self.msgid_plural = None
        self.msgstrs = []
        self.current_field = None

    @property
    def is_header(self):
        return self.msgid == ""

    def is_started(self):
        return self.msgctxt is not None or self.msgid is not None or self.msgid_plural is not None or self.msgstrs

    def key(self):
        if self.msgctxt is not None and self.msgid_plural is not None:
            return ("npgettext", self.msgctxt, self.msgid, self.msgid_plural)
        if self.msgctxt is not None:
            return ("pgettext", self.msgctxt, self.msgid)
        if self.msgid_plural is not None:
            return ("plural", self.msgid, self.msgid_plural)
        return ("gettext", self.msgid)

    def set_field(self, field, value):
        self.current_field = field
        if field == "msgctxt":
            self.msgctxt = value
        elif field == "msgid":
            self.msgid = value
        elif field == "msgid_plural":
            self.msgid_plural = value
        elif field.startswith("msgstr"):
            idx = 0
            if field != "msgstr":
                idx = int(field[field.index("[") + 1 : field.index("]")])
            while len(self.msgstrs) <= idx:
                self.msgstrs.append("")
            self.msgstrs[idx] = value

    def append(self, value):
        if self.current_field is None:
            return
        if self.current_field == "msgctxt":
            self.msgctxt = (self.msgctxt or "") + value
        elif self.current_field == "msgid":
            self.msgid = (self.msgid or "") + value
        elif self.current_field == "msgid_plural":
            self.msgid_plural = (self.msgid_plural or "") + value
        elif self.current_field.startswith("msgstr"):
            idx = 0
            if self.current_field != "msgstr":
                idx = int(self.current_field[self.current_field.index("[") + 1 : self.current_field.index("]")])
            while len(self.msgstrs) <= idx:
                self.msgstrs.append("")
            self.msgstrs[idx] += value

    def clone_template(self):
        entry = PoEntry()
        entry.comments = list(self.comments)
        entry.msgctxt = self.msgctxt
        entry.msgid = self.msgid
        entry.msgid_plural = self.msgid_plural
        return entry


def parse_po(path):
    entries = []
    current = PoEntry()
    pending_comments = []
    obsolete = False

    def flush():
        nonlocal current, pending_comments
        if current.is_started():
            current.comments = pending_comments
            entries.append(current)
        current = PoEntry()
        pending_comments = []

    for raw_line in path.read_text(encoding="utf-8-sig").splitlines():
        line = raw_line.rstrip("\n")

        if line.startswith("#~"):
            obsolete = True
            continue
        if obsolete:
            if not line.strip():
                obsolete = False
            continue
        if not line.strip():
            flush()
            continue
        if line.startswith("#"):
            if current.is_started():
                flush()
            pending_comments.append(line)
            continue

        field_match = FIELD_RE.match(line)
        if field_match:
            field, value = field_match.groups()
            current.set_field(field, unescape_po_string(value))
            continue

        string_match = STRING_RE.match(line.strip())
        if string_match:
            current.append(unescape_po_string(string_match.group(1)))
            continue

        pending_comments.append(f"# {line}")

    flush()
    return entries


def order_entries(entries):
    return sorted(entries, key=lambda entry: entry.key())


def merge_duplicate_templates(entries):
    merged = {}
    order = []
    for entry in entries:
        if entry.is_header:
            continue
        key = entry.key()
        if key not in merged:
            merged[key] = entry.clone_template()
            order.append(key)
            continue
        merged[key].comments = list(dict.fromkeys(merged[key].comments + entry.comments))
    return [merged[key] for key in order]


def translation_map(entries):
    result = {}
    for entry in entries:
        if entry.is_header:
            continue
        key = entry.key()
        existing = result.setdefault(key, [])
        while len(existing) < len(entry.msgstrs):
            existing.append("")
        for index, value in enumerate(entry.msgstrs):
            if value and not existing[index]:
                existing[index] = value
    return result


def fallback_msgstrs(entry):
    if entry.msgid_plural is not None:
        return [entry.msgid or "", entry.msgid_plural or ""]
    return [entry.msgid or ""]


def merge_po_with_pot(po_entries, pot_entries, fill_empty):
    translations = translation_map(po_entries)
    merged = []
    for template in order_entries(pot_entries):
        entry = template.clone_template()
        fallback = fallback_msgstrs(entry)
        values = list(translations.get(entry.key(), []))
        while len(values) < len(fallback):
            values.append("")
        values = values[: len(fallback)]
        if fill_empty:
            values = [value if value else fallback[index] for index, value in enumerate(values)]
        entry.msgstrs = values
        merged.append(entry)
    return merged


def find_header(entries):
    for entry in entries:
        if entry.is_header:
            return entry
    return None


def entry_lines(entry, pot=False):
    lines = list(entry.comments)
    if entry.msgctxt is not None:
        lines.append(f"msgctxt {quote(entry.msgctxt)}")
    lines.append(f"msgid {quote(entry.msgid or '')}")
    if entry.msgid_plural is not None:
        lines.append(f"msgid_plural {quote(entry.msgid_plural)}")
        values = ["", ""] if pot else entry.msgstrs
        for index, value in enumerate(values):
            lines.append(f"msgstr[{index}] {quote(value)}")
    else:
        value = "" if pot else (entry.msgstrs[0] if entry.msgstrs else "")
        lines.append(f"msgstr {quote(value)}")
    lines.append("")
    return lines


def write_po(path, header, entries, pot=False):
    lines = []
    if header is not None:
        lines.extend(entry_lines(header))
    elif not pot:
        lines.extend(
            [
                'msgid ""',
                'msgstr "Content-Type: text/plain; charset=UTF-8\\nContent-Transfer-Encoding: 8bit\\n"',
                "",
            ]
        )
    for entry in entries:
        lines.extend(entry_lines(entry, pot=pot))
    path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")


def active_po_files(po_directory):
    return sorted(po_directory.glob("*.po"))


def validate(po_directory, pot_path):
    pot_entries = [entry for entry in parse_po(pot_path) if not entry.is_header]
    pot_keys = [entry.key() for entry in pot_entries]
    pot_set = set(pot_keys)
    failures = []

    pot_duplicates = [key for key, count in Counter(pot_keys).items() if count > 1]
    print(f"{pot_path.name}: entries={len(pot_keys)} unique={len(pot_set)} duplicate={len(pot_duplicates)}")
    if pot_duplicates:
        failures.append(f"{pot_path.name}: duplicate={len(pot_duplicates)}")

    for po_file in active_po_files(po_directory):
        entries = [entry for entry in parse_po(po_file) if not entry.is_header]
        keys = [entry.key() for entry in entries]
        key_set = set(keys)
        duplicates = [key for key, count in Counter(keys).items() if count > 1]
        missing = pot_set - key_set
        extra = key_set - pot_set
        empty = [
            entry.key()
            for entry in entries
            if any(value == "" for value in entry.msgstrs[: len(fallback_msgstrs(entry))])
        ]
        print(
            f"{po_file.name}: entries={len(keys)} unique={len(key_set)} "
            f"missing={len(missing)} extra={len(extra)} duplicate={len(duplicates)} empty={len(empty)}"
        )
        if missing or extra or duplicates or empty:
            failures.append(
                f"{po_file.name}: missing={len(missing)} extra={len(extra)} "
                f"duplicate={len(duplicates)} empty={len(empty)}"
            )

    if failures:
        raise SystemExit("PO validation failed:\n" + "\n".join(failures))


def normalize(po_directory, pot_path, fill_empty):
    parsed_pot = parse_po(pot_path)
    pot_header = find_header(parsed_pot)
    unique_pot = merge_duplicate_templates(parsed_pot)
    write_po(pot_path, pot_header, order_entries(unique_pot), pot=True)
    print(f"Updated {pot_path.name}: entries={len(unique_pot)}")

    for po_file in active_po_files(po_directory):
        po_entries = parse_po(po_file)
        header = find_header(po_entries)
        merged = merge_po_with_pot(po_entries, unique_pot, fill_empty)
        write_po(po_file, header, merged)
        print(f"Updated {po_file.name}: entries={len(merged)}")


def main():
    parser = argparse.ArgumentParser(description="Normalize and validate mpv.net gettext PO files.")
    parser.add_argument("--po-directory", default=str(Path(__file__).with_name("po")))
    parser.add_argument("--pot-path", default=str(Path(__file__).with_name("source.pot")))
    parser.add_argument("--fill-empty", action="store_true")
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()

    po_directory = Path(args.po_directory)
    pot_path = Path(args.pot_path)
    if not po_directory.exists():
        raise SystemExit(f"PO directory not found: {po_directory}")
    if not pot_path.exists():
        raise SystemExit(f"POT file not found: {pot_path}")

    if not args.validate_only:
        normalize(po_directory, pot_path, args.fill_empty)
    validate(po_directory, pot_path)


if __name__ == "__main__":
    main()
