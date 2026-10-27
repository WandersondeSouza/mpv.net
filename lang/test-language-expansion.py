"""Regression tests for catalog contracts and the offline MO compiler."""
import importlib
from collections import Counter
from pathlib import Path
import tempfile
import unittest

verify = importlib.import_module("verify-language-expansion")


class LanguageExpansionTests(unittest.TestCase):
    def test_all_catalogs_have_no_untranslated_prose_or_ui_labels(self):
        audit = importlib.import_module("audit-catalog-languages")
        self.assertEqual([], audit.audit()["candidates"])

    def test_language_audit_finds_english_prose_without_flagging_literal_identifiers(self):
        audit = importlib.import_module("audit-catalog-languages")
        with tempfile.TemporaryDirectory() as directory:
            fixture = Path(directory) / "pt_BR.po"
            fixture.write_text(
                'msgid "Allow only Vulkan"\nmsgstr "Allow only Vulkan"\n\n'
                'msgid "User interface display language. mpv.net must be restarted after a change."\n'
                'msgstr "Idioma da interface. mpv.net must be restarted after a change."\n\n'
                'msgid "audio-file-auto"\nmsgstr "audio-file-auto"\n\n'
                'msgid "GitHub Sponsors"\nmsgstr "GitHub Sponsors"\n\n'
                'msgid "Open file"\nmsgstr "Abrir arquivo"\n', encoding="utf-8")
            report = audit.audit(Path(directory))
            self.assertEqual(["identical_prose", "mixed_english_fragment"],
                             [entry["reason"] for entry in report["candidates"]])

    def test_all_catalogs_preserve_technical_tokens_and_readable_encoding(self):
        for path in sorted((verify.ROOT / "po").glob("*.po")):
            for entry in verify.compiler.parse_po(path):
                if not entry.msgid:
                    continue
                for translated in entry.msgstrs:
                    with self.subTest(locale=path.stem, source=entry.msgid):
                        self.assertIsNone(verify.CORRUPTION.search(translated))
                        # Strg is the German label printed on the Ctrl key.
                        comparable = translated.replace("Strg+", "Ctrl+") if path.stem == "de" else translated
                        self.assertEqual(Counter(verify.TOKENS.findall(entry.msgid)),
                                         Counter(verify.TOKENS.findall(comparable)))

    def test_all_expanded_catalogs_and_offline_mo_lookups(self):
        verify.verify()

    def test_validation_rejects_corrupted_tokens_and_text(self):
        cases = [
            ("Open {0}", "Ouvrir {1}"),
            ("Value {0:N0}", "Valeur {0}"),
            ("mpv-shot%n", "mpv-shot%d"),
            ("--vo=gpu", "--video=gpu"),
            ("Shift+Q", "ShiftQQ"),
            ("Help https://mpv.io/", "Aide https://example.com/"),
            ("Value 10000 K", "Valeur 1000 K"),
            (r"Text\nText", "Texte Texte"),
            ("Text", "Texte \ufffd"),
            ("Text", "TODO"),
        ]
        for source, translation in cases:
            with self.subTest(source=source):
                self.assertTrue(verify.contract_errors(source, translation))

    def test_context_utf8_plural_forms_and_nul_lengths(self):
        expected = {
            "zh_TW": [(0, 0), (1, 0), (2, 0)],
            "zh_HK": [(0, 0), (1, 0), (2, 0)],
            "hi_IN": [(0, 1), (1, 0), (2, 1)],
            "ar_SA": [(0, 0), (1, 1), (2, 2), (3, 3), (11, 4), (100, 5)],
            "uk_UA": [(1, 0), (2, 1), (5, 2), (11, 2), (21, 0), (22, 1)],
            "id_ID": [(0, 0), (1, 0), (2, 0)],
        }
        for locale, count in verify.LOCALES.items():
            with self.subTest(locale=locale):
                header = verify.compiler.parse_po(verify.ROOT / "po" / (locale + ".po"))[0]
                entry = verify.compiler.PoEntry()
                entry.msgctxt = "count"
                entry.msgid = "{0} item"
                entry.msgid_plural = "{0} items"
                entry.msgstrs = [f"عينة 樣本 зразок {i}: {{0}}" for i in range(count)]
                catalog = verify.load_catalog(verify.compiler.build_mo([header, entry]))
                self.assertEqual(catalog.info()["language"], locale)
                for number, index in expected[locale]:
                    self.assertEqual(catalog.npgettext("count", entry.msgid, entry.msgid_plural, number), entry.msgstrs[index])
                self.assertEqual(catalog.ngettext(entry.msgid, entry.msgid_plural, 2), entry.msgid_plural)


if __name__ == "__main__":
    unittest.main()
