import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EDITOR_CONF = ROOT / "src" / "MpvNet.Windows" / "Resources" / "editor_conf.txt"
PO_DIR = ROOT / "lang" / "po"

KEYWORDS = ["name", "directory", "help", "option"]


def parse_editor_conf(path: Path) -> set[str]:
    strings = set()

    for line in path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue

        parts = stripped.split("=", 1)
        if len(parts) != 2:
            continue

        key = parts[0].strip().lower()
        value = parts[1].strip()

        if key == "name":
            if value:
                strings.add(value)
        elif key == "directory":
            parts = [part.strip() for part in value.split("/") if part.strip()]
            strings.update(parts)
        elif key == "help":
            if value:
                strings.add(value)
        elif key == "option":
            if value:
                if " " in value:
                    name, help_text = value.split(" ", 1)
                    strings.add(name)
                    if help_text:
                        strings.add(help_text)
                else:
                    strings.add(value)
            if value:
                if " " in value:
                    name, help_text = value.split(" ", 1)
                    strings.add(name)
                    if help_text:
                        strings.add(help_text)
                else:
                    strings.add(value)

    return strings


def escape_po_string(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def read_active_msgids(po_path: Path) -> set[str]:
    content = po_path.read_text(encoding="utf-8")
    # Only active msgids, ignore commented-out entries (#~ msgid)
    msgids = set()
    for match in re.finditer(r'^(?!#~)msgid\s+"((?:[^"\\]|\\.)*)"', content, re.MULTILINE):
        msgids.add(bytes(match.group(1), "utf-8").decode("unicode_escape"))
    return msgids


def append_po_entries(po_path: Path, msgids: set[str], source_ref: str) -> int:
    existing = read_active_msgids(po_path)
    missing = sorted(msgids - existing)
    if not missing:
        return 0

    with po_path.open("a", encoding="utf-8") as f:
        f.write("\n# Editor config entries generated from editor_conf.txt\n")
        for msgid in missing:
            escaped = escape_po_string(msgid)
            f.write(f"#: {source_ref}\n")
            f.write(f"msgid \"{escaped}\"\n")
            f.write("msgstr \"\"\n\n")

    return len(missing)


if __name__ == "__main__":
    msgids = parse_editor_conf(EDITOR_CONF)
    if not msgids:
        raise SystemExit("No strings found in editor_conf.txt")

    po_files = sorted(PO_DIR.glob("*.po"))
    total = 0
    for po_file in po_files:
        count = append_po_entries(po_file, msgids, str(EDITOR_CONF))
        if count > 0:
            print(f"Updated {po_file.name}: added {count} entries")
            total += count
        else:
            print(f"No changes to {po_file.name}")

    print(f"Total new entries added: {total}")
