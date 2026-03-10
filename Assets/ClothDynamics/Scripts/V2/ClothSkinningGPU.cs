using UnityEngine;

namespace ClothDynamics
{
    [DefaultExecutionOrder(15260)] //When using Final IK
    public class ClothSkinningGPU : MonoBehaviour
    {
        private enum SkinTypes
        {
            NoSkinning,
            GPUSkinning,
            DualQuaternionSkinner,
            //SkinnerSource
        }
        private SkinTypes _skinTypeCloth = SkinTypes.NoSkinning;

        [Tooltip("This blends how strong the skinning will affect the cloth. Also the red vertex color channel will affect the results.")]
        [SerializeField] private float _blendSkinning = 0.7f;
        [Tooltip("This will always blend to the skinned vertex position independent of the red vertex color channel. It can be used to reset the cloth original position e.g. beaming characters from A to B.")]
        [SerializeField] public float _minBlend = 0.01f;
        [Tooltip("Activate this to push the cloth outside of the colliding mesh surface. You need to add a mesh (e.g. a character) to the MeshObjects list.")]
        [SerializeField] public bool _useSurfacePush = true;
        [Tooltip("This says how much the skinning should affect the surface push. A Value of 1 will try to reset the cloth vertex to the skinned position, a value of 0 will reset the cloth vertex to the last position outside the mesh.")]
        [SerializeField] public float _skinningForSurfacePush = 1.0f;
        [Tooltip("This is the intensity of the push force. A too high value can create a jitter effect.")]
        [SerializeField] public float _surfacePush = 5;
        [Tooltip("This is the offset for the surface position in the negative normal direction. So positive values will move the push-surface further inside the mesh.")]
        [SerializeField] public float _surfaceOffset = 0.02f;
        [Tooltip("This is needed if you use this cloth as a child object of an animated parent.")]
        public Transform _bodyParent;

        internal int prevNumParticles;
        internal int newParticles;
        GPUSkinning _gpuSkinning = null;

        private void OnEnable()
        {
            if (_skinComponent != null && _skinComponent.transform.parent != this.transform.parent)
                _skinComponent = null;
            _skinComponent = this.GetComponent<GPUSkinning>();
            if (_skinComponent == null)
                _skinComponent = this.GetComponent<DualQuaternionSkinner>();
            if (_skinComponent != null && !_skinComponent.isActiveAndEnabled)
                _skinComponent = null;

            if (_skinComponent != null)
            {
                if (_skinComponent.GetType() == typeof(GPUSkinning))
                    _skinTypeCloth = SkinTypes.GPUSkinning;
                if (_skinComponent.GetType() == typeof(DualQuaternionSkinner))
                    _skinTypeCloth = SkinTypes.DualQuaternionSkinner;
                //if (_skinComponent.GetType() == typeof(SkinnerSource))
                //    _skinType = SkinTypes.SkinnerSource;
                Debug.Log("<color=blue>CD: </color><color=lime>" + this.name + " is using " + _skinComponent.GetType() + " for cloth (self) skinning</color>");
            }

            if (_skinComponent == null)
            {
                //_minBlend = 0;
                _blendSkinning = 0;
                _dummyTex = new RenderTexture(4, 4, 0);
                _skinTypeCloth = SkinTypes.NoSkinning;
                _skinningForSurfacePush = 0;
            }
        }

        private void SetBodyParent(bool localSetup)
        {
            if (localSetup == false && (_bodyParent != null || this.transform.parent != null))
            {
                this.transform.parent = null;
                _bodyParent = null;
            }
            if (localSetup == true && _bodyParent == null)
            {
                _bodyParent = this.transform.parent;
            }
        }

        public void SkinUpdate(ComputeShader m_cs, ComputeBuffer positions, ComputeBuffer velocities, int start, int count, int BLOCK_SIZE, bool localSetup = false)
        {
            SetBodyParent(localSetup);

            int UpdateSkinning_Kernel = 12;
            int UpdateSkinningAndBlends_Kernel = 13;

            //if (_useGarmentMesh)
            //{
            //    bool prewarm = Time.frameCount < 30;
            //    if (prewarm || (!_onlyAtStart && _baseVerticesBuffer != null))
            //    {
            //        _clothSolver.SetFloat(_blendGarment_ID, prewarm ? 0.01f : _blendGarment * 0.01f);
            //        _clothSolver.SetFloat(_pushVertsByNormals_ID, _pushVertsByNormals);
            //        _clothSolver.SetBuffer(_blendGarmentOriginKernel, _normalsBuffer_ID, _objBuffers[0].normalsBuffer);
            //        _clothSolver.SetBuffer(_blendGarmentOriginKernel, _baseVertices_ID, _baseVerticesBuffer);
            //        //_clothSolver.SetBuffer(blendGarmentOriginKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            //        _clothSolver.SetBuffer(_blendGarmentOriginKernel, _projectedPositions_ID, _projectedPositionsBuffer);
            //        _clothSolver.Dispatch(_blendGarmentOriginKernel, _numGroups_Vertices, 1, 1);
            //    }
            //}
            if (_skinTypeCloth == SkinTypes.DualQuaternionSkinner)
            {
                //_clothSolver.Dispatch(_updatePositionsKernel, _numGroups_Vertices, 1, 1);
                Debug.LogError("SkinTypes.DualQuaternionSkinner currently not supported for CD V2!");
            }
            else if (_skinTypeCloth == SkinTypes.GPUSkinning)
            {
                bool morph = false;
                if (this.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                    morph = true;

                int kernel = morph ? UpdateSkinningAndBlends_Kernel : UpdateSkinning_Kernel;

                if (morph)
                {
                    //print("blendShapes " + blendShapes.gameObject.name);
                    var blendShapes = (GPUBlendShapes)monoMorph;
                    if (blendShapes != null && blendShapes._rtArrayCombined != null)
                    {
                        m_cs.SetInt("_rtArrayWidth", blendShapes._rtArrayCombined.width);
                        m_cs.SetTexture(kernel, "_rtArray", blendShapes._rtArrayCombined);
                    }
                }

                if (_gpuSkinning == null) _gpuSkinning = this.GetComponent<GPUSkinning>();
                if (_gpuSkinning != null && _gpuSkinning._meshVertsOut != null && _gpuSkinning.isActiveAndEnabled)
                {
                    m_cs.SetFloat("_minBlend", _minBlend);
                    m_cs.SetFloat("_blendSkinning", _blendSkinning);
                    m_cs.SetInt("start", start);
                    m_cs.SetInt("count", count);
                    m_cs.SetMatrix("_localToWorldMatrix", this.transform.localToWorldMatrix);
                    m_cs.SetBool("_localSetup", localSetup);
                    if (_bodyParent != null) m_cs.SetMatrix("_bodyMatrix", _bodyParent.worldToLocalMatrix);
                    if (_gpuSkinning != null && _gpuSkinning._meshVertsOut != null) m_cs.SetBuffer(kernel, "_meshVertsOut", _gpuSkinning._meshVertsOut);
                    m_cs.SetBuffer(kernel, "positions", positions);
                    m_cs.SetBuffer(kernel, "velocities", velocities);
                    m_cs.Dispatch(kernel, count.GetComputeShaderThreads(BLOCK_SIZE), 1, 1);
                }
            }
        }


        private int _skinningForSurfacePush_ID = Shader.PropertyToID("_skinningForSurfacePush");

        private int _skinned_tex_width_ID = Shader.PropertyToID("_skinned_tex_width");
        private int _skinned_data_1_ID = Shader.PropertyToID("_skinned_data_1");

        private int _meshVertsOut_ID = Shader.PropertyToID("_meshVertsOut");
        private int _surfacePush_ID = Shader.PropertyToID("_surfacePush");
        private int _surfaceOffset_ID = Shader.PropertyToID("_surfaceOffset");

        private GPUSkinnerBase _skinComponent;

        internal Texture _dummyTex;//TODO needed?


        internal void SurfacePush(ClothSolverGPU solver, ComputeShader m_cs, ComputeBuffer positions, ComputeBuffer predicted, int start, int count, int BLOCK_SIZE, bool localSetup = false)
        {
            SetBodyParent(localSetup);

            if (_skinComponent == null) { _useSurfacePush = false; _skinComponent = null; }

            bool useCF = true;// _forceSurfacePushColliders ? false : _useCollisionFinder && _collisionFinder != null;
            //if (!useCF) _forceSurfacePushColliders = true;
            int _surfacePushKernel = 16;
            int _surfacePushCollidersKernel = _surfacePushKernel;//TODO
            int _surfacePushDQSKernel = 17;
            int _surfacePushCollidersDQSKernel = _surfacePushDQSKernel;//TODO
            int _surfacePushSkinningBlendsKernel = 18;
            int _surfacePushCollidersSkinningBlendsKernel = _surfacePushSkinningBlendsKernel;//TODO
            int _surfacePushSkinningKernel = 19;
            int _surfacePushCollidersSkinningKernel = _surfacePushSkinningKernel;//TODO

            int kernel = useCF ? _surfacePushKernel : _surfacePushCollidersKernel;


            m_cs.SetFloat(_skinningForSurfacePush_ID, _skinningForSurfacePush);
            if (_skinningForSurfacePush > 0)
            {
                if (_skinTypeCloth == SkinTypes.DualQuaternionSkinner)
                {
                    var dqs = _skinComponent as DualQuaternionSkinner;
                    if (dqs.gameObject.activeInHierarchy)
                    {
                        kernel = useCF ? _surfacePushDQSKernel : _surfacePushCollidersDQSKernel;
                        m_cs.SetInt(_skinned_tex_width_ID, dqs ? dqs._textureWidth : 4);//should be set already
                        m_cs.SetTexture(kernel, _skinned_data_1_ID, dqs ? dqs._rtSkinnedData_1 : _dummyTex);
                    }
                }
                else if (_skinTypeCloth == SkinTypes.GPUSkinning)
                {
                    var skinning = _skinComponent as GPUSkinning;
                    if (skinning.gameObject.activeInHierarchy)
                    {
                        bool morph = false;
                        if (skinning.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                            morph = true;

                        kernel = morph ? (useCF ? _surfacePushSkinningBlendsKernel : _surfacePushCollidersSkinningBlendsKernel) : (useCF ? _surfacePushSkinningKernel : _surfacePushCollidersSkinningKernel);

                        if (morph)
                        {
                            //print("blendShapes " + blendShapes.gameObject.name);
                            var blendShapes = (GPUBlendShapes)monoMorph;
                            m_cs.SetInt("_rtArrayWidth", blendShapes._rtArrayCombined.width);
                            m_cs.SetTexture(kernel, "_rtArray", blendShapes._rtArrayCombined);
                        }

                        if (skinning._meshVertsOut != null) m_cs.SetBuffer(kernel, _meshVertsOut_ID, skinning._meshVertsOut);
                    }
                }
            }
            if (useCF)
            {
                //m_cs.SetFloat("_normalScale", solver._trisMode ? solver.m_dynamics._globalSimParams._triangleNormalScale : solver.m_dynamics._globalSimParams._vertexNormalScale);
                m_cs.SetBool("_trisMode", solver._trisMode);
                m_cs.SetInt("params_numObjects", solver._simParams.numParticles + (solver._collisionMeshes._sphereDataBuffer.count > 1 ? solver._collisionMeshes._sphereDataBuffer.count : 0));
                m_cs.SetInt("params_numParticles", solver._simParams.numParticles);
                m_cs.SetInt("params_maxNumNeighbors", solver._dynamics._globalSimParams.maxNumNeighbors);

                m_cs.SetBuffer(kernel, "neighbors", solver._spatialHash.neighbors);
                m_cs.SetBuffer(kernel, "_sphereDataBuffer", solver._collisionMeshes._sphereDataBuffer);
                m_cs.SetInt("_sphereDataBufferCount", solver._collisionMeshes._sphereDataBuffer.count);

            }
            else
            {
                //m_cs.SetBuffer(kernel, _collidableSpheres_ID, _collidableSpheresBuffer);
                //m_cs.SetBuffer(kernel, _collidableSDFs_ID, _collidableSDFsBuffer);
            }
            m_cs.SetFloat(_surfacePush_ID, _surfacePush);
            m_cs.SetFloat(_surfaceOffset_ID, _surfaceOffset);
            m_cs.SetInt("start", start);
            m_cs.SetInt("count", count);
            m_cs.SetMatrix("_localToWorldMatrix", this.transform.localToWorldMatrix);
            m_cs.SetBool("_localSetup", localSetup);
            if (_bodyParent != null) m_cs.SetMatrix("_bodyMatrix", _bodyParent.worldToLocalMatrix);
            m_cs.SetBuffer(kernel, "predicted", predicted);
            m_cs.SetBuffer(kernel, "positions", positions);
            m_cs.Dispatch(kernel, count.GetComputeShaderThreads(BLOCK_SIZE), 1, 1);
        }
    }
}