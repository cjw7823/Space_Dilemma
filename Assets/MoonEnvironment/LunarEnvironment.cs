using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class LunarEnvironment : MonoBehaviour
{
    [Header("References")]
    public Material skyTemplate;
    public Light sunlight;
    [Header("Illustrative sky — not a dated Site6 ephemeris")]
    [Tooltip("World azimuth: 0 = +Z, 90 = +X. Not surveyed lunar north.")]
    [Range(0, 360)] public float sunAzimuth = 180;
    [Range(-10, 90)] public float sunElevation = 8;
    [Range(0.1f, 2)] public float sunDiameter = 0.53f;
    [Range(0, 360)] public float earthAzimuth = 15;
    [Range(-10, 90)] public float earthElevation = 15;
    [Range(0.5f, 12)] public float earthDiameter = 4;
    [Range(0, 360)] public float earthMapRotation = 30;
    [Range(0, 4)] public float earthBrightness = 1.6f;
    [Range(0, 1)] public float illustrativeStars = 0;
    [Header("Surface lighting")]
    [Range(0, 3)] public float sunIntensity = 1.25f;
    [Tooltip("Small artistic fill to keep the base readable; not lunar atmospheric scattering.")]
    [Range(0, 0.3f)] public float shadowFill = 0.18f;

    Material sky;
    Material previousSky;
    Light previousSun;
    AmbientMode previousMode;
    Color previousAmbient;
    float previousReflection;
    bool previousFog;
    bool captured;
    bool needsApply;

    public static Vector3 Direction(float azimuth, float elevation)
    {
        float az = azimuth * Mathf.Deg2Rad, el = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
    }

    void OnEnable()
    {
        previousSky = RenderSettings.skybox;
        previousSun = RenderSettings.sun;
        previousMode = RenderSettings.ambientMode;
        previousAmbient = RenderSettings.ambientLight;
        previousReflection = RenderSettings.reflectionIntensity;
        previousFog = RenderSettings.fog;
        captured = true;
        Apply();
    }

    void OnValidate() { needsApply = true; }
    void LateUpdate() { if (needsApply) { needsApply = false; Apply(); } }

    public void Apply()
    {
        if (!skyTemplate) return;
        if (!sky)
        {
            sky = Application.isPlaying
                ? new Material(skyTemplate) { name = "Lunar Sky (instance)", hideFlags = HideFlags.HideAndDontSave }
                : skyTemplate;
        }
        Vector3 sun = Direction(sunAzimuth, sunElevation);
        sky.SetVector("_SunDirection", sun);
        sky.SetVector("_EarthDirection", Direction(earthAzimuth, earthElevation));
        sky.SetFloat("_SunDiameter", sunDiameter);
        sky.SetFloat("_EarthDiameter", earthDiameter);
        sky.SetFloat("_EarthRotation", earthMapRotation);
        sky.SetFloat("_EarthBrightness", earthBrightness);
        sky.SetFloat("_StarBrightness", illustrativeStars);
        RenderSettings.skybox = sky;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(shadowFill, shadowFill, shadowFill);
        RenderSettings.reflectionIntensity = 0.15f;
        if (sunlight)
        {
            sunlight.transform.rotation = Quaternion.LookRotation(-sun);
            sunlight.color = new Color(1, 0.98f, 0.94f);
            sunlight.intensity = sunIntensity;
            sunlight.shadows = LightShadows.Hard;
            RenderSettings.sun = sunlight;
        }
    }

    [ContextMenu("Preview preset (illustrative)")]
    public void PreviewPreset()
    {
        sunAzimuth = 180; sunElevation = 8; earthElevation = 15; earthDiameter = 4; shadowFill = 0.18f;
        Apply();
    }

    [ContextMenu("Polar scale reference (not a Site6 ephemeris)")]
    public void PolarReference()
    {
        sunElevation = 1.5f; earthElevation = 3; earthDiameter = 1.9f; shadowFill = 0.025f;
        Apply();
    }

    void OnDisable()
    {
        if (captured && RenderSettings.skybox == sky)
        {
            RenderSettings.skybox = previousSky;
            RenderSettings.sun = previousSun;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.ambientLight = previousAmbient;
            RenderSettings.reflectionIntensity = previousReflection;
            RenderSettings.fog = previousFog;
        }
        if (sky && sky != skyTemplate) { if (Application.isPlaying) Destroy(sky); else DestroyImmediate(sky); }
        sky = null;
    }
}