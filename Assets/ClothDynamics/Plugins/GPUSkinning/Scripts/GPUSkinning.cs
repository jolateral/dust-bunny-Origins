using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace ClothDynamics
{
    //[RequireComponent(typeof(SkinnedMeshRenderer))]
    [DefaultExecutionOrder(15050)] //When using Final IK
    public class GPUSkinning : GPUSkinnerBase
    {
        [Tooltip("This is a simple frustum culling based on the bounding box of the mesh to stop skinning for offscreen objects.")]
        [SerializeField] private bool _useFrustumClipping = true;
        [Tooltip("This adds an offset to the culling frustum.")]
        [SerializeField] private float _camCullDist = 1.0f;
        [Tooltip("This will use the light's direction to keep your mesh animation visible if you only see the shadow in the camera view. Currently only works with directional lights properly.")]
        [SerializeField] public bool _useShadowCulling = true;
        [Tooltip("Here you can add directional lights for shadow culling. It will automatically search for a directional light if none is selected.")]
        [SerializeField] private Light[] _cullingLights;
        [Tooltip("This scales the bounding box like a cube if the value is higher than zero. The cube size uses the max length of all sides compared.")]
        [SerializeField] private float _scaleBoundingBox = 0;
        [Tooltip("This will recalculate the normals of the mesh.")]
        [SerializeField] private bool _recalculateNormals = false;
        [Tooltip("This is the camera for the frustum culling, it will be automatically selected, if it's set to none.")]
        [SerializeField] private Camera _cam;
        [Tooltip("This shows the vertices and normals during runtime.")]
        [SerializeField] private bool _debugDraw = false;
        //[Range(2, 8)]
        //[Tooltip("This set the uv id that is needed for the SRP setup. If your mesh needs the uv2 use a higher number. If you change this you need to change it in the Shader Graph too!")]
        //[SerializeField] private int _vertexIdsToUvId = 2;

#if UNITY_2021_2_OR_NEWER
        //[Header("UNITY 2021.2 or higher")]
        //[Tooltip("Make sure your mesh has the same local rotation as the RootBone parent has (in SkinnedMeshRender)! You might have to change the RootBone Object or the rotation values, also local position can affect this. Use \"Debug Draw\" to see if it fits! This is a beta feature an might not work with all models!")]
        //[SerializeField] 
        [HideInInspector]
        public bool _useTransferData = false;
        private Transform _rootBone = null;
#endif

        internal ComputeBuffer _meshVertsOut;
        private ComputeBuffer _sourceVBO;
        private ComputeBuffer _sourceSkin;
        private ComputeBuffer _mBonesBuffer;
        private ComputeShader _cs;
        //internal Shader _shader;
        private NativeArray<Matrix4x4> _nativeMatrices;
        private NativeArray<Matrix4x4> _nativeBindposes;
        private TransformAccessArray _transBones;
        public Transform[] _bones;
        private int _vertCount;
        internal int _propID = Shader.PropertyToID("_VertIn");
        internal MaterialPropertyBlock _mpb;

        //private static int _id; // this only works with objects of the same Camera -> else _diffCams = true
        private Plane[] _planes;
        private WaitForSeconds _waitForSeconds = new WaitForSeconds(1);
        internal bool _init = false;
        private GPUClothDynamics _clothLink;
        private Mesh _mesh;

        struct SVertInVBO
        {
            public Vector3 pos;
            public Vector3 norm;
            public Vector4 tang;
        }

        struct SVertInSkin
        {
            public float weight0, weight1, weight2, weight3;
            public int index0, index1, index2, index3;
        }

        internal struct SVertOut
        {
            internal Vector3 pos;
            internal Vector3 norm;
            internal Vector4 tang;
        }

        // Use this for initialization
        public void OnEnable()
        {
            _useTransferData = false;
            //_id++;
            Initialize();

#if UNITY_2021_2_OR_NEWER
            if (smr != null && _rootBone == null)
                _rootBone = smr.rootBone;
#endif
        }

        private void OnDisable()
        {
            new[] { _sourceVBO, _sourceSkin, _meshVertsOut, _mBonesBuffer }.ToList().ForEach(b => b.ClearBuffer());
            if (_nativeMatrices.IsCreated) _nativeMatrices.Dispose();
            if (_nativeBindposes.IsCreated) _nativeBindposes.Dispose();
            if (_transBones.isCreated) _transBones.Dispose();
#if UNITY_2021_2_OR_NEWER
            if (_vertexBuffer != null) _vertexBuffer.Dispose();
#endif
            _init = false;
        }

        enum UpdateType
        {
            FixedUpdate = 0,
            Update = 1,
            LateUpdate = 2
        }
        [SerializeField] private UpdateType _updateType = UpdateType.FixedUpdate;


        internal override void UpdateSync()
        {
            if (_updateSync) SkinUpdate();
        }

        private void FixedUpdate()
        {
            if (_updateType == UpdateType.FixedUpdate)
                if (!_updateSync) SkinUpdate();
        }

        private void Update()
        {
            if (_updateType == UpdateType.Update)
                if (!_updateSync) SkinUpdate();
        }

        private void LateUpdate()
        {
            if (_updateType == UpdateType.LateUpdate)
                if (!_updateSync) SkinUpdate();
        }


        private void SkinUpdate()
        {
            var foundInFrustum = false;

            if (_useFrustumClipping)
            {
                if (_clothLink)
                {
                    foundInFrustum = _clothLink._foundInFrustum;
                }
                else if (this != null)
                {
                    //if (_id == 1 || _diffCams || _planes == null)
                    _planes = GeometryUtility.CalculateFrustumPlanes(this._cam);
#if UNITY_2021_2_OR_NEWER
                    var mesh = _mesh != null ? _mesh : _useTransferData ? this.smr.sharedMesh : this.mf.mesh;// GetComponent<MeshFilter>() != null ? GetComponent<MeshFilter>().sharedMesh : GetComponent<SkinnedMeshRenderer>() != null ? GetComponent<SkinnedMeshRenderer>().sharedMesh : null;
#else
					var mesh = _mesh != null ? _mesh : this.mf.mesh;
#endif
                    var frustumBounds = new Bounds(this.transform.TransformPoint(mesh.bounds.center), this.transform.localRotation * mesh.bounds.size); //this.transform.TransformVector(mesh.bounds.size));// 
                    float3 extents = frustumBounds.extents;
                    int inside = 0;
                    for (int p = 0; p < 4; ++p)
                    {
                        float d = _planes[p].distance + _camCullDist + math.dot(frustumBounds.center, _planes[p].normal);
                        float r = math.dot(extents, math.abs(_planes[p].normal));
                        inside += (int)math.sign(d + r);
                    }

                    foundInFrustum = false;

                    float sphereRadius = frustumBounds.size.magnitude;// * 0.5f;
                    foreach (var light in _cullingLights)
                    {
                        if (_useShadowCulling && light != null && GPUClothDynamics.TestSweptSphere(frustumBounds.center, sphereRadius, light.transform.forward, _planes))
                        {
                            foundInFrustum = true;
                        }
                    }

                    if (inside >= 4)
                        foundInFrustum = true;

                    //foundInFrustum = GeometryUtility.TestPlanesAABB(_planes, frustumBounds);
                }

            }
            else { foundInFrustum = true; }

            if (foundInFrustum)
            {
#if UNITY_2021_2_OR_NEWER
                if (_useTransferData)
                {
                    TransferData();
                }
                else
                {
#endif
                    SetBoneMatrices();
                    ComputeSkinning();
#if UNITY_2021_2_OR_NEWER
                }
#endif
            }
        }

#if UNITY_2021_2_OR_NEWER
        private Vector4 QuatToVec(Quaternion rot)
        {
            Vector4 rotVec;
            rotVec.x = rot.x;
            rotVec.y = rot.y;
            rotVec.z = rot.z;
            rotVec.w = rot.w;
            return rotVec;
        }

        private GraphicsBuffer _vertexBuffer;
        private int _vertexStride;

        public virtual void TransferData()
        {
            var skin = this.smr;
            if (skin != null)
            {
                skin.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
                if (_vertexBuffer == null)
                {
                    _vertexBuffer = skin.GetVertexBuffer();
                    _vertexStride = skin.sharedMesh.GetVertexBufferStride(0);
                }
                if (_vertexBuffer != null && _vertexBuffer.IsValid())
                {
                    var kernel = 1;
                    _cs.SetInt("g_VertCount", _vertCount);
                    _cs.SetInt("g_VertStride", _vertexStride);
                    _cs.SetVector("g_RootRot", QuatToVec(_rootBone.localRotation));
                    _cs.SetVector("g_RootPos", _rootBone.localPosition);
                    _cs.SetBuffer(kernel, "g_VertexData", _vertexBuffer);
                    _cs.SetBuffer(kernel, "g_MeshVertsOut", _meshVertsOut);
                    _cs.Dispatch(kernel, _vertCount.GetComputeShaderThreads(64), 1, 1);
                }
            }
        }
#endif

        [BurstCompile]
        public struct CopyMatrixJob : IJobParallelForTransform
        {
            [ReadOnly] public Matrix4x4 objMatrix;
            [ReadOnly] public NativeArray<Matrix4x4> bindposes;
            [WriteOnly] public NativeArray<Matrix4x4> targets;
            public void Execute(int index, TransformAccess t)
            {
                this.targets[index] = objMatrix * t.localToWorldMatrix * bindposes[index];
            }
        }

        void SetBoneMatrices()
        {
            var job = new CopyMatrixJob
            {
                objMatrix = this.transform.worldToLocalMatrix,
                bindposes = this._nativeBindposes,
                targets = this._nativeMatrices
            };
            if (this._transBones.isCreated)
            {
                JobHandle handle = job.Schedule(this._transBones);
                handle.Complete();
            }
            if (this._nativeMatrices.IsCreated)
            {
                this._mBonesBuffer.SetData(this._nativeMatrices);
            }
            //for (var i = 0; i < boneMatrices.Length; i++)
            //    boneMatrices[i] = transform.worldToLocalMatrix * bones[i].localToWorldMatrix * mesh.bindposes[i];
        }

        void ComputeSkinning()
        {
            if (_mBonesBuffer == null) { this.enabled = false; return; }
            var kernel = 0;
            _cs.SetInt("g_VertCount", _vertCount);
            _cs.SetBuffer(kernel, "g_SourceVBO", _sourceVBO);
            _cs.SetBuffer(kernel, "g_SourceSkin", _sourceSkin);
            _cs.SetBuffer(kernel, "g_MeshVertsOut", _meshVertsOut);
            _cs.SetBuffer(kernel, "g_mBones", _mBonesBuffer);
            _cs.Dispatch(kernel, _vertCount.GetComputeShaderThreads(64), 1, 1);
        }

        internal List<int> _vertexMap = null;

        public void Initialize(bool renderSetup = true)
        {
            if (_init) return;
            //if (this.GetComponent<SkinnedMeshRenderer>() == null) { this.enabled = false; return; }

            var foundLight = FindObjectsOfType<Light>().Where(x => x.type == LightType.Directional).FirstOrDefault();
            if (foundLight != null && (_cullingLights == null || _cullingLights.Length < 1)) _cullingLights = new Light[] { foundLight };

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) _cam = FindObjectOfType<Camera>();

            //var skins = FindObjectsOfType<GPUSkinning>();
            //if (skins.Any(x => x._cam != _cam)) _diffCams = true;

            if (this._cs == null) this._cs = Resources.Load("Compute/skinning") as ComputeShader;

            Mesh mesh = this.smr != null ? this.smr.sharedMesh : this.mf.sharedMesh;

            if (this.smr != null)
            {
                _bones = smr.bones;
            }

            var weights = (this.GetComponent<GPUBlendShapes>() != null && this.GetComponent<GPUBlendShapes>()._weights != null) ? this.GetComponent<GPUBlendShapes>()._weights : mesh.boneWeights;
            if (weights.Length < 1) { Debug.LogError("SkinnedMeshRenderer has a wrong mesh!"); this.enabled = false; return; }

            var cloth = this.GetComponent<GPUClothDynamics>();
            var cloth2 = this.GetComponent<ClothObjectGPU>();

            _vertexMap = (this.GetComponent<GPUBlendShapes>() != null && this.GetComponent<GPUBlendShapes>()._vertexMap != null) ? this.GetComponent<GPUBlendShapes>()._vertexMap : null;
            //print("_vertexMap " + _vertexMap.Count);
            if (_vertexMap == null && (cloth != null || cloth2 != null))
            {
                if ((cloth != null && cloth._weldVertices && !cloth._weldDone) || (cloth2 != null && cloth2._weldVertices && !cloth2._weldDone))
                {
                    mesh = Instantiate(mesh);
                    if (this.smr != null) this.smr.sharedMesh = mesh;
                    else this.mf.sharedMesh = mesh;
                    GPUClothDynamics.WeldVerticesOld(mesh, out _vertexMap);
                    if (cloth != null) cloth._weldDone = true;
                    if (cloth2 != null) cloth2._weldDone = true;
                }
            }

            //if (_vertexMap != null) mesh = this.GetComponent<GPUBlendShapes>()._mesh;

            if (_recalculateNormals) mesh.RecalculateNormals();

            _mesh = mesh;

            _vertCount = mesh.vertexCount;
            _meshVertsOut = new ComputeBuffer(_vertCount, Marshal.SizeOf(typeof(SVertOut)));

            _clothLink = this.GetComponent<GPUClothDynamics>();
            if (_clothLink)
            {
                if (_scaleBoundingBox < _clothLink._scaleBoundingBox)
                    _scaleBoundingBox = _clothLink._scaleBoundingBox;
                else
                    _clothLink._scaleBoundingBox = _scaleBoundingBox;
            }

#if UNITY_2021_2_OR_NEWER
            if (!_useTransferData)
            {
#endif
                SetShader();

                SVertInVBO[] inVBO = new SVertInVBO[_vertCount];
                var verts = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
                for (int i = 0; i < inVBO.Length; i++)
                {
                    inVBO[i].pos = verts[i];
                    if (i < normals.Length)
                        inVBO[i].norm = normals[i];
                    if (i < tangents.Length) inVBO[i].tang = tangents[i];
                }

                _sourceVBO = new ComputeBuffer(_vertCount, Marshal.SizeOf(typeof(SVertInVBO)));
                _sourceVBO.SetData(inVBO);

                //print("weights.Length " + weights.Length + " verts " + mesh.vertexCount + " vertexMap "+ (vertexMap!=null));

                bool copyWeights = false;
                SVertInSkin[] inSkin = new SVertInSkin[1];
                if (_vertexMap != null || Marshal.SizeOf(typeof(BoneWeight)) != Marshal.SizeOf(typeof(SVertInSkin)))
                {
                    //Debug.Log("Copy Weights! Looks like Unity's BoneWeight Struct has changed!");
                    copyWeights = true;
                    if (_vertexMap == null)
                    {
                        _vertexMap = new List<int>();
                        for (int i = 0; i < mesh.vertexCount; i++) _vertexMap.Add(i);
                    }
                    inSkin = new SVertInSkin[mesh.vertexCount];
                    for (int i = 0; i < inSkin.Length; i++)
                    {
                        var w = weights[_vertexMap[i]];
                        inSkin[i].weight0 = w.weight0;
                        inSkin[i].weight1 = w.weight1;
                        inSkin[i].weight2 = w.weight2;
                        inSkin[i].weight3 = w.weight3;
                        inSkin[i].index0 = w.boneIndex0;
                        inSkin[i].index1 = w.boneIndex1;
                        inSkin[i].index2 = w.boneIndex2;
                        inSkin[i].index3 = w.boneIndex3;
                    }
                }

                _sourceSkin = new ComputeBuffer(mesh.vertexCount, Marshal.SizeOf(typeof(SVertInSkin)));
                _sourceSkin.SetData(copyWeights ? inSkin : weights);

                if (_bones != null && _bones.Length > 0)
                {
                    _mBonesBuffer = new ComputeBuffer(_bones.Length, Marshal.SizeOf(typeof(Matrix4x4)));
                    //boneMatrices = bones.Select((b, idx) => transform.worldToLocalMatrix * b.localToWorldMatrix * mesh.bindposes[idx]).ToArray();
                    _transBones = new TransformAccessArray(_bones);
                    var poseMatrices = new Matrix4x4[_mesh.bindposes.Length];
                    _nativeMatrices = new NativeArray<Matrix4x4>(poseMatrices, Allocator.Persistent);
                    _nativeBindposes = new NativeArray<Matrix4x4>(_mesh.bindposes, Allocator.Persistent);
                }

                if (renderSetup) StartCoroutine(DelayRenderSetup());

                if (_scaleBoundingBox > 0)
                {
                    _mesh.RecalculateBounds();
                    var b = _mesh.bounds;
                    var maxSize = math.max(b.size.x, math.max(b.size.y, b.size.z));
                    b.size = Vector3.one * maxSize * _scaleBoundingBox;
                    //if (_updateTransformCenter) b.center -= this.GetComponent<MeshRenderer>().bounds.center - this.transform.position;
                    this.mf.mesh.bounds = b;
                }
#if UNITY_2021_2_OR_NEWER
            }
#endif
            _init = true;
        }

        internal void SetShader(string addon = "", bool force = false)
        {
            if (!force && this.GetComponent<GPUClothDynamics>() != null) return; //Don't set the shader if this a cloth object

            var cacheShader = this._shader;
            if (GraphicsSettings.currentRenderPipeline)
            {
                if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                {
                    //Debug.Log("HDRP active");
                    if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/Lit_Graph_GPUSkinning" + addon, typeof(Shader)) as Shader; //Shader.Find("Shader Graphs/Lit_Graph_GPUSkinning") as Shader;
                }
                else // assuming here we only have HDRP or URP options here
                {
                    //Debug.Log("URP active");
#if UNITY_2020_1_OR_NEWER
                    if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/URP_Lit_Graph_GPUSkinning" + addon, typeof(Shader)) as Shader;
#else
					if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/URP_GPUSkinning_2019" + addon, typeof(Shader)) as Shader;
#endif
                }
            }
            else
            {
                //Debug.Log("Built-in RP active");
                if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/GPUSkinning" + addon) as Shader;
            }

            if (!string.IsNullOrEmpty(addon) && this._render)
            {
                Material[] materials = this.GetComponent<Renderer>().materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i].shader = cacheShader.name.ToLower().Contains("skinning") ? materials[i].shader : this._shader;
                    materials[i].SetShaderPassEnabled("MotionVectors", true);
                    if (cacheShader != _shader) materials[i].SetFloat("_EmissiveIntensity", 0);
#if UNITY_2021_2_OR_NEWER
                    if (!_useTransferData)
#endif
                        materials[i].EnableKeyword("USE_BUFFERS");
                    materials[i].SetFloat("_AlphaCutoffEnable", 1);
                    //StartCoroutine(DelaySetCutoff(materials[i]));
                }
                this.GetComponent<Renderer>().materials = materials;
            }
        }

        //IEnumerator DelaySetCutoff(Material mat)
        //{
        //    for (int i = 0; i < 120; i++) yield return null;
        //    mat.SetFloat("_AlphaCutoffEnable", 1);
        //}

        private IEnumerator DelayRenderSetup()
        {
            yield return null;// _waitForSeconds; //TODO needs testing
            if (_render)
            {
                if (this.GetComponent<SkinnedMeshRenderer>()) this.GetComponent<SkinnedMeshRenderer>().enabled = false;
                //if (this.smr)
                //    this.mf.sharedMesh = this.smr.sharedMesh;
                var mf = this.GetComponent<MeshFilter>();
                if (mf == null) mf = this.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = _mesh != null ? _mesh : this.GetComponent<SkinnedMeshRenderer>().sharedMesh;

                Material[] materials = this.smr ? this.smr.materials : this.mr.materials;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i].shader.name.ToLower().Contains("skinning") && this.GetComponent<GPUClothDynamics>() == null && this.GetComponent<ClothObjectGPU>() == null)
                        materials[i].shader = this._shader;
                    materials[i].SetShaderPassEnabled("MotionVectors", true);
                    materials[i].EnableKeyword("USE_BUFFERS");
                }
                if (this.GetComponent<SkinnedMeshRenderer>())
#if UNITY_2021_2_OR_NEWER
                    if (!_useTransferData)
#endif
                        DestroyImmediate(this.GetComponent<SkinnedMeshRenderer>());

                mf.sharedMesh.MarkDynamic();
                //mf.sharedMesh.RecalculateBounds();

                this.mr.materials = materials;
                _mpb = new MaterialPropertyBlock();
                this.mr.GetPropertyBlock(_mpb);
                _mpb.SetBuffer(_propID, _meshVertsOut);
                this.mr.SetPropertyBlock(_mpb);

                //if (GraphicsSettings.currentRenderPipeline)
                //{
                //    var uv2 = new Vector2[this.mf.mesh.vertexCount];
                //    for (int i = 0; i < uv2.Length; i++)
                //    {
                //        uv2[i] = new Vector2(i, 0);
                //    }
                //    switch (this._vertexIdsToUvId)
                //    {
                //        case 2:
                //            this.mf.mesh.uv2 = uv2;
                //            break;
                //        case 3:
                //            this.mf.mesh.uv3 = uv2;
                //            break;
                //        case 4:
                //            this.mf.mesh.uv4 = uv2;
                //            break;
                //        case 5:
                //            this.mf.mesh.uv5 = uv2;
                //            break;
                //        case 6:
                //            this.mf.mesh.uv6 = uv2;
                //            break;
                //        case 7:
                //            this.mf.mesh.uv7 = uv2;
                //            break;
                //        default:
                //            this.mf.mesh.uv8 = uv2;
                //            break;
                //    }
                //}
            }
        }

        void OnDrawGizmos()
        {
            if (_debugDraw && _meshVertsOut != null)
            {
                SVertOut[] data = new SVertOut[_meshVertsOut.count];
                _meshVertsOut.GetData(data);
                for (int i = 0; i < data.Length; i++)
                {
                    var pos = this.transform.TransformPoint(data[i].pos);
                    Gizmos.DrawLine(pos, pos + this.transform.TransformVector(data[i].norm) * 0.025f);
                }

                Gizmos.color = Color.red;
                var sMesh = GetComponent<MeshFilter>() != null ? GetComponent<MeshFilter>().sharedMesh : GetComponent<SkinnedMeshRenderer>() != null ? GetComponent<SkinnedMeshRenderer>().sharedMesh : null;
                if (sMesh)
                {
                    Gizmos.DrawWireCube(transform.TransformPoint(sMesh.bounds.center), sMesh.bounds.size);
                }
            }
        }

        MeshFilter mf
        {
            get
            {
                if (this._mf == null && this != null)
                {
                    this._mf = this.GetComponent<MeshFilter>();
                    if (this._mf == null)
                    {
                        this._mf = this.gameObject.AddComponent<MeshFilter>();
                        //this._mf.sharedMesh = this.smr.sharedMesh;
                    }

                }
                if (this == null) return null;
                return this._mf;
            }
            set
            {
                this._mf = value;
            }
        }
        MeshFilter _mf;

        MeshRenderer mr
        {
            get
            {
                if (this._mr == null && this != null)
                {
                    this._mr = this.GetComponent<MeshRenderer>();
                    if (this._mr == null)
                    {
                        this._mr = this.gameObject.AddComponent<MeshRenderer>();
                    }
                }
                if (this == null) return null;
                return this._mr;
            }
            set
            {
                this._mr = value;
            }
        }
        MeshRenderer _mr;

        SkinnedMeshRenderer smr
        {
            get
            {
                if (this._smr == null && this != null)
                {
                    this._smr = this.GetComponent<SkinnedMeshRenderer>();
                }
                if (this == null) return null;
                return this._smr;
            }
        }
        SkinnedMeshRenderer _smr;
    }
}