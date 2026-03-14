using UnityEngine;
using static ClothDynamics.GPUClothDynamics;

namespace ClothDynamics
{
    public class GPUClothBase : MonoBehaviour
    {
        [Tooltip("The cloth will collide with the objects of this list. You can use SkinnedMeshes, Meshes, AutomaticBoneSpheres, other GPUClothDynamics or this cloth for self collision. However beware of cloth-to-cloth collision, it is very expensive performance-wise.")]
        [SerializeField] public Transform[] _meshObjects;      
        [Tooltip("Add a HighRes mesh object here, which should be controlled by this cloth object. (The cloth object will be invisible.)")]
        [SerializeField] public GameObject _meshProxy;
        [Tooltip("Here you can toggle if the MeshProxy will be used, however you need to add a mesh to make this work.")]
        [SerializeField] public bool _useMeshProxy = true;
       
        internal ObjectBuffers[] _objBuffers;
        //internal int _version = 1;
        internal bool _finishedLoading = false;

        internal virtual void SetSecondUVsForVertexID(Mesh mesh)
        {

        }

        internal virtual ComputeBuffer GetPositionsBuffer()
        {
            return null;
        }

        internal virtual ComputeBuffer GetNormalsBuffer()
        {
            return null;
        }

        internal virtual void SetCustomProperties(MaterialPropertyBlock mpb)
        {

        }
    }
}