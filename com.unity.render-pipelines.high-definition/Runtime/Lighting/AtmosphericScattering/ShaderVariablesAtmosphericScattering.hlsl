#ifdef SHADER_VARIABLES_INCLUDE_CB
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/ShaderVariablesAtmosphericScattering.cs.hlsl"
#else
    TEXTURE3D(_VBufferLighting);
    TEXTURECUBE_ARRAY(_SkyTexture);
    TEXTURE2D(_SkyTextureMarginalRows);
    TEXTURE2D(_SkyTextureMarginalCols);
    float4 _HDRISkySphereIntegral;

    uint _SkyFrameIndex;

    #define _MipFogNear                     _MipFogParameters.x
    #define _MipFogFar                      _MipFogParameters.y
    #define _MipFogMaxMip                   _MipFogParameters.z

    #define _FogColor                       _FogColor
#endif
