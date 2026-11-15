#!/usr/bin/env python3
"""Space Dilemma: event-time anchored RadLab + DONKI analyzer.

The RadLab instrument absorbs radiation into a detector.  Output ``game_mSv``
uses an arbitrary GAMEPLAY multiplier and is NOT a NASA astronaut effective dose.

Python >=3.10; standard library only.
"""
from __future__ import annotations

import argparse
import csv
import io
import json
import math
import statistics
import sys
import urllib.error
import urllib.parse
import urllib.request
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

UTC = timezone.utc
HOUR = timedelta(hours=1)
MINUTE = timedelta(minutes=1)
DONKI_URL = "https://ccmc.gsfc.nasa.gov/DONKI-API/get/SEP"
RADLAB_URL = "https://visualization.osdr.nasa.gov/radlab/api/"
MISSION_HOURS = 48
MISSION_PLAY_SECONDS = 600
SEP_PLAY_SECONDS = 480


@dataclass(frozen=True)
class Reading:
    time: datetime
    instrument_id: str
    location: str
    measurement: str
    value: float
    source: str


@dataclass(frozen=True)
class Event:
    time: datetime
    event_id: str
    instruments: str
    source: str


def iso(t: datetime) -> str:
    return t.astimezone(UTC).isoformat(timespec="seconds").replace("+00:00", "Z")


def parse_time(raw: Any) -> datetime:
    if isinstance(raw, datetime):
        return raw.astimezone(UTC) if raw.tzinfo else raw.replace(tzinfo=UTC)
    txt = str(raw).strip()
    if not txt:
        raise ValueError("Empty UTC timestamp")
    if txt.endswith("Z"):
        txt = txt[:-1] + "+00:00"
    t = datetime.fromisoformat(txt)
    return t.astimezone(UTC) if t.tzinfo else t.replace(tzinfo=UTC)


def get_text(url: str, timeout: int = 35) -> str:
    req = urllib.request.Request(url, headers={
        "User-Agent": "SpaceDilemma-MoonSEPAnalyzer/7.0 (research)",
        "Accept": "text/csv,application/json,text/plain,*/*",
    })
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            return response.read().decode("utf-8-sig", errors="replace")
    except urllib.error.HTTPError as exc:
        body = exc.read(350).decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {exc.code}: {body} | {url}") from exc
    except (urllib.error.URLError, TimeoutError) as exc:
        raise RuntimeError(f"Network error: {exc} | {url}") from exc


def radlab_url(start: datetime, end: datetime, instrument_id: str | None = None) -> str:
    """Key-value grammar: timestamp>=DATE is 'timestamp>' key plus '=DATE'."""
    pairs: list[tuple[str, str]] = []
    if instrument_id:
        pairs.append(("instrument_id", instrument_id))
    else:
        pairs.append(("celestial_body", "Moon"))
    pairs.extend([
        ("timestamp>", iso(start)[:16]),
        ("timestamp<", iso(end)[:16]),
        ("absorbed_dose_rate", ""),
        ("dose_equivalent_rate", ""),
        ("instrument", ""),
        ("spacecraft", ""),
        ("celestial_body", "") if instrument_id else ("trajectory", ""),
        ("trajectory", "") if instrument_id else ("instrument_id", ""),
        ("format", "csv"),
    ])
    return RADLAB_URL + "?" + urllib.parse.urlencode(pairs)


def donki_url(start: datetime, end: datetime) -> str:
    return DONKI_URL + "?" + urllib.parse.urlencode({
        "startDate": start.date().isoformat(),
        "endDate": end.date().isoformat(),
    })


def safe_number(raw: Any) -> float | None:
    try:
        n = float(str(raw).strip())
    except (ValueError, TypeError):
        return None
    return n if math.isfinite(n) and n >= 0 else None


def load_observations(text: str, source: str) -> list[Reading]:
    rdr = csv.DictReader(io.StringIO(text))
    if not rdr.fieldnames or "timestamp" not in rdr.fieldnames:
        raise ValueError(f"Missing 'timestamp' column in {source}")
    fields = set(rdr.fieldnames)
    normalized = {"measurement", "unit", "value"}.issubset(fields)
    if not normalized and not ({"absorbed_dose_rate", "dose_equivalent_rate"} & fields):
        raise ValueError(f"No radiation dose rate field in {source}: {rdr.fieldnames}")
    out: list[Reading] = []
    for row in rdr:
        try:
            t = parse_time(row["timestamp"])
        except (ValueError, TypeError):
            continue
        instrument = (row.get("instrument_id") or row.get("instrument") or "UNKNOWN").strip()
        loc = (row.get("observation_location") or row.get("trajectory") or "UNKNOWN").strip()
        if normalized:
            measurement = (row.get("measurement") or "").strip()
            if measurement not in ("absorbed_dose_rate", "dose_equivalent_rate"):
                continue
            unit = (row.get("unit") or "").strip().lower().replace("μ", "u").replace("µ", "u")
            expected = "ugy/h" if measurement == "absorbed_dose_rate" else "usv/h"
            if unit != expected:
                continue
            n = safe_number(row.get("value"))
            if n is not None:
                out.append(Reading(t, instrument, loc, measurement, n, source))
        else:
            for measurement in ("absorbed_dose_rate", "dose_equivalent_rate"):
                n = safe_number(row.get(measurement))
                if n is not None:
                    out.append(Reading(t, instrument, loc, measurement, n, source))
    return out


def parse_donki(data: Any, source: str) -> list[Event]:
    if isinstance(data, dict):
        for key in ("events", "data", "results", "items"):
            if isinstance(data.get(key), list):
                data = data[key]
                break
    if not isinstance(data, list):
        raise ValueError(f"Unexpected DONKI JSON shape from {source}")
    out: list[Event] = []
    for item in data:
        if not isinstance(item, dict) or not item.get("eventTime"):
            continue
        try:
            t = parse_time(item["eventTime"])
        except ValueError:
            continue
        obs = item.get("instruments") or []
        if isinstance(obs, list):
            instruments = "; ".join(str(x.get("displayName", x)) if isinstance(x, dict) else str(x) for x in obs)
        else:
            instruments = str(obs)
        out.append(Event(t, str(item.get("sepID") or item.get("activityID") or "UNKNOWN"), instruments, source))
    return out


def fetch_donki(start: datetime, end: datetime) -> tuple[list[Event], list[str]]:
    events: dict[tuple[datetime, str], Event] = {}
    urls: list[str] = []
    cursor = start
    while cursor < end:
        until = min(cursor + timedelta(days=28), end)
        url = donki_url(cursor, until)
        urls.append(url)
        response = json.loads(get_text(url))
        for event in parse_donki(response, "NASA_DONKI_API"):
            if start <= event.time <= end:
                events[(event.time, event.event_id)] = event
        cursor = until
    return sorted(events.values(), key=lambda ev: ev.time), urls


def fetch_radlab(start: datetime, end: datetime, instrument_id: str) -> tuple[list[Reading], list[str]]:
    """Try exact detector first, then broad Moon query if needed."""
    urls: list[str] = []
    for ident in (instrument_id, None):
        url = radlab_url(start, end, ident)
        urls.append(url)
        rows = load_observations(get_text(url), "NASA_RADLAB_API")
        matches = [r for r in rows if r.instrument_id == instrument_id]
        if any(r.measurement == "absorbed_dose_rate" for r in matches):
            return matches, urls
    return [], urls


def nearest_hour_end(t: datetime) -> datetime:
    """Hourly CRaTER measurement timestamps are usually at HH:59:59 or HH:00:00.

    Assume sample time marks the END of its 1-h measurement interval.
    Offset timestamps (e.g. HH:16) are rounded; quality is checked separately.
    """
    v = t + timedelta(minutes=30)
    return v.replace(minute=0, second=0, microsecond=0)


def hourly_index(readings: list[Reading], measurement: str, instrument_id: str) -> tuple[dict[datetime, Reading], int]:
    buckets: dict[datetime, Reading] = {}
    collision = 0
    for r in readings:
        if r.measurement != measurement or r.instrument_id != instrument_id:
            continue
        key = nearest_hour_end(r.time) - HOUR
        if key in buckets:
            collision += 1
            # Keep sample closest to the bin end, deterministic for duplicates.
            prev = buckets[key]
            if abs((r.time - (key + HOUR)).total_seconds()) < abs((prev.time - (key + HOUR)).total_seconds()):
                buckets[key] = r
        else:
            buckets[key] = r
    return buckets, collision


def floor_hour(t: datetime) -> datetime:
    return t.replace(minute=0, second=0, microsecond=0)


def iter_segments(index: dict[datetime, Reading], start: datetime, end: datetime):
    current = floor_hour(start)
    while current < end:
        lower = max(current, start)
        upper = min(current + HOUR, end)
        hours = (upper - lower).total_seconds() / 3600.0
        if hours > 0:
            yield current, index.get(current), hours
        current += HOUR


def window_stats(index: dict[datetime, Reading], start: datetime, end: datetime, baseline: float | None = None):
    requested = (end - start).total_seconds() / 3600
    observed = 0.0
    dose = 0.0
    peak = 0.0
    missing: list[str] = []
    observed_vals: list[float] = []
    excess = 0.0
    for hour, row, hours in iter_segments(index, start, end):
        if row is None:
            missing.append(iso(hour))
            continue
        observed += hours
        dose += hours * row.value
        peak = max(peak, row.value)
        observed_vals.append(row.value)
        if baseline is not None:
            excess += hours * max(row.value - baseline, 0)
    return {
        "coverage": (observed / requested if missing else 1.0) if requested > 0 else 0.0,
        "hours_observed": observed,
        "hours_requested": requested,
        "ugy_observed": dose,
        "mean_ugy_per_h_observed": dose / observed if observed else None,
        "peak_ugy_per_h": peak if observed_vals else None,
        "median_sample_ugy_per_h": statistics.median(observed_vals) if observed_vals else None,
        "sampled_hour_bins": len(observed_vals),
        "missing_hour_starts": missing,
        "excess_ugy_above_baseline": excess if baseline is not None else None,
    }


def sep_early_rise(index: dict[datetime, Reading], t: datetime, baseline: float, factor: float = 1.5) -> bool:
    """Rate already > factor*baseline in the 6 h preceding SEP eventTime."""
    for _hour, r, hours in iter_segments(index, t - timedelta(hours=6), t):
        if hours and r is not None and r.value > baseline * factor:
            return True
    return False


def analyze_event(event: Event, idx: dict[datetime, Reading], instrument: str,
                  game_gain: float, shield_sep_fraction: float, min_coverage: float,
                  collisions: int, provenance: str, pre_hours: int, post_hours: int) -> dict[str, Any]:
    p0 = event.time - timedelta(hours=pre_hours)
    p1 = event.time
    p2 = event.time + timedelta(hours=post_hours)
    before = window_stats(idx, p0, p1)
    baseline = before["median_sample_ugy_per_h"]
    after = window_stats(idx, p1, p2, baseline)
    valid = (baseline is not None and after["peak_ugy_per_h"] is not None
             and min(before["coverage"], after["coverage"]) >= min_coverage)
    post_excess = after["excess_ugy_above_baseline"]
    peak_ratio = after["peak_ugy_per_h"] / baseline if baseline and after["peak_ugy_per_h"] is not None else None
    return {
        "sep_time_utc": iso(event.time),
        "sep_id": event.event_id,
        "sep_instruments": event.instruments,
        "sep_source": event.source,
        "radlab_source": provenance,
        "instrument_id": instrument,
        "measurement": "absorbed_dose_rate",
        "original_unit": "uGy/h",
        "window_start_utc": iso(p0),
        "window_end_utc": iso(p2),
        "pre_hours": pre_hours,
        "post_hours": post_hours,
        "pre_coverage": before["coverage"],
        "post_coverage": after["coverage"],
        "pre_absorbed_uGy": before["ugy_observed"],
        "post_absorbed_uGy": after["ugy_observed"],
        "pre_average_uGy_per_h": before["mean_ugy_per_h_observed"],
        "post_average_uGy_per_h": after["mean_ugy_per_h_observed"],
        "baseline_uGy_per_h": baseline,
        "peak_uGy_per_h": after["peak_ugy_per_h"],
        "peak_over_baseline": peak_ratio,
        "post_excess_uGy_above_baseline": after["excess_ugy_above_baseline"],
        "pre_rise_already_visible": sep_early_rise(idx, event.time, baseline) if baseline else None,
        "pre_missing_hours": before["missing_hour_starts"],
        "post_missing_hours": after["missing_hour_starts"],
        "timestamp_bin_collisions": collisions,
        "data_window_qualified": valid,
        "game_gain_arbitrary": game_gain,
        "game_shelter_sep_fraction_arbitrary": shield_sep_fraction,
        "game_baseline_mSv_per_h": baseline * game_gain / 1000 if baseline is not None else None,
        "game_peak_unshielded_mSv_per_h": after["peak_ugy_per_h"] * game_gain / 1000 if after["peak_ugy_per_h"] is not None else None,
        "game_post_unshielded_mSv": after["ugy_observed"] * game_gain / 1000,
        "game_post_excess_mSv": post_excess * game_gain / 1000 if post_excess is not None else None,
        "game_post_sheltered_mSv": (
            (after["ugy_observed"] - (1 - shield_sep_fraction) * post_excess) * game_gain / 1000
            if post_excess is not None else None
        ),
        "warning": "game_mSv NOT physical astronaut effective dose; hourly timestamp=interval end assumed; data missing not filled",
    }


def to_game_rate(rate: float, baseline: float, gain: float, sep_shield_factor: float) -> tuple[float, float, float]:
    b = min(rate, baseline)
    excess = max(rate - baseline, 0)
    unshielded = gain * rate / 1000
    sheltered = gain * (b + excess * sep_shield_factor) / 1000
    return unshielded, sheltered, gain * b / 1000


def mission_calibration(idx: dict[datetime, Reading], event: Event, pre_hours: int, post_hours: int,
                        target_dose: float, target_seconds: float) -> dict[str, Any]:
    game_hours_per_second = MISSION_HOURS / MISSION_PLAY_SECONDS
    mission_pre_hours = SEP_PLAY_SECONDS * game_hours_per_second
    mission_post_hours = MISSION_HOURS - mission_pre_hours
    mission_start = event.time - timedelta(hours=mission_pre_hours)
    mission_end = event.time + timedelta(hours=mission_post_hours)
    post_hours_at_target = (target_seconds - SEP_PLAY_SECONDS) * game_hours_per_second
    models = {}
    for name, (start, end) in {
        "analysis_window_means": (event.time - timedelta(hours=pre_hours),
                                  event.time + timedelta(hours=post_hours)),
        "mission_window_means": (mission_start, mission_end),
    }.items():
        before = window_stats(idx, start, event.time)
        after = window_stats(idx, event.time, end)
        pre_rate = before["mean_ugy_per_h_observed"]
        post_rate = after["mean_ugy_per_h_observed"]
        gain = None
        if (pre_rate is not None and post_rate is not None
                and not before["missing_hour_starts"] and not after["missing_hour_starts"]):
            raw_dose_mGy = (pre_rate * mission_pre_hours + post_rate * post_hours_at_target) / 1000
            if math.isfinite(raw_dose_mGy) and raw_dose_mGy > 0:
                candidate = target_dose / raw_dose_mGy
                if math.isfinite(candidate):
                    gain = candidate
        models[name] = {
            "rate_window_start_utc": iso(start),
            "rate_window_end_utc": iso(end),
            "pre_average_uGy_per_h": pre_rate,
            "post_average_uGy_per_h": post_rate,
            "pre_coverage": before["coverage"],
            "post_coverage": after["coverage"],
            "missing_hour_starts": before["missing_hour_starts"] + after["missing_hour_starts"],
            "game_gain_arbitrary": gain,
        }
    return {
        "mission_start_utc": iso(mission_start),
        "sep_time_utc": iso(event.time),
        "mission_end_utc": iso(mission_end),
        "sep_id": event.event_id,
        "sep_instruments": event.instruments,
        "mission_game_hours": MISSION_HOURS,
        "mission_play_seconds": MISSION_PLAY_SECONDS,
        "sep_play_seconds": SEP_PLAY_SECONDS,
        "sep_game_hour": mission_pre_hours,
        "target_play_seconds": target_seconds,
        "target_game_mSv": target_dose,
        "models": models,
    }


def run_calibration(args: argparse.Namespace, event: Event) -> int:
    urls = []
    try:
        if args.input:
            readings = load_observations(Path(args.input).read_text(encoding="utf-8-sig"), "UPLOADED_RADLAB_CSV")
        else:
            mission_pre = MISSION_HOURS * SEP_PLAY_SECONDS / MISSION_PLAY_SECONDS
            start = event.time - timedelta(hours=max(args.pre_hours, mission_pre) + 2)
            end = event.time + timedelta(hours=max(args.post_hours, MISSION_HOURS - mission_pre) + 2)
            readings, urls = fetch_radlab(start, end, args.instrument)
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"RadLab calibration input failed: {exc}", file=sys.stderr)
        return 3
    idx, collisions = hourly_index(readings, "absorbed_dose_rate", args.instrument)
    summary = mission_calibration(idx, event, args.pre_hours, args.post_hours,
                                  args.target_game_dose, args.target_play_seconds)
    summary.update({
        "instrument_id": args.instrument,
        "radlab_input_csv": args.input,
        "radlab_urls": urls,
        "timestamp_bin_collisions": collisions,
    })
    print(f"Moon Trek start (UTC): {summary['mission_start_utc']}")
    print(f"SEP at {SEP_PLAY_SECONDS}s (UTC): {summary['sep_time_utc']}")
    print(f"Moon Trek end (UTC): {summary['mission_end_utc']}")
    for name, model in summary["models"].items():
        gain = model["game_gain_arbitrary"]
        if gain is None:
            print(f"{name}: no k (missing observations or no positive finite dose)")
        else:
            print(f"{name}: k={gain:.9f}")
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    (out / "mission_calibration.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if any(model["game_gain_arbitrary"] is not None for model in summary["models"].values()) else 1


def write_rows(path: Path, rows: list[dict[str, Any]], headers: list[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if headers is None:
        headers = list(rows[0]) if rows else ["message"]
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=headers, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({k: (json.dumps(v, ensure_ascii=False) if isinstance(v, (list, dict)) else v) for k, v in row.items()})


def write_game_minutes(path: Path, idx: dict[datetime, Reading], start: datetime, end: datetime,
                       event: Event, baseline: float, gain: float, shield_sep_factor: float) -> dict[str, Any]:
    rows: list[dict[str, Any]] = []
    t = start
    u_cum = 0.0
    g_cum = 0.0
    sheltered_cum = 0.0
    missing_minutes = 0
    while t < end:
        if (end-t) < MINUTE:
            raise ValueError("Output window must have whole minutes")
        reading = idx.get(floor_hour(t))
        phase = "PRE_SEP" if t < event.time else "POST_SEP"
        out = {
            "minute_index": len(rows), "utc_minute_start": iso(t), "phase": phase,
            "source_hour_start_utc": iso(floor_hour(t)),
            "source_reading_timestamp_utc": iso(reading.time) if reading else "",
            "instrument_id": reading.instrument_id if reading else "",
            "raw_uGy_per_hour": reading.value if reading else "",
            "raw_absorbed_uGy_this_minute": "",
            "cumulative_observed_absorbed_uGy": "",
            "game_gain": gain,
            "shield_state": "ACTIVE" if phase == "POST_SEP" else "OFF",
            "game_unshielded_mSv_per_hour": "",
            "game_unshielded_mSv_this_minute": "",
            "game_cumulative_unshielded_mSv": "",
            "game_sheltered_mSv_this_minute": "",
            "game_cumulative_sheltered_mSv": "",
            "model_not_physical_effective_dose": "YES",
        }
        if reading is not None:
            absorbed = reading.value / 60.0
            game_unshielded_rate, game_sheltered_rate, _ = to_game_rate(reading.value, baseline, gain, shield_sep_factor)
            if phase == "PRE_SEP":
                # The shelter only turns on at the SEP alert (24 h into the game).
                game_sheltered_rate = game_unshielded_rate
            g_delta = game_unshielded_rate / 60
            sh_delta = game_sheltered_rate / 60
            u_cum += absorbed
            g_cum += g_delta
            sheltered_cum += sh_delta
            out.update({
                "raw_absorbed_uGy_this_minute": absorbed,
                "cumulative_observed_absorbed_uGy": u_cum,
                "game_unshielded_mSv_per_hour": game_unshielded_rate,
                "game_unshielded_mSv_this_minute": g_delta,
                "game_cumulative_unshielded_mSv": g_cum,
                "game_sheltered_mSv_this_minute": sh_delta,
                "game_cumulative_sheltered_mSv": sheltered_cum,
            })
        else:
            missing_minutes += 1
        rows.append(out)
        t += MINUTE
    write_rows(path, rows)
    return {"rows": len(rows), "missing_minutes": missing_minutes,
            "observed_absorbed_uGy": u_cum,
            "game_unshielded_mSv": g_cum,
            "game_sheltered_mSv": sheltered_cum}


def write_event_outputs(out: Path, event: Event, readings: list[Reading], instrument: str,
                        gain: float, sep_shield_factor: float, pre_hours: int, post_hours: int,
                        min_coverage: float, provenance: str) -> dict[str, Any]:
    idx, collisions = hourly_index(readings, "absorbed_dose_rate", instrument)
    summary = analyze_event(event, idx, instrument, gain, sep_shield_factor,
                            min_coverage, collisions, provenance, pre_hours, post_hours)
    if not summary["data_window_qualified"]:
        return summary
    baseline = summary["baseline_uGy_per_h"]
    start, end = event.time - timedelta(hours=pre_hours), event.time + timedelta(hours=post_hours)
    full = write_game_minutes(out / "sep_anchored_96h_1min.csv", idx, start, end,
                              event, baseline, gain, sep_shield_factor)
    gameplay = write_game_minutes(out / "game_48h_sep_at_24h_1min.csv", idx,
                                  event.time - timedelta(hours=24), event.time + timedelta(hours=24),
                                  event, baseline, gain, sep_shield_factor)
    pre24 = window_stats(idx, event.time - timedelta(hours=24), event.time, baseline)
    post24 = window_stats(idx, event.time, event.time + timedelta(hours=24), baseline)
    summary["game_48h_before_absorbed_uGy"] = pre24["ugy_observed"]
    summary["game_48h_after_absorbed_uGy"] = post24["ugy_observed"]
    summary["game_48h_unshielded_mSv"] = (pre24["ugy_observed"]+post24["ugy_observed"]) * gain / 1000
    summary["game_48h_sheltered_during_post_mSv"] = (
        pre24["ugy_observed"] + post24["ugy_observed"]
        -(1-sep_shield_factor) * post24["excess_ugy_above_baseline"]
    ) * gain / 1000
    summary["full96_export"] = full
    summary["game48_export"] = gameplay
    with (out / "analysis_summary.json").open("w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    return summary


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="DONKI SEP UTC anchored +/-48 h RadLab analysis")
    parser.add_argument("command", choices=["scan", "analyze", "calibrate", "check-apis"])
    parser.add_argument("--input", help="RadLab CSV; scan/analyze default to bundled data, calibrate defaults to live RadLab")
    parser.add_argument("--out", default="output")
    parser.add_argument("--instrument", default="CRaTER-D1D2")
    parser.add_argument("--sep-time", help="UTC SEP event time for analyze/calibrate, e.g. 2024-03-23T05:57:00Z")
    parser.add_argument("--sep-id", help="DONKI identifier for the supplied SEP time")
    parser.add_argument("--sep-observer", help="Instrument for the supplied SEP time")
    parser.add_argument("--donki-json", help="Offline DONKI API response saved as a JSON file (scan)")
    parser.add_argument("--refresh-radlab", action="store_true", help="Verify/requery each candidate directly from RadLab API")
    parser.add_argument("--require-live-radlab", action="store_true", help="Fail rather than fall back to the supplied CSV")
    parser.add_argument("--pre-hours", type=int, default=48)
    parser.add_argument("--post-hours", type=int, default=48)
    parser.add_argument("--min-coverage", type=float, default=0.98)
    parser.add_argument("--game-gain", type=float,
                        help="Fixed game gain for analyze/scan only (default 200)")
    parser.add_argument("--target-game-dose", type=float, default=600.0,
                        help="calibrate: outdoor dose threshold in game mSv (default 600)")
    parser.add_argument("--target-play-seconds", type=float, default=MISSION_PLAY_SECONDS,
                        help="calibrate: elapsed play seconds for reaching the threshold (default 600)")
    parser.add_argument("--shield-sep-fraction", type=float, default=0.10,
                        help="ARBITRARY sheltered fraction of SEP excess, 0..1 (default 0.10)")
    args = parser.parse_args(argv)
    if args.command == "calibrate" and args.game_gain is not None:
        parser.error("calibrate calculates k; use --target-game-dose or --target-play-seconds")
    if args.game_gain is None:
        args.game_gain = 200.0
    if not (args.pre_hours > 0 and args.post_hours > 0 and 0 <= args.min_coverage <= 1
            and 0 <= args.shield_sep_fraction <= 1 and math.isfinite(args.game_gain) and args.game_gain > 0):
        parser.error("Invalid duration, coverage, game-gain, or shield fraction")
    event = None
    if args.command in ("analyze", "calibrate", "check-apis"):
        if args.command != "check-apis" and not args.sep_time:
            parser.error(f"{args.command} requires --sep-time")
        try:
            event_time = parse_time(args.sep_time or "2022-08-27T11:12:00Z")
        except ValueError:
            parser.error("--sep-time must be an ISO 8601 UTC timestamp")
        is_bundled_event = iso(event_time) == "2022-08-27T11:12:00Z"
        event = Event(
            event_time,
            args.sep_id or ("2022-08-27T11:12:00-SEP-001" if is_bundled_event else "UNSPECIFIED"),
            args.sep_observer or ("SOHO: COSTEP 15.8-39.8 MeV" if is_bundled_event else "UNSPECIFIED"),
            "MANUAL_TIME_REPORTED_FROM_DONKI",
        )
    out = Path(args.out)

    if args.command == "check-apis":
        urls = {"donki": donki_url(event.time - timedelta(days=1), event.time + timedelta(days=1)),
                "radlab": radlab_url(event.time - timedelta(hours=48), event.time + timedelta(hours=48), args.instrument)}
        failed = False
        for kind, url in urls.items():
            print(f"{kind} URL: {url}")
            try:
                raw = get_text(url)
                if kind == "donki":
                    items = parse_donki(json.loads(raw), "NASA_DONKI_API")
                else:
                    items = load_observations(raw, "NASA_RADLAB_API")
                print(f"{kind}: OK, parsed {len(items)} entries")
            except Exception as ex:
                print(f"{kind}: FAILED: {ex}")
                failed = True
        return 2 if failed else 0

    if args.command == "calibrate":
        if not (math.isfinite(args.target_game_dose) and args.target_game_dose > 0
                and SEP_PLAY_SECONDS <= args.target_play_seconds <= MISSION_PLAY_SECONDS):
            parser.error("calibrate requires a finite positive target dose and a target time from 480 to 600 seconds")
        return run_calibration(args, event)

    filename = Path(args.input or "data/radlab_moon_observations.csv")
    if not filename.is_file():
        parser.error(f"Input RadLab CSV not found: {filename}")
    readings = load_observations(filename.read_text(encoding="utf-8-sig"), "UPLOADED_RADLAB_CSV")
    primary = [r for r in readings if r.instrument_id == args.instrument and r.measurement == "absorbed_dose_rate"]
    if not primary:
        parser.error(f"No absorbed-dose readings for {args.instrument}; available: {sorted({r.instrument_id for r in readings})}")
    t_min, t_max = min(r.time for r in primary), max(r.time for r in primary)
    equiv_count = sum(r.instrument_id == args.instrument and r.measurement == "dose_equivalent_rate" for r in readings)
    print(f"RadLab input: {len(primary)} absorbed-dose rows; {equiv_count} equivalent-dose rows; {iso(t_min)}..{iso(t_max)}")
    # Only SEP eventTimes that can fit the entire required +/- window.
    evt_start = t_min + timedelta(hours=args.pre_hours)
    evt_end = t_max - timedelta(hours=args.post_hours)
    if evt_start >= evt_end:
        parser.error("Not enough observation span for requested +/- windows")

    events: list[Event]
    api_metadata: dict[str, Any] = {"donki_urls": [], "radlab_urls": [], "warnings": []}
    if args.command == "analyze":
        events = [event]
    elif args.donki_json:
        events = parse_donki(json.loads(Path(args.donki_json).read_text(encoding="utf-8-sig")), "USER_DONKI_JSON")
    else:
        try:
            events, api_metadata["donki_urls"] = fetch_donki(evt_start, evt_end)
        except Exception as ex:
            print(f"DONKI retrieval failed: {ex}", file=sys.stderr)
            print("Offline alternative: analyze --sep-time 2022-08-27T11:12:00Z", file=sys.stderr)
            return 2
    events = [ev for ev in events if evt_start <= ev.time <= evt_end]
    print(f"SEP events within +/- window boundaries: {len(events)}")
    results: list[dict[str, Any]] = []
    eligible: list[tuple[Event, dict[str, Any], list[Reading], str]] = []
    for ev in events:
        obs = readings
        provenance = "UPLOADED_RADLAB_CSV"
        if args.refresh_radlab:
            try:
                live, urls = fetch_radlab(ev.time-timedelta(hours=args.pre_hours+2),
                                           ev.time+timedelta(hours=args.post_hours+2), args.instrument)
                api_metadata["radlab_urls"].extend(urls)
                if not live:
                    raise ValueError("RadLab returned no absorbed-dose rows")
                obs = live
                provenance = "NASA_RADLAB_API_LIVE"
            except Exception as ex:
                warning = f"{iso(ev.time)}: RadLab live query failed ({ex}); uploaded CSV fallback"
                api_metadata["warnings"].append(warning)
                if args.require_live_radlab:
                    print(warning, file=sys.stderr)
                    return 3
                print("WARNING:", warning, file=sys.stderr)
        idx, coll = hourly_index(obs, "absorbed_dose_rate", args.instrument)
        record = analyze_event(ev, idx, args.instrument, args.game_gain,
                               args.shield_sep_fraction, args.min_coverage, coll,
                               provenance, args.pre_hours, args.post_hours)
        results.append(record)
        status = "VALID" if record["data_window_qualified"] else "REJECTED_COVERAGE"
        print(f"{status} {iso(ev.time)} {ev.event_id} | pre={record['pre_coverage']:.1%} post={record['post_coverage']:.1%} peak={record['peak_over_baseline']!s}")
        if record["data_window_qualified"]:
            eligible.append((ev, record, obs, provenance))
    results.sort(key=lambda x: (
        not x["data_window_qualified"], -(x["peak_over_baseline"] or 0), x["sep_time_utc"]))
    write_rows(out / "all_sep_window_candidates.csv", results, headers=[
        "sep_time_utc", "sep_id", "sep_instruments", "sep_source", "radlab_source",
        "instrument_id", "window_start_utc", "window_end_utc", "pre_hours", "post_hours",
        "pre_coverage", "post_coverage", "data_window_qualified", "baseline_uGy_per_h",
        "peak_uGy_per_h", "peak_over_baseline", "pre_absorbed_uGy", "post_absorbed_uGy",
        "post_excess_uGy_above_baseline", "pre_rise_already_visible",
        "game_gain_arbitrary", "game_baseline_mSv_per_h", "game_peak_unshielded_mSv_per_h",
        "game_post_unshielded_mSv", "game_post_sheltered_mSv", "pre_missing_hours", "post_missing_hours",
    ])
    if eligible:
        best_id = (results[0]["sep_time_utc"], results[0]["sep_id"])
        winner = next((it for it in eligible if (iso(it[0].time), it[0].event_id) == best_id), eligible[0])
        ev, _, obs, provenance = winner
        summary = write_event_outputs(out, ev, obs, args.instrument, args.game_gain,
                                      args.shield_sep_fraction, args.pre_hours,
                                      args.post_hours, args.min_coverage, provenance)
        print(f"SELECTED {iso(ev.time)}: baseline={summary['baseline_uGy_per_h']:.5f} uGy/h; peak={summary['peak_uGy_per_h']:.5f} uGy/h")
        print(f"96h unshielded game model gain={args.game_gain}: before={summary['pre_absorbed_uGy']*args.game_gain/1000:.3f} mSv, after={summary['game_post_unshielded_mSv']:.3f} mSv")
        print(f"48h game (SEP at 24h): unshielded={summary['game_48h_unshielded_mSv']:.3f} game mSv; shelter during post={summary['game_48h_sheltered_during_post_mSv']:.3f} game mSv")
    else:
        print("No SEP candidates pass coverage threshold. No minute CSV exported.")
    api_metadata["generated_from_csv"] = str(filename)
    api_metadata["data_quality_note"] = "One-hour values assigned to hourly buckets nearest observed timestamps; hour boundaries across SEP are approximated."
    api_metadata["dose_warning"] = "GAME mSv IS NOT NASA astronaut effective dose. Gain is gameplay-only."
    (out / "api_provenance.json").write_text(json.dumps(api_metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if eligible else 1


if __name__ == "__main__":
    sys.exit(main())
