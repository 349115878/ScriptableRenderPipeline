using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace UnityEngine.Experimental.Rendering.HDPipelineTest.TestGenerator
{
    // Prototype
    public class UpdateShaderGraphMaterial : MonoBehaviour, IUpdateGameObjects
    {
        public ExecuteMode executeMode => m_ExecuteMode;

        public void UpdateInPlayMode(Transform parent, List<GameObject> instances)
        {
            UpdateInstances(instances);
        }

        public void UpdateInEditMode(Transform parent, List<GameObject> instances)
        {
            UpdateInstances(instances);
        }

        void UpdateInstances(List<GameObject> instances)
        {
            var c = Mathf.Min(instances.Count, m_Updates.Length);
            for (var i = 0; i < c; ++i)
            {
                var instance = instances[i];
                if (instance == null || instance.Equals(null)) continue;

                var renderer = instance.GetComponent<Renderer>();
                if (renderer == null || renderer.Equals(null)) continue;

                var material = renderer.sharedMaterial;
                if (material == null || material.Equals(null)) continue;

                var shader = material.shader;
                if (shader == null || shader.Equals(null)) continue;

                var shaderPath = AssetDatabase.GetAssetPath(shader);
                var isShaderGraph = Path.GetExtension(shaderPath) == ".shadergraph";
                if (!isShaderGraph) continue;

                var shaderText = File.ReadAllText(shaderPath);

                var modifications = m_Updates[i];
                foreach (var modification in modifications.Updates)
                    shaderText = modification.ApplyTo(shaderText);

                File.WriteAllText(shaderPath, shaderText);
                AssetDatabase.ImportAsset(shaderPath);
            }
        }

        /// <summary>
        ///     A modification to apply to a shader graph.
        ///     We modify directly the json of the shader graph.
        /// </summary>
        [Serializable]
        struct ShaderGraphUpdate
        {
#pragma warning disable 649
            [Tooltip("Path to the property to update. (ex: 'my_obj.my_array[0].property'")]
            public string jsonPath;

            [Tooltip("The value to set.")] public string value;
#pragma warning restore 649

            static readonly Regex k_LineReturn = new Regex(@"(?<=\\)\n");
            static readonly Regex k_BackSlash = new Regex(@"(?<=\\)\\");
            static readonly Regex k_Quote = new Regex(@"(?<=\\)\""");

            public string ApplyTo(string shaderText)
            {
                var obj = JObject.Parse(shaderText);
                var jtoken = obj.SelectToken("m_SerializableNodes[0].JSONnodeData");
                var masterNodeEscapedJSON = jtoken.ToString();

                var masterNodeJSON = k_LineReturn.Replace(masterNodeEscapedJSON, "\n");
                masterNodeJSON = k_Quote.Replace(masterNodeJSON, "\"");
                masterNodeJSON = k_BackSlash.Replace(masterNodeJSON, "\\");

                var masterNodeJObject = JObject.Parse(masterNodeJSON);
                var masterNodeJToken = masterNodeJObject.SelectToken(jsonPath);
                masterNodeJToken?.Replace(JToken.Parse(value));

                masterNodeJSON = masterNodeJObject.ToString();
                jtoken.Replace(masterNodeJSON);
                return obj.ToString();
            }
        }

        /// <summary>
        ///     A list of shader graph updates.
        /// </summary>
        [Serializable]
        struct ShaderGraphUpdateList
        {
#pragma warning disable 649
            public List<ShaderGraphUpdate> Updates;
#pragma warning restore 649
        }

#pragma warning disable 649
        [Tooltip("When to execute this updater.")] [SerializeField]
        ExecuteMode m_ExecuteMode = ExecuteMode.All;

        [Tooltip("The modifications to apply to the shader graph.")] [SerializeField]
        ShaderGraphUpdateList[] m_Updates;
#pragma warning restore 649
    }
}
