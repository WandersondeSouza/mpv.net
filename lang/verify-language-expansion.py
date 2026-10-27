"""Read-only checks for the six expanded catalogs and both MO compilers.

Run after validate-po-files.ps1; no translation filling or rewriting occurs here.
Only Python's standard library is required.
"""
import argparse
from collections import Counter
import gettext
import importlib
import io
from pathlib import Path
import re

normalize = importlib.import_module("normalize-po-files")
compiler = importlib.import_module("create-mo-files-fallback")
ROOT = Path(__file__).resolve().parent
LOCALES = {"zh_TW": 1, "zh_HK": 1, "hi_IN": 2, "ar_SA": 6, "uk_UA": 3, "id_ID": 1}
TOKENS = re.compile(r"\{\d+(?:,[^{}:]+)?(?::[^{}]+)?\}|\$\{[^}]+\}|%(?:\d+\$)?[-+#0 ]*(?:\d+)?(?:\.\d+)?[sdifn]|https?://[^\s]+|--[\w-]+|<[^>]+>|(?:Ctrl|Shift|Alt)\+[A-Za-z0-9]+|\\[nrt]")
NUMBERS = re.compile(r"(?<![A-Za-z])[-+]?\d+(?:\.\d+)*\+?")
CORRUPTION = re.compile(r"\ufffd|Ã.|Â[\x80-\xbf]|â€|ä¸|å­|æ­|ç¡|\bTODO\b|\bFIXME\b")
# Identifiers, filter names and numeric choices are deliberately literal.
BRANDS = {"GitHub Sponsors", "Codec Guide: ", "Pan & Scan"}


def contract_errors(source, translated):
    errors = []
    if Counter(TOKENS.findall(source)) != Counter(TOKENS.findall(translated)):
        errors.append("placeholders/escapes/flags/ranges/links/shortcuts")
    if set(NUMBERS.findall(source)) != set(NUMBERS.findall(translated)):
        errors.append("numeric literals")
    if CORRUPTION.search(translated):
        errors.append("encoding/TODO")
    return errors


def load_catalog(data):
    return gettext.GNUTranslations(io.BytesIO(data))


def verify(po_directory=ROOT / "po", mo_directory=None):
    source = [e for e in normalize.parse_po(ROOT / "source.pot") if not e.is_header]
    expected = {e.key() for e in source}
    extractor = importlib.import_module("update-source-pot-fallback")
    editor_keys = set(extractor.parse_editor_conf(ROOT.parent / "src/MpvNet.Windows/Resources/editor_conf.txt"))
    assert not editor_keys - expected, f"Editor keys missing from POT: {editor_keys - expected}"
    for locale, plural_count in LOCALES.items():
        path = po_directory / (locale + ".po")
        parsed = normalize.parse_po(path)
        entries = [e for e in parsed if not e.is_header]
        assert len(entries) == len(expected), f"{locale}: entry count/duplicates"
        assert {e.key() for e in entries} == expected, f"{locale}: key/context/plural parity"
        header = normalize.find_header(parsed)
        assert header and f"Language: {locale}\n" in header.msgstrs[0], f"{locale}: language header"
        assert f"nplurals={plural_count};" in header.msgstrs[0], f"{locale}: plural header"
        for entry in entries:
            assert not any("fuzzy" in c for c in entry.comments if c.startswith("#,")), f"{locale}: fuzzy {entry.msgid}"
            assert len(entry.msgstrs) == (plural_count if entry.msgid_plural else 1), f"{locale}: plural forms {entry.msgid}"
            for text in entry.msgstrs:
                assert text.strip(), f"{locale}: empty {entry.msgid}"
                errors = contract_errors(entry.msgid, text)
                assert not errors, f"{locale}: {errors}: {entry.msgid!r} => {text!r}"
                if text == entry.msgid:
                    assert " " not in text.strip() or text in BRANDS or re.fullmatch(r"\d+ %", text), f"{locale}: English fallback {text!r}"

        # Parse and look up every key in the produced binary, not just its header.
        fallback = load_catalog(compiler.build_mo(compiler.parse_po(path)))
        catalogs = [fallback]
        if mo_directory is not None:
            mo = mo_directory / "Locale" / locale / "LC_MESSAGES" / "mpvnet.mo"
            catalogs.append(gettext.GNUTranslations(mo.open("rb")))
        for catalog in catalogs:
            assert catalog.info()["language"] == locale
            for entry in entries:
                if entry.msgid_plural:
                    continue  # Source currently has no plural entries; fixtures test all rules.
                actual = catalog.pgettext(entry.msgctxt, entry.msgid) if entry.msgctxt else catalog.gettext(entry.msgid)
                assert actual == entry.msgstrs[0], f"{locale}: MO lookup {entry.key()}"
            plural_indices = {catalog.plural(n) for n in range(0, 201)}
            assert plural_indices == set(range(plural_count)), f"{locale}: plural evaluation {plural_indices}"
        print(f"{locale}: {len(entries)} keys, tokens, metadata, plurals and MO lookups OK")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mo-directory", type=Path)
    args = parser.parse_args()
    verify(mo_directory=args.mo_directory)
