#ifndef LUNAR_SKY_MAPPING_INCLUDED
#define LUNAR_SKY_MAPPING_INCLUDED
float2 LunarEarthUV(float3 normal, float rotationDegrees, float geographicMode)
{
    // Geographic frame is Earth-fixed xyz reordered as xzy, not an Euler rotation.
    float longitude = geographicMode > 0.5 ? atan2(normal.z,normal.x) : atan2(normal.x,normal.z);
    return float2(longitude/(2*UNITY_PI)+0.5+rotationDegrees/360,
                  asin(clamp(normal.y,-1,1))/UNITY_PI+0.5);
}
#endif
