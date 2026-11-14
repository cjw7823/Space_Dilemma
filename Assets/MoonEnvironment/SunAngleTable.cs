using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>Moon Trek Time,Elevation,Azimuth CSV. Times use UTC year/day-of-year.</summary>
public sealed class SunAngleTable
{
    public struct Sample
    {
        public DateTime utc;
        public float elevation, azimuth;
    }

    readonly List<Sample> samples = new List<Sample>();
    public int Count => samples.Count;
    public DateTime StartUtc => samples[0].utc;
    public DateTime EndUtc => samples[samples.Count - 1].utc;
    public double DurationHours => (EndUtc - StartUtc).TotalHours;

    public static bool TryParse(string csv, out SunAngleTable table, out string error)
    {
        table = null;
        error = null;
        var result = new SunAngleTable();
        using (var reader = new StringReader(csv ?? ""))
        {
            string header = reader.ReadLine();
            if (header == null || header.Trim().TrimStart('\uFEFF') != "Time,Elevation,Azimuth")
            { error = "Expected Time,Elevation,Azimuth CSV header."; return false; }
            string line;
            int row = 1;
            while ((line = reader.ReadLine()) != null)
            {
                row++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] cells = line.Split(',');
                if (cells.Length != 3 || !TryTime(cells[0].Trim(), out DateTime utc) ||
                    !float.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float el) ||
                    !float.TryParse(cells[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float az) ||
                    float.IsNaN(el) || float.IsInfinity(el) || el < -90 || el > 90 ||
                    float.IsNaN(az) || float.IsInfinity(az) || az < 0 || az > 360 ||
                    (result.Count > 0 && utc <= result.EndUtc))
                { error = $"Invalid or unordered Sun Angle row {row}."; return false; }
                result.samples.Add(new Sample { utc = utc, elevation = el, azimuth = Mathf.Repeat(az, 360) });
            }
        }
        if (result.Count < 2) { error = "At least two ordered Sun Angle samples are required."; return false; }
        table = result;
        return true;
    }

    static bool TryTime(string text, out DateTime utc)
    {
        utc = default;
        if (text.Length < 17 || text[4] != '-' || text[8] != 'T' ||
            !int.TryParse(text.Substring(0, 4), out int year) || year < 1 || year > 9999 ||
            !int.TryParse(text.Substring(5, 3), out int day) || day < 1 || day > (DateTime.IsLeapYear(year) ? 366 : 365) ||
            !TimeSpan.TryParseExact(text.Substring(9), new[] { @"hh\:mm\:ss", @"hh\:mm\:ss\.FFFFFFF" },
                CultureInfo.InvariantCulture, out TimeSpan time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return false;
        utc = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day - 1).Add(time);
        return true;
    }

    public Sample Evaluate(double hoursFromStart)
    {
        if (double.IsNaN(hoursFromStart)) hoursFromStart = 0;
        double hours = Math.Max(0, Math.Min(DurationHours, hoursFromStart));
        DateTime utc = StartUtc.AddHours(hours);
        int lo = 0, hi = Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (samples[mid].utc <= utc) lo = mid; else hi = mid;
        }
        Sample a = samples[lo], b = samples[hi];
        float t = (float)((utc - a.utc).TotalSeconds / (b.utc - a.utc).TotalSeconds);
        return new Sample { utc = utc, elevation = Mathf.Lerp(a.elevation, b.elevation, t),
            azimuth = Mathf.Repeat(a.azimuth + Mathf.DeltaAngle(a.azimuth, b.azimuth) * t, 360) };
    }
}
