using UnityEngine;

namespace ClothDynamics
{
    public class GPUSkinnerBase : GPUMeshData
    {
        [Tooltip("This let you control if this component should render the skinning.")]
        [SerializeField] internal bool _render = true;
        internal bool _updateSync = false;
        internal Shader _shader = null;
        internal virtual void UpdateSync()
        {
        }
    }
}