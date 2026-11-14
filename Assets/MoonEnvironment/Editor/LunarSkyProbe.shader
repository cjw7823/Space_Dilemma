Shader "Hidden/Moon/SkyValidationProbe"
{
    Properties { _MainTex ("Unused",2D)="black"{} _Cube ("Probe",Cube)="black"{} _Direction("Direction",Vector)=(0,0,1,0) _Mode("Mode",Float)=0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../LunarSkyMapping.cginc"
            samplerCUBE _Cube;
            float4 _Direction;
            float _Mode;
            float4 frag(v2f_img i):SV_Target
            {
                if(_Mode<0.5) return float4(LunarEarthUV(normalize(_Direction.xyz),0,1),0,1);
                return float4(texCUBE(_Cube,normalize(_Direction.xyz)).rrr,1);
            }
            ENDCG
        }
    }
}
