using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Run from the existing celestial validation menu; adds no new toolbar entry.</summary>
public static class LunarStarDesignValidation
{
    const string Output="QA/StarDesign";
    static readonly List<string> checks=new List<string>();
    static int failures;
    static void Check(bool ok,string text) {checks.Add((ok?"PASS ":"FAIL ")+text); if(!ok) failures++;}

    public static void Run(LunarEnvironment env)
    {
        Directory.CreateDirectory(Output); checks.Clear(); failures=0;
        bool visibility=env.showStars; Color tint=env.starTint; float brightness=env.illustrativeStars;
        var cube=env.starCubemap; var originalSky=RenderSettings.skybox;
        var cameraObject=new GameObject("Star Design QA Camera") {hideFlags=HideFlags.HideAndDontSave};
        var camera=cameraObject.AddComponent<Camera>(); camera.cullingMask=0; camera.clearFlags=CameraClearFlags.Skybox;
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var temporaryCubes=new List<Cubemap>(); Material mask=null;
        try
        {
            env.showStars=false; env.Apply();
            Check(RenderSettings.skybox.GetFloat("_StarBrightness")==0,"Visibility toggle hides stars");
            env.showStars=true; env.illustrativeStars=0.2f; env.starTint=new Color(0.3f,0.6f,1); env.Apply();
            Check(Mathf.Abs(RenderSettings.skybox.GetFloat("_StarBrightness")-0.2f)<0.00001f,"Brightness applies without rebuilding map");
            Check(Vector4.Distance(RenderSettings.skybox.GetColor("_StarTint"),env.starTint)<0.00001f,"Tint applies without rebuilding map");
            Check(env.starCubemap==cube,"Live controls preserve cube reference and star layout");
            bool pending=env.StarDesignNeedsBake;
            float size=env.starSizeScale; env.starSizeScale=size+0.25f;
            Check(env.StarDesignNeedsBake,"Unapplied size setting is detected"); env.starSizeScale=size;
            Check(env.StarDesignNeedsBake==pending,"Returning settings clears pending state");
            env.showStars=true; env.starTint=Color.white; env.illustrativeStars=1;
            env.Apply();
            var starDirection=env.EphemerisActive?env.CelestialFrame.starsOrientation*Vector3.forward:Vector3.forward;
            camera.transform.rotation=Quaternion.LookRotation(starDirection); camera.fieldOfView=60;
            var small=LunarStarBaker.CreateCubemap(0.5f,1,20261114); temporaryCubes.Add(small);
            var large=LunarStarBaker.CreateCubemap(2,1,20261114); temporaryCubes.Add(large);
            env.starCubemap=small; env.Apply(); var a=Render(camera,1280,720,"single-star-small.png");
            env.starCubemap=large; env.Apply(); var b=Render(camera,1280,720,"single-star-large.png");
            int smallArea=CountCenter(a,1280,720),largeArea=CountCenter(b,1280,720);
            Check(smallArea>0 && largeArea>smallArea*2,$"Size setting changes measured footprint ({smallArea} -> {largeArea} pixels)");
            var empty=LunarStarBaker.CreateCubemap(1,0,7); temporaryCubes.Add(empty);
            env.starCubemap=empty; env.Apply(); var zero=Render(camera,1280,720);
            Check(CountCenter(zero,1280,720)==0,"Zero star count removes the reference star");
            var first=LunarStarBaker.CreateCubemap(1,256,71); temporaryCubes.Add(first);
            var repeated=LunarStarBaker.CreateCubemap(1,256,71); temporaryCubes.Add(repeated);
            var different=LunarStarBaker.CreateCubemap(1,256,72); temporaryCubes.Add(different);
            // The fixed central reference star is excluded from this comparison of the surrounding layout.
            env.starCubemap=first; env.Apply(); var seedA=Render(camera,1280,720);
            env.starCubemap=repeated; env.Apply(); var seedB=Render(camera,1280,720);
            env.starCubemap=different; env.Apply(); var seedC=Render(camera,1280,720);
            Check(seedA.SequenceEqual(seedB),"Same seed reproduces identical rendered star pixels");
            Check(!seedA.SequenceEqual(seedC),"Different seed changes surrounding star layout");
            env.starCubemap=cube; env.Apply();
            mask=new Material(RenderSettings.skybox) {hideFlags=HideFlags.HideAndDontSave};
            mask.SetFloat("_StarBrightness",0); mask.SetFloat("_EarthBrightness",1);
            mask.SetTexture("_EarthTex",Texture2D.whiteTexture);
            mask.SetVector("_SunDirection",-env.EarthDirection);
            mask.SetVector("_EarthSunDirection",-env.EarthDirection);
            RenderSettings.skybox=mask;
            camera.transform.rotation=Quaternion.LookRotation(env.EarthDirection);
            var lines=new List<string>{"width,height,verticalFovDegrees,earthDiameterDegrees,predictedPixels,measuredWidth,measuredHeight"};
            foreach(int width in new[]{1280,1920}) foreach(float fov in new[]{30f,60f,90f})
            {
                int height=width*9/16; camera.fieldOfView=fov;
                var pixels=Render(camera,width,height,$"earth-mask-{width}-fov{fov:0}.png");
                // Fit the half-lit interior, away from the antialiased limb. Counting any nonzero
                // pixel of an overexposed disk incorrectly includes its AA halo in the diameter.
                float sphereRadius=Mathf.Sin(env.AppliedEarthDiameter*Mathf.Deg2Rad*0.5f);
                float interiorRatio=Mathf.Sqrt(1-sphereRadius*sphereRadius)*Mathf.Sqrt(0.75f)/(1-0.5f*sphereRadius);
                float measuredWidth=MeasureHalfLightSpan(pixels,width,height,true)/interiorRatio;
                float measuredHeight=MeasureHalfLightSpan(pixels,width,height,false)/interiorRatio;
                float predicted=height*Mathf.Tan(env.AppliedEarthDiameter*Mathf.Deg2Rad*0.5f)/Mathf.Tan(fov*Mathf.Deg2Rad*0.5f);
                Check(Mathf.Abs(measuredWidth-predicted)<=0.75f && Mathf.Abs(measuredHeight-predicted)<=0.75f,
                    $"Earth disk {width} FOV{fov:0}: predicted {predicted:F2}px, GPU fit {measuredWidth:F2}x{measuredHeight:F2}px");
                Check(Math.Abs(measuredWidth-measuredHeight)<=0.15f,$"Earth disk remains circular at {width} FOV{fov:0}");
                lines.Add(string.Join(",",new[]{width,height,fov,env.AppliedEarthDiameter,predicted,measuredWidth,measuredHeight}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            }
            File.WriteAllLines(Output+"/earth-screen-size.csv",lines);
        }
        finally
        {
            RenderSettings.skybox=originalSky;
            env.starCubemap=cube; env.showStars=visibility; env.starTint=tint; env.illustrativeStars=brightness; env.Apply();
            UnityEngine.Object.DestroyImmediate(cameraObject);
            if(mask) UnityEngine.Object.DestroyImmediate(mask);
            foreach(var temp in temporaryCubes) UnityEngine.Object.DestroyImmediate(temp);
            File.WriteAllText(Output+"/validation.txt",$"UTC: {DateTime.UtcNow:O}\nPassed: {checks.Count-failures}; Failed: {failures}\n"+string.Join("\n",checks));
        }
        if(failures>0) Debug.LogError($"Star design / Earth size validation: {failures} failures. See {Output}.");
    }

    static int CountCenter(Color32[] pixels,int width,int height)
    {
        int n=0;
        for(int y=height/2-20;y<height/2+20;y++) for(int x=width/2-20;x<width/2+20;x++)
            if(pixels[y*width+x].r>25) n++;
        return n;
    }

    static float MeasureHalfLightSpan(Color32[] pixels,int width,int height,bool horizontal)
    {
        int length=horizontal?width:height;
        Func<int,float> value=i=>Mathf.GammaToLinearSpace(pixels[horizontal?(height/2)*width+i:i*width+width/2].r/255f);
        const float threshold=0.5f;
        float left=-1,right=-1;
        for(int i=0;i<length-1;i++)
        {
            float a=value(i),b=value(i+1);
            if(a<threshold && b>=threshold) left=i+(threshold-a)/(b-a);
            if(a>=threshold && b<threshold) right=i+(threshold-a)/(b-a);
        }
        return right-left;
    }

    static Color32[] Render(Camera camera,int width,int height,string name=null)
    {
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var before=RenderTexture.active; var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target; camera.aspect=(float)width/height; camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            if(name!=null) File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally {camera.targetTexture=null;RenderTexture.active=before;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
