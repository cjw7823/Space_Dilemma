import contextlib
import io
import json
import sys
import tempfile
import unittest
from datetime import timedelta
from pathlib import Path
from urllib.parse import parse_qs, urlsplit
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import moon_sep_windows as m

BASE = Path(__file__).resolve().parent.parent


class MoonSepTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.readings = m.load_observations((BASE / 'data/radlab_moon_observations.csv').read_text(encoding='utf-8-sig'), 'test')
        cls.event = m.Event(m.parse_time('2022-08-27T11:12:00Z'), 'test-001', 'SOHO', 'fixture')
        cls.index, cls.collisions = m.hourly_index(cls.readings, 'absorbed_dose_rate', 'CRaTER-D1D2')

    def test_input_rows(self):
        self.assertEqual(525, len(self.readings))
        self.assertEqual(0, self.collisions)

    def test_radlab_api_time_operators(self):
        u = m.radlab_url(m.parse_time('2022-08-25T11:12:00Z'), m.parse_time('2022-08-29T11:12:00Z'), 'CRaTER-D1D2')
        parsed = parse_qs(urlsplit(u).query, keep_blank_values=True)
        self.assertEqual(parsed['timestamp>'], ['2022-08-25T11:12'])
        self.assertEqual(parsed['timestamp<'], ['2022-08-29T11:12'])
        self.assertIn('dose_equivalent_rate', parsed)
        self.assertNotIn('fields+', parsed)

    def test_donki_api_url(self):
        u = m.donki_url(m.parse_time('2022-08-25T11:12:00Z'), m.parse_time('2022-08-29T11:12:00Z'))
        self.assertTrue(u.startswith('https://ccmc.gsfc.nasa.gov/DONKI-API/get/SEP?'))
        self.assertIn('startDate=2022-08-25', u)

    def test_donki_json_parse(self):
        events = m.parse_donki([{'eventTime':'2022-08-27T11:12Z','sepID':'001','instruments':[{'displayName':'SOHO: COSTEP'}]}], 'test')
        self.assertEqual(1, len(events))
        self.assertEqual('001', events[0].event_id)
        self.assertIn('SOHO', events[0].instruments)

    def test_coverage_is_100_percent(self):
        ev = self.event.time
        for a,b in ((ev-timedelta(hours=48),ev),(ev,ev+timedelta(hours=48))):
            stats = m.window_stats(self.index,a,b)
            self.assertAlmostEqual(stats['coverage'],1.0)
            self.assertAlmostEqual(stats['hours_observed'],48.0)

    def test_values_match_independent_sum(self):
        ev=self.event.time
        before=m.window_stats(self.index,ev-timedelta(hours=48),ev)
        after=m.window_stats(self.index,ev,ev+timedelta(hours=48))
        self.assertAlmostEqual(before['ugy_observed'],550.3891666666667,places=5)
        self.assertAlmostEqual(after['ugy_observed'],3125.9358333333334,places=5)

    def test_unit_and_game_not_identical(self):
        direct,shielded,base = m.to_game_rate(386.55,9.234583333333331,200,0.1)
        self.assertAlmostEqual(direct,77.31)
        self.assertLess(shielded,direct)
        self.assertAlmostEqual(base,1.8469166666666665)

    def test_event_rising_before_donki_time(self):
        self.assertTrue(m.sep_early_rise(self.index,self.event.time,9.234583333333331))

    def test_game48_case_unshielded_600_exceeded(self):
        res=m.analyze_event(self.event,self.index,'CRaTER-D1D2',200,0.1,0.98,self.collisions,'test',48,48)
        self.assertTrue(res['data_window_qualified'])
        self.assertAlmostEqual(res['game_post_unshielded_mSv'],625.18716666667,places=4)
        self.assertLess(res['game_post_sheltered_mSv'],625.18716666)

    def test_missing_samples_are_not_imputed(self):
        reduced = dict(self.index)
        reduced.pop(m.parse_time('2022-08-27T12:00:00Z'))
        stats=m.window_stats(reduced,self.event.time,self.event.time+timedelta(hours=48))
        self.assertLess(stats['coverage'],1.0)
        self.assertEqual(len(stats['missing_hour_starts']),1)

    def test_normalized_csv_and_api_csv(self):
        raw='timestamp,instrument_id,absorbed_dose_rate,dose_equivalent_rate,trajectory\n2022-08-27T12:00:00Z,CRaTER-D1D2,100,NA,Lunar orbit\n'
        parsed=m.load_observations(raw,'test')
        self.assertEqual(len(parsed),1)
        self.assertEqual(parsed[0].value,100)
        self.assertEqual(parsed[0].measurement,'absorbed_dose_rate')

    def test_calibration_uses_current_mission_clock_and_exact_target(self):
        event = m.Event(m.parse_time("2024-03-23T05:57:00Z"), "test-002", "SOHO", "fixture")
        summary = m.mission_calibration(self.index, event, 48, 48, 600, 600)
        self.assertEqual("2024-03-21T15:33:00Z", summary["mission_start_utc"])
        self.assertEqual("2024-03-23T05:57:00Z", summary["sep_time_utc"])
        self.assertEqual("2024-03-23T15:33:00Z", summary["mission_end_utc"])
        self.assertEqual(38.4, summary["sep_game_hour"])

        summary = m.mission_calibration(self.index, self.event, 48, 48, 600, 600)
        analysis = summary["models"]["analysis_window_means"]
        self.assertAlmostEqual(563.1167007743, analysis["game_gain_arbitrary"], places=8)
        for model in summary["models"].values():
            raw_dose = model["pre_average_uGy_per_h"] * 38.4 + model["post_average_uGy_per_h"] * 9.6
            self.assertAlmostEqual(600, raw_dose * model["game_gain_arbitrary"] / 1000)
        earlier = m.mission_calibration(self.index, self.event, 48, 48, 600, 597.5)
        self.assertGreater(earlier["models"]["analysis_window_means"]["game_gain_arbitrary"],
                           analysis["game_gain_arbitrary"])

    def test_calibration_with_gap_outside_mission_still_solves_mission_model(self):
        reduced = dict(self.index)
        reduced.pop(m.parse_time("2022-08-28T12:00:00Z"))
        models = m.mission_calibration(reduced, self.event, 48, 48, 600, 600)["models"]
        self.assertIsNone(models["analysis_window_means"]["game_gain_arbitrary"])
        self.assertIsNotNone(models["mission_window_means"]["game_gain_arbitrary"])

    def test_empty_data_does_not_qualify(self):
        stats = m.analyze_event(self.event, {}, "CRaTER-D1D2", 200, 0.1, 0, 0, "test", 48, 48)
        self.assertFalse(stats["data_window_qualified"])
        self.assertIsNone(stats["game_post_sheltered_mSv"])
        models = m.mission_calibration({}, self.event, 48, 48, 600, 600)["models"]
        self.assertTrue(all(model["game_gain_arbitrary"] is None for model in models.values()))

    def test_calibrate_cli_uses_live_without_a_required_local_csv(self):
        with tempfile.TemporaryDirectory() as tmp, contextlib.redirect_stdout(io.StringIO()):
            with patch.object(m, "fetch_radlab", return_value=(self.readings, ["https://example.com/raw"])) as fetch:
                exit_code = m.main(["calibrate", "--sep-time", m.iso(self.event.time), "--out", tmp])
            self.assertEqual(0, exit_code)
            fetch.assert_called_once()
            result = json.loads((Path(tmp) / "mission_calibration.json").read_text(encoding="utf-8"))
            self.assertIsNone(result["radlab_input_csv"])
            self.assertEqual(["https://example.com/raw"], result["radlab_urls"])
            self.assertEqual("2022-08-27T11:12:00-SEP-001", result["sep_id"])

    def test_check_apis_failure_propagates_without_creating_output(self):
        with tempfile.TemporaryDirectory() as tmp, contextlib.redirect_stdout(io.StringIO()):
            output = Path(tmp) / "unused"
            with patch.object(m, "get_text", side_effect=RuntimeError("offline")):
                self.assertEqual(2, m.main(["check-apis", "--out", str(output)]))
            self.assertFalse(output.exists())


if __name__=='__main__':
    unittest.main()
