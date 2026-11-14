Shader "Hidden/Moon/PackTerrainMask"
{
    Properties { _MainTex ("Linear roughness", 2D) = "white" {} _HeightTex ("Linear height", 2D) = "gray" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _HeightTex;
            float4 frag(v2f_img i) : SV_Target
            {
                // URP terrain mask: metallic, occlusion, height, smoothness.
                // No measured AO exists in this asset, so leave occlusion at 1.
                return float4(0, 1, tex2D(_HeightTex, i.uv).r, 1-saturate(tex2D(_MainTex, i.uv).r));
            }
            ENDCG
        }
    }
}
