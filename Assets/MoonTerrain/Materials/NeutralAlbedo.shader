Shader "Hidden/Moon/NeutralAlbedo"
{
    Properties { _MainTex ("Source albedo", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 frag(v2f_img i) : SV_Target
            {
                float3 source = tex2D(_MainTex, i.uv).rgb;
                // Neutralize the simulant's brown tint in linear space; preserve its luminance detail.
                float value = dot(source, float3(0.2126, 0.7152, 0.0722));
                return float4(value, value, value, 1);
            }
            ENDCG
        }
    }
}
