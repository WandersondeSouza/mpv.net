"""Read-only screening for untranslated prose and mixed English in every PO.

This complements structural validation; it is not a native-language review.
Names, commands and configuration values can legitimately remain literal.
Run with --output <path.json> to retain every candidate for manual review.
Only Python's standard library is required. No text is sent to a service.
"""
import argparse
from collections import Counter
import importlib
import json
from pathlib import Path
import re

compiler = importlib.import_module("create-mo-files-fallback")
ROOT = Path(__file__).resolve().parent
BRANDS = {"GitHub Sponsors", "Codec Guide: "}
UI_LABELS = {
    "About", "Donation", "Center", "Left", "Right", "Up", "Down", "Retry",
    "Abort", "Exit", "Enlarge", "Shrink", "Confirm", "Forced", "External",
    "Half", "Height", "Ignore", "Input", "Mute", "Progress", "Reset",
    "Scaling", "Screenshot", "Size", "Speed", "Width", "Yes", "Repository: ",
}
# Exclude literal identifiers, syntax and extension lists before comparing prose.
LITERALS = re.compile(
    r"https?://\S+|\$\{[^}]+\}|\{\d+[^}]*\}|<[^>]+>|"
    r"\b\w+(?:,\w+){2,}\b|[\w]+(?:[-_./=][\w+-]+)+|\\[nrt]"
)
ENGLISH_SENTENCE_WORDS = {
    "the", "this", "when", "with", "which", "requires", "instead", "default",
    "enable", "disable", "only", "used", "using", "use", "for", "from", "and",
    "will", "can", "more", "must", "after", "before", "between", "allows",
}


def words(text):
    return re.findall(r"[A-Za-z]{2,}", LITERALS.sub(" ", text).lower())


def audit(po_directory=ROOT / "po"):
    summaries, candidates = [], []
    for path in sorted(po_directory.glob("*.po")):
        entries = [e for e in compiler.parse_po(path) if e.msgid]
        counts = Counter()
        for entry in entries:
            source_words = words(entry.msgid)
            source_grams = {tuple(source_words[i:i + 4]) for i in range(len(source_words) - 3)}
            for plural_index, translated in enumerate(entry.msgstrs):
                reason, fragments = None, []
                if (translated == entry.msgid and translated not in BRANDS
                        and re.search(r"[A-Za-z]+\s+[A-Za-z]+", translated)):
                    reason = "identical_prose"
                elif translated == entry.msgid and translated in UI_LABELS:
                    reason = "untranslated_ui_label"
                elif translated != entry.msgid:
                    translated_words = words(translated)
                    for i in range(len(translated_words) - 3):
                        gram = tuple(translated_words[i:i + 4])
                        if gram in source_grams and set(gram) & ENGLISH_SENTENCE_WORDS:
                            fragments.append(" ".join(gram))
                    if fragments:
                        reason = "mixed_english_fragment"
                if reason:
                    counts[reason] += 1
                    candidates.append({
                        "locale": path.stem, "file": str(path), "context": entry.msgctxt,
                        "plural_index": plural_index, "reason": reason,
                        "source": entry.msgid, "translation": translated,
                        "fragments": list(dict.fromkeys(fragments)),
                    })
        summaries.append({"locale": path.stem, "entries": len(entries),
                          "identical_prose": counts["identical_prose"],
                          "untranslated_ui_label": counts["untranslated_ui_label"],
                          "mixed_english_fragment": counts["mixed_english_fragment"]})
    return {"summaries": summaries, "candidates": candidates,
            "limitation": "Candidates require review; zero candidates does not certify linguistic correctness."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--fail-on-candidates", action="store_true")
    args = parser.parse_args()
    report = audit()
    for summary in report["summaries"]:
        print(f"{summary['locale']}: entries={summary['entries']}, "
              f"identical_prose={summary['identical_prose']}, "
              f"untranslated_ui_label={summary['untranslated_ui_label']}, "
              f"mixed_english_fragment={summary['mixed_english_fragment']}")
    print(f"Total review candidates: {len(report['candidates'])}")
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.fail_on_candidates and report["candidates"]:
        raise SystemExit(1)
