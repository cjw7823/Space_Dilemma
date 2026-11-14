using System;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic, illustrative (not catalogue) sky, baked once with prefiltered mips.</summary>
public static class LunarStarBaker
{
    public const string AssetPath = "Assets/MoonEnvironment/Data/FixedIllustrativeStars.asset";
    const int Resolution = 1024;
    static readonly Vector3[] Forward = { Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back };
    static readonly Vector3[] Right = { Vector3.back,Vector3.forward,Vector3.right,Vector3.right,Vector3.right,Vector3.left };
    static readonly Vector3[] Vertical = { Vector3.down,Vector3.down,Vector3.forward,Vector3.back,Vector3.down,Vector3.down };

    public static Vector3 FaceDirection(int face,float u,float v) => (Forward[face]+Right[face]*u+Vertical[face]*v).normalized;

    [MenuItem("Tools/Moon Environment/Rebake Illustrative Star Cubemap")]
    public static void Rebake() => ApplyTo(UnityEngine.Object.FindFirstObjectByType<LunarEnvironment>());

    public static void ApplyTo(LunarEnvironment env, bool force = true)
    {
        if(!env) throw new InvalidOperationException("Select a scene with LunarEnvironment first.");
        if(Application.isPlaying) throw new InvalidOperationException("Bake the star design outside Play mode.");
        env.starSizeScale=Mathf.Clamp(env.starSizeScale,0.25f,3);
        env.starCount=Mathf.Clamp(env.starCount,0,12000);
        Undo.RecordObject(env,"Apply star design");
        var cube=Build(force || env.StarDesignNeedsBake,env.starSizeScale,env.starCount,env.starSeed);
        env.RecordStarDesignApplied(cube); env.Apply();
        EditorUtility.SetDirty(env);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
        SceneView.RepaintAll();
    }

    public static Cubemap Build(bool replace = false,float sizeScale = 1,int starCount = 6000,int seed = 20261114)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(AssetPath);
        if (existing && !replace) return existing;
        var cube=CreateCubemap(sizeScale,starCount,seed);
        try
        {
            if(existing)
            {
                EditorUtility.CopySerialized(cube,existing);
                UnityEngine.Object.DestroyImmediate(cube);
                EditorUtility.SetDirty(existing); cube=existing;
            }
            else AssetDatabase.CreateAsset(cube,AssetPath);
            AssetDatabase.SaveAssets(); return cube;
        }
        catch { if(cube && !AssetDatabase.Contains(cube)) UnityEngine.Object.DestroyImmediate(cube); throw; }
    }

    public static Cubemap CreateCubemap(float sizeScale,int starCount,int seed)
    {
        sizeScale=Mathf.Clamp(sizeScale,0.25f,3); starCount=Mathf.Clamp(starCount,0,12000);
        var cube = new Cubemap(Resolution,TextureFormat.R8,true)
        {
            name="FixedIllustrativeStars",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=4
        };
        var rng = new System.Random(seed);
        var directions = new Vector3[starCount];
        var sigma = new float[starCount];
        var intensity = new float[starCount];
        // One isolated bright reference star also allows repeatable subpixel-motion validation.
        if(starCount>0) { directions[0]=Vector3.forward; sigma[0]=0.0018f; intensity[0]=1; }
        for(int i=1;i<starCount;i++)
        {
            Vector3 d;
            do
            {
                double y=rng.NextDouble()*2-1, az=rng.NextDouble()*Math.PI*2;
                double r=Math.Sqrt(1-y*y);
                d=new Vector3((float)(r*Math.Sin(az)),(float)y,(float)(r*Math.Cos(az)));
            } while(Vector3.Dot(d,Vector3.forward)>0.9997f);
            directions[i]=d;
            sigma[i]=Mathf.Lerp(0.0008f,0.0016f,(float)rng.NextDouble());
            intensity[i]=Mathf.Lerp(0.18f,0.95f,Mathf.Pow((float)rng.NextDouble(),2));
        }
        try
        {
            for(int face=0;face<6;face++)
            {
                var pixels=new float[Resolution*Resolution];
                for(int i=0;i<starCount;i++)
                {
                    float facing=Vector3.Dot(directions[i],Forward[face]);
                    if(facing<0.5f) continue;
                    float u=Vector3.Dot(directions[i],Right[face])/facing;
                    float v=Vector3.Dot(directions[i],Vertical[face])/facing;
                    float cx=(u+1)*0.5f*(Resolution-1),cy=(v+1)*0.5f*(Resolution-1);
                    float targetSigma=sigma[i]*sizeScale;
                    // Approximate texel integration: retain a small filtered footprint when stars are shrunk.
                    float filterSigma=0.7f*facing/Resolution;
                    float variance=targetSigma*targetSigma+filterSigma*filterSigma;
                    float energy=targetSigma*targetSigma/variance;
                    int radius=Mathf.CeilToInt(Mathf.Sqrt(variance)*Resolution*4/(facing*facing));
                    int x0=Mathf.Max(0,Mathf.FloorToInt(cx)-radius),x1=Mathf.Min(Resolution-1,Mathf.CeilToInt(cx)+radius);
                    int y0=Mathf.Max(0,Mathf.FloorToInt(cy)-radius),y1=Mathf.Min(Resolution-1,Mathf.CeilToInt(cy)+radius);
                    float scale=-0.5f/variance;
                    for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                    {
                        Vector3 ray=FaceDirection(face,2f*x/(Resolution-1)-1,2f*y/(Resolution-1)-1);
                        pixels[y*Resolution+x]+=Mathf.Exp((ray-directions[i]).sqrMagnitude*scale)*intensity[i]*energy;
                    }
                }
                var bytes=new byte[pixels.Length];
                for(int i=0;i<bytes.Length;i++) bytes[i]=(byte)Mathf.Clamp(Mathf.RoundToInt(pixels[i]*255),0,255);
                cube.SetPixelData(bytes,0,(CubemapFace)face);
            }
            cube.Apply(true,true);
            return cube;
        }
        catch { if(cube && !AssetDatabase.Contains(cube)) UnityEngine.Object.DestroyImmediate(cube); throw; }
    }
}
