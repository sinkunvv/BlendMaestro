using UnityEngine;
using System.Collections.Generic;

namespace dev.sworks.blendmaestro.runtime.model
{
    [System.Serializable]
    public class BlendMaestroData
    {
        public string name;
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;
    }
}