using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClothDynamics
{
    public class MeshWelding : MonoBehaviour
    {
        [Tooltip("This is the threshold that will be used for welding.")]
        [SerializeField] private float _threshold = 1e-05f;
        [Tooltip("This checks which triangles are used and removes unnecessary onces.")]
        [SerializeField] private bool _checkTris = false;
        [Tooltip("This is the original welding method used by CD.")]
        [SerializeField] private bool _oldMethod = false;

        void Awake()
        {
            var mesh = GetComponent<SkinnedMeshRenderer>() ? GetComponent<SkinnedMeshRenderer>().sharedMesh : GetComponent<MeshFilter>() ? GetComponent<MeshFilter>().sharedMesh : null;
            if (mesh != null)
            {
                if(_oldMethod)
                    GPUClothDynamics.WeldVerticesOld(mesh, _threshold);
                else if(_checkTris)
                    GPUClothDynamics.WeldVerticesAndTris(mesh, out _, null, _threshold);
                 else
                    GPUClothDynamics.WeldVertices(mesh, out _, _threshold);
            }
        }

    }
}