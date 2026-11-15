#ifndef MOON_REGOLITH_VARIATION_INCLUDED
#define MOON_REGOLITH_VARIATION_INCLUDED

// Variation is anchored to world positions, with no per-frame or per-tile seed.
float2 LunarHash22(float2 position)
{
    float3 p = frac(float3(position.xyx) * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);
    return frac((p.xx + p.yz) * p.zy);
}

float LunarNoise(float2 position)
{
    float2 cell = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3.0 - 2.0 * blend);
    float a = LunarHash22(cell).x;
    float b = LunarHash22(cell + float2(1, 0)).x;
    float c = LunarHash22(cell + float2(0, 1)).x;
    float d = LunarHash22(cell + 1.0).x;
    return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
}

half LunarMacro(float3 positionWS)
{
    float2 position = GetAbsolutePositionWS(positionWS).xz;
    float variation = 0.50 * LunarNoise(position / 11.0);
    variation += 0.32 * LunarNoise(mul(float2x2(0.8, -0.6, 0.6, 0.8), position) / 47.0 + 17.31);
    variation += 0.18 * LunarNoise(position / 173.0 + 51.73);
    return 1.0h + (half)(_LunarMacroStrength * (2.0 * variation - 1.0));
}

struct LunarDetailSampling
{
    float2 firstOffset;
    float2 secondOffset;
    float blend;
    float strength;
};

LunarDetailSampling LunarDetailAt(float3 positionWS)
{
    LunarDetailSampling result;
    float2 position = GetAbsolutePositionWS(positionWS).xz / max(_LunarDetailCellMeters, 0.1);
    float noise = 0.65 * LunarNoise(position);
    noise += 0.35 * LunarNoise(position * 0.47 + 37.21);
    float variation = noise * 8.0;
    float cell = floor(variation);
    float phase = frac(variation);

    // Neighbouring scalar cells share endpoint samples. The smooth crossfade
    // keeps the sampled surface continuous at every cell boundary.
    result.firstOffset = LunarHash22(float2(cell, 19.17)) * 2.0 - 1.0;
    result.secondOffset = LunarHash22(float2(cell + 1.0, 19.17)) * 2.0 - 1.0;
    result.blend = smoothstep(0.2, 0.8, phase);
    float fadeEnd = max(_LunarDetailFadeEnd, _LunarDetailFadeStart + 0.01);
    result.strength = saturate(_LunarDetailStrength) *
        (1.0 - smoothstep(_LunarDetailFadeStart, fadeEnd, distance(_WorldSpaceCameraPos, positionWS)));
    return result;
}

half4 LunarSampleLayer(TEXTURE2D_PARAM(layer, layerSampler), float2 uv, LunarDetailSampling detail)
{
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    // The UV offsets do not force a coarser mip at the crossfade boundaries.
    float footprint = max(length(dx), length(dy));
    float strength = detail.strength * (1.0 - smoothstep(0.22, 0.75, footprint));
    if (strength < 0.0001)
        return SAMPLE_TEXTURE2D_GRAD(layer, layerSampler, uv, dx, dy);

    half4 first = SAMPLE_TEXTURE2D_GRAD(layer, layerSampler, uv + detail.firstOffset * strength, dx, dy);
    half4 second = SAMPLE_TEXTURE2D_GRAD(layer, layerSampler, uv + detail.secondOffset * strength, dx, dy);
    return lerp(first, second, detail.blend);
}

void LunarFadeDetailNormal(float3 positionWS, inout half3 normalTS)
{
    float distanceToCamera = distance(_WorldSpaceCameraPos, positionWS);
    half detail = 1.0h - (half)smoothstep(30.0, 180.0, distanceToCamera);
    normalTS = normalize(lerp(half3(0, 0, 1), normalTS, detail));
}

#endif
