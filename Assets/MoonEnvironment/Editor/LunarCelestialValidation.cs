using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Scene integration, GPU mapping and deterministic sky-motion regression checks.</summary>
[InitializeOnLoad]
public static class LunarCelestialValidation
{
    const string Output="QA/Celestial";
    static readonly List<string> results=new List<string>();
    static int failures,playStage,playFrame;

    static LunarCelestialValidation() { EditorApplication.update+=Update; }
    static void Update()
    {
        try
        {
            if(SessionState.GetBool("Moon.CelestialPlayCheck",false) && EditorApplication.isPlaying && !EditorApplication.isPaused)
                PlayUpdate();
        }
        catch(Exception e)
        {
            Directory.CreateDirectory(Output); File.WriteAllText(Output+"/error.txt",e.ToString());
            SessionState.SetBool("Moon.CelestialPlayCheck",false);
            if(EditorApplication.isPlaying) EditorApplication.isPlaying=false;
            Debug.LogException(e);
        }
    }

    static LunarEnvironment Env()
    {
        var env=UnityEngine.Object.FindFirstObjectByType<LunarEnvironment>();
        if(!env || env.gameObject.scene.path!="Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open existing SampleScene.");
        return env;
    }

    [MenuItem("Tools/Moon Environment/Apply Offline Celestial Sky")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before setup.");
        var env=Env(); Directory.CreateDirectory(Output);
        Undo.RecordObject(env,"Apply offline celestial sky");
        env.celestialEphemeris=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MoonEnvironment/Data/Site6Celestial.json");
        if(!env.celestialEphemeris) throw new InvalidOperationException("Build Site6Celestial.json first.");
        LunarStarBaker.ApplyTo(env,false);
        env.useCelestialEphemeris=true;
        env.illustrativeStars=0.85f;
        env.sunDiameter=0.53f;
        env.Apply();
        if(!env.EphemerisActive) throw new InvalidOperationException(env.CelestialDataError);
        EditorUtility.SetDirty(env); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(env.gameObject.scene); EditorSceneManager.SaveScene(env.gameObject.scene);
        SceneView.RepaintAll(); Selection.activeGameObject=env.gameObject;
        File.WriteAllText(Output+"/configured.txt",$"{DateTime.UtcNow:O}\n{env.CelestialTable.Count} frames; Earth {env.AppliedEarthDiameter:F6} degrees; fixed star cube {env.starCubemap.width}");
    }

    static void Check(bool ok,string label) { results.Add((ok?"PASS ":"FAIL ")+label); if(!ok) failures++; }
    static bool Near(float a,float b,float tolerance=0.0001f)=>Mathf.Abs(a-b)<tolerance;
    static void Finish(string file)
    {
        string report=$"UTC: {DateTime.UtcNow:O}\nPassed: {results.Count-failures}; Failed: {failures}\n"+string.Join("\n",results);
        File.WriteAllText(Output+"/"+file,report); Debug.Log(report);
    }

    [MenuItem("Tools/Moon Environment/Validate Celestial Sky and Motion")]
    public static void Validate()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Run the editor checks outside Play mode.");
        var env=Env(); Directory.CreateDirectory(Output); results.Clear(); failures=0;
        Check(LunarCelestialTable.TryParse(env.celestialEphemeris.text,out var table,out _),"Parse offline SPICE data");
        if(table==null) { Finish("validation.txt"); return; }
        Check(table.Count==577 && table.DurationSeconds==172800,"577 five-minute records over 48 hours");
        Check(table.StartUtc==env.SunTable.StartUtc,"Earth and solar CSV share the same UTC origin");
        Check(Near(table.Observer.northYawDegrees,env.trueNorthYaw,0.001f),"Observer north basis matches projected map yaw");
        Check(Near(table.Observer.worldXMeters,9997.5f) && Near(table.Observer.worldZMeters,9997.5f),"Observer is existing terrain center");
        Check(Near(table.Evaluate(0).earthDiameter,1.8021298f) && Near(table.Evaluate(172800).earthDiameter,1.8152088f),"Physical Earth diameters replace artistic scale");
        Check(table.Evaluate(-1).seconds==0 && table.Evaluate(double.PositiveInfinity).seconds==172800,"Clamp both ephemeris endpoints");
        Check(table.Evaluate(double.NaN).seconds==0,"Nonfinite requested time stays at start");
        Check(Quaternion.Angle(table.Evaluate(0).starsOrientation,table.Evaluate(172800).starsOrientation)>20,"Star sphere follows lunar orientation across 48 hours");
        Check(!LunarCelestialTable.TryParse("{\"version\":99}",out _,out _),"Reject corrupt/incompatible ephemeris");
        for(int i=0;i<576;i++)
        {
            var a=table.Evaluate(i*300); var m=table.Evaluate(i*300+150); var b=table.Evaluate((i+1)*300);
            if(Mathf.Abs(Quaternion.Dot(m.earthOrientation,m.earthOrientation)-1)>0.00001f ||
                Quaternion.Angle(a.earthOrientation,b.earthOrientation)>2 ||
                Vector3.Angle(a.earthDirection,b.earthDirection)>0.1f)
            { Check(false,"Quaternion and direction interpolation is continuous"); break; }
            if(i==575) Check(true,"Quaternion and direction interpolation is continuous across all 576 intervals");
        }
        float savedHours=env.sunHoursFromStart; bool savedMode=env.useCelestialEphemeris;
        var savedOrientation=env.EarthOrientation; var savedPlacement=env.EarthPlacement;
        try
        {
            env.useCelestialEphemeris=true; env.sunHoursFromStart=0; env.Apply();
            Check(env.EphemerisActive && Near(env.AppliedEarthDiameter,1.8021298f),"Scene consumes first physical frame");
            Check(Vector3.Angle(env.EarthDirection,table.Evaluate(0).earthDirection)<0.02f,"Calculated Earth direction reaches scene");
            Check(Vector3.Distance(env.EarthDirection,RenderSettings.skybox.GetVector("_EarthDirection"))<0.0001f,"Earth direction reaches shader");
            Check(Vector3.Distance(env.CelestialFrame.earthSunDirection,RenderSettings.skybox.GetVector("_EarthSunDirection"))<0.0001f,"Globe has its own Earth-to-Sun light direction");
            Check(Vector3.Distance(RenderSettings.skybox.GetMatrix("_WorldToStars").MultiplyVector(env.CelestialFrame.starsOrientation*Vector3.forward),Vector3.forward)<0.0001f,"Time-dependent star sphere orientation reaches shader");
            Check(Near(env.sunDiameter,0.53f),"Sun angular diameter remains 0.53 degrees");
            Check(Near(env.AppliedAzimuth,90.170044f) && Near(env.AppliedElevation,1.548597f),"Ground sunlight still uses unmodified CSV");
            var origin=env.EarthDirection; var initial=env.AppliedEarthOrientation;
            env.sunHoursFromStart=24; env.Apply();
            Check(Vector3.Angle(origin,env.EarthDirection)>1,"Earth position changes with shared clock");
            Check(Quaternion.Angle(initial,env.AppliedEarthOrientation)>5,"Globe continents change with Earth rotation");
            env.sunHoursFromStart=0; env.Apply();
            Check(Vector3.Distance(origin,env.EarthDirection)<0.000001f && Quaternion.Angle(initial,env.AppliedEarthOrientation)<0.02f,"Rewinding time restores physical Earth pose");
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<100;i++) env.Apply();
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Check(allocated<1024*1024,$"100 cached updates avoid repeated JSON/CSV string allocation ({allocated} bytes)");
            env.EarthOrientation=Quaternion.AngleAxis(80,Vector3.up); env.EarthPlacement=Quaternion.AngleAxis(120,Vector3.right); env.Apply();
            Check(Vector3.Distance(origin,env.EarthDirection)<0.000001f && Quaternion.Angle(initial,env.AppliedEarthOrientation)<0.02f,"Physical mode bypasses stored manual offsets");
            env.useCelestialEphemeris=false;
            env.EarthOrientation=Quaternion.AngleAxis(90,Vector3.right); env.Apply();
            var q=env.EarthOrientation; env.RotateEarth(Vector3.up,30);
            Check(Quaternion.Angle(q,env.EarthOrientation)>29.9f,"Manual local rotation works at 90 degrees");
            Check(!env.EphemerisActive && Near(env.AppliedEarthDiameter,env.earthDiameter),"Manual location and artistic scale remain available");
            env.EarthOrientation=savedOrientation; env.EarthPlacement=savedPlacement; env.useCelestialEphemeris=true; env.Apply();
            var inverse=RenderSettings.skybox.GetMatrix("_EarthWorldToTexture");
            Check(Vector3.Distance(inverse.MultiplyVector(env.AppliedEarthOrientation*Vector3.up),Vector3.up)<0.0001f,"Inverse Earth orientation reaches GPU matrix");
            Check(env.starCubemap && env.starCubemap.mipmapCount>1 && env.starCubemap.filterMode==FilterMode.Trilinear,"Fixed stars have mipmaps and Trilinear filtering");
            Check(env.illustrativeStars>0 && !GraphicsFormatUtility.IsSRGBFormat(env.starCubemap.graphicsFormat),"Illustrative stars stay visible in linear space");
            Check(!ShaderUtil.ShaderHasError(env.skyTemplate.shader),"Sky shader compiles");
            ValidateGpuMapping();
            CaptureEarthTimeline(env);
            env.sunHoursFromStart=0; env.Apply();
            CaptureStarMotion(env);
        }
        finally
        {
            env.sunHoursFromStart=savedHours; env.useCelestialEphemeris=savedMode;
            env.EarthOrientation=savedOrientation; env.EarthPlacement=savedPlacement; env.Apply();
        }
        Finish("validation.txt");
        LunarStarDesignValidation.Run(env);
        SceneView.RepaintAll();
    }

    static Color Probe(Material mat,Vector3 direction,int mode)
    {
        mat.SetVector("_Direction",direction); mat.SetFloat("_Mode",mode);
        var rt=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var old=RenderTexture.active; bool oldSrgb=GL.sRGBWrite;
        var tex=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        try
        {
            GL.sRGBWrite=false; Graphics.Blit(null,rt,mat); RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,1,1),0,0); tex.Apply(); return tex.GetPixel(0,0);
        }
        finally { GL.sRGBWrite=oldSrgb; RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(tex); }
    }

    static void ValidateGpuMapping()
    {
        var mat=new Material(Shader.Find("Hidden/Moon/SkyValidationProbe"));
        var diagnostic=new Cubemap(64,TextureFormat.R8,false) { filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp };
        try
        {
            var longitudeNormals=new[]{Vector3.right,Vector3.forward,Vector3.back,Vector3.up,Vector3.down};
            var expected=new[]{new Vector2(0.5f,0.5f),new Vector2(0.75f,0.5f),new Vector2(0.25f,0.5f),new Vector2(0.5f,1),new Vector2(0.5f,0)};
            for(int i=0;i<5;i++)
            {
                Color value=Probe(mat,longitudeNormals[i],0);
                Check(Near(value.g,expected[i].y,0.0001f) && (i>=3 || Near(value.r,expected[i].x)),"GPU geographic mapping "+new[]{"Greenwich","east 90","west 90","north pole","south pole"}[i]);
            }
            for(int face=0;face<6;face++)
            {
                var bytes=new byte[64*64];
                for(int y=0;y<64;y++) for(int x=0;x<64;x++)
                {
                    var d=LunarStarBaker.FaceDirection(face,2f*x/63-1,2f*y/63-1);
                    bytes[y*64+x]=(byte)Mathf.RoundToInt((0.5f+d.x*0.1f+d.y*0.15f+d.z*0.2f)*255);
                }
                diagnostic.SetPixelData(bytes,0,(CubemapFace)face);
            }
            diagnostic.Apply(false,false); mat.SetTexture("_Cube",diagnostic);
            var probes=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back,
                new Vector3(1,0.5f,0.4f).normalized,new Vector3(0.5f,1,0.4f).normalized,new Vector3(0.4f,0.5f,1).normalized,
                new Vector3(1,0.5f,1).normalized,new Vector3(-1,-1,0.5f).normalized,new Vector3(0.5f,1,1).normalized};
            float error=0;
            foreach(var d in probes) error=Mathf.Max(error,Mathf.Abs(Probe(mat,d,1).r-(0.5f+d.x*0.1f+d.y*0.15f+d.z*0.2f)));
            Check(error<0.015f,$"Cubemap faces and seams match GPU direction (max error {error:F5})");
        }
        finally { UnityEngine.Object.DestroyImmediate(mat); UnityEngine.Object.DestroyImmediate(diagnostic); }
    }

    static Camera CreateCamera(bool skyOnly)
    {
        var go=new GameObject("Celestial QA Camera") { hideFlags=HideFlags.HideAndDontSave };
        var camera=go.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.Skybox;
        camera.nearClipPlane=0.1f; camera.farClipPlane=40000; camera.allowHDR=true;
        if(skyOnly) camera.cullingMask=0;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        return camera;
    }

    static Color32[] Render(Camera camera,int width,int height,string path=null)
    {
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var old=RenderTexture.active; var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt; camera.aspect=(float)width/height; camera.Render(); RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,width,height),0,0); tex.Apply();
            if(path!=null) File.WriteAllBytes(Output+"/"+path,tex.EncodeToPNG());
            return tex.GetPixels32();
        }
        finally { camera.targetTexture=null; RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(tex); }
    }

    static void CaptureEarthTimeline(LunarEnvironment env)
    {
        var camera=CreateCamera(true); var ground=CreateCamera(false);
        var site=env.CelestialTable.Observer;
        // Terrain Y has sourceMinHeight removed by the original converter.
        ground.transform.position=new Vector3(site.worldXMeters,(float)site.elevationMeters-860.0025634765625f+2,site.worldZMeters);
        try
        {
            foreach(int h in new[]{0,24,48})
            {
                env.sunHoursFromStart=h; env.Apply();
                camera.transform.rotation=Quaternion.LookRotation(env.EarthDirection); camera.fieldOfView=env.AppliedEarthDiameter*3.5f;
                ground.transform.rotation=Quaternion.LookRotation(env.EarthDirection); ground.fieldOfView=60;
                foreach(int width in new[]{1280,1920})
                {
                    int height=width*9/16;
                    Render(camera,width,height,$"earth-{h:00}h-{width}.png");
                    Render(ground,width,height,$"ground-{h:00}h-{width}.png");
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(ground.gameObject); }
    }

    static Vector2 Project(Camera camera,Vector3 direction,int width,int height)
    {
        var local=camera.transform.InverseTransformDirection(direction);
        float tan=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*0.5f);
        return new Vector2((local.x/local.z/(tan*width/height)*0.5f+0.5f)*width-0.5f,
                           (local.y/local.z/tan*0.5f+0.5f)*height-0.5f);
    }

    static void CaptureStarMotion(LunarEnvironment env)
    {
        var camera=CreateCamera(true); camera.fieldOfView=60;
        var star=env.CelestialFrame.starsOrientation*Vector3.forward;
        Quaternion basis=Quaternion.LookRotation(star);
        try
        {
            foreach(int width in new[]{1280,1920})
            {
                int height=width*9/16; camera.aspect=(float)width/height;
                var flux=new List<float>(); var peaks=new List<float>(); var centroidError=new List<float>();
                var lines=new List<string>{"yawDegrees,linearFlux,peakLinear,centroidErrorPixels"};
                for(int k=-4;k<=4;k++)
                {
                    float yaw=k*0.25f;
                    camera.transform.rotation=basis*Quaternion.AngleAxis(yaw,Vector3.up);
                    var pixels=Render(camera,width,height,k==0?$"stars-{width}.png":null);
                    Vector2 point=Project(camera,star,width,height); int cx=Mathf.RoundToInt(point.x),cy=Mathf.RoundToInt(point.y);
                    float sum=0,peak=0,sx=0,sy=0;
                    for(int y=cy-7;y<=cy+7;y++) for(int x=cx-7;x<=cx+7;x++)
                    {
                        float value=Mathf.GammaToLinearSpace(pixels[y*width+x].r/255f);
                        sum+=value; peak=Mathf.Max(peak,value); sx+=value*x; sy+=value*y;
                    }
                    float error=sum>0?Vector2.Distance(new Vector2(sx/sum,sy/sum),point):999;
                    flux.Add(sum); peaks.Add(peak); centroidError.Add(error);
                    lines.Add(string.Join(",",new[]{yaw,sum,peak,error}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
                }
                float fluxChange=(flux.Max()-flux.Min())/Mathf.Max(flux.Average(),0.00001f);
                float peakChange=(peaks.Max()-peaks.Min())/Mathf.Max(peaks.Average(),0.00001f);
                Check(flux.Min()>0.1f,$"Reference star remains visible during slow yaw at {width}");
                Check(fluxChange<0.15f,$"Integrated star light stable during subpixel yaw at {width} (spread {fluxChange:P2})");
                Check(peakChange<0.35f,$"Star peak avoids on/off flicker at {width} (spread {peakChange:P2})");
                Check(centroidError.Max()<0.8f,$"Star follows fixed celestial direction at {width} (max {centroidError.Max():F3}px)");
                File.WriteAllLines(Output+$"/star-motion-{width}.csv",lines);
                camera.transform.rotation=basis;
                var first=Render(camera,width,height);
                camera.transform.position=new Vector3(1234,5900,-4312);
                var moved=Render(camera,width,height);
                int max=0; for(int i=0;i<first.Length;i++) max=Mathf.Max(max,Mathf.Abs(first[i].r-moved[i].r));
                Check(max<=2,$"Camera translation creates no sky parallax at {width} (max byte delta {max})");
                camera.transform.rotation=basis*Quaternion.AngleAxis(170,Vector3.up);
                Render(camera,width,height);
                camera.transform.rotation=basis;
                var returned=Render(camera,width,height);
                Check(returned.SequenceEqual(moved),$"Returning to same view reproduces identical star pixels at {width}");
                camera.transform.position=Vector3.zero;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }
    }

    [MenuItem("Tools/Moon Environment/Validate Celestial Play Mode")]
    public static void ValidatePlay()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Start outside Play mode.");
        EditorSceneManager.SaveScene(Env().gameObject.scene);
        SessionState.SetBool("Moon.CelestialPlayCheck",true); playStage=0;
        EditorApplication.isPlaying=true;
    }

    static void PlayUpdate()
    {
        var env=Env();
        if(playStage==0)
        {
            results.Clear(); failures=0;
            Check(env.EphemerisActive && env.CelestialTable.Count==577,"Offline ephemeris reloads in actual Play mode");
            Check(env.starCubemap && env.illustrativeStars>0,"Fixed stars available in player without network");
            env.sunHoursFromStart=0; env.animateSun=false; env.Apply();
            var start=env.EarthDirection;
            env.sunHoursFromStart=24; env.Apply();
            Check(Vector3.Angle(start,env.EarthDirection)>1,"Runtime shared clock moves Earth");
            env.sunHoursFromStart=0; env.Apply();
            Check(Vector3.Distance(start,env.EarthDirection)<0.000001f,"Runtime rewind restores Earth");
            env.animateSun=true; env.sunHoursPerSecond=10; playFrame=Time.frameCount; playStage=1;
            return;
        }
        if(playStage==1 && Time.frameCount-playFrame>=3)
        {
            Check(env.sunHoursFromStart>0 && env.CelestialFrame.seconds>0,"Runtime animation advances both Sun and Earth");
            env.sunHoursFromStart=48; env.sunHoursPerSecond=1000; playFrame=Time.frameCount; playStage=2; return;
        }
        if(playStage==2 && Time.frameCount-playFrame>=3)
        {
            Check(env.sunHoursFromStart==48 && Near(env.CelestialFrame.seconds,172800),"Runtime clamps both bodies at final record");
            Check(Near(env.AppliedEarthDiameter,1.8152088f),"Runtime endpoint has physical Earth size");
            env.useCelestialEphemeris=false; env.Apply(); var q=env.EarthOrientation;
            env.RotateEarth(Vector3.up,15);
            Check(Quaternion.Angle(q,env.EarthOrientation)>14.9f,"Manual rotation still works during Play mode");
            Finish("play-validation.txt"); SessionState.SetBool("Moon.CelestialPlayCheck",false); EditorApplication.isPlaying=false;
        }
    }
}
