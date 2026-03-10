using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClothDynamics
{
    //[CreateAssetMenu(fileName = "ClothExtensionGPU", menuName = "ScriptableObjects/ClothExtensionGPU")]
    public class ClothExtensionGPU : ScriptableObject
    {
        [Tooltip("This shows the color collision, it only is visible if \"Debug Mesh Points\" is active in the Colliders tab of CD2.")]
        [SerializeField] public bool _showDebugColors = false;

        public virtual void Init(ClothSolverGPU clothSolver)
        {

        }

        public virtual void CollideBodyParticles(ComputeBuffer deltas, ComputeBuffer deltaCounts, ComputeBuffer predicted, ComputeBuffer invMasses, ComputeBuffer neighbors, ComputeBuffer positions, float deltaTime)
        {

        }
    }
}