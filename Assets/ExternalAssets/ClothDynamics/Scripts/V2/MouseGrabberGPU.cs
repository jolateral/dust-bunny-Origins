using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClothDynamics
{
    [DefaultExecutionOrder(15200)] //When using Final IK
    public class MouseGrabberGPU
    {
        //made this struct blittable for GetData to work
        public struct HandleMouse
        {
            //public RaycastCollision collision;
            public int collide;
            public int objectIndex;
            public float distanceToOrigin;
            public int m_isGrabbing;
            public int m_grabbedVertexMass;
        }
        public void Initialize(ClothSolverGPU solver, GPUClothDynamicsV2 dynamics, ComputeBuffer positions, ComputeBuffer velocities, ComputeBuffer invMass)
        {
            if (_cs == null)
            {
                _cs = GraphicsUtilities.LoadComputeShaderAt("Shaders/Compute/V2/MouseGrabberGPU");
            }
            if (_handleBuffer == null) _handleBuffer = new ComputeBuffer(1, Marshal.SizeOf<HandleMouse>());
            _handleBuffer.SetData(new HandleMouse[1] { new HandleMouse() { collide=0, objectIndex=-1, distanceToOrigin = 0, m_grabbedVertexMass = 0, m_isGrabbing = 0 } });
            _dynamics = dynamics;
            _solver = solver;
        }

        public void OnDestroy()
        {
            _handleBuffer.ClearBuffer();
        }

        public void HandleMouseInteraction()
        {
            if (_dynamics == null && _solver == null) return;
            bool shouldPickObject = Input.GetMouseButtonDown(0);

            bool shouldReleaseObject = Input.GetMouseButtonUp(0);

            _cs.SetBool("_shouldPickObject", shouldPickObject);
            _cs.SetBool("_shouldReleaseObject", shouldReleaseObject);
            _cs.SetVector("_mousePositionAndScreen", new Vector4(Input.mousePosition.x, Input.mousePosition.y, Screen.width, Screen.height));
            _cs.SetFloat("params_particleDiameter", _dynamics._globalSimParams.particleDiameter);
            _cs.SetFloat("_fixedDeltaTime", _dynamics._globalSimParams.deltaTime);
            _cs.SetInt("params_numParticles", _dynamics._globalSimParams.numParticles);
            float4x4 invVP = math.inverse(Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix);
            _cs.SetMatrix("_invVP", invVP);

            _cs.SetBuffer(0, "positions", _solver._positions);
            _cs.SetBuffer(0, "velocities", _solver._velocities);
            _cs.SetBuffer(0, "invMasses", _solver._invMasses);
            _cs.SetBuffer(0, "_handle", _handleBuffer);
            _cs.Dispatch(0, 1, 1, 1);
        }

        public void UpdateGrappedVertex()
        {
            if (_dynamics == null && _solver == null) return;

            _cs.SetVector("_mousePositionAndScreen", new Vector4(Input.mousePosition.x, Input.mousePosition.y, Screen.width, Screen.height));
            _cs.SetFloat("params_particleDiameter", _dynamics._globalSimParams.particleDiameter);
            _cs.SetFloat("_fixedDeltaTime", _dynamics._globalSimParams.deltaTime);
            _cs.SetInt("params_numParticles", _dynamics._globalSimParams.numParticles);
            float4x4 invVP = math.inverse(Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix);
            _cs.SetMatrix("_invVP", invVP);

            _cs.SetBuffer(1, "positions", _solver._positions);
            _cs.SetBuffer(1, "velocities", _solver._velocities);
            _cs.SetBuffer(1, "invMasses", _solver._invMasses);
            _cs.SetBuffer(1, "_handle", _handleBuffer);
            _cs.Dispatch(1, 1, 1, 1);

        }

        private ComputeShader _cs;
        private GPUClothDynamicsV2 _dynamics;
        private ClothSolverGPU _solver;
        internal ComputeBuffer _handleBuffer;
    }
}