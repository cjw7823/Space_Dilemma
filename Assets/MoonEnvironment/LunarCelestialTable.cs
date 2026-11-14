using System;
using System.Globalization;
using UnityEngine;

/// <summary>Sampled SPICE output only; no native dependency or network in the player.</summary>
public sealed class LunarCelestialTable
{
    [Serializable] public sealed class Site
    {
        public double longitudeEastDegrees, latitudeDegrees, elevationMeters;
        public float northYawDegrees, worldXMeters, worldZMeters;
        public string moonFrame, earthFrame, note;
    }

    [Serializable] public struct Frame
    {
        public float seconds;
        public Vector3 earthDirection, earthSunDirection;
        public Quaternion earthOrientation, starsOrientation;
        public float earthDiameter, earthAzimuth, earthElevation, illuminatedFraction;
    }

    [Serializable] sealed class Document
    {
        public int version, stepSeconds;
        public string startUtc;
        public double durationSeconds;
        public Site site;
        public Frame[] samples;
    }

    readonly Document document;
    public DateTime StartUtc { get; }
    public double DurationSeconds => document.durationSeconds;
    public int Count => document.samples.Length;
    public Site Observer => document.site;

    LunarCelestialTable(Document data, DateTime utc) { document = data; StartUtc = utc; }

    public static bool TryParse(string json, out LunarCelestialTable table, out string error)
    {
        table = null; error = null;
        try
        {
            var d = JsonUtility.FromJson<Document>(json);
            if (d == null || d.version != 1 || d.site == null || d.samples == null || d.samples.Length < 2 ||
                !DateTime.TryParse(d.startUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime utc) ||
                d.durationSeconds <= 0 || double.IsInfinity(d.durationSeconds) || double.IsNaN(d.durationSeconds))
                throw new FormatException("Invalid celestial document header.");
            float previous = -1;
            foreach (var f in d.samples)
            {
                if (!Finite(f.seconds) || f.seconds <= previous || !Unit(f.earthDirection) || !Unit(f.earthSunDirection) ||
                    !Rotation(f.earthOrientation) || !Rotation(f.starsOrientation) || !Finite(f.earthDiameter) ||
                    f.earthDiameter <= 0 || f.earthDiameter > 10 || !Finite(f.earthAzimuth) || !Finite(f.earthElevation) ||
                    !Finite(f.illuminatedFraction) || f.illuminatedFraction < 0 || f.illuminatedFraction > 1)
                    throw new FormatException("Invalid celestial sample.");
                previous = f.seconds;
            }
            if (d.samples[0].seconds != 0 || Math.Abs(previous - d.durationSeconds) > 0.01)
                throw new FormatException("Celestial samples must span the complete declared interval.");
            table = new LunarCelestialTable(d, DateTime.SpecifyKind(utc, DateTimeKind.Utc));
            return true;
        }
        catch (Exception e) { error = e.Message; return false; }
    }

    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    static bool Unit(Vector3 v) => Finite(v.sqrMagnitude) && Mathf.Abs(v.sqrMagnitude-1) < 0.01f;
    static bool Rotation(Quaternion q) => Finite(Quaternion.Dot(q,q)) && Mathf.Abs(Quaternion.Dot(q,q)-1) < 0.01f;

    public Frame Evaluate(double seconds)
    {
        if (double.IsNaN(seconds)) seconds = 0;
        seconds = Math.Max(0, Math.Min(DurationSeconds, seconds));
        int lo = 0, hi = Count-1;
        while (hi-lo > 1)
        {
            int mid = (hi+lo)/2;
            if (document.samples[mid].seconds <= seconds) lo = mid; else hi = mid;
        }
        Frame a = document.samples[lo], b = document.samples[hi];
        float t = (float)((seconds-a.seconds)/(b.seconds-a.seconds));
        return new Frame
        {
            seconds = (float)seconds,
            earthDirection = Vector3.Slerp(a.earthDirection,b.earthDirection,t).normalized,
            earthSunDirection = Vector3.Slerp(a.earthSunDirection,b.earthSunDirection,t).normalized,
            earthOrientation = Quaternion.Slerp(a.earthOrientation,b.earthOrientation,t).normalized,
            starsOrientation = Quaternion.Slerp(a.starsOrientation,b.starsOrientation,t).normalized,
            earthDiameter = Mathf.Lerp(a.earthDiameter,b.earthDiameter,t),
            earthAzimuth = Mathf.Repeat(a.earthAzimuth+Mathf.DeltaAngle(a.earthAzimuth,b.earthAzimuth)*t,360),
            earthElevation = Mathf.Lerp(a.earthElevation,b.earthElevation,t),
            illuminatedFraction = Mathf.Lerp(a.illuminatedFraction,b.illuminatedFraction,t)
        };
    }
}
