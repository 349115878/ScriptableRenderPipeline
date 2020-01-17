PackedVaryings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    output = BuildVaryings(input);
    PackedVaryings packedOutput = PackVaryings(output);
    return packedOutput;
}

struct Targets
{
    float4  color   : SV_Target0;
    float4  mask    : SV_Target1;
    float4  normal  : SV_Target2;
};

Targets frag(PackedVaryings packedInput)
{
    Targets o = (Targets)0;

    Varyings unpacked = UnpackVaryings(packedInput);
    UNITY_SETUP_INSTANCE_ID(unpacked);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(unpacked);

    SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(unpacked);
    SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

    float4 mainTex = unpacked.color * surfaceDescription.Color;
    mainTex.rgb *= mainTex.a;
    o.color = mainTex;

    half4 maskTex = surfaceDescription.Mask;
    maskTex.a = mainTex.a;
    maskTex.rgb *= maskTex.a;
    o.mask = maskTex;

    float4 normalVS = NormalsRenderingShared(mainTex, surfaceDescription.Normal, unpacked.tangentWS.xyz, unpacked.bitangentWS, unpacked.normalWS);
    normalVS.rgb *= normalVS.a;
    o.normal = normalVS;

    return o;
}
