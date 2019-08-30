using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.Graphing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    // Node a StackShaderProperty has no user settable values it is only used to ensure correct data is emitted into the shader. So we use a dummy value of  "int" type.
    class StackShaderProperty : AbstractShaderProperty<int>
    {
        [SerializeField]
        private bool m_Modifiable = false;

        public StackShaderProperty()
        {
            displayName = "Stack";
            slotNames = new List<string>();
            slotNames.Add("Dummy");
        }

        public override PropertyType propertyType
        {
            get { return PropertyType.TextureStack; }
        }

        public bool modifiable
        {
            get { return m_Modifiable; }
            set { m_Modifiable = value; }
        }

        public override bool isBatchable
        {
            // Note we are semi batchable, constants are but texture slots not. Need to clarify this.
            get { return true; }
        }

        public override bool isRenamable
        {
            get { return true; }
        }

        public override bool isExposable
        {
            get { return true; }
        }

        public List<string> slotNames;

        private string GetSlotNamesString(string delimiter=",")
        {
            var result = new StringBuilder();

            for (int i = 0; i < slotNames.Count; i++)
            {
                if (i != 0) result.Append(delimiter);
                result.Append(slotNames[i]);
            }

            return result.ToString();
        }

        public override string GetPropertyBlockString()
        {
            return ""; //A stack only has variables declared in the actual shader not in the shaderlab wrapper code
        }

        public override string GetPropertyDeclarationString(string delimiter = ";")
        {
            // This node needs to generate some properties both in batched as in unbatched mode
            throw new Exception("Don't use this, use GetPropertyDeclarationStringForBatchMode instead");
        }

        public override string GetPropertyDeclarationStringForBatchMode(GenerationMode mode, string delimiter = ";")
        {
            int numSlots = slotNames.Count;

            if (mode == GenerationMode.InConstantBuffer)
            {
                return string.Format("DECLARE_STACK_CB({0}){1}", referenceName, delimiter);
            }
            else
            {
                return string.Format("DECLARE_STACK{0}({1}, {2}){3}", (numSlots <= 1) ? "" : "" + numSlots, referenceName, GetSlotNamesString(), delimiter);
            }
        }

        public override string GetPropertyAsArgumentString()
        {
            throw new NotImplementedException();
        }

        public override PreviewProperty GetPreviewMaterialProperty()
        {
            return new PreviewProperty(PropertyType.TextureStack)
            {
                name = referenceName
            };
        }

        public override AbstractMaterialNode ToConcreteNode()
        {
            return null;
        }

        public override ShaderInput Copy()
        {
            var copied = new StackShaderProperty();
            copied.displayName = displayName;
            copied.value = value;
            return copied;
        }
    }
}
