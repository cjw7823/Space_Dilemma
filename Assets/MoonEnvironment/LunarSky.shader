Shader "Custom/LunarSky"
{
    Properties
    {
        _EarthTex ("NASA Blue Marble", 2D) = "white" {}
        _SunDirection ("Direction to Sun", Vector) = (-0.7,0.14,0.7,0)
        _EarthDirection ("Direction to Earth", Vector) = (0.25,0.26,0.93,0)
        _SunDiameter ("Sun angular diameter (degrees)", Float) = 0.53
        _EarthDiameter ("Earth angular diameter (degrees)", Float) = 4
        _EarthRotation ("Earth map rotation (degrees)", Float) = 30
        _EarthBrightness ("Earth brightness", Float) = 1.6
        _StarBrightness ("Illustrative stars (0 = daylight exposure)", Range(0,1)) = 0
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
            sampler2D _EarthTex;
            float4 _SunDirection, _EarthDirection;
            float _SunDiameter, _EarthDiameter, _EarthRotation, _EarthBrightness, _StarBrightness;
            struct v2f { float4 pos : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.direction = mul((float3x3)unity_ObjectToWorld, vertex.xyz);
                return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))) * 43758.5453); }
            float4 frag(v2f i) : SV_Target
            {
                float3 ray = normalize(i.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float3 earth = normalize(_EarthDirection.xyz);
                float3 color = 0;
                // Decorative deterministic stars, deliberately off for a sunlit surface exposure.
                float3 grid = ray * 700;
                float seed = hash(floor(grid));
                float starDot = 1 - smoothstep(0.02,0.15,length(frac(grid)-0.5));
                color += _StarBrightness * step(0.998,seed) * starDot;
                // Angular distance uses cross products to retain precision for a small solar disk.
                float sunRadius = radians(_SunDiameter * 0.5);
                float sunDistance = atan2(length(cross(ray,sun)), dot(ray,sun));
                float sunAA = max(fwidth(sunDistance),0.00001);
                color += float3(1,0.98,0.92) * (1-smoothstep(sunRadius-sunAA,sunRadius+sunAA,sunDistance)) * 3;
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
                    float rotation = radians(_EarthRotation);
                    float2 uv = float2(atan2(normal.x,normal.z)/(2*UNITY_PI)+0.5+rotation/(2*UNITY_PI),
                                       asin(clamp(normal.y,-1,1))/UNITY_PI+0.5);
                    // Longitude wraps at atan2's seam; keep mip derivatives continuous there.
                    float2 dx = ddx(uv), dy = ddy(uv);
                    dx.x -= round(dx.x);
                    dy.x -= round(dy.x);
                    float3 albedo = tex2Dgrad(_EarthTex,uv,dx,dy).rgb;
                    float daylight = saturate(dot(normal,sun));
                    float3 globe = albedo * daylight * _EarthBrightness;
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
