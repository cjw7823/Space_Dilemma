using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Focused scene setup and repeatable real-Editor render/behavior checks.</summary>
[InitializeOnLoad]
public static class LunarEnvironmentValidation
{
    const string Output = "QA/Environment";
    const string TextureRoot = "Assets/Textures/";
    const string MaskPath = "Assets/MoonTerrain/Materials/moon_01_mask_4k.png";
    const string AlbedoPath = "Assets/MoonTerrain/Materials/moon_01_neutral_albedo_4k.png";
    const string MaterialPath = "Assets/MoonTerrain/Materials/LunarRegolithURP.mat";
    static readonly List<string> checks = new List<string>();
    static int failures;
    static int playFrames;
    static int playStage, stageFrame;

    static LunarEnvironmentValidation()
    {
        EditorApplication.update += UpdatePlayCheck;
    }

    static void UpdatePlayCheck()
    {
        if (!SessionState.GetBool("LunarEnvironment.PlayCheck",false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try { RunPlayCheck(); }
        catch (Exception e)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/error.txt", e.ToString());
            SessionState.SetBool("LunarEnvironment.PlayCheck",false);
            EditorApplication.isPlaying=false;
            Debug.LogException(e);
        }
    }

    static LunarEnvironment Environment()
    {
        var env = UnityEngine.Object.FindFirstObjectByType<LunarEnvironment>();
        if (!env || env.gameObject.scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open the existing SampleScene containing LunarEnvironment first.");
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before scene setup/validation.");
        return env;
    }

    [MenuItem("Tools/Moon Environment/Capture Before")]
    public static void CaptureBaseline()
    {
        var env = Environment();
        Directory.CreateDirectory(Output);
        // Match the pre-change lighting as well as texture settings.
        Color oldColor = env.sunlightColor;
        env.sunlightColor = new Color(1, 0.98f, 0.94f);
        env.Apply();
        try { CaptureViews("before"); }
        finally { env.sunlightColor = oldColor; env.Apply(); }
        File.WriteAllText(Output + "/baseline-complete.txt", DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("Tools/Moon Environment/Apply Site6 Surface and Sun CSV")]
    public static void ApplySite6()
    {
        var env = Environment();
        var terrainRoot = GameObject.Find("NASA_NobileRim1_Full");
        if (!terrainRoot) throw new InvalidOperationException("Existing rim1 root missing.");
        var terrains = terrainRoot.GetComponentsInChildren<Terrain>();
        if (terrains.Length != 16) throw new InvalidOperationException("Expected all 16 existing terrain tiles.");
        Directory.CreateDirectory(Output);
        ConfigureTexture(TextureRoot + "moon_01_diff_4k.jpg", true, false);
        ConfigureTexture(TextureRoot + "moon_01_nor_gl_4k.exr", false, true);
        ConfigureTexture(TextureRoot + "moon_01_rough_4k.exr", false, false);
        ConfigureTexture(TextureRoot + "moon_01_disp_4k.png", false, false);
        PackMask();
        BakeNeutralAlbedo();
        ConfigureTexture(MaskPath, false, false);
        ConfigureTexture(AlbedoPath, true, false);
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(TextureRoot + "NewLayer.terrainlayer");
        Undo.RecordObject(layer, "Improve lunar surface");
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "moon_01_nor_gl_4k.exr");
        layer.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
        layer.tileSize = new Vector2(3.1f, 3.1f);
        layer.normalScale = 0.8f;
        layer.metallic = 0;
        layer.smoothness = 1;
        // Neutral luminance albedo is derived separately; original source bytes stay intact.
        layer.diffuseRemapMin = Vector4.zero;
        layer.diffuseRemapMax = Vector4.one;
        layer.maskMapRemapMin = Vector4.zero;
        layer.maskMapRemapMax = Vector4.one;
        EditorUtility.SetDirty(layer);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.enableInstancing = true;
        material.SetFloat("_EnableInstancedPerPixelNormal", 1);
        material.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_MASKMAP");
        EditorUtility.SetDirty(material);
        foreach (var terrain in terrains)
        {
            // No TerrainData setters: heights, alphamaps and original layer references are preserved.
            Undo.RecordObject(terrain, "Improve lunar surface rendering");
            terrain.materialTemplate = material;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3;
            terrain.basemapDistance = 3000;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            terrain.terrainData.SetBaseMapDirty();
            terrain.Flush();
            EditorUtility.SetDirty(terrain);
        }
        Undo.RecordObject(env, "Apply recorded Moon Trek sun");
        env.sunAngleCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MoonEnvironment/Data/SunAngle.csv");
        env.useSunAngleData = true;
        env.sunHoursFromStart = 0;
        env.animateSun = true;
        env.shadowUpdateSeconds = 1f;
        // Grid axes preserved by convert_full_sdem.py. South polar meridian convergence at map center.
        env.trueNorthYaw = Mathf.Atan2(84000, 110000)*Mathf.Rad2Deg;
        env.sunDiameter = 0.53f;
        env.sunlightColor = Color.white;
        env.shadowFill = 0.025f;
        // Preserve the user's always-visible star preference and current star-map setting.
        // Preserve user-authored Earth location, size, brightness and legacy map rotation.
        env.sunlight.shadowBias = 0.05f;
        env.sunlight.shadowNormalBias = 0.15f;
        env.sunlight.shadows = LightShadows.Soft;
        env.sunlight.GetUniversalAdditionalLightData().softShadowQuality = SoftShadowQuality.High;
        EditorUtility.SetDirty(env.sunlight);
        EditorUtility.SetDirty(env);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        Undo.RecordObject(pipeline, "Enable long lunar terrain shadows");
        pipeline.mainLightShadowmapResolution = 4096;
        pipeline.shadowDistance = 12000;
        pipeline.shadowCascadeCount = 4;
        pipeline.cascade4Split = new Vector3(0.004f, 0.018f, 0.13f);
        pipeline.cascadeBorder = 0.1f;
        EditorUtility.SetDirty(pipeline);
        env.Apply();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
        EditorSceneManager.SaveScene(env.gameObject.scene);
        SceneView.RepaintAll();
        File.WriteAllText(Output + "/apply-complete.txt", $"{DateTime.UtcNow:O}\nNorth yaw {env.trueNorthYaw:R}\nSun {env.AppliedAzimuth:R}, {env.AppliedElevation:R}");
        Debug.Log("Site6 surface and Moon Trek Sun Angle applied. Existing TerrainData and camera transforms preserved.");
    }

    static void ConfigureTexture(string path, bool srgb, bool normal)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 16;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    static void PackMask()
    {
        var shader = Shader.Find("Hidden/Moon/PackTerrainMask");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Mask packing shader failed.");
        var mat = new Material(shader);
        mat.SetTexture("_HeightTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "moon_01_disp_4k.png"));
        var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "moon_01_rough_4k.exr");
        var rt = RenderTexture.GetTemporary(4096, 4096, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        var tex = new Texture2D(4096, 4096, TextureFormat.RGBA32, false, true);
        bool oldSrgbWrite = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = false;
            Graphics.Blit(rough, rt, mat);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 4096, 4096), 0, 0);
            tex.Apply();
            File.WriteAllBytes(MaskPath, tex.EncodeToPNG());
        }
        finally
        {
            GL.sRGBWrite = oldSrgbWrite;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(mat);
        }
        AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceSynchronousImport);
    }

    static void BakeNeutralAlbedo()
    {
        var mat = new Material(Shader.Find("Hidden/Moon/NeutralAlbedo"));
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "moon_01_diff_4k.jpg");
        var rt = RenderTexture.GetTemporary(4096,4096,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        bool oldSrgbWrite = GL.sRGBWrite;
        var tex = new Texture2D(4096,4096,TextureFormat.RGB24,false,false);
        try
        {
            GL.sRGBWrite = true;
            Graphics.Blit(source,rt,mat);
            RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,4096,4096),0,0); tex.Apply();
            File.WriteAllBytes(AlbedoPath,tex.EncodeToPNG());
        }
        finally
        {
            GL.sRGBWrite=oldSrgbWrite; RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(mat);
        }
        AssetDatabase.ImportAsset(AlbedoPath,ImportAssetOptions.ForceSynchronousImport);
    }

    [MenuItem("Tools/Moon Environment/Validate and Render")]
    public static void Validate()
    {
        var env = Environment();
        bool savedCelestialMode=env.useCelestialEphemeris;
        float savedSolarHours=env.sunHoursFromStart;
        try
        {
        // This older suite validates the manual-rotation path. Restore the user's physical mode afterwards.
        env.useCelestialEphemeris=false; env.sunHoursFromStart=0; env.Apply();
        checks.Clear(); failures = 0;
        Check(SunAngleTable.TryParse(env.sunAngleCsv.text, out var table, out _), "Parse supplied CSV");
        Check(table.Count == 51 && Near((float)table.DurationHours, 48), "51 records spanning 48 hours");
        var first = table.Evaluate(0); var last = table.Evaluate(48); var middle = table.Evaluate(0.48);
        Check(first.utc == new DateTime(2022,8,25,20,48,0,DateTimeKind.Utc), "Day-of-year 237 -> Aug 25 UTC");
        Check(Near(first.azimuth,154.816995f) && Near(first.elevation,-5.390978f), "Exact first solar angles");
        Check(Near(last.azimuth,130.280402f) && Near(last.elevation,-4.268556f), "Exact last solar angles");
        Check(Near(middle.azimuth,(154.816995f+154.325639f)/2) &&
              Near(middle.elevation,(-5.390978f-5.374971f)/2), "Interpolate between the first two records");
        Check(table.Evaluate(-10).utc == first.utc && table.Evaluate(100).utc == last.utc, "Clamp beyond CSV bounds");
        Check(!SunAngleTable.TryParse("Time,Elevation,Azimuth\n2026-366T00:00:00,1,2\n2027-001T00:00:00,2,3", out _, out _), "Reject invalid day-of-year");
        Check(!SunAngleTable.TryParse("Time,Elevation,Azimuth\n2026-318T00:00:00,NaN,2\n2026-319T00:00:00,1,3", out _, out _), "Reject nonfinite angle");
        Check(!SunAngleTable.TryParse("Time,Elevation,Azimuth\n2026-319T00:00:00,1,2\n2026-318T00:00:00,2,3", out _, out _), "Reject unordered records");
        SunAngleTable.TryParse("Time,Elevation,Azimuth\n2026-318T00:00:00,1,359\n2026-318T01:00:00,3,1", out var seam, out _);
        Check(Mathf.Abs(Mathf.DeltaAngle(0,seam.Evaluate(0.5).azimuth)) < 0.001f, "Interpolate across north through 0, not 180");
        Check(Vector3.Distance(LunarEnvironment.Direction(0,0),Vector3.forward)<0.0001f && Vector3.Distance(LunarEnvironment.Direction(90,0),Vector3.right)<0.0001f, "Cardinal direction convention");
        var north = LunarEnvironment.Direction(env.trueNorthYaw,0);
        Check(Vector3.Distance(north,new Vector3(84000,0,110000).normalized)<0.0001f, "Polar map true north follows radial meridian");
        env.Apply();
        Check(Near(env.AppliedAzimuth,first.azimuth) && Near(env.AppliedElevation,first.elevation), "Scene starts at first recorded sample");
        Check(Vector3.Angle(-env.sunlight.transform.forward,env.SunDirection)<0.02f, "Directional light matches CSV direction");
        Check(Vector3.Distance(RenderSettings.skybox.GetVector("_SunDirection"),env.SunDirection)<0.0001f, "Sky Sun direction matches directional light");
        Check(env.sunlight.intensity==0, "Sun below the horizon gives no direct light");
        Check(RenderSettings.skybox != env.skyTemplate, "Private sky material does not overwrite template");
        Quaternion saved = env.EarthOrientation, placement = env.EarthPlacement;
        Vector3 oldSun = env.SunDirection;
        try
        {
            env.EarthOrientation = Quaternion.AngleAxis(90,Vector3.right);
            Quaternion before = env.EarthOrientation;
            env.RotateEarth(Vector3.up,30);
            Check(Quaternion.Angle(before,env.EarthOrientation)>29.9f, "Local Y still rotates at X=90 degrees");
            before = env.EarthOrientation;
            env.RotateEarth(Vector3.forward,30);
            Check(Quaternion.Angle(before,env.EarthOrientation)>29.9f, "Local Z stays independent at X=90 degrees");
            for(int i=0;i<10000;i++) env.RotateEarth(new Vector3(1,2,3),0.1f);
            var q = env.EarthOrientation;
            Check(Mathf.Abs(Quaternion.Dot(q,q)-1)<0.00001f, "10000 accumulated rotations remain normalized");
            var matrix = RenderSettings.skybox.GetMatrix("_EarthWorldToTexture");
            Check(Vector3.Distance(matrix.MultiplyVector(q*Vector3.up),Vector3.up)<0.0001f, "Shader uses inverse quaternion for globe mapping");
            Check(Vector3.Angle(oldSun,env.SunDirection)<0.02f, "Earth rotation preserves solar lighting/phase direction");
            env.EarthPlacement = Quaternion.AngleAxis(90,Vector3.right)*Quaternion.AngleAxis(45,Vector3.up);
            env.Apply();
            Check(Vector3.Distance(env.EarthDirection,RenderSettings.skybox.GetVector("_EarthDirection"))<0.0001f, "Free sky placement reaches shader");
        }
        finally { env.EarthOrientation=saved; env.EarthPlacement=placement; env.Apply(); }
        foreach (var path in new[] {AlbedoPath,TextureRoot+"moon_01_nor_gl_4k.exr",MaskPath})
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Check(tex.width==4096 && tex.height==4096 && tex.mipmapCount>1 && tex.anisoLevel==16 && tex.filterMode==FilterMode.Trilinear, "4K+mips+Trilinear+Aniso16: "+Path.GetFileName(path));
            Check(importer.sRGBTexture == (path==AlbedoPath), "Correct color space: "+Path.GetFileName(path));
        }
        var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(TextureRoot+"NewLayer.terrainlayer");
        Check(layer.diffuseTexture.name=="moon_01_neutral_albedo_4k", "Neutralized surface color preserves separate original source");
        Check(layer.maskMapTexture && layer.maskMapTexture.name=="moon_01_mask_4k", "Linear roughness/height packed into URP mask");
        var terrains=GameObject.Find("NASA_NobileRim1_Full").GetComponentsInChildren<Terrain>();
        Check(terrains.Length>0 && terrains.All(t=>t.drawInstanced && t.materialTemplate.shader.name=="Universal Render Pipeline/Terrain/Lit"), "All scene tiles use instanced URP Terrain/Lit");
        Check(terrains.All(t=>t.terrainData.terrainLayers.Contains(layer)), "All original layer references retained");
        Check(!ShaderUtil.ShaderHasError(env.skyTemplate.shader) && !ShaderUtil.ShaderHasError(terrains[0].materialTemplate.shader), "Sky and terrain shaders compile");
        CaptureViews("after");
        // Isolate the surface improvement from the new low Sun: same old lighting, same camera.
        bool data=env.useSunAngleData; float fill=env.shadowFill; Color color=env.sunlightColor;
        env.useSunAngleData=false; env.shadowFill=0.18f; env.sunlightColor=new Color(1,0.98f,0.94f); env.Apply();
        env.sunlight.useColorTemperature=true; // Match the original scene's 5000 K light for this comparison.
        try { CaptureViews("surface-same-light"); }
        finally { env.useSunAngleData=data; env.shadowFill=fill; env.sunlightColor=color; env.Apply(); }
        string report=$"UTC: {DateTime.UtcNow:O}\nPassed: {checks.Count-failures}; Failed: {failures}\n"+string.Join("\n",checks);
        File.WriteAllText(Output+"/validation.txt",report);
        SceneView.RepaintAll();
        Selection.activeGameObject=env.gameObject;
        Debug.Log(report);
        }
        finally { env.useCelestialEphemeris=savedCelestialMode; env.sunHoursFromStart=savedSolarHours; env.Apply(); }
    }

    static bool Near(float a,float b) => Mathf.Abs(a-b)<0.0001f;
    static void Check(bool ok,string label) { checks.Add((ok?"PASS ":"FAIL ")+label); if(!ok) failures++; }

    [MenuItem("Tools/Moon Environment/Validate Play Mode")]
    public static void StartPlayCheck()
    {
        var env=Environment();
        EditorSceneManager.SaveScene(env.gameObject.scene);
        playFrames=0; playStage=0; stageFrame=0;
        SessionState.SetBool("LunarEnvironment.PlayCheck",true);
        EditorApplication.isPlaying=true;
    }

    static void RunPlayCheck()
    {
        if(++playFrames<20) return;
        var env=UnityEngine.Object.FindFirstObjectByType<LunarEnvironment>();
        if(playStage==0)
        {
            HudPlayValidation.EnterGameplay();
            // This check exercises the autonomous environment clock.
            var session=UnityEngine.Object.FindFirstObjectByType<SpaceDilemma.Survival.SurvivalSession>();
            if(session) session.enabled=false;
            env.ReleaseSessionClock();
            env.shadowUpdateSeconds=0;
            checks.Clear(); failures=0;
            Check(env && env.isActiveAndEnabled,"Environment active in actual Editor Play mode");
            Check(env.SunTable!=null && env.useSunAngleData,"CSV reparsed after Play mode domain reload");
            env.sunHoursFromStart=0; env.animateSun=false; env.Apply();
            var first=env.SunTable.Evaluate(0);
            Check(Near(env.AppliedAzimuth,first.azimuth) && Near(env.AppliedElevation,first.elevation),"Play mode first sample exact");
            env.useCelestialEphemeris=false; env.Apply();
            CaptureGlobe(env,"play-earth-before");
            env.EarthOrientation=Quaternion.AngleAxis(90,Vector3.right);
            env.RotateEarth(Vector3.up,60);
            env.RotateEarth(Vector3.forward,35);
            CaptureGlobe(env,"play-earth-rotated");
            Check(Quaternion.Angle(env.EarthOrientation,Quaternion.AngleAxis(90,Vector3.right))>60,"Play mode rotation remains independent at 90 degrees");
            env.sunHoursFromStart=(float)env.SunTable.DurationHours; env.animateSun=true; env.sunHoursPerSecond=1000;
            stageFrame=Time.frameCount; playStage=1;
            return;
        }
        if(playStage==1 && Time.frameCount-stageFrame>=3)
        {
            var last=env.SunTable.Evaluate(env.SunTable.DurationHours);
            Check(Near(env.sunHoursFromStart,(float)env.SunTable.DurationHours) &&
                  Near(env.AppliedAzimuth,last.azimuth) && Near(env.AppliedElevation,last.elevation),"Animated time clamps at CSV endpoint");
            env.sunHoursFromStart=0; env.sunHoursPerSecond=10;
            stageFrame=Time.frameCount; playStage=2;
            return;
        }
        if(playStage==2 && Time.frameCount-stageFrame>=3)
        {
            var first=env.SunTable.Evaluate(0);
            Check(env.sunHoursFromStart>0 && !Near(env.AppliedAzimuth,first.azimuth),"Sun animation advances and changes angle");
            Check(Vector3.Angle(-env.sunlight.transform.forward,env.SunDirection)<0.02f,"Light tracks animated Sun");
            Check(!float.IsNaN(env.EarthDirection.x),"Earth direction remains finite");
            string report=$"UTC: {DateTime.UtcNow:O}\nPassed: {checks.Count-failures}; Failed: {failures}\n"+string.Join("\n",checks);
            File.WriteAllText(Output+"/play-validation.txt",report);
            Debug.Log(report);
            SessionState.SetBool("LunarEnvironment.PlayCheck",false);
            EditorApplication.isPlaying=false;
        }
    }

    static void CaptureGlobe(LunarEnvironment env,string name)
    {
        var go=new GameObject("Earth QA Camera");
        try
        {
            var camera=go.AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.Skybox; camera.cullingMask=0;
            camera.fieldOfView=env.earthDiameter*2;
            go.transform.rotation=Quaternion.LookRotation(env.EarthDirection);
            go.AddComponent<UniversalAdditionalCameraData>();
            Render(camera,1280,720,Output+"/"+name+".png");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static float Height(float x,float z)
    {
        foreach(var t in Terrain.activeTerrains)
        {
            var p=t.transform.position; var s=t.terrainData.size;
            if(x>=p.x && x<=p.x+s.x && z>=p.z && z<=p.z+s.z) return t.SampleHeight(new Vector3(x,0,z))+p.y;
        }
        return 0;
    }

    static void CaptureViews(string prefix)
    {
        Directory.CreateDirectory(Output);
        var go=new GameObject("Environment QA Camera") { hideFlags=HideFlags.HideAndDontSave };
        var camera=go.AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.Skybox;
        camera.nearClipPlane=0.1f; camera.farClipPlane=40000; camera.fieldOfView=60;
        camera.allowHDR=true;
        var extra=go.AddComponent<UniversalAdditionalCameraData>();
        extra.renderPostProcessing=true;
        extra.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        try
        {
            foreach(int width in new[]{1280,1920})
            {
                int height=width*9/16;
                Vector3 look=new Vector3(10000,Height(10000,10000),10000);
                go.transform.position=look+new Vector3(-7500,6000,-10000);
                go.transform.rotation=Quaternion.LookRotation(look-go.transform.position);
                Render(camera,width,height,Output+$"/{prefix}-overview-{width}.png");
                // Surface at the selected rim, showing detail from a human-scale eye height.
                var pos=new Vector3(7050,Height(7050,12520)+1.8f,12520);
                go.transform.position=pos;
                go.transform.rotation=Quaternion.LookRotation(new Vector3(0.6f,-0.22f,1));
                Render(camera,width,height,Output+$"/{prefix}-surface-{width}.png");
                Vector3 lit=FindSunlitSurface();
                go.transform.position=lit+Vector3.up*1.8f;
                Vector3 sun=Environment().SunDirection;
                Vector3 along=new Vector3(sun.z,-0.3f,-sun.x);
                go.transform.rotation=Quaternion.LookRotation(along);
                Render(camera,width,height,Output+$"/{prefix}-sunlit-{width}.png");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static Vector3 FindSunlitSurface()
    {
        Vector3 sun=Environment().SunDirection;
        Vector3 best=new Vector3(7050,Height(7050,12520),12520);
        float score=float.MinValue;
        Physics.SyncTransforms();
        foreach(var t in Terrain.activeTerrains)
        {
            var s=t.terrainData.size; var origin=t.transform.position;
            for(int z=1;z<20;z++) for(int x=1;x<20;x++)
            {
                float u=x/20f,v=z/20f;
                Vector3 normal=t.terrainData.GetInterpolatedNormal(u,v);
                float facing=Vector3.Dot(normal,sun);
                if(facing<0.15f) continue;
                var p=new Vector3(origin.x+u*s.x,0,origin.z+v*s.z); p.y=Height(p.x,p.z);
                float candidate=p.y+facing*500;
                if(candidate<=score || Physics.Raycast(p+Vector3.up*2,sun,40000)) continue;
                score=candidate; best=p;
            }
        }
        return best;
    }

    static void Render(Camera camera,int width,int height,string path)
    {
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt; camera.aspect=(float)width/height; camera.Render(); RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,width,height),0,0); tex.Apply();
            File.WriteAllBytes(path,tex.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null; RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
