using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.ShaderGraph
{
    class PropertyCollector
    {
        public struct TextureInfo
        {
            public string name;
            public int textureId;
            public bool modifiable;
        }

        public readonly List<AbstractShaderProperty> properties = new List<AbstractShaderProperty>();
        public bool HasDotsInstancingProps { get; private set; }

        public void AddShaderProperty(AbstractShaderProperty property)
        {
            if (properties.Any(x => x.referenceName == property.referenceName))
                return;
            if (property.gpuInstanced)
                HasDotsInstancingProps = true;
            properties.Add(property);
        }

        private const string s_UnityPerMaterialCbName = "UnityPerMaterial";

        private string GetPropertyCbName(AbstractShaderProperty property, GenerationMode generationMode)
        {
            var cbName = property.propertyType.IsBatchable() ? s_UnityPerMaterialCbName : string.Empty;

            //
            // Old behaviours that I don't know why we do them:

            // If the property is not exposed, put it to Global
            if (cbName == s_UnityPerMaterialCbName && !property.generatePropertyBlock)
                cbName = string.Empty;
            // If we are in preview, put all CB variables to UnityPerMaterial CB
            if (cbName != string.Empty && generationMode == GenerationMode.Preview)
                cbName = s_UnityPerMaterialCbName;

            return cbName;
        }

        public void GetPropertiesDeclaration(ShaderStringBuilder builder, GenerationMode mode, ConcretePrecision inheritedPrecision, ShaderStringBuilder dotsInstancingVars)
        {
            foreach (var prop in properties)
                prop.ValidateConcretePrecision(inheritedPrecision);

            var cbProps = new Dictionary<string, List<AbstractShaderProperty>>();
            foreach (var prop in properties)
            {
                var cbName = GetPropertyCbName(prop, mode);
                if (!cbProps.TryGetValue(cbName, out var vars))
                {
                    vars = new List<AbstractShaderProperty>();
                    cbProps.Add(cbName, vars);
                }
                vars.Add(prop);
            }

            // SamplerState properties are tricky:
            // - Unity only allows declaring SamplerState variable name of either sampler_{textureName} ("texture sampler") or SamplerState_{filterMode}_{wrapMode} ("system sampler").
            //   * That's why before the branch sg-texture-properties we have the referenceName of a SamplerStateShaderProperty set to the actual system sampler names.
            // - But with the existance of SubGraph functions we'll need unique SamplerState variable name for the function inputs.
            //   * That means if we have two SamplerState properties on the SubGraph blackboard of the same filterMode & wrapMode settings, it fails to compile because there are two
            //     identical function parameter names.
            // - So we'll have to use different names for each SamplerState property, which contradicts #1 (we could do special casing only for SubGraph function generation, but it needs
            //   changes to PropertyNode code generation, doable but more hacky).
            // - Instead, the branch sg-texture-properties changes the SamplerState property declaration to simply be:
            //       #define SamplerState_{referenceName} SamplerState{system sampler name}
            //   for all system sampler names (texture sampler names stay the same).
            //   And at the end collect all unique system sampler names and generate:
            //       SAMPLER(SamplerState{system sampler name});
            var systemSamplerNames = new HashSet<string>();

            List<AbstractShaderProperty> instancedProps = null;
            if (HasDotsInstancingProps && dotsInstancingVars != null)
            {
                builder.AppendLine("#ifdef UNITY_INSTANCING_ENABLED");
                builder.AppendLine("    #define UNITY_DOTS_INSTANCING_ENABLED");
                builder.AppendLine("#endif");
                instancedProps = new List<AbstractShaderProperty>();
            }

            foreach (var kvp in cbProps)
            {
                var cbName = kvp.Key;
                if (cbName != string.Empty)
                {
                    builder.AppendLine($"CBUFFER_START({cbName})");
                    builder.IncreaseIndent();
                }

                bool insideGpuInstancedBlock = false;

                // Use OrderBy for stable sort
                var props = kvp.Value.OrderBy(p => p.gpuInstanced);
                foreach (var prop in kvp.Value)
                {
                    if (instancedProps != null)
                    {
                        if (!insideGpuInstancedBlock && prop.gpuInstanced)
                        {
                            insideGpuInstancedBlock = true;
                            builder.AppendLine("#ifndef UNITY_DOTS_INSTANCING_ENABLED");
                            builder.IncreaseIndent();
                        }
                        else if (insideGpuInstancedBlock && !prop.gpuInstanced)
                        {
                            insideGpuInstancedBlock = false;
                            builder.DecreaseIndent();
                            builder.AppendLine("#endif");
                        }
                        if (prop.gpuInstanced)
                            instancedProps.Add(prop);
                    }

                    if (prop is GradientShaderProperty gradientProperty)
                        builder.AppendLine(gradientProperty.GetGraidentPropertyDeclarationString());
                    else if (prop is SamplerStateShaderProperty samplerProperty)
                        builder.AppendLine(samplerProperty.GetSamplerPropertyDeclarationString(systemSamplerNames));
                    else
                        builder.AppendLine($"{prop.propertyType.FormatDeclarationString(prop.concretePrecision, prop.referenceName)};");
                }

                if (insideGpuInstancedBlock)
                {
                    builder.DecreaseIndent();
                    builder.AppendLine("#endif");
                }

                if (systemSamplerNames.Count > 0)
                {
                    UnityEngine.Debug.Assert(cbName == string.Empty);
                    SamplerStateShaderProperty.GenerateSystemSamplerNames(builder, systemSamplerNames);
                    systemSamplerNames.Clear();
                }

                if (cbName != string.Empty)
                {
                    builder.DecreaseIndent();
                    builder.AppendLine($"CBUFFER_END");
                }
            }
            builder.AppendNewLine();

            if (instancedProps != null && instancedProps.Count > 0)
            {
                dotsInstancingVars.AppendLine("#define DOTS_CUSTOM_ADDITIONAL_MATERIAL_VARS \\");
                dotsInstancingVars.IncreaseIndent();
                builder.AppendLine("#ifdef UNITY_DOTS_INSTANCING_ENABLED");
                builder.IncreaseIndent();

                for (int i = 0; i < instancedProps.Count; ++i)
                {
                    var prop = instancedProps[i];
                    dotsInstancingVars.AppendLine($"UNITY_DEFINE_INSTANCED_PROP({prop.concreteShaderValueType.ToShaderString(prop.concretePrecision)}, {prop.referenceName}_Array){(i != instancedProps.Count - 1 ? " \\" : string.Empty)}");
                    builder.AppendLine($"#define {prop.referenceName} UNITY_ACCESS_INSTANCED_PROP(unity_Builtins0, {prop.referenceName}_Array)");
                }
                dotsInstancingVars.DecreaseIndent();
                builder.DecreaseIndent();
                builder.AppendLine("#endif");
            }
        }

        public List<TextureInfo> GetConfiguredTexutres()
        {
            var result = new List<TextureInfo>();

            foreach (var prop in properties.OfType<TextureShaderProperty>())
            {
                if (prop.referenceName != null)
                {
                    var textureInfo = new TextureInfo
                    {
                        name = prop.referenceName,
                        textureId = prop.value.texture != null ? prop.value.texture.GetInstanceID() : 0,
                        modifiable = prop.modifiable
                    };
                    result.Add(textureInfo);
                }
            }

            foreach (var prop in properties.OfType<Texture2DArrayShaderProperty>())
            {
                if (prop.referenceName != null)
                {
                    var textureInfo = new TextureInfo
                    {
                        name = prop.referenceName,
                        textureId = prop.value.textureArray != null ? prop.value.textureArray.GetInstanceID() : 0,
                        modifiable = prop.modifiable
                    };
                    result.Add(textureInfo);
                }
            }

            foreach (var prop in properties.OfType<Texture3DShaderProperty>())
            {
                if (prop.referenceName != null)
                {
                    var textureInfo = new TextureInfo
                    {
                        name = prop.referenceName,
                        textureId = prop.value.texture != null ? prop.value.texture.GetInstanceID() : 0,
                        modifiable = prop.modifiable
                    };
                    result.Add(textureInfo);
                }
            }

            foreach (var prop in properties.OfType<CubemapShaderProperty>())
            {
                if (prop.referenceName != null)
                {
                    var textureInfo = new TextureInfo
                    {
                        name = prop.referenceName,
                        textureId = prop.value.cubemap != null ? prop.value.cubemap.GetInstanceID() : 0,
                        modifiable = prop.modifiable
                    };
                    result.Add(textureInfo);
                }
            }
            return result;
        }
    }
}
