using UnityEngine;
using System.Collections.Generic;

namespace dev.sworks.blendmaestro.runtime.model
{
    public class BlendMaestroDataAssets : ScriptableObject
    {
        public byte[] VertexData;
    }

    [System.Serializable]
    public class BlendMaestroDataAsset
    {
        public string meshName;
        public List<BlendMaestroData> blendShapes;
    }
}