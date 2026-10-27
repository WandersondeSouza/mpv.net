"""Regression tests for catalog contracts and the offline MO compiler."""
import importlib
import unittest

verify = importlib.import_module("verify-language-expansion")


class LanguageExpansionTests(unittest.TestCase):
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
