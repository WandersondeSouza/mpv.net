# Localization tools

This folder groups the maintenance scripts used for gettext files.

## Checks

- `checks/check_bg_dups.py`
- `checks/find_dups.py`
- `checks/find_po_dups.py`
- `checks/find_po_dups2.py`

These scripts inspect `lang/source.pot` and `lang/po/*.po` for duplicate entries.

## Cleanup

- `cleanup/clean_bg_po.py`
- `cleanup/clean_bg_po_v2.py`
- `cleanup/clean_source_pot.py`

These scripts rewrite gettext files to remove duplicate message definitions.

Run them from the repository root so the relative `lang/...` paths resolve correctly.

## Reference

- `reference/msgattrib_help.txt`

This file stores the localized `msgattrib` help text used while working on gettext maintenance.
