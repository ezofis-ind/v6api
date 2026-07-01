#!/usr/bin/env python3
"""
Generate INSERT SQL for dbo.ezfb_7f8a9b0c_items from legacy SSMS tab export.

Usage:
  python generate_ezfb_items_sql.py MSP_MF_ezfb_export.tsv > Insert_MSP_MF_ezfb_items.sql

Export notes:
  - Tab-separated, first line = header
  - Rows 1-211 may have legacy formEntry id in column 2 (25462, etc.) — auto-skipped
  - Rows 212+ often start with PO number directly in column 2
"""
from __future__ import annotations

import csv
import re
import sys
from pathlib import Path

TABLE = "dbo.ezfb_7f8a9b0c_items"
FORM_COLS = [
    "pLIax1zKPXRCdlnCOLHzy",
    "IChfsjDJrJsfQDRVM9EAD",
    "szG2cMaKkEtkY-V8dNQQU",
    "l7GRxwpdjcWv8LSz2BlmV",
    "cpyUkkJaPgvNbUQCb_z6e",
    "Cghd2DGqBZpvfmqwFmd8z",
    "bnWTWmxoqfafNDOrwqYVT",
    "Adcwz0bdXvk6dvSCeN9xB",
    "K7jbH86jW3DOVC9ElLn4z",
    "A6WOvkJ7iVzFiSp6uftqC",
    "8G9Iy7Dtj6ksL1ikYVmEp",
]
META_COLS = ["createdAt", "modifiedAt", "createdBy", "modifiedBy", "isDeleted", "todayTask", "isMarked"]

LEGACY_ENTRY_RE = re.compile(r"^\d{5,}$")  # 25462, 00025958, etc.


def sql_str(val: str | None) -> str:
    if val is None:
        return "NULL"
    v = val.strip()
    if v == "" or v.upper() == "NULL":
        return "NULL"
    return "N'" + v.replace("'", "''") + "'"


def sql_bit(val: str | None, default: str = "0") -> str:
    if val is None or str(val).strip() == "":
        return default
    v = str(val).strip().lower()
    if v in ("1", "true", "yes"):
        return "1"
    return "0"


def bracket(name: str) -> str:
    return f"[{name.replace(']', ']]')}]"


def field_offset(fields: list[str]) -> int:
    """Skip legacy numeric formEntry column after itemId when present."""
    if len(fields) < 3:
        return 0
    second = fields[1].strip()
    if LEGACY_ENTRY_RE.match(second):
        return 1
    return 0


def map_row(fields: list[str]) -> dict[str, str | None]:
    off = field_offset(fields)
    data: dict[str, str | None] = {"itemId": fields[0].strip()}

    # Legacy SSMS exports (rows with numeric formEntry id after itemId) often omit an
    # empty Ship-To tab, so form values shift left by one from PO Date onward.
    ship_to_idx = FORM_COLS.index("cpyUkkJaPgvNbUQCb_z6e")
    short_legacy = off == 1 and len(fields) == 19

    for i, col in enumerate(FORM_COLS):
        if short_legacy and i >= ship_to_idx:
            idx = 1 + off + i - 1
        else:
            idx = 1 + off + i
        if short_legacy and i == ship_to_idx:
            data[col] = None
            continue
        val = fields[idx].strip() if idx < len(fields) else ""
        data[col] = val if val and val.upper() != "NULL" else None

    if short_legacy:
        meta_start = 1 + off + len(FORM_COLS) - 1
    else:
        meta_start = 1 + off + len(FORM_COLS)
    for j, col in enumerate(META_COLS):
        idx = meta_start + j
        val = fields[idx].strip() if idx < len(fields) else ""
        data[col] = val if val and val.upper() != "NULL" else None
    return data


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: python generate_ezfb_items_sql.py <export.tsv>", file=sys.stderr)
        return 1

    path = Path(sys.argv[1])
    if not path.exists():
        print(f"File not found: {path}", file=sys.stderr)
        return 1

    rows: list[dict[str, str | None]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f, delimiter="\t")
        header = next(reader, None)
        for line in reader:
            if not line or not line[0].strip():
                continue
            rows.append(map_row(line))

    col_list = ", ".join([bracket(c) for c in FORM_COLS] + META_COLS)
    print("-- Auto-generated ezfb items bulk insert")
    print(f"-- Source: {path.name}  Rows: {len(rows)}")
    print(f"-- Table: {TABLE}")
    print("SET NOCOUNT ON;")
    print("SET XACT_ABORT ON;")
    print("BEGIN TRANSACTION;")
    print()
    print(f"IF OBJECT_ID(N'{TABLE}', N'U') IS NULL")
    print("BEGIN")
    print("    RAISERROR('Run ManualInsert_MSP_MF_MasterForm.sql first to create the table.', 16, 1);")
    print("    ROLLBACK TRANSACTION;")
    print("    RETURN;")
    print("END")
    print()
    print(f"SET IDENTITY_INSERT {TABLE} ON;")
    print()

    for r in rows:
        item_id = r["itemId"]
        vals = []
        for c in FORM_COLS:
            vals.append(sql_str(r.get(c)))
        vals.append(sql_str(r.get("createdAt")))
        vals.append(sql_str(r.get("modifiedAt")))
        vals.append(sql_str(r.get("createdBy") or "1"))
        vals.append(sql_str(r.get("modifiedBy") or "0"))
        vals.append(sql_bit(r.get("isDeleted"), "0"))
        vals.append(sql_bit(r.get("todayTask"), "1"))
        vals.append(sql_bit(r.get("isMarked"), "0"))

        print(f"IF NOT EXISTS (SELECT 1 FROM {TABLE} WHERE itemId = {item_id})")
        print(f"INSERT INTO {TABLE} (itemId, {col_list})")
        print(f"VALUES ({item_id}, {', '.join(vals)});")
        print()

    print(f"SET IDENTITY_INSERT {TABLE} OFF;")
    print("COMMIT TRANSACTION;")
    print(f"PRINT 'Inserted/verified {len(rows)} rows in {TABLE}';")
    print(f"SELECT COUNT(*) AS ItemCount FROM {TABLE};")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
