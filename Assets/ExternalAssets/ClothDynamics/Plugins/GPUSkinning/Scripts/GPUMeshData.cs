using UnityEngine;

namespace ClothDynamics
{
    public class GPUMeshData : MonoBehaviour
    {
        [Tooltip("This changes the collision size of the colliding spheres per mesh object. Can not be changed at runtime! (Default = 1)")]
        public float _vertexCollisionScale = 1;
    }
}