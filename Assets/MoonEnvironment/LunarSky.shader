Shader "Custom/LunarSky"
{
    Properties
    {
        _EarthTex ("NASA Blue Marble", 2D) = "white" {}
        _StarCube ("Fixed illustrative star map", Cube) = "black" {}
        _UseEphemeris ("Geographic Earth mapping", Float) = 0
        _SunDirection ("Direction to Sun", Vector) = (-0.7,0.14,0.7,0)
        _EarthDirection ("Direction to Earth", Vector) = (0.25,0.26,0.93,0)
        _EarthSunDirection ("Earth to Sun lighting direction", Vector) = (-0.7,0.14,0.7,0)
        _SunDiameter ("Sun angular diameter (degrees)", Float) = 0.53
        _SunDiskRadiance ("Sun disk radiance", Range(0,12)) = 3
        _SunGlare ("Sun glare intensity", Range(0,1)) = 0
        _EarthDiameter ("Earth angular diameter (degrees)", Float) = 4
        _EarthRotation ("Earth map rotation (degrees)", Float) = 30
        _EarthBrightness ("Earth brightness", Float) = 1.6
        _EarthEmission ("Earth night-side emission", Range(0,0.3)) = 0
        _StarBrightness ("Illustrative star visibility", Range(0,4)) = 1
        _StarTint ("Illustrative star tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "LunarSkyMapping.cginc"
            sampler2D _EarthTex;
            samplerCUBE _StarCube;
            float4 _SunDirection, _EarthDirection, _EarthSunDirection;
            float4 _StarTint;
            float4x4 _EarthWorldToTexture, _WorldToStars;
            float _SunDiameter, _EarthDiameter, _EarthRotation, _EarthBrightness, _StarBrightness, _UseEphemeris;
            float _SunDiskRadiance, _SunGlare, _EarthEmission;
            struct v2f { float4 pos : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.direction = mul((float3x3)unity_ObjectToWorld, vertex.xyz);
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 ray = normalize(i.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float3 earth = normalize(_EarthDirection.xyz);
                float3 color = 0;
                // Smooth, prefiltered fixed stars: no pixel hash, camera-position input or temporal twinkle.
                float3 starRay = mul((float3x3)_WorldToStars, ray);
                color += _StarTint.rgb * _StarBrightness * texCUBE(_StarCube, starRay).rrr;
                // Angular distance uses cross products to retain precision for a small solar disk.
                float sunRadius = radians(_SunDiameter * 0.5);
                float sunDistance = atan2(length(cross(ray,sun)), dot(ray,sun));
                float sunAA = max(fwidth(sunDistance),0.00001);
                float solarDisk = 1 - smoothstep(sunRadius - sunAA, sunRadius + sunAA, sunDistance);
                const float3 sunTint = float3(1, 0.98, 0.92);
                color += sunTint * solarDisk * _SunDiskRadiance;

                // Lens-like glare, drawn in sky directions so it also works in cameras without Bloom.
                float fromSunLimb = max(sunDistance - sunRadius, 0);
                if (_SunGlare > 0 && fromSunLimb < radians(2.0))
                {
                    float2 falloff = fromSunLimb / radians(float2(0.42, 0.95));
                    float halo = dot(exp2(-falloff * falloff), float2(0.8, 0.2));
                    halo *= 1 - smoothstep(radians(1.35), radians(2.0), fromSunLimb);
                    color += sunTint * (halo * _SunGlare);
                }

                float facing = dot(ray,earth);
                float3 perpendicular = ray - earth * facing;
                float radius = sin(radians(_EarthDiameter * 0.5));
                float r = length(perpendicular) / max(radius,0.00001);
                float edge = max(fwidth(r),0.0001);
                if (facing > 0 && r < 1 + edge)
                {
                    // Ray-sphere intersection in unit sky space, with no world-distance parallax.
                    float t = facing - sqrt(max(0,radius*radius-dot(perpendicular,perpendicular)));
                    float3 normal = normalize(ray*t-earth);
                    float3 mapNormal = normalize(mul((float3x3)_EarthWorldToTexture, normal));
                    // SPICE xyz is encoded xzy before quaternion conversion to handle Unity handedness.
                    // Greenwich = +X, east 90 = +Z, north = +Y in this texture frame.
                    float2 uv = LunarEarthUV(mapNormal,_EarthRotation,_UseEphemeris);
                    // Longitude wraps at atan2's seam; keep mip derivatives continuous there.
                    float2 dx = ddx(uv), dy = ddy(uv);
                    dx.x -= round(dx.x);
                    dy.x -= round(dy.x);
                    float3 albedo = tex2Dgrad(_EarthTex,uv,dx,dy).rgb;
                    float daylight = saturate(dot(normal,normalize(_EarthSunDirection.xyz)));
                    float3 globe = albedo * daylight * _EarthBrightness;
                    // A faint emissive contribution reveals the globe's shape across the night side.
                    float night = 1 - smoothstep(0, 0.2, daylight);
                    float3 nightTint = albedo * float3(0.64, 0.78, 1) + float3(0.009, 0.018, 0.032);
                    globe += nightTint * (night * _EarthEmission * saturate(_EarthBrightness));
                    // A thin lit limb belongs to Earth's atmosphere, not the Moon's sky.
                    globe += float3(0.1,0.3,0.65) * pow(saturate(1+dot(normal,ray)),4) * daylight * 0.25;
                    color = lerp(color,globe,1-smoothstep(1-edge,1+edge,r));
                }
                return float4(color,1);
            }
            ENDCG
        }
    }
}
