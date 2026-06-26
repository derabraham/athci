#!/usr/bin/env python3
"""
ATHCI / EVA study analysis
==========================

Reusable analysis script for your Google Forms questionnaire export, the
counterbalancing table, and an optional Unity study log.

What this script does
---------------------
1. Reads the survey export and the counterbalancing table.
2. Converts Part 1 / Part 2 questionnaire answers into long format.
3. Maps Part 1 / Part 2 to the actual condition and task using the
   counterbalancing table.
4. Computes descriptive questionnaire scores for NASA-TLX, IOS, GSQ and UEQ-S.
5. Reads the optional study log and creates plots for task time, success,
   number of user questions and number of EVA interventions.
6. Exports clean CSV tables and publication-draft plots.

Expected current Google Forms structure
---------------------------------------
Demographics: columns 0-7
Part 1:        columns 8-45
Part 2:        columns 46-83
Open answers:  columns 84+

Per part:
- NASA-TLX raw items: 6 columns
- IOS: 1 column
- GSQ / Godspeed-like items: 24 columns
- UEQ-S items: 7 columns in the current CSV export

Note: The screenshots/Google Forms summary show all 24 Godspeed items.
The current CSV export has 31 columns after IOS per part, so with all
24 GSQ items only 7 columns remain for UEQ-S. In the current form the
missing UEQ-S item is Usual-Leading edge. This script therefore maps
GSQ correctly to 24 items and scores UEQ-S from the 7 available items.

Important: If you change the Google Form structure, update QUESTIONNAIRE_BLOCKS
below. The script validates the number of columns and prints warnings.

Usage examples
--------------
PowerShell, all in one line:

    python analyze_athci_survey.py --survey Survey_ATHCI.csv --counterbalancing "Counterbalancing_ATHCI.xlsx - Tabelle1.csv" --log study_log.csv --out athci_results

PowerShell, multi-line with backticks:

    python analyze_athci_survey.py `
      --survey Survey_ATHCI.csv `
      --counterbalancing "Counterbalancing_ATHCI.xlsx - Tabelle1.csv" `
      --log study_log.csv `
      --out athci_results

Dependencies:
    pip install pandas numpy matplotlib

Optional, only if you read .xlsx directly:
    pip install openpyxl
"""

from __future__ import annotations

import argparse
import math
import re
import shutil
import textwrap
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

import numpy as np
import pandas as pd

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt


# ---------------------------------------------------------------------------
# Configuration: update this if the Google Form column order changes.
# ---------------------------------------------------------------------------

PARTICIPANT_COL = "Participant-ID"

# Column index ranges are zero-based and end-exclusive.
# The current Google Forms export contains repeated matrix questions exported as
# "Unnamed" columns. Index ranges are therefore more reliable than column names.
QUESTIONNAIRE_BLOCKS = {
    1: {
        "nasa": range(8, 14),      # 6 NASA-TLX items
        "ios": range(14, 15),      # 1 IOS item
        "gsq": range(15, 39),      # 24 Godspeed / GSQ-like items
        "ueqs": range(39, 46),     # 7 UEQ-S items in current form export
    },
    2: {
        "nasa": range(46, 52),
        "ios": range(52, 53),
        "gsq": range(53, 77),
        "ueqs": range(77, 84),
    },
}

DEMOGRAPHIC_COLS = [
    "Participant-ID",
    "Sex",
    "Age",
    "Are you familiar with head-mounted virtual reality?",
    "Are you familiar with virtual agents?",
    "Are you a native English speaker?",
    "Do you have any accessibility needs?",
]

NASA_ITEMS = [
    "Mental Demand",
    "Physical Demand",
    "Temporal Demand",
    "Performance_success",  # high value means high success in your form
    "Effort",
    "Frustration",
]

# UEQ-S item order as exported by the current Google Form.
# Standard UEQ-S has 8 items. In the current CSV export only 7 UEQ-S columns
# remain after the 24 GSQ items. The form is missing the last UEQ-S item
# (Usual-Leading edge), so UEQ-S means are computed from the 7 available items.
# If you add the missing item later, update QUESTIONNAIRE_BLOCKS and this list.
UEQS_STANDARD_ITEMS = [
    "Obstructive-Supportive",
    "Complicated-Easy",
    "Inefficient-Efficient",
    "Confusing-Clear",
    "Boring-Exciting",
    "Not interesting-Interesting",
    "Conventional-Inventive",
    "Usual-Leading edge",
]

UEQS_ITEMS = [
    "Obstructive-Supportive",
    "Complicated-Easy",
    "Inefficient-Efficient",
    "Confusing-Clear",
    "Boring-Exciting",
    "Not interesting-Interesting",
    "Conventional-Inventive",
]

UEQS_PRAGMATIC_ITEMS = [
    "Obstructive-Supportive",
    "Complicated-Easy",
    "Inefficient-Efficient",
    "Confusing-Clear",
]
UEQS_HEDONIC_ITEMS = [
    "Boring-Exciting",
    "Not interesting-Interesting",
    "Conventional-Inventive",
    "Usual-Leading edge",
]

# Godspeed Questionnaire (GSQ) item order.
# The current Google Forms export contains all 24 standard Godspeed items per
# condition. The item labels do not appear in the CSV because the linear scale
# question text is empty in the form, so the mapping below must match the exact
# order in the Google Form.
GSQ_FULL_ITEMS = [
    ("Anthropomorphism", "Fake-Natural"),
    ("Anthropomorphism", "Machinelike-Humanlike"),
    ("Anthropomorphism", "Unconscious-Conscious"),
    ("Anthropomorphism", "Artificial-Lifelike"),
    ("Anthropomorphism", "Moving rigidly-Moving elegantly"),
    ("Animacy", "Dead-Alive"),
    ("Animacy", "Stagnant-Lively"),
    ("Animacy", "Mechanical-Organic"),
    ("Animacy", "Artificial-Lifelike"),
    ("Animacy", "Inert-Interactive"),
    ("Animacy", "Apathetic-Responsive"),
    ("Likeability", "Dislike-Like"),
    ("Likeability", "Unfriendly-Friendly"),
    ("Likeability", "Unkind-Kind"),
    ("Likeability", "Unpleasant-Pleasant"),
    ("Likeability", "Awful-Nice"),
    ("Perceived Intelligence", "Incompetent-Competent"),
    ("Perceived Intelligence", "Ignorant-Knowledgeable"),
    ("Perceived Intelligence", "Irresponsible-Responsible"),
    ("Perceived Intelligence", "Unintelligent-Intelligent"),
    ("Perceived Intelligence", "Foolish-Sensible"),
    ("Perceived Safety", "Anxious-Relaxed"),
    ("Perceived Safety", "Agitated-Calm"),
    ("Perceived Safety", "Quiescent-Surprised"),
]

# Some item-pair labels occur in more than one Godspeed subscale
# (e.g., Artificial-Lifelike). Internally we make those labels unique so pandas
# can score and plot them reliably. The plotting functions strip the suffix again
# when the subscale is already clear from the title.
_label_counts = pd.Series([label for _, label in GSQ_FULL_ITEMS]).value_counts().to_dict()

def make_gsq_item_id(subscale: str, label: str) -> str:
    return f"{label} ({subscale})" if _label_counts.get(label, 0) > 1 else label

GSQ_ITEMS = [make_gsq_item_id(subscale, label) for subscale, label in GSQ_FULL_ITEMS]
GSQ_ITEM_TO_SUBSCALE = {
    make_gsq_item_id(subscale, label): subscale
    for subscale, label in GSQ_FULL_ITEMS
}
GSQ_ITEM_DISPLAY_LABELS = {
    make_gsq_item_id(subscale, label): label
    for subscale, label in GSQ_FULL_ITEMS
}
GSQ_SUBSCALE_ORDER = [
    "Anthropomorphism",
    "Animacy",
    "Likeability",
    "Perceived Intelligence",
    "Perceived Safety",
]
GSQ_SUBSCALE_ITEMS = {
    subscale: [make_gsq_item_id(s, label) for s, label in GSQ_FULL_ITEMS if s == subscale]
    for subscale in GSQ_SUBSCALE_ORDER
}

QUESTIONNAIRE_ITEM_LABELS = {
    "nasa": NASA_ITEMS,
    "ios": ["IOS"],
    "gsq": GSQ_ITEMS,
    "ueqs": UEQS_ITEMS,
}

# NASA-TLX items are currently on a 1-10 scale and Performance is phrased
# positively: "How successful were you...?". For a workload score, we reverse
# Performance so that higher = more workload.
NASA_SCALE_MIN = 1
NASA_SCALE_MAX = 10

DEFAULT_CONDITION_ORDER = ["Tool", "Collab"]
DEFAULT_TASK_ORDER = ["Campus", "Escape Room"]


# ---------------------------------------------------------------------------
# I/O helpers
# ---------------------------------------------------------------------------

def read_table(path: Path) -> pd.DataFrame:
    """Read CSV/XLSX robustly. CSV separator is auto-detected."""
    suffix = path.suffix.lower()
    if suffix in {".xlsx", ".xls"}:
        return pd.read_excel(path)

    last_error: Optional[Exception] = None
    for enc in ("utf-8-sig", "utf-8", "latin1"):
        try:
            return pd.read_csv(path, sep=None, engine="python", encoding=enc)
        except Exception as exc:
            last_error = exc
    raise RuntimeError(f"Could not read {path}: {last_error}")


def find_file_by_keyword(directory: Path, keywords: Iterable[str]) -> Optional[Path]:
    """Find a likely file in directory if the default CLI path is missing."""
    lowered_keywords = [k.lower() for k in keywords]
    candidates: List[Path] = []
    for p in directory.iterdir():
        if p.is_file() and p.suffix.lower() in {".csv", ".xlsx", ".xls"}:
            name = p.name.lower()
            if all(k in name for k in lowered_keywords):
                candidates.append(p)
    if not candidates:
        return None
    candidates.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    return candidates[0]


def clean_column_names(df: pd.DataFrame) -> pd.DataFrame:
    """Strip whitespace and collapse repeated spaces in column names."""
    out = df.copy()
    out.columns = [re.sub(r"\s+", " ", str(c).replace("\ufeff", "").strip()) for c in out.columns]
    return out


def normalize_id(value) -> Optional[int]:
    """Convert participant IDs like '1', 1.0, 'P01' to int if possible."""
    if pd.isna(value):
        return None
    text = str(value).strip()
    try:
        return int(float(text))
    except ValueError:
        match = re.search(r"\d+", text)
        if match:
            return int(match.group(0))
    return None


def to_numeric_series(s: pd.Series) -> pd.Series:
    """Convert survey/log answers to numeric values; accepts decimal commas."""
    return pd.to_numeric(
        s.astype(str).str.replace(",", ".", regex=False).replace({"nan": np.nan, "None": np.nan, "": np.nan}),
        errors="coerce",
    )


def parse_binary_success(series: pd.Series) -> pd.Series:
    """Convert common binary values to 0/1 floats."""
    def one(x):
        if pd.isna(x):
            return np.nan
        if isinstance(x, (int, float, np.integer, np.floating)):
            if pd.isna(x):
                return np.nan
            return float(x)
        txt = str(x).strip().lower()
        if txt in {"1", "true", "yes", "y", "ja", "success", "successful", "passed"}:
            return 1.0
        if txt in {"0", "false", "no", "n", "nein", "fail", "failed", "unsuccessful"}:
            return 0.0
        try:
            return float(txt.replace(",", "."))
        except ValueError:
            return np.nan
    return series.map(one)


def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def clean_plots(plot_dir: Path) -> None:
    """Remove old PNGs so outdated plots do not remain after reruns."""
    ensure_dir(plot_dir)
    for p in plot_dir.glob("*.png"):
        p.unlink()


def safe_filename(text: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", text).strip("_").lower()


# ---------------------------------------------------------------------------
# Counterbalancing
# ---------------------------------------------------------------------------

def find_col(df: pd.DataFrame, candidates: Iterable[str]) -> str:
    """Find a column by case-insensitive, whitespace-insensitive name."""
    normalized = {re.sub(r"\s+", " ", c.lower()).strip(): c for c in df.columns}
    for cand in candidates:
        key = re.sub(r"\s+", " ", cand.lower()).strip()
        if key in normalized:
            return normalized[key]
    raise KeyError(f"Could not find one of these columns: {list(candidates)}. Available: {list(df.columns)}")


def normalize_condition(value) -> str:
    txt = "" if pd.isna(value) else str(value).strip()
    low = txt.lower()
    if "collab" in low or "partner" in low:
        return "Collab"
    if "tool" in low:
        return "Tool"
    return txt


def normalize_task(value) -> str:
    txt = "" if pd.isna(value) else str(value).strip()
    low = txt.lower().replace("_", " ").replace("-", " ")
    if "escape" in low:
        return "Escape Room"
    if "campus" in low:
        return "Campus"
    return txt


def counterbalancing_to_long(cb: pd.DataFrame) -> pd.DataFrame:
    cb = clean_column_names(cb)
    p_col = find_col(cb, ["Participant", "Participant-ID", "Participant ID", "pid"])
    c1_col = find_col(cb, ["Condition 1", "Condition1"])
    t1_col = find_col(cb, ["Task 1", "Task1"])
    c2_col = find_col(cb, ["Condition 2", "Condition2"])
    t2_col = find_col(cb, ["Task 2", "Task2"])

    rows = []
    for _, row in cb.iterrows():
        pid = normalize_id(row[p_col])
        if pid is None:
            continue
        rows.append({
            "participant_id": pid,
            "part": 1,
            "condition": normalize_condition(row[c1_col]),
            "task": normalize_task(row[t1_col]),
        })
        rows.append({
            "participant_id": pid,
            "part": 2,
            "condition": normalize_condition(row[c2_col]),
            "task": normalize_task(row[t2_col]),
        })
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Survey reshaping and scoring
# ---------------------------------------------------------------------------

def validate_blocks(survey: pd.DataFrame) -> List[str]:
    warnings: List[str] = []
    max_needed = max(max(cols) for block in QUESTIONNAIRE_BLOCKS.values() for cols in block.values())
    if survey.shape[1] <= max_needed:
        warnings.append(
            f"Survey has only {survey.shape[1]} columns, but the configured questionnaire ranges need column index {max_needed}."
        )
    for part, block in QUESTIONNAIRE_BLOCKS.items():
        for q_name, cols in block.items():
            expected = len(QUESTIONNAIRE_ITEM_LABELS[q_name])
            actual = len(list(cols))
            if expected != actual:
                warnings.append(f"Part {part} / {q_name}: configured {actual} columns but {expected} labels.")
    configured_gsq_cols = len(list(QUESTIONNAIRE_BLOCKS[1]["gsq"]))
    if configured_gsq_cols != len(GSQ_FULL_ITEMS):
        warnings.append(
            "GSQ: standard Godspeed has "
            f"{len(GSQ_FULL_ITEMS)} items, but the configured form has {configured_gsq_cols}. "
            "Edit QUESTIONNAIRE_BLOCKS and GSQ_ITEMS so the order matches your Google Form."
        )

    configured_ueqs_cols = len(list(QUESTIONNAIRE_BLOCKS[1]["ueqs"]))
    if configured_ueqs_cols != len(UEQS_STANDARD_ITEMS):
        missing_ueq = [x for x in UEQS_STANDARD_ITEMS if x not in UEQS_ITEMS]
        warnings.append(
            "UEQ-S: standard UEQ-S has "
            f"{len(UEQS_STANDARD_ITEMS)} items, but the current CSV mapping has {configured_ueqs_cols}. "
            f"Assuming missing item(s): {', '.join(missing_ueq) if missing_ueq else 'unknown'}. "
            "This does not affect GSQ; edit UEQS_ITEMS if your Google Form order differs."
        )
    return warnings


def extract_demographics(survey: pd.DataFrame) -> pd.DataFrame:
    cols: List[str] = []
    normalized = {re.sub(r"\s+", " ", c).strip(): c for c in survey.columns}
    for wanted in DEMOGRAPHIC_COLS:
        key = re.sub(r"\s+", " ", wanted).strip()
        if key in normalized:
            cols.append(normalized[key])
    demographics = survey[cols].copy() if cols else pd.DataFrame()
    demographics.columns = [re.sub(r"\s+", " ", c).strip() for c in demographics.columns]
    if PARTICIPANT_COL in demographics.columns:
        demographics["participant_id"] = demographics[PARTICIPANT_COL].apply(normalize_id)
    return demographics


def build_long_tables(survey: pd.DataFrame, cb_long: pd.DataFrame) -> Tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    survey = clean_column_names(survey)
    if PARTICIPANT_COL not in survey.columns:
        raise KeyError(f"Survey does not contain '{PARTICIPANT_COL}'. Available columns: {list(survey.columns)}")

    survey = survey.copy()
    survey["participant_id"] = survey[PARTICIPANT_COL].apply(normalize_id)
    demographics = extract_demographics(survey)

    item_rows = []
    score_rows = []
    cb_map = cb_long.set_index(["participant_id", "part"])

    for _, srow in survey.iterrows():
        pid = srow["participant_id"]
        if pid is None or pd.isna(pid):
            continue
        pid = int(pid)

        for part, block in QUESTIONNAIRE_BLOCKS.items():
            if (pid, part) not in cb_map.index:
                condition = np.nan
                task = np.nan
            else:
                cb_row = cb_map.loc[(pid, part)]
                condition = cb_row["condition"]
                task = cb_row["task"]

            per_questionnaire_values: Dict[str, pd.Series] = {}
            for q_name, col_range in block.items():
                cols = list(col_range)
                labels = QUESTIONNAIRE_ITEM_LABELS[q_name]
                values = to_numeric_series(srow.iloc[cols])
                values.index = labels
                per_questionnaire_values[q_name] = values

                for item, value in values.items():
                    item_rows.append({
                        "participant_id": pid,
                        "part": part,
                        "condition": condition,
                        "task": task,
                        "questionnaire": q_name.upper(),
                        "subscale": GSQ_ITEM_TO_SUBSCALE.get(item, np.nan) if q_name == "gsq" else np.nan,
                        "item": item,
                        "value": value,
                    })

            nasa = per_questionnaire_values["nasa"].copy()
            nasa_performance_reversed = NASA_SCALE_MAX + NASA_SCALE_MIN - nasa.get("Performance_success", np.nan)
            nasa_workload_items = pd.Series({
                "Mental Demand": nasa.get("Mental Demand", np.nan),
                "Physical Demand": nasa.get("Physical Demand", np.nan),
                "Temporal Demand": nasa.get("Temporal Demand", np.nan),
                "Effort": nasa.get("Effort", np.nan),
                "Frustration": nasa.get("Frustration", np.nan),
                "Performance_reversed": nasa_performance_reversed,
            })

            ios = per_questionnaire_values["ios"]
            gsq = per_questionnaire_values["gsq"]
            ueqs = per_questionnaire_values["ueqs"]

            score_rows.append({
                "participant_id": pid,
                "part": part,
                "condition": condition,
                "task": task,
                "nasa_raw_mean": nasa.mean(skipna=True),
                "nasa_workload_mean_perf_reversed": nasa_workload_items.mean(skipna=True),
                "nasa_mental": nasa.get("Mental Demand", np.nan),
                "nasa_physical": nasa.get("Physical Demand", np.nan),
                "nasa_temporal": nasa.get("Temporal Demand", np.nan),
                "nasa_performance_success": nasa.get("Performance_success", np.nan),
                "nasa_performance_reversed": nasa_performance_reversed,
                "nasa_effort": nasa.get("Effort", np.nan),
                "nasa_frustration": nasa.get("Frustration", np.nan),
                "ios": ios.mean(skipna=True),
                "gsq_mean": gsq.mean(skipna=True),
                "gsq_anthropomorphism": gsq.reindex(GSQ_SUBSCALE_ITEMS["Anthropomorphism"]).mean(skipna=True),
                "gsq_animacy": gsq.reindex(GSQ_SUBSCALE_ITEMS["Animacy"]).mean(skipna=True),
                "gsq_likeability": gsq.reindex(GSQ_SUBSCALE_ITEMS["Likeability"]).mean(skipna=True),
                "gsq_perceived_intelligence": gsq.reindex(GSQ_SUBSCALE_ITEMS["Perceived Intelligence"]).mean(skipna=True),
                "gsq_perceived_safety": gsq.reindex(GSQ_SUBSCALE_ITEMS["Perceived Safety"]).mean(skipna=True),
                "ueqs_mean": ueqs.mean(skipna=True),
                "ueqs_pragmatic_quality": ueqs.reindex([x for x in UEQS_PRAGMATIC_ITEMS if x in ueqs.index]).mean(skipna=True),
                "ueqs_hedonic_quality": ueqs.reindex([x for x in UEQS_HEDONIC_ITEMS if x in ueqs.index]).mean(skipna=True),
            })

    scores = pd.DataFrame(score_rows)
    items = pd.DataFrame(item_rows)
    return scores, items, demographics


# ---------------------------------------------------------------------------
# Study log parsing and performance summaries
# ---------------------------------------------------------------------------

def read_study_log(log_path: Path, cb_long: Optional[pd.DataFrame] = None) -> Tuple[pd.DataFrame, List[str]]:
    """Read and normalize the Unity study log."""
    warnings: List[str] = []
    raw = clean_column_names(read_table(log_path))
    cols_lower = {c.lower(): c for c in raw.columns}

    def col(candidates: Sequence[str], required: bool = True) -> Optional[str]:
        for cand in candidates:
            key = cand.lower()
            if key in cols_lower:
                return cols_lower[key]
        if required:
            raise KeyError(f"Study log is missing one of {list(candidates)}. Available columns: {list(raw.columns)}")
        return None

    pid_col = col(["pid", "participant_id", "participant", "Participant-ID"])
    timestamp_col = col(["timestamp", "time", "datetime"], required=False)
    level_col = col(["level_id", "level", "task", "task_id"], required=False)
    condition_col = col(["condition_id", "condition"], required=False)
    time_col = col(["task_time_seconds", "time_seconds", "duration_seconds", "duration", "task_time"])
    success_col = col(["success", "task_success", "completed"])
    questions_col = col(["num_questions", "questions", "number_questions"])
    interactions_col = col(["num_interactions", "interactions", "num_interaction", "number_interactions"])

    out = pd.DataFrame()
    out["participant_id"] = raw[pid_col].apply(normalize_id)
    out["timestamp"] = pd.to_datetime(raw[timestamp_col], errors="coerce") if timestamp_col else pd.NaT
    out["level_id"] = raw[level_col].astype(str) if level_col else ""
    out["condition_id"] = raw[condition_col].astype(str) if condition_col else ""
    out["condition"] = out["condition_id"].map(normalize_condition)
    if (out["condition"].astype(str).str.len() == 0).any() and level_col:
        fallback = out["level_id"].map(normalize_condition)
        out.loc[out["condition"].astype(str).str.len() == 0, "condition"] = fallback

    out["task"] = out["level_id"].map(normalize_task) if level_col else ""
    out["task_time_seconds"] = to_numeric_series(raw[time_col])
    out["task_time_minutes"] = out["task_time_seconds"] / 60.0
    out["success"] = parse_binary_success(raw[success_col])
    out["success_percent"] = out["success"] * 100.0
    out["num_questions"] = to_numeric_series(raw[questions_col])
    out["num_interactions"] = to_numeric_series(raw[interactions_col])

    # Sort by timestamp within participant to infer first/second task order.
    sort_cols = ["participant_id"] + (["timestamp"] if timestamp_col else [])
    out = out.sort_values(sort_cols).reset_index(drop=True)
    out["part"] = out.groupby("participant_id").cumcount() + 1

    # Check against counterbalancing if available.
    if cb_long is not None and not cb_long.empty:
        merged = out.merge(
            cb_long.rename(columns={"condition": "condition_cb", "task": "task_cb"}),
            on=["participant_id", "part"],
            how="left",
        )
        mismatch_condition = merged[
            merged["condition_cb"].notna()
            & merged["condition"].notna()
            & (merged["condition"].astype(str) != merged["condition_cb"].astype(str))
        ]
        mismatch_task = merged[
            merged["task_cb"].notna()
            & merged["task"].notna()
            & (merged["task"].astype(str) != merged["task_cb"].astype(str))
        ]
        if not mismatch_condition.empty:
            ids = sorted(mismatch_condition["participant_id"].dropna().astype(int).unique())
            warnings.append(f"Study log condition/order mismatches counterbalancing for participants: {ids}")
        if not mismatch_task.empty:
            ids = sorted(mismatch_task["participant_id"].dropna().astype(int).unique())
            warnings.append(f"Study log task/order mismatches counterbalancing for participants: {ids}")

    tool_interactions = out[(out["condition"] == "Tool") & (out["num_interactions"].fillna(0) != 0)]
    if not tool_interactions.empty:
        ids = sorted(tool_interactions["participant_id"].dropna().astype(int).unique())
        warnings.append(f"Tool condition has non-zero num_interactions for participants: {ids}")

    return out, warnings


def summarize_metrics(df: pd.DataFrame, group_cols: Sequence[str], metrics: Sequence[str]) -> pd.DataFrame:
    rows = []
    if df.empty:
        return pd.DataFrame()
    grouped = df.groupby(list(group_cols), dropna=False)
    for group_values, grp in grouped:
        if not isinstance(group_values, tuple):
            group_values = (group_values,)
        base = dict(zip(group_cols, group_values))
        for metric in metrics:
            values = pd.to_numeric(grp[metric], errors="coerce").dropna()
            n = int(len(values))
            sd = values.std(ddof=1) if n > 1 else np.nan
            se = sd / math.sqrt(n) if n > 1 else np.nan
            row = {
                **base,
                "metric": metric,
                "n": n,
                "mean": values.mean() if n else np.nan,
                "sd": sd,
                "se": se,
                "median": values.median() if n else np.nan,
                "min": values.min() if n else np.nan,
                "max": values.max() if n else np.nan,
            }
            if metric == "success":
                row["success_count"] = int(values.sum()) if n else 0
                row["success_rate_percent"] = values.mean() * 100.0 if n else np.nan
            rows.append(row)
    return pd.DataFrame(rows)


def summarize_scores(scores: pd.DataFrame) -> pd.DataFrame:
    metric_cols = [c for c in scores.columns if c not in {"participant_id", "part", "condition", "task"}]
    return summarize_metrics(scores, ["condition"], metric_cols)


def paired_differences(scores: pd.DataFrame, condition_a: str = "Collab", condition_b: str = "Tool") -> pd.DataFrame:
    metric_cols = [c for c in scores.columns if c not in {"participant_id", "part", "condition", "task"}]
    rows = []
    for metric in metric_cols:
        pivot = scores.pivot_table(index="participant_id", columns="condition", values=metric, aggfunc="first")
        if condition_a not in pivot.columns or condition_b not in pivot.columns:
            continue
        diff = pd.to_numeric(pivot[condition_a], errors="coerce") - pd.to_numeric(pivot[condition_b], errors="coerce")
        diff = diff.dropna()
        n = len(diff)
        sd = diff.std(ddof=1) if n > 1 else np.nan
        dz = diff.mean() / sd if n > 1 and sd != 0 else np.nan
        rows.append({
            "metric": metric,
            "contrast": f"{condition_a} - {condition_b}",
            "n_pairs": n,
            "mean_difference": diff.mean() if n else np.nan,
            "sd_difference": sd,
            "median_difference": diff.median() if n else np.nan,
            "cohens_dz": dz,
            "min_difference": diff.min() if n else np.nan,
            "max_difference": diff.max() if n else np.nan,
        })
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Open questions / preference parsing
# ---------------------------------------------------------------------------

def extract_open_answers(survey: pd.DataFrame) -> pd.DataFrame:
    survey = clean_column_names(survey).copy()
    survey["participant_id"] = survey[PARTICIPANT_COL].apply(normalize_id)

    last_q_col = max(max(cols) for block in QUESTIONNAIRE_BLOCKS.values() for cols in block.values())
    open_cols = list(survey.columns[last_q_col + 1:])
    rows = []
    for _, row in survey.iterrows():
        pid = row["participant_id"]
        if pd.isna(pid):
            continue
        for col in open_cols:
            answer = row[col]
            if pd.isna(answer) or str(answer).strip() == "":
                continue
            rows.append({"participant_id": int(pid), "question": col, "answer": str(answer).strip()})
    return pd.DataFrame(rows)


def infer_preferred_condition(open_answers: pd.DataFrame, cb_long: pd.DataFrame) -> pd.DataFrame:
    """Naively map textual 'version 1/2' preference to the real condition."""
    if open_answers.empty:
        return pd.DataFrame()

    preference_q = open_answers[
        open_answers["question"].str.contains("prefer", case=False, na=False)
        | open_answers["question"].str.contains("preferred", case=False, na=False)
        | open_answers["question"].str.contains("version", case=False, na=False)
    ].copy()
    if preference_q.empty:
        return pd.DataFrame()

    cb_map_condition = cb_long.set_index(["participant_id", "part"])["condition"].to_dict()
    cb_map_task = cb_long.set_index(["participant_id", "part"])["task"].to_dict()
    rows = []
    for _, row in preference_q.iterrows():
        text = row["answer"].lower()
        pid = int(row["participant_id"])
        preferred_part = np.nan
        if re.search(r"\b(version\s*)?1\b|\bfirst\b|\berste", text):
            preferred_part = 1
        if re.search(r"\b(version\s*)?2\b|\bsecond\b|\bzweite", text):
            if pd.isna(preferred_part) or re.search(r"^\s*(version\s*)?2\b|^\s*second\b|^\s*zweite", text):
                preferred_part = 2
        preferred_condition = cb_map_condition.get((pid, int(preferred_part))) if not pd.isna(preferred_part) else np.nan
        preferred_task = cb_map_task.get((pid, int(preferred_part))) if not pd.isna(preferred_part) else np.nan
        rows.append({
            "participant_id": pid,
            "answer": row["answer"],
            "preferred_part_inferred": preferred_part,
            "preferred_condition_inferred": preferred_condition,
            "preferred_task_inferred": preferred_task,
            "note": "Check manually before reporting; simple text heuristic.",
        })
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Plotting
# ---------------------------------------------------------------------------

METRIC_LABELS = {
    "nasa_raw_mean": "NASA-TLX raw mean",
    "nasa_workload_mean_perf_reversed": "NASA-TLX workload mean (performance reversed)",
    "nasa_mental": "NASA-TLX Mental Demand",
    "nasa_physical": "NASA-TLX Physical Demand",
    "nasa_temporal": "NASA-TLX Temporal Demand",
    "nasa_performance_success": "NASA-TLX Performance / Success",
    "nasa_performance_reversed": "NASA-TLX Performance reversed",
    "nasa_effort": "NASA-TLX Effort",
    "nasa_frustration": "NASA-TLX Frustration",
    "ios": "IOS closeness",
    "gsq_mean": "GSQ mean",
    "gsq_anthropomorphism": "GSQ Anthropomorphism",
    "gsq_animacy": "GSQ Animacy",
    "gsq_likeability": "GSQ Likeability",
    "gsq_perceived_intelligence": "GSQ Perceived Intelligence",
    "gsq_perceived_safety": "GSQ Perceived Safety",
    "ueqs_mean": "UEQ-S mean",
    "ueqs_pragmatic_quality": "UEQ-S pragmatic quality",
    "ueqs_hedonic_quality": "UEQ-S hedonic quality",
    "task_time_seconds": "Task time",
    "task_time_minutes": "Task time",
    "success": "Task success",
    "success_percent": "Task success",
    "num_questions": "Number of questions",
    "num_interactions": "Number of EVA interventions",
}

METRIC_AXIS_LABELS = {
    "task_time_seconds": "Mean time (seconds)",
    "task_time_minutes": "Mean time (minutes)",
    "success": "Success rate (%)",
    "success_percent": "Success rate (%)",
    "num_questions": "Mean number of questions",
    "num_interactions": "Mean number of EVA interventions",
}


def pretty_metric(metric: str) -> str:
    return METRIC_LABELS.get(metric, metric.replace("_", " "))


def axis_label(metric: str) -> str:
    return METRIC_AXIS_LABELS.get(metric, "Mean rating")


def ordered_unique(values: Iterable, preferred_order: Sequence[str]) -> List[str]:
    present = [str(v) for v in pd.Series(list(values)).dropna().unique()]
    ordered = [v for v in preferred_order if v in present]
    ordered += sorted([v for v in present if v not in ordered])
    return ordered


def savefig(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    plt.tight_layout()
    plt.savefig(path, dpi=300, bbox_inches="tight")
    plt.close()


def mean_se(values: pd.Series) -> Tuple[float, float, int]:
    vals = pd.to_numeric(values, errors="coerce").dropna()
    n = len(vals)
    if n == 0:
        return np.nan, np.nan, 0
    sd = vals.std(ddof=1) if n > 1 else np.nan
    se = sd / math.sqrt(n) if n > 1 else np.nan
    return float(vals.mean()), float(se) if not pd.isna(se) else np.nan, int(n)


def plot_bar_with_points(
    df: pd.DataFrame,
    group_col: str,
    metric: str,
    out_path: Path,
    title: str,
    preferred_order: Sequence[str],
    scale: float = 1.0,
) -> None:
    data = df[[group_col, metric]].copy().dropna(subset=[group_col])
    data[metric] = pd.to_numeric(data[metric], errors="coerce") * scale
    data = data.dropna(subset=[metric])
    if data.empty:
        return

    categories = ordered_unique(data[group_col], preferred_order)
    x = np.arange(len(categories))
    means, ses = [], []
    for cat in categories:
        m, se, _ = mean_se(data.loc[data[group_col].astype(str) == cat, metric])
        means.append(m)
        ses.append(se)

    fig, ax = plt.subplots(figsize=(max(5.5, 1.25 * len(categories)), 4.4))
    ax.bar(x, means, yerr=ses, capsize=5, alpha=0.75, label="Mean +/- SE")

    rng = np.random.default_rng(1)
    for i, cat in enumerate(categories):
        vals = data.loc[data[group_col].astype(str) == cat, metric].dropna().to_numpy()
        jitter = rng.normal(0, 0.035, size=len(vals))
        ax.scatter(np.full(len(vals), x[i]) + jitter, vals, s=32, alpha=0.75, zorder=3, label="Individual task" if i == 0 else None)

    ax.set_xticks(x)
    ax.set_xticklabels(categories)
    ax.set_ylabel(axis_label(metric))
    ax.set_title(title)
    ax.grid(axis="y", alpha=0.25)
    if metric in {"success", "success_percent"}:
        ax.set_ylim(0, 105)
    ax.legend()
    savefig(out_path)


def plot_grouped_bar_with_points(
    df: pd.DataFrame,
    x_col: str,
    hue_col: str,
    metric: str,
    out_path: Path,
    title: str,
    x_order: Sequence[str],
    hue_order: Sequence[str],
    scale: float = 1.0,
) -> None:
    data = df[[x_col, hue_col, metric]].copy().dropna(subset=[x_col, hue_col])
    data[metric] = pd.to_numeric(data[metric], errors="coerce") * scale
    data = data.dropna(subset=[metric])
    if data.empty:
        return

    xs = ordered_unique(data[x_col], x_order)
    hues = ordered_unique(data[hue_col], hue_order)
    if not xs or not hues:
        return

    n_hue = len(hues)
    x = np.arange(len(xs))
    width = min(0.8 / max(n_hue, 1), 0.38)

    fig, ax = plt.subplots(figsize=(max(6.5, 1.45 * len(xs)), 4.6))
    rng = np.random.default_rng(2)

    for j, hue in enumerate(hues):
        offset = (j - (n_hue - 1) / 2) * width
        means, ses = [], []
        for xv in xs:
            vals = data[(data[x_col].astype(str) == xv) & (data[hue_col].astype(str) == hue)][metric]
            m, se, _ = mean_se(vals)
            means.append(m)
            ses.append(se)
        ax.bar(x + offset, means, width=width, yerr=ses, capsize=4, alpha=0.75, label=hue)

        for i, xv in enumerate(xs):
            vals = data[(data[x_col].astype(str) == xv) & (data[hue_col].astype(str) == hue)][metric].dropna().to_numpy()
            jitter = rng.normal(0, width * 0.08, size=len(vals))
            ax.scatter(np.full(len(vals), x[i] + offset) + jitter, vals, s=28, alpha=0.65, zorder=3)

    ax.set_xticks(x)
    ax.set_xticklabels(xs)
    ax.set_ylabel(axis_label(metric))
    ax.set_title(title)
    ax.grid(axis="y", alpha=0.25)
    if metric in {"success", "success_percent"}:
        ax.set_ylim(0, 105)
    ax.legend(title=hue_col.replace("_", " "))
    savefig(out_path)


def wrap_label(label: str, width: int = 32) -> str:
    """Wrap long labels without changing the underlying data label."""
    return "\n".join(textwrap.wrap(str(label), width=width, break_long_words=False, break_on_hyphens=False))


def plot_gsq_subscale_item_bars(items: pd.DataFrame, group_col: str, out_dir: Path) -> None:
    """Create one GSQ item plot per Godspeed subscale with readable item labels."""
    data = items[(items["questionnaire"].str.lower() == "gsq") & (items["subscale"].notna())].copy()
    if data.empty or group_col not in data.columns:
        return
    data["value"] = pd.to_numeric(data["value"], errors="coerce")

    for subscale in GSQ_SUBSCALE_ORDER:
        wanted_items = [item for item in GSQ_SUBSCALE_ITEMS.get(subscale, []) if item in set(data["item"])]
        if not wanted_items:
            continue
        sub = data[data["item"].isin(wanted_items)].copy()
        summary = sub.groupby(["item", group_col], dropna=False)["value"].mean().reset_index()
        pivot = summary.pivot(index="item", columns=group_col, values="value")
        if pivot.empty:
            continue
        pivot = pivot.reindex(wanted_items)
        pivot.index = [wrap_label(GSQ_ITEM_DISPLAY_LABELS.get(x, x), width=36) for x in pivot.index]

        fig_height = max(3.8, 0.55 * len(pivot))
        ax = pivot.plot(kind="barh", figsize=(8.8, fig_height))
        ax.set_xlabel("Mean rating (1-5)")
        ax.set_ylabel("")
        ax.set_title(f"GSQ {subscale} by {group_col}")
        ax.grid(axis="x", alpha=0.25)
        savefig(out_dir / f"items_gsq_{safe_filename(subscale)}_by_{safe_filename(group_col)}.png")


def plot_item_bars(items: pd.DataFrame, questionnaire: str, group_col: str, out_dir: Path) -> None:
    data = items[items["questionnaire"].str.lower() == questionnaire.lower()].copy()
    if data.empty or group_col not in data.columns:
        return
    data["value"] = pd.to_numeric(data["value"], errors="coerce")
    summary = data.groupby(["item", group_col], dropna=False)["value"].mean().reset_index()
    pivot = summary.pivot(index="item", columns=group_col, values="value")
    if pivot.empty:
        return

    # Keep configured item order.
    labels = QUESTIONNAIRE_ITEM_LABELS.get(questionnaire.lower(), list(pivot.index))
    pivot = pivot.reindex([x for x in labels if x in pivot.index])

    fig_height = max(4.5, 0.40 * len(pivot))
    pivot.index = [wrap_label(x, width=38) for x in pivot.index]
    ax = pivot.plot(kind="barh", figsize=(9.2, fig_height))
    ax.set_xlabel("Mean rating")
    ax.set_ylabel("")
    ax.set_title(f"{questionnaire.upper()} items by {group_col}")
    ax.grid(axis="x", alpha=0.25)
    savefig(out_dir / f"items_{safe_filename(questionnaire)}_by_{safe_filename(group_col)}.png")


def plot_score_bars(scores: pd.DataFrame, group_col: str, out_dir: Path) -> None:
    """Generate only the curated score plots kept for the current analysis folder."""
    main_metrics = ["ios"] if group_col == "condition" else []
    order = DEFAULT_CONDITION_ORDER if group_col == "condition" else DEFAULT_TASK_ORDER
    for metric in main_metrics:
        if metric in scores.columns:
            plot_bar_with_points(
                scores,
                group_col=group_col,
                metric=metric,
                out_path=out_dir / f"score_{safe_filename(metric)}_by_{safe_filename(group_col)}.png",
                title=f"{pretty_metric(metric)} by {group_col}",
                preferred_order=order,
            )


def plot_paired_metric(scores: pd.DataFrame, metric: str, out_dir: Path, preferred_order: Tuple[str, ...] = ("Tool", "Collab")) -> None:
    data = scores[["participant_id", "condition", metric]].copy()
    data[metric] = pd.to_numeric(data[metric], errors="coerce")
    data = data.dropna(subset=[metric, "condition"])
    if data.empty:
        return

    conditions = ordered_unique(data["condition"], preferred_order)
    x_pos = np.arange(len(conditions))

    fig, ax = plt.subplots(figsize=(6.5, 4.5))
    pivot = data.pivot_table(index="participant_id", columns="condition", values=metric, aggfunc="first")

    for _, row in pivot.iterrows():
        y = [row.get(c, np.nan) for c in conditions]
        if np.count_nonzero(~pd.isna(y)) >= 1:
            ax.plot(x_pos, y, marker="o", linewidth=1, alpha=0.45)

    means = data.groupby("condition")[metric].mean().reindex(conditions)
    ses = data.groupby("condition")[metric].sem().reindex(conditions)
    ax.errorbar(x_pos, means, yerr=ses, marker="D", linewidth=2.5, capsize=5, label="Mean +/- SE")

    ax.set_xticks(x_pos)
    ax.set_xticklabels(conditions)
    ax.set_ylabel(pretty_metric(metric))
    ax.set_title(pretty_metric(metric))
    ax.grid(axis="y", alpha=0.25)
    ax.legend()
    savefig(out_dir / f"paired_{safe_filename(metric)}.png")


def plot_all_paired_metrics(scores: pd.DataFrame, out_dir: Path) -> None:
    main_metrics = ["nasa_workload_mean_perf_reversed", "ios", "gsq_mean", "ueqs_mean"]
    for metric in main_metrics:
        if metric in scores.columns:
            plot_paired_metric(scores, metric, out_dir)


def plot_demographics(demo: pd.DataFrame, out_dir: Path, warnings: List[str]) -> None:
    if demo.empty:
        return

    if "Age" in demo.columns:
        age = to_numeric_series(demo["Age"]).dropna()
        age_summary = pd.DataFrame({
            "n": [len(age)],
            "mean": [age.mean() if len(age) else np.nan],
            "sd": [age.std(ddof=1) if len(age) > 1 else np.nan],
            "median": [age.median() if len(age) else np.nan],
            "min": [age.min() if len(age) else np.nan],
            "max": [age.max() if len(age) else np.nan],
        })
        age_summary.to_csv(out_dir.parent / "tables" / "demographics_age_summary.csv", index=False)
        if len(age) >= 10:
            fig, ax = plt.subplots(figsize=(6, 4))
            ax.hist(age, bins=min(8, max(3, len(age))))
            ax.set_xlabel("Age")
            ax.set_ylabel("Count")
            ax.set_title("Participant age")
            savefig(out_dir / "demographics_age_histogram.png")
        else:
            warnings.append(f"Skipped age plot because only n={len(age)} ages are available.")

    categorical_candidates = [
        "Sex",
        "Are you familiar with head-mounted virtual reality?",
        "Are you familiar with virtual agents?",
        "Are you a native English speaker?",
    ]
    for col in categorical_candidates:
        if col in demo.columns:
            counts = demo[col].dropna().astype(str).str.strip().value_counts().sort_index()
            if counts.empty:
                continue
            fig, ax = plt.subplots(figsize=(7, 4))
            counts.plot(kind="bar", ax=ax)
            ax.set_xlabel(col)
            ax.set_ylabel("Count")
            ax.set_title(col)
            ax.tick_params(axis="x", labelrotation=35)
            savefig(out_dir / f"demographics_{safe_filename(col)}.png")


def plot_preference(preferences: pd.DataFrame, out_dir: Path) -> None:
    if preferences.empty or "preferred_condition_inferred" not in preferences.columns:
        return
    counts = preferences["preferred_condition_inferred"].dropna().astype(str).value_counts()
    if counts.empty:
        return
    fig, ax = plt.subplots(figsize=(6, 4))
    counts.plot(kind="bar", ax=ax)
    ax.set_xlabel("Inferred preferred condition")
    ax.set_ylabel("Count")
    ax.set_title("Naively inferred condition preference")
    ax.tick_params(axis="x", labelrotation=0)
    savefig(out_dir / "preference_condition_inferred.png")


def plot_counterbalancing_counts(cb_long: pd.DataFrame, out_dir: Path) -> None:
    if cb_long.empty:
        return
    counts = cb_long.groupby(["task", "condition"]).size().reset_index(name="count")
    plot_grouped_bar_with_points(
        counts,
        x_col="task",
        hue_col="condition",
        metric="count",
        out_path=out_dir / "counterbalancing_counts_task_by_condition.png",
        title="Counterbalancing cells: task by condition",
        x_order=DEFAULT_TASK_ORDER,
        hue_order=DEFAULT_CONDITION_ORDER,
    )


def plot_log_metrics(log_df: pd.DataFrame, out_dir: Path) -> None:
    """Generate only the curated study-log plots.

    Kept plots:
    - task time in seconds by condition, by task, and by task x condition
    - success by task x condition
    - number of questions by task x condition
    - EVA interventions in Collab only by task
    """
    if log_df.empty:
        return

    if "task_time_seconds" in log_df.columns:
        plot_bar_with_points(
            log_df,
            group_col="condition",
            metric="task_time_seconds",
            out_path=out_dir / "log_task_time_seconds_by_condition.png",
            title="Task time by condition",
            preferred_order=DEFAULT_CONDITION_ORDER,
        )
        plot_bar_with_points(
            log_df,
            group_col="task",
            metric="task_time_seconds",
            out_path=out_dir / "log_task_time_seconds_by_task.png",
            title="Task time by task",
            preferred_order=DEFAULT_TASK_ORDER,
        )
        plot_grouped_bar_with_points(
            log_df,
            x_col="task",
            hue_col="condition",
            metric="task_time_seconds",
            out_path=out_dir / "log_task_time_seconds_by_task_condition.png",
            title="Task time by task and condition",
            x_order=DEFAULT_TASK_ORDER,
            hue_order=DEFAULT_CONDITION_ORDER,
        )

    if "success" in log_df.columns:
        plot_grouped_bar_with_points(
            log_df,
            x_col="task",
            hue_col="condition",
            metric="success",
            out_path=out_dir / "log_success_by_task_condition.png",
            title="Task success by task and condition",
            x_order=DEFAULT_TASK_ORDER,
            hue_order=DEFAULT_CONDITION_ORDER,
            scale=100.0,
        )

    if "num_questions" in log_df.columns:
        plot_grouped_bar_with_points(
            log_df,
            x_col="task",
            hue_col="condition",
            metric="num_questions",
            out_path=out_dir / "log_num_questions_by_task_condition.png",
            title="Number of questions by task and condition",
            x_order=DEFAULT_TASK_ORDER,
            hue_order=DEFAULT_CONDITION_ORDER,
        )

    # Collab-only intervention plot: more meaningful because Tool should be zero.
    if "num_interactions" in log_df.columns:
        collab = log_df[log_df["condition"] == "Collab"].copy()
        if not collab.empty:
            plot_bar_with_points(
                collab,
                group_col="task",
                metric="num_interactions",
                out_path=out_dir / "log_num_interactions_collab_only_by_task.png",
                title="EVA interventions in Collab by task",
                preferred_order=DEFAULT_TASK_ORDER,
            )


# ---------------------------------------------------------------------------
# Report helpers
# ---------------------------------------------------------------------------

def df_to_markdown_simple(df: pd.DataFrame, floatfmt: str = ".3f") -> str:
    """Small markdown table helper that avoids requiring the optional tabulate package."""
    if df.empty:
        return ""
    show = df.copy()
    for col in show.columns:
        if pd.api.types.is_float_dtype(show[col]):
            show[col] = show[col].map(lambda x: "" if pd.isna(x) else format(float(x), floatfmt))
        else:
            show[col] = show[col].map(lambda x: "" if pd.isna(x) else str(x))
    headers = list(show.columns)
    rows = show.values.tolist()
    widths = [len(str(h)) for h in headers]
    for row in rows:
        for i, val in enumerate(row):
            widths[i] = max(widths[i], len(str(val)))
    def fmt_row(row):
        return "| " + " | ".join(str(v).ljust(widths[i]) for i, v in enumerate(row)) + " |"
    sep = "| " + " | ".join("-" * widths[i] for i in range(len(widths))) + " |"
    return "\n".join([fmt_row(headers), sep] + [fmt_row(r) for r in rows])


def write_markdown_report(
    out_path: Path,
    survey: pd.DataFrame,
    cb_long: pd.DataFrame,
    scores: pd.DataFrame,
    summary_condition: pd.DataFrame,
    paired: pd.DataFrame,
    warnings: List[str],
    log_df: Optional[pd.DataFrame] = None,
    log_summary_task: Optional[pd.DataFrame] = None,
    log_summary_task_condition: Optional[pd.DataFrame] = None,
) -> None:
    n_participants = scores["participant_id"].nunique() if not scores.empty else 0
    conditions = ", ".join(sorted([str(c) for c in scores["condition"].dropna().unique()])) if not scores.empty else "n/a"
    tasks = ", ".join(sorted([str(t) for t in scores["task"].dropna().unique()])) if not scores.empty else "n/a"

    lines = []
    lines.append("# ATHCI Analysis Report")
    lines.append("")
    lines.append(f"Survey rows: {len(survey)}")
    lines.append(f"Participants with parsed questionnaire data: {n_participants}")
    lines.append(f"Conditions found in survey/counterbalancing: {conditions}")
    lines.append(f"Tasks found in survey/counterbalancing: {tasks}")
    if log_df is not None and not log_df.empty:
        lines.append(f"Study log rows: {len(log_df)}")
        lines.append(f"Participants with study log data: {log_df['participant_id'].nunique()}")
    lines.append("")

    if warnings:
        lines.append("## Warnings")
        lines.extend([f"- {w}" for w in warnings])
        lines.append("")

    lines.append("## Important scoring note")
    lines.append(
        "NASA Performance is phrased positively in the form ('How successful were you...?'). "
        "The table therefore includes both `nasa_raw_mean` and "
        "`nasa_workload_mean_perf_reversed`, where Performance is reversed so that higher values consistently indicate higher workload."
    )
    lines.append("")

    main_metrics = [
        "nasa_workload_mean_perf_reversed", "ios", "gsq_mean", "ueqs_mean",
        "ueqs_pragmatic_quality", "ueqs_hedonic_quality",
    ]
    main_summary = summary_condition[summary_condition["metric"].isin(main_metrics)].copy() if not summary_condition.empty else pd.DataFrame()
    if not main_summary.empty:
        lines.append("## Main questionnaire summary by condition")
        lines.append(df_to_markdown_simple(main_summary, floatfmt=".3f"))
        lines.append("")

    if log_summary_task is not None and not log_summary_task.empty:
        selected = log_summary_task[log_summary_task["metric"].isin(["task_time_seconds", "task_time_minutes", "success", "num_questions", "num_interactions"])].copy()
        if not selected.empty:
            lines.append("## Study log summary by task")
            lines.append(df_to_markdown_simple(selected, floatfmt=".3f"))
            lines.append("")

    if log_summary_task_condition is not None and not log_summary_task_condition.empty:
        selected = log_summary_task_condition[
            log_summary_task_condition["metric"].isin(["task_time_seconds", "success", "num_questions", "num_interactions"])
        ].copy()
        if not selected.empty:
            lines.append("## Study log summary by task and condition")
            lines.append(df_to_markdown_simple(selected, floatfmt=".3f"))
            lines.append("")

    if not paired.empty:
        main_paired = paired[paired["metric"].isin(main_metrics)].copy()
        if not main_paired.empty:
            lines.append("## Optional paired differences")
            lines.append("These are exported as tables, but paired plots are disabled unless `--include-paired-plots` is set.")
            lines.append(df_to_markdown_simple(main_paired, floatfmt=".3f"))
            lines.append("")

    lines.append("## Generated tables")
    lines.append("- `tables/counterbalancing_long.csv`")
    lines.append("- `tables/condition_scores_long.csv`")
    lines.append("- `tables/item_responses_long.csv`")
    lines.append("- `tables/condition_summary.csv`")
    lines.append("- `tables/task_summary.csv`")
    lines.append("- `tables/task_condition_summary.csv`")
    lines.append("- `tables/open_answers.csv`")
    lines.append("- `tables/preference_from_open_text.csv`")
    if log_df is not None and not log_df.empty:
        lines.append("- `tables/study_log_clean.csv`")
        lines.append("- `tables/study_log_summary_by_task.csv`")
        lines.append("- `tables/study_log_summary_by_condition.csv`")
        lines.append("- `tables/study_log_summary_by_task_condition.csv`")
    lines.append("")
    lines.append("## Generated plots")
    lines.append("- Questionnaire item plots: `plots/items_*_by_condition.png` and `plots/items_*_by_task.png`")
    lines.append("- Curated questionnaire score plot: `plots/score_ios_by_condition.png`")
    if log_df is not None and not log_df.empty:
        lines.append("- Curated study log plots for time, success, questions and Collab-only interventions")
    lines.append("")
    lines.append("For small N, use the plots to inspect trends and data quality, not for strong statistical claims.")

    out_path.write_text("\n".join(lines), encoding="utf-8")


# ---------------------------------------------------------------------------
# Pipeline
# ---------------------------------------------------------------------------

def run_analysis(
    survey_path: Path,
    cb_path: Path,
    out_dir: Path,
    log_path: Optional[Path] = None,
    include_paired_plots: bool = False,
) -> None:
    ensure_dir(out_dir)
    table_dir = out_dir / "tables"
    plot_dir = out_dir / "plots"
    ensure_dir(table_dir)
    ensure_dir(plot_dir)
    clean_plots(plot_dir)

    survey = clean_column_names(read_table(survey_path))
    cb = clean_column_names(read_table(cb_path))

    warnings = validate_blocks(survey)
    cb_long = counterbalancing_to_long(cb)
    scores, items, demographics = build_long_tables(survey, cb_long)

    summary_condition = summarize_metrics(
        scores,
        ["condition"],
        [c for c in scores.columns if c not in {"participant_id", "part", "condition", "task"}],
    )
    summary_task = summarize_metrics(
        scores,
        ["task"],
        [c for c in scores.columns if c not in {"participant_id", "part", "condition", "task"}],
    )
    summary_task_condition = summarize_metrics(
        scores,
        ["task", "condition"],
        [c for c in scores.columns if c not in {"participant_id", "part", "condition", "task"}],
    )
    paired = paired_differences(scores, condition_a="Collab", condition_b="Tool")
    open_answers = extract_open_answers(survey)
    preferences = infer_preferred_condition(open_answers, cb_long)

    # Data validation warnings
    survey_pids = set(scores["participant_id"].dropna().astype(int).unique()) if not scores.empty else set()
    cb_pids = set(cb_long["participant_id"].dropna().astype(int).unique())
    missing_cb = [int(x) for x in sorted(survey_pids - cb_pids)]
    if missing_cb:
        warnings.append(f"Survey contains participants missing in counterbalancing table: {missing_cb}")
    missing_survey = [int(x) for x in sorted(cb_pids - survey_pids)]
    if missing_survey:
        warnings.append(f"Counterbalancing contains participants without survey rows yet: {missing_survey}")

    # Save questionnaire tables
    cb_long.to_csv(table_dir / "counterbalancing_long.csv", index=False)
    demographics.to_csv(table_dir / "demographics.csv", index=False)
    scores.to_csv(table_dir / "condition_scores_long.csv", index=False)
    items.to_csv(table_dir / "item_responses_long.csv", index=False)
    summary_condition.to_csv(table_dir / "condition_summary.csv", index=False)
    summary_task.to_csv(table_dir / "task_summary.csv", index=False)
    summary_task_condition.to_csv(table_dir / "task_condition_summary.csv", index=False)
    paired.to_csv(table_dir / "paired_differences.csv", index=False)
    open_answers.to_csv(table_dir / "open_answers.csv", index=False)
    preferences.to_csv(table_dir / "preference_from_open_text.csv", index=False)

    # Questionnaire plots (curated set only).
    plot_score_bars(scores, "condition", plot_dir)
    for questionnaire in ["nasa", "ios", "ueqs", "gsq"]:
        plot_item_bars(items, questionnaire, "condition", plot_dir)
        plot_item_bars(items, questionnaire, "task", plot_dir)
    plot_gsq_subscale_item_bars(items, "condition", plot_dir)
    if include_paired_plots:
        plot_all_paired_metrics(scores, plot_dir)
    plot_demographics(demographics, plot_dir, warnings)

    # Optional study log
    log_df: Optional[pd.DataFrame] = None
    log_summary_task: Optional[pd.DataFrame] = None
    log_summary_condition: Optional[pd.DataFrame] = None
    log_summary_task_condition: Optional[pd.DataFrame] = None

    if log_path is not None:
        if log_path.exists():
            log_df, log_warnings = read_study_log(log_path, cb_long)
            warnings.extend(log_warnings)
            log_metrics = ["task_time_seconds", "task_time_minutes", "success", "success_percent", "num_questions", "num_interactions"]
            log_summary_task = summarize_metrics(log_df, ["task"], log_metrics)
            log_summary_condition = summarize_metrics(log_df, ["condition"], log_metrics)
            log_summary_task_condition = summarize_metrics(log_df, ["task", "condition"], log_metrics)

            log_df.to_csv(table_dir / "study_log_clean.csv", index=False)
            log_summary_task.to_csv(table_dir / "study_log_summary_by_task.csv", index=False)
            log_summary_condition.to_csv(table_dir / "study_log_summary_by_condition.csv", index=False)
            log_summary_task_condition.to_csv(table_dir / "study_log_summary_by_task_condition.csv", index=False)
            plot_log_metrics(log_df, plot_dir)
        else:
            warnings.append(f"Study log path was provided but not found: {log_path}")

    write_markdown_report(
        out_dir / "analysis_report.md",
        survey=survey,
        cb_long=cb_long,
        scores=scores,
        summary_condition=summary_condition,
        paired=paired,
        warnings=warnings,
        log_df=log_df,
        log_summary_task=log_summary_task,
        log_summary_task_condition=log_summary_task_condition,
    )

    print(f"Done. Results written to: {out_dir.resolve()}")
    if warnings:
        print("\nWarnings:")
        for w in warnings:
            print(f"- {w}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Analyze ATHCI survey, counterbalancing and optional Unity study log.")
    parser.add_argument("--survey", default="Survey_ATHCI.csv", help="Path to Google Forms survey CSV/XLSX export.")
    parser.add_argument("--counterbalancing", default="counterbalancing.csv", help="Path to counterbalancing CSV/XLSX.")
    parser.add_argument("--log", default=None, help="Optional path to Unity study log CSV/XLSX. If omitted, the script auto-detects study_log*.csv when present.")
    parser.add_argument("--out", default="athci_results", help="Output directory.")
    parser.add_argument("--include-paired-plots", action="store_true", help="Also generate paired plots. Disabled by default.")
    return parser.parse_args()


def resolve_input_path(path_text: Optional[str], fallback_keywords: Sequence[str], cwd: Path, required: bool = True) -> Optional[Path]:
    if path_text:
        path = Path(path_text)
        if path.exists():
            return path
        found = find_file_by_keyword(cwd, fallback_keywords)
        if found:
            print(f"File not found at {path}. Using auto-detected file: {found}")
            return found
        if required:
            raise FileNotFoundError(f"File not found: {path}")
        return None

    found = find_file_by_keyword(cwd, fallback_keywords)
    return found


def main() -> None:
    args = parse_args()
    cwd = Path.cwd()

    survey_path = resolve_input_path(args.survey, ["survey", "athci"], cwd, required=True)
    cb_path = resolve_input_path(args.counterbalancing, ["counterbalancing", "athci"], cwd, required=True)
    log_path = resolve_input_path(args.log, ["study", "log"], cwd, required=False)

    if log_path is None:
        print("No study log detected/provided. Survey-only analysis will be generated.")

    run_analysis(
        survey_path=survey_path,
        cb_path=cb_path,
        log_path=log_path,
        out_dir=Path(args.out),
        include_paired_plots=args.include_paired_plots,
    )


if __name__ == "__main__":
    main()
