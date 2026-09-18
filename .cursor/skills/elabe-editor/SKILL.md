---
name: elabe-editor
description: >-
  Maintains the RefreshVIR WinForms ELABE table editor (ElabeEditorForm, dbo.t_e_elabe_2026,
  BIZHO/TERMEK_KOD/E_ELABE validation, row lock, Rögzít/Elvet). Use when working on ELABE
  editor, ElabeEditorForm, t_e_elabe_2026, BIZHO, TERMEK_KOD, E_ELABE, Új/Módosít/Töröl/Frissít,
  or grid insert/edit/delete in RefreshVIR.
---

# ELABE editor

Workspace is `C:\VIR\.cursor\RefreshVIR` (WinForms, Hungarian UI). This is **not** the Excel importer.

Code: `ElabeEditorForm.cs`. Opened from main menu button **ELABE editor** (`MainForm`).

Keep the form a short, simple class. UI strings stay Hungarian. After changes, `dotnet build` and actually verify the flow.

## Table

`dbo.t_e_elabe_2026`

| Column | Role |
|---|---|
| `ID` | Identity PK. Read-only in the grid. Use it to identify rows for UPDATE/DELETE. After INSERT, write `OUTPUT INSERTED.ID` back onto the row. |
| `BIZHO` | `int`, **YYYYMM**: exactly 6 digits, month 01–12. Example: `202609`. |
| `TERMEK_KOD` | Required. Cannot be empty. |
| `TERMEK_NEV` | Optional. |
| `E_ELABE` | Required number (Hungarian decimal comma OK). |

Load with `SELECT ID, BIZHO, TERMEK_KOD, TERMEK_NEV, E_ELABE ... ORDER BY ID`, asynchronously, so the UI does not freeze.

## UI contract

- Top toolbar: **Új**, **Módosít**, **Töröl**, **Frissít**, search box, **Keres**
- Stretched `DataGridView` between toolbar and bottom
- Bottom: **<< Vissza** (same as **ESC**)
- Grid is read-only until Új or Módosít
- Do not allow resizing rows
- Maximize the form; no maximize/minimize buttons

### Browse vs edit/insert

Browse: Új, Módosít, Töröl, Frissít, search box, Keres visible. Rögzít/Elvet hidden.

Edit or insert: Új, Módosít, Töröl, Frissít, search box, Keres **hidden**. **Rögzít** (save) and **Elvet** (cancel) visible.

**Frissít** reloads from SQL. Only in browse. Keep the current row selected when that `ID` still exists.

**Keres** (browse only): case-insensitive substring across every column. Focus and select the first hit. **F3** does nothing in edit/insert; in browse, after a previous hit, it seeks the next match (wraps; Hungarian message if none). Empty term / no hit also use Hungarian dialogs.

**Töröl** confirms in Hungarian, then deletes by `ID`.

Closing the form (Vissza, ESC, X) during edit/insert asks whether to discard. **No** restores focus to the row being edited.

## Modes

Lock **one** `DataRow` (`_lockedDataRow`), not a grid index (INSERT can shift indices).

- **Módosít**: only the current row is editable. User cannot leave it until Rögzít or Elvet.
- **Új**: append an empty row at the end; same lock. Rögzít INSERTs; Elvet cancels and removes the new row.
- INSERT lock must be as strict as EDIT (no leaving the new row).
- Gold highlight on the locked row (`255,224,130` / selected `255,179,0`) so it stays visible while scrolling.
- After Új or Módosít, focus **BIZHO** and start typing immediately.
- On Módosít, show the **original** BIZHO (and other values). Clear a cell only after an invalid value.

## Validation

Validate in **both** INSERT and EDIT.

| Field | Rule |
|---|---|
| BIZHO | 6 digits YYYYMM, month 01–12. `2222` is invalid. Do **not** format as `000000` (that displayed `2222` as `002222`). |
| TERMEK_KOD | Required. |
| E_ELABE | Required, must be a number. |

Invalid value: short Hungarian message, clear **only that cell's editor**, put the cell back in edit focus after the dialog closes. Do not make the user click the cell again.

Missing required value: Hungarian "kitöltése kötelező" message. Do **not** say the cell was cleared.

**Failed validation must abort save.** Stay in EDIT/INSERT. Never persist an empty BIZHO (or other field) because validation failed.

Rögzít uses `CausesValidation = false`, so `CellValidating` often does **not** run. Always validate:

1. Current editor text (before `EndEdit`)
2. `EndEdit` result / `_cellValidationFailed`
3. Bound `DataRow` values (unpadded `int.ToString()` for BIZHO — do not pad then parse)

If BIZHO parse fails, do not let the default int converter accept a short number.

## WinForms pitfalls (do not regress)

These failed in this editor before. Do not reintroduce them.

- **Rögzít / Elvet must work.** Do not `Cancel` `RowValidating` for toolbar clicks. Wire those buttons with `MouseDown` + `CausesValidation = false`. Toolbar `CausesValidation = false`. Use `AutoValidate.EnableAllowFocusChange`.
- **No reentrant `SetCurrentCellAddressCore`.** Guard selection snap-back with `_syncingSelection`. Do not set `CurrentCell` from inside `RowValidating` in a way that re-enters.
- **Row leave:** in `RowValidating`, cancel only when the click is on **another grid row** (or keys that would change row: Up/Down/Page/Home/End). Allow Vissza/close via `_allowRowLeave`.
- **`FocusCellForEdit`:** `clearValue: true` only after invalid input. Default is keep the current value. When programmatically ending edit to refocus, suppress cell validating (`_suppressCellValidating`) so required-field checks do not loop.
- **Do not write `DBNull` onto the `DataRow` during failed validation** if that would let Rögzít save an empty field. Clear the editing control only; abort save.
- Build after every change. `RowValidating` uses `DataGridViewCellCancelEventHandler` (`DataGridViewCellCancelEventArgs`).

## Messages

User-facing errors are short Hungarian dialogs, not technical DataError text like "Adat megjelenítési hiba (ELABE editor)". Log actions with `SQLUtils.LogAction`.
