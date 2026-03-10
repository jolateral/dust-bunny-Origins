using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace ClothDynamics
{
    //[ExecuteAlways]
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
    public class BodyMeshToSDF : MeshToSDF
#else
    public class BodyMeshToSDF : MonoBehaviour
#endif
    {
        [SerializeField]
        [Tooltip("The SDFTexture that the SDF will be rendered to. Make sure the SDFTexture references a 3D RenderTexture.")]
        BodySDFTexture _SDFTexture;

#if !HAS_PACKAGE_DEMOTEAM_MESHTOSDF
        public enum UpdateMode
        {
            OnBeginFrame,
            Explicit
        }

        public enum FloodMode
        {
            Linear,
            Jump
        }

        public enum FloodFillQuality
        {
            Normal,
            Ultra
        }

        public enum DistanceMode
        {
            Signed,
            Unsigned
        }
#endif
        [Header("Flood fill")]
        [SerializeField]
        [Tooltip(@"Use jump flood if you need to fill the entire volume, but it only outputs unsigned distance.
If you need signed distance or just need a limited shell around your surface, use linear flood fill, but many iterations are expensive.")]
        FloodMode _FloodMode = FloodMode.Linear;
        [SerializeField]
        [Tooltip("Normal - flood in orthogonal directions only, faster. \nUltra - flood in orthogonal and diagonal directions, slower.")]
        FloodFillQuality _FloodFillQuality;
        [Range(0, 64), SerializeField]
        int _FloodFillIterations = 4;
        [Header("Distance")]
        [SerializeField]
        DistanceMode _DistanceMode = DistanceMode.Signed;
        [SerializeField]
        [Tooltip("Offset the surface by the offset amount by adding it to distances in the final generation step. Applicable only in signed distance mode.")]
        float _Offset = 0;
        [SerializeField]
        UpdateMode _UpdateMode = UpdateMode.Explicit;

        GPUClothDynamicsV2 _dynamics = null;

        public new BodySDFTexture sdfTexture { get { return (BodySDFTexture)_SDFTexture; } set { _SDFTexture = value; } }
        public new FloodMode floodMode { get { return _FloodMode; } set { _FloodMode = value; } }
        public new FloodFillQuality floodFillQuality { get { return _FloodFillQuality; } set { _FloodFillQuality = value; } }
        public new int floodFillIterations { get { return _FloodFillIterations; } set { _FloodFillIterations = Mathf.Clamp(value, 0, 64); } }
        public new DistanceMode distanceMode { get { return _DistanceMode; } set { _DistanceMode = value; } }
        public new float offset { get { return _Offset; } set { _Offset = value; } }
        public new UpdateMode updateMode { get { return _UpdateMode; } set { _UpdateMode = value; } }

        [SerializeField]
        ComputeShader _Compute = null;

        SkinnedMeshRenderer _SkinnedMeshRenderer = null;
        MeshFilter _MeshFilter = null;

        ComputeBuffer _SDFBuffer = null;
        ComputeBuffer _SDFBufferBis = null;
        ComputeBuffer _JumpBuffer = null;
        ComputeBuffer _JumpBufferBis = null;
        int _InitializeKernel = -1;
        int _SplatTriangleDistancesSignedKernel = -1;
        int _SplatTriangleDistancesUnsignedKernel = -1;
        int _SplatTriangleDistancesSignedSkinningKernel = -1;
        int _SplatTriangleDistancesUnsignedSkinningKernel = -1;
        int _SplatTriangleDistancesSignedSkinningMorphKernel = -1;
        int _SplatTriangleDistancesUnsignedSkinningMorphKernel = -1;
        int _FinalizeKernel = -1;
        int _LinearFloodStepKernel = -1;
        int _LinearFloodStepUltraQualityKernel = -1;
        int _JumpFloodInitialize = -1;
        int _JumpFloodStep = -1;
        int _JumpFloodStepUltraQuality = -1;
        int _JumpFloodFinalize = -1;
        int _BufferToTextureCalcGradient = -1;

        public GraphicsBuffer _VertexBuffer = null;
        public GraphicsBuffer _IndexBuffer = null;
        int _VertexBufferStride;
        int _VertexBufferPosAttributeOffset;
        IndexFormat _IndexFormat;
        public CommandBuffer _CommandBuffer = null;

        public static int _kThreadCount = 256;
        const int _kMaxThreadGroupCount = 65535;
        int _ThreadGroupCountTriangles;

        static class Uniforms
        {
            internal static int _SDF = Shader.PropertyToID("_SDF");
            internal static int _SDFBuffer = Shader.PropertyToID("_SDFBuffer");
            internal static int _SDFBufferRW = Shader.PropertyToID("_SDFBufferRW");
            internal static int _JumpBuffer = Shader.PropertyToID("_JumpBuffer");
            internal static int _JumpBufferRW = Shader.PropertyToID("_JumpBufferRW");
            internal static int _VoxelResolution = Shader.PropertyToID("_VoxelResolution");
            internal static int _MaxDistance = Shader.PropertyToID("_MaxDistance");
            internal static int INITIAL_DISTANCE = Shader.PropertyToID("INITIAL_DISTANCE");
            internal static int _WorldToLocal = Shader.PropertyToID("_WorldToLocal");
            internal static int _Offset = Shader.PropertyToID("_Offset");
            internal static int g_SignedDistanceField = Shader.PropertyToID("g_SignedDistanceField");
            internal static int g_NumCellsX = Shader.PropertyToID("g_NumCellsX");
            internal static int g_NumCellsY = Shader.PropertyToID("g_NumCellsY");
            internal static int g_NumCellsZ = Shader.PropertyToID("g_NumCellsZ");
            internal static int g_Origin = Shader.PropertyToID("g_Origin");
            internal static int g_CellSize = Shader.PropertyToID("g_CellSize");
            internal static int _VertexBuffer = Shader.PropertyToID("_VertexBuffer");
            internal static int _IndexBuffer = Shader.PropertyToID("_IndexBuffer");
            internal static int _IndexFormat16bit = Shader.PropertyToID("_IndexFormat16bit");
            internal static int _VertexBufferStride = Shader.PropertyToID("_VertexBufferStride");
            internal static int _VertexBufferPosAttributeOffset = Shader.PropertyToID("_VertexBufferPosAttributeOffset");
            internal static int _JumpOffset = Shader.PropertyToID("_JumpOffset");
            internal static int _JumpOffsetInterleaved = Shader.PropertyToID("_JumpOffsetInterleaved");
            internal static int _DispatchSizeX = Shader.PropertyToID("_DispatchSizeX");
        }

        static class Labels
        {
            internal static string MeshToSDF = "MeshToSDF";
            internal static string Initialize = "Initialize";
            internal static string SplatTriangleDistances = "SplatTriangleDistances";
            internal static string SplatTriangleDistancesSigned = "SplatTriangleDistancesSigned";
            internal static string SplatTriangleDistancesUnsigned = "SplatTriangleDistancesUnsigned";
            internal static string SplatTriangleDistancesSignedSkinning = "SplatTriangleDistancesSignedSkinning";
            internal static string SplatTriangleDistancesUnsignedSkinning = "SplatTriangleDistancesUnsignedSkinning";
            internal static string SplatTriangleDistancesSignedSkinningMorph = "SplatTriangleDistancesSignedSkinningMorph";
            internal static string SplatTriangleDistancesUnsignedSkinningMorph = "SplatTriangleDistancesUnsignedSkinningMorph";
            internal static string Finalize = "Finalize";
            internal static string LinearFloodStep = "LinearFloodStep";
            internal static string LinearFloodStepUltraQuality = "LinearFloodStepUltraQuality";
            internal static string JumpFloodInitialize = "JumpFloodInitialize";
            internal static string JumpFloodStep = "JumpFloodStep";
            internal static string JumpFloodStepUltraQuality = "JumpFloodStepUltraQuality";
            internal static string JumpFloodFinalize = "JumpFloodFinalize";
            internal static string BufferToTexture = "BufferToTexture";
        }

        bool _Initialized = false;

        void Init()
        {
            if (_Initialized)
                return;

            if (_Compute == null) _Compute = Resources.Load<ComputeShader>("BodyMeshToSDF");

            _Initialized = true;

            _InitializeKernel = _Compute.FindKernel(Labels.Initialize);
            _SplatTriangleDistancesSignedKernel = _Compute.FindKernel(Labels.SplatTriangleDistancesSigned);
            _SplatTriangleDistancesUnsignedKernel = _Compute.FindKernel(Labels.SplatTriangleDistancesUnsigned);
            _SplatTriangleDistancesSignedSkinningKernel = _Compute.FindKernel(Labels.SplatTriangleDistancesSignedSkinning);
            _SplatTriangleDistancesUnsignedSkinningKernel = _Compute.FindKernel(Labels.SplatTriangleDistancesUnsignedSkinning);
            _SplatTriangleDistancesSignedSkinningMorphKernel = _Compute.FindKernel(Labels.SplatTriangleDistancesSignedSkinningMorph);
            _SplatTriangleDistancesUnsignedSkinningMorphKernel = _Compute.FindKernel(Labels.SplatTriangleDistancesUnsignedSkinningMorph);
            _FinalizeKernel = _Compute.FindKernel(Labels.Finalize);
            _LinearFloodStepKernel = _Compute.FindKernel(Labels.LinearFloodStep);
            _LinearFloodStepUltraQualityKernel = _Compute.FindKernel(Labels.LinearFloodStepUltraQuality);
            _JumpFloodInitialize = _Compute.FindKernel(Labels.JumpFloodInitialize);
            _JumpFloodStep = _Compute.FindKernel(Labels.JumpFloodStep);
            _JumpFloodStepUltraQuality = _Compute.FindKernel(Labels.JumpFloodStepUltraQuality);
            _JumpFloodFinalize = _Compute.FindKernel(Labels.JumpFloodFinalize);
            _BufferToTextureCalcGradient = _Compute.FindKernel(Labels.BufferToTexture);

            _SkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            _MeshFilter = GetComponent<MeshFilter>();
        }

        public new void UpdateSDF(CommandBuffer cmd)
        {
            //Debug.Log("UpdateSDF");

            if (_UpdateMode != UpdateMode.Explicit)
            {
                Debug.LogError("Switch MeshToSDF to explicit scheduling mode before directly controlling its update.", this);
                return;
            }

            RenderSDF(cmd);
        }

#if USING_HDRP || USING_URP
    void OnBeginContextRendering(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
    {
        if (_UpdateMode != UpdateMode.OnBeginFrame)
            return;

        CommandBuffer cmd = CommandBufferPool.Get(Labels.MeshToSDF);

        RenderSDF(cmd);
        context.ExecuteCommandBuffer(cmd);
        
        ReleaseGraphicsBuffer(ref _VertexBuffer);
        ReleaseGraphicsBuffer(ref _IndexBuffer);
        CommandBufferPool.Release(cmd);
    }
#else

        int _LastFrame = -1;

        void OnPreRenderCamera(Camera camera)
        {
            if (_UpdateMode != UpdateMode.OnBeginFrame)
                return;

            if (Time.renderedFrameCount == _LastFrame)
                return;

            if (_CommandBuffer == null)
                _CommandBuffer = new CommandBuffer() { name = Labels.MeshToSDF };
            else
                _CommandBuffer.Clear();

            RenderSDF(_CommandBuffer);

            Graphics.ExecuteCommandBuffer(_CommandBuffer);

            ReleaseGraphicsBuffer(ref _VertexBuffer);
            ReleaseGraphicsBuffer(ref _IndexBuffer);
            _LastFrame = Time.renderedFrameCount;
        }
#endif

        void RenderSDF(CommandBuffer cmd)
        {
            // Debug.Log(" _SDFTexture " + _SDFTexture);
            //if(_SDFTexture) Debug.Log(" _SDFTexture.mode  " + _SDFTexture.mode);
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
            if (_SDFTexture == null || _SDFTexture.mode != SDFTexture.Mode.Dynamic)
#else
            if (_SDFTexture == null || _SDFTexture.mode != BodySDFTexture.Mode.Dynamic)
#endif
            {
                Debug.LogError("MeshToSDF requires a dynamic SDFTexture to render into.", this);
                return;
            }
            Vector3Int voxelResolution = _SDFTexture.voxelResolution;
            int voxelCount = voxelResolution.x * voxelResolution.y * voxelResolution.z;
            Bounds voxelBounds = _SDFTexture.voxelBounds;
            float voxelSize = _SDFTexture.voxelSize;
            int threadGroupCountVoxels = (int)Mathf.Ceil((float)voxelCount / (float)_kThreadCount);

            int dispatchSizeX = threadGroupCountVoxels;
            int dispatchSizeY = 1;
            // Dispatch size in any dimension can't exceed kMaxThreadGroupCount, so when we're above that limit
            // start dispatching groups in two dimensions.
            if (threadGroupCountVoxels > _kMaxThreadGroupCount)
            {
                // Make it roughly square-ish as a heuristic to avoid too many unused at the end
                dispatchSizeX = Mathf.CeilToInt(Mathf.Sqrt(threadGroupCountVoxels));
                dispatchSizeY = Mathf.CeilToInt((float)threadGroupCountVoxels / dispatchSizeX);
            }

            if (_SDFBuffer == null) CreateComputeBuffer(ref _SDFBuffer, voxelCount, sizeof(float));
            if (_SDFBufferBis == null) CreateComputeBuffer(ref _SDFBufferBis, voxelCount, sizeof(float));
            if (_FloodMode == FloodMode.Jump)
            {
                CreateComputeBuffer(ref _JumpBuffer, voxelCount, sizeof(int));
                CreateComputeBuffer(ref _JumpBufferBis, voxelCount, sizeof(int));
            }
            else
            {
                ReleaseComputeBuffer(ref _JumpBuffer);
                ReleaseComputeBuffer(ref _JumpBufferBis);
            }

            Init();

            var gpuSkinning = this.GetComponent<GPUSkinning>().ExistsAndEnabled(out MonoBehaviour behaviour) || this.GetComponent<GPUMesh>().ExistsAndEnabled(out behaviour);
            //Debug.Log("gpuSkinning: " + gpuSkinning);

            if (!gpuSkinning)
            {
                if (!LoadMeshToComputeBuffers())
                {
                    ReleaseGraphicsBuffer(ref _VertexBuffer);
                    ReleaseGraphicsBuffer(ref _IndexBuffer);
                    return;
                }
            }

            cmd.SetComputeIntParam(_Compute, Uniforms._DispatchSizeX, dispatchSizeX);
            cmd.SetComputeVectorParam(_Compute, Uniforms.g_Origin, voxelBounds.center - voxelBounds.extents);
            cmd.SetComputeFloatParam(_Compute, Uniforms.g_CellSize, voxelSize);
            cmd.SetComputeIntParam(_Compute, Uniforms.g_NumCellsX, voxelResolution.x);
            cmd.SetComputeIntParam(_Compute, Uniforms.g_NumCellsY, voxelResolution.y);
            cmd.SetComputeIntParam(_Compute, Uniforms.g_NumCellsZ, voxelResolution.z);
            int[] voxelResolutionArray = { voxelResolution.x, voxelResolution.y, voxelResolution.z, voxelCount };
            cmd.SetComputeIntParams(_Compute, Uniforms._VoxelResolution, voxelResolutionArray);
            float maxDistance = voxelBounds.size.magnitude;
            cmd.SetComputeFloatParam(_Compute, Uniforms._MaxDistance, maxDistance);
            cmd.SetComputeFloatParam(_Compute, Uniforms.INITIAL_DISTANCE, maxDistance * 1.01f);
            cmd.SetComputeMatrixParam(_Compute, Uniforms._WorldToLocal, GetMeshToSDFMatrix());

            // Last FloodStep should finish writing into _SDFBufferBis, so that we always end up
            // writing to _SDFBuffer in FinalizeFlood
            ComputeBuffer bufferPing = _SDFBufferBis;
            ComputeBuffer bufferPong = _SDFBuffer;
            if (_FloodFillIterations % 2 == 0 && _FloodMode == FloodMode.Linear)
            {
                bufferPing = _SDFBuffer;
                bufferPong = _SDFBufferBis;
            }

            cmd.BeginSample(Labels.Initialize);
            int kernel = _InitializeKernel;
            cmd.SetComputeBufferParam(_Compute, kernel, Uniforms.g_SignedDistanceField, bufferPing);
            cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
            cmd.EndSample(Labels.Initialize);

            cmd.BeginSample(Labels.SplatTriangleDistances);
            kernel = _DistanceMode == DistanceMode.Signed && _FloodMode == FloodMode.Linear ? (gpuSkinning ? _SplatTriangleDistancesSignedSkinningKernel : _SplatTriangleDistancesSignedKernel) : (gpuSkinning ? _SplatTriangleDistancesUnsignedSkinningKernel : _SplatTriangleDistancesUnsignedKernel);

            if (gpuSkinning)
            {
                var meshVertsOut = behaviour.GetType() == typeof(GPUSkinning) ? (behaviour as GPUSkinning)._meshVertsOut : (behaviour as GPUMesh)._meshVertsOut;
                if (meshVertsOut != null)
                {
                    if (_dynamics == null) _dynamics = FindObjectOfType<GPUClothDynamicsV2>();

                    bool morph = false;
                    if (this.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                        morph = true;

                    kernel = morph ? (_DistanceMode == DistanceMode.Signed && _FloodMode == FloodMode.Linear ? _SplatTriangleDistancesSignedSkinningMorphKernel : _SplatTriangleDistancesUnsignedSkinningMorphKernel) : kernel;

                    int i = Array.IndexOf(_dynamics._collisionMeshes._meshObjects, this.transform);

                    int vertexCount = _dynamics._collisionMeshes._vertexCounts[i];
                    //int length = vertexCount * 1;// innerVertexCount;
                    //cmd.SetComputeIntParam(_Compute, "_meshTrisLength", length);
                    cmd.SetComputeIntParam(_Compute, "_vertexCount", vertexCount);
                    cmd.SetComputeIntParam(_Compute, "_lastParticleSum", _dynamics._collisionMeshes._lastParticleSums[i]);

                    if (morph)
                    {
                        var blendShapes = (GPUBlendShapes)monoMorph;
                        if (blendShapes._rtArrayCombined != null)
                        {
                            cmd.SetComputeIntParam(_Compute, "_rtArrayWidth", blendShapes._rtArrayCombined.width);
                            cmd.SetComputeTextureParam(_Compute, kernel, "_rtArray", blendShapes._rtArrayCombined);
                        }
                    }

                    cmd.SetComputeBufferParam(_Compute, kernel, "_meshVertsOut", meshVertsOut);
                    cmd.SetComputeBufferParam(_Compute, kernel, "_trisData", _dynamics._collisionMeshes._trisDataBuffer);

                    int triangleCount = _dynamics._collisionMeshes._trisDataBuffer.count / 3;
                    _ThreadGroupCountTriangles = (int)Mathf.Ceil((float)triangleCount / (float)_kThreadCount);
                }
            }
            else
            {
                cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._VertexBuffer, _VertexBuffer);
                cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._IndexBuffer, _IndexBuffer);
                cmd.SetComputeIntParam(_Compute, Uniforms._IndexFormat16bit, _IndexFormat == IndexFormat.UInt16 ? 1 : 0);
                cmd.SetComputeIntParam(_Compute, Uniforms._VertexBufferStride, _VertexBufferStride);
                cmd.SetComputeIntParam(_Compute, Uniforms._VertexBufferPosAttributeOffset, _VertexBufferPosAttributeOffset);

                int triangleCount = _IndexBuffer.count / 3;
                _ThreadGroupCountTriangles = (int)Mathf.Ceil((float)triangleCount / (float)_kThreadCount);
            }

            cmd.SetComputeBufferParam(_Compute, kernel, Uniforms.g_SignedDistanceField, bufferPing);
            cmd.DispatchCompute(_Compute, kernel, _ThreadGroupCountTriangles, 1, 1);
            cmd.EndSample(Labels.SplatTriangleDistances);

            cmd.BeginSample(Labels.Finalize);
            kernel = _FinalizeKernel;
            cmd.SetComputeBufferParam(_Compute, kernel, Uniforms.g_SignedDistanceField, bufferPing);
            cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
            cmd.EndSample(Labels.Finalize);

            if (_FloodMode == FloodMode.Linear)
            {
                cmd.BeginSample(Labels.LinearFloodStep);
                kernel = _FloodFillQuality == FloodFillQuality.Normal ? _LinearFloodStepKernel : _LinearFloodStepUltraQualityKernel;
                for (int i = 0; i < _FloodFillIterations; i++)
                {
                    cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._SDFBuffer, i % 2 == 0 ? bufferPing : bufferPong);
                    cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._SDFBufferRW, i % 2 == 0 ? bufferPong : bufferPing);
                    cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
                }
                cmd.EndSample(Labels.LinearFloodStep);
            }
            else
            {
                cmd.BeginSample(Labels.JumpFloodInitialize);
                kernel = _JumpFloodInitialize;
                cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._SDFBuffer, bufferPing);
                cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._JumpBufferRW, _JumpBuffer);
                cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
                cmd.EndSample(Labels.JumpFloodInitialize);

                int maxDim = Mathf.Max(Mathf.Max(voxelResolution.x, voxelResolution.y), voxelResolution.z);
                int jumpFloodStepCount = Mathf.FloorToInt(Mathf.Log(maxDim, 2)) - 1;

                cmd.BeginSample(Labels.JumpFloodStep);
                bool bufferFlip = true;
                int[] jumpOffsetInterleaved = new int[3];
                for (int i = 0; i < jumpFloodStepCount; i++)
                {
                    int jumpOffset = Mathf.FloorToInt(Mathf.Pow(2, jumpFloodStepCount - 1 - i) + 0.5f);
                    if (_FloodFillQuality == FloodFillQuality.Normal)
                    {
                        kernel = _JumpFloodStep;
                        for (int j = 0; j < 3; j++)
                        {
                            jumpOffsetInterleaved[j] = jumpOffset;
                            jumpOffsetInterleaved[(j + 1) % 3] = jumpOffsetInterleaved[(j + 2) % 3] = 0;
                            cmd.SetComputeIntParams(_Compute, Uniforms._JumpOffsetInterleaved, jumpOffsetInterleaved);
                            cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._JumpBuffer, bufferFlip ? _JumpBuffer : _JumpBufferBis);
                            cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._JumpBufferRW, bufferFlip ? _JumpBufferBis : _JumpBuffer);
                            cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
                            bufferFlip = !bufferFlip;
                        }
                    }
                    else
                    {
                        kernel = _JumpFloodStepUltraQuality;
                        cmd.SetComputeIntParam(_Compute, Uniforms._JumpOffset, jumpOffset);
                        cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._JumpBuffer, bufferFlip ? _JumpBuffer : _JumpBufferBis);
                        cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._JumpBufferRW, bufferFlip ? _JumpBufferBis : _JumpBuffer);
                        cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
                        bufferFlip = !bufferFlip;
                    }
                }
                cmd.EndSample(Labels.JumpFloodStep);

                cmd.BeginSample(Labels.JumpFloodFinalize);
                kernel = _JumpFloodFinalize;
                cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._JumpBuffer, bufferFlip ? _JumpBuffer : _JumpBufferBis);
                cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._SDFBuffer, _SDFBufferBis);
                cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._SDFBufferRW, _SDFBuffer);
                cmd.SetComputeFloatParam(_Compute, Uniforms.g_CellSize, voxelSize);
                cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
                cmd.EndSample(Labels.JumpFloodFinalize);
            }

            cmd.BeginSample(Labels.BufferToTexture);
            kernel = _BufferToTextureCalcGradient;
            cmd.SetComputeBufferParam(_Compute, kernel, Uniforms._SDFBuffer, _SDFBuffer);
            cmd.SetComputeTextureParam(_Compute, kernel, Uniforms._SDF, _SDFTexture.sdf);
            cmd.SetComputeFloatParam(_Compute, Uniforms._Offset, _DistanceMode == DistanceMode.Signed && _FloodMode != FloodMode.Jump ? _Offset : 0);
            cmd.DispatchCompute(_Compute, kernel, dispatchSizeX, dispatchSizeY, 1);
            cmd.EndSample(Labels.BufferToTexture);
        }

        bool LoadMeshToComputeBuffers()
        {
            Mesh mesh = null;

            if (_SkinnedMeshRenderer != null)
            {
                mesh = _SkinnedMeshRenderer.sharedMesh;
                if (mesh == null)
                    return false;
                _SkinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            }
            else if (_MeshFilter != null)
            {
                mesh = _MeshFilter.sharedMesh;
                if (mesh == null)
                    return false;
                mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            }
            else
                return false;

            if (mesh.GetTopology(0) != MeshTopology.Triangles)
            {
                Debug.LogError("MeshToSDF needs a mesh with triangle topology.", this);
                return false;
            }

            int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
            if (stream < 0)
            {
                Debug.LogError("MeshToSDF: no vertex positions in mesh '" + mesh.name + "', aborting.", this);
                return false;
            }

            _IndexFormat = mesh.indexFormat;
            _VertexBufferStride = mesh.GetVertexBufferStride(stream);
            _VertexBufferPosAttributeOffset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            _VertexBuffer = _SkinnedMeshRenderer != null ? _SkinnedMeshRenderer.GetVertexBuffer() : mesh.GetVertexBuffer(stream);

            mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            _IndexBuffer = mesh.GetIndexBuffer();

            return _VertexBuffer != null && _IndexBuffer != null;
        }

        Matrix4x4 GetMeshToSDFMatrix()
        {
            Matrix4x4 meshToWorld;

            if (_SkinnedMeshRenderer != null)
            {
                if (_SkinnedMeshRenderer.rootBone != null)
                    meshToWorld = _SkinnedMeshRenderer.rootBone.localToWorldMatrix * Matrix4x4.Scale(_SkinnedMeshRenderer.rootBone.lossyScale).inverse;
                else
                    meshToWorld = transform.localToWorldMatrix * Matrix4x4.Scale(transform.lossyScale).inverse;
            }
            else // static mesh
                meshToWorld = transform.localToWorldMatrix;

            return _SDFTexture.transform.worldToLocalMatrix * meshToWorld;
        }

        static void CreateComputeBuffer(ref ComputeBuffer cb, int length, int stride)
        {
            if (cb != null && cb.count == length && cb.stride == stride)
                return;

            ReleaseComputeBuffer(ref cb);
            cb = new ComputeBuffer(length, stride);
        }

        static void ReleaseComputeBuffer(ref ComputeBuffer buffer)
        {
            if (buffer != null)
                buffer.Release();
            buffer = null;
        }

        static public void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer != null)
                buffer.Release();
            buffer = null;
        }

#if UNITY_EDITOR
        void OnBeforeAssemblyReload() => OnDestroy();
#endif

        void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
#if USING_HDRP || USING_URP
        RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
#else
            Camera.onPreRender += OnPreRenderCamera;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
#if USING_HDRP || USING_URP
        RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
#else
            Camera.onPreRender -= OnPreRenderCamera;
#endif
        }

        void OnDestroy()
        {
            ReleaseComputeBuffer(ref _SDFBuffer);
            ReleaseComputeBuffer(ref _SDFBufferBis);
            ReleaseComputeBuffer(ref _JumpBuffer);
            ReleaseComputeBuffer(ref _JumpBufferBis);
            ReleaseGraphicsBuffer(ref _VertexBuffer);
            ReleaseGraphicsBuffer(ref _IndexBuffer);
            if (_CommandBuffer != null)
                _CommandBuffer.Release();
        }
    }
}