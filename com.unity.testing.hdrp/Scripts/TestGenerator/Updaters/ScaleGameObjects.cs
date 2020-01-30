using System.Collections.Generic;

namespace UnityEngine.Experimental.Rendering.HDPipelineTest.TestGenerator
{
    // Prototype
    [AddComponentMenu("TestGenerator/Updaters/Scale GameObjects")]
    public class ScaleGameObjects : MonoBehaviour, IUpdateGameObjects
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

        void UpdateInstances(IEnumerable<GameObject> instances)
        {
            foreach (var instance in instances)
            {
                var tr = instance.transform;
                tr.localScale = m_Scale;
            }
        }
#pragma warning disable 649
        [Tooltip("When to execute this updater.")] [SerializeField]
        ExecuteMode m_ExecuteMode = ExecuteMode.All;

        [Tooltip("The local scale to set.")] [SerializeField]
        Vector3 m_Scale;
#pragma warning restore 649
    }
}
