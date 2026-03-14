using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Linq;
//using System.IO;
using System;
using Unity.Burst;
using Unity.Jobs;
//using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClothDynamics
{
    [System.Serializable]
    public class GPUNeighbourFinder
    {
        #region Global Properties

        [Tooltip("The size of the voxel grid for each side -> VoxelCount = GridCount ^ 3.")]
        [SerializeField] public int _gridCount = 64;
        [Tooltip("The the minimal particles collision size. Call \"mask\", because you can affect this value by the red color channel of a PaintObject and mask out the unused particles with black.")]
        [SerializeField] private float _clothMaskValue = 0.001f;
        [Tooltip("This initializes the sim with faster GPU methods instead of CPU methods.")]
        [SerializeField] private bool _initWithGPU = true;

        [Header("Debug")]
#if UNITY_EDITOR
        [ReadOnly]
#endif
        [Tooltip("The total number of used particles.")]
        [SerializeField] private int _numparticles;
        [Tooltip("This renders the debug points of the cloth sim. Turn this on before play mode starts.")]
        [SerializeField] public bool _renderDebugPoints = false;
        //[SerializeField] internal bool _debugRun = false;
        [Tooltip("This is set automatically and used to display the particles as a mesh object (use a cube or a low res sphere).")]
        [SerializeField] private Mesh _debugMesh;
        [Tooltip("This is set automatically and renders the debug mesh with this material.")]
        [SerializeField] private Material _debugMat;
        [Tooltip("This scales the DebugMesh during runtime.")]
        [SerializeField] private float _debugVertexScale = 1.0f;
        [Tooltip("This should be set to visualize the current selected voxel of a potential collision. You can move the object in the editor view to the spot of your cloth that you want to inspect. It also shows the nearest voxel, if it's inside the voxel cube.")]
        [SerializeField] private Transform _debugObject;
        [Tooltip("This is experimental and only for debugging (default = false).")]
        [SerializeField] private bool _useDirectDispatch = false;
        [Tooltip("This is an experimental feature. If you set it higher than zero, it will replace the collision sphere size of all mesh objects. (Default = 0)")]
        [SerializeField] private float _unifiedSphereSize = 0;


        #endregion // Global Properties

        #region Private Properties

        private GPUClothDynamics _clothSim;
        internal ComputeShader _clothSolver;
        internal ComputeShader _scanCS;
        internal ComputeShader _sphereFinderCS;

        private int _inputPropId = Shader.PropertyToID("_Input");
        private int _resultPropId = Shader.PropertyToID("_Result");

        //int _scanInBucketExclusiveKernel;
        private int _scanInBucketInclusiveKernel;
        private int _scanBucketResultKernel;
        private int _scanAddBucketResultKernel;
        private int _autoSpheresKernel;

        private Transform[] _meshObjects;
        private const int _voxelCubeGridSize = 4;
        internal int[] _vertexCounts;
        private MaterialPropertyBlock _matBlock;
        private Vector4[] _meshPos;
        private Bounds _b;

        private int _updateAllParticlesKernel;
        private int _updateAllParticleTrisKernel;
        private int _updateSkinnerParticlesKernel;

        private sData[] _voxelBufferData;
        private sData[] _voxelBufferData2;
        private int[] _usedVoxelList;
        private int[] _counterPerVoxel2;
        //internal int _newSpheresPerVoxel;
        //private int[] _lastUsedVoxelList;
        private GraphicsBuffer _indexBuffer;
        private ComputeBuffer _scanCountBuffer;
        private ComputeBuffer debugVertexBuffer;
        private ComputeBuffer debugNormalBuffer;
        private ComputeBuffer _sphereDataBuffer;
        private ComputeBuffer _voxelDataBuffer;
        internal ComputeBuffer _usedVoxelListBuffer; //RenderTexture
        private ComputeBuffer _counterPerVoxelBuffer;
        private ComputeBuffer _usedVoxelListInverseBuffer;
        private ComputeBuffer _usedVoxelListInverseBuffer2;
        internal ComputeBuffer _counterPerVoxelBuffer2;
        internal ComputeBuffer _voxelDataBuffer2;
        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _argsBuffer2;
        private ComputeBuffer _lastVoxelCountBuffer;
        internal ComputeBuffer _trisDataBuffer;
        internal ComputeBuffer _selfTrisDataBuffer;
        private ComputeBuffer _lastCounterPerVoxelBuffer;
        internal ComputeBuffer _lastCounterPerVoxelBuffer2;

        private int _usedVoxelListBufferDispatch;

        internal struct sData
        {
            internal float4 pr;
            internal float4 nId;
            internal float4 temp;
        };
        private sData[] _sphereData;
        internal bool _useTrisMesh = false;
        private int[] _lastCounterPerVoxel;
        private int[] _lastCounterPerVoxel2;
        private bool _useSelfCollision = false;
        private int _lastParticleSum = 0;
        private bool _useAutoSpheresOnly = false;
        private int _counterPerVoxelThread = 512;
        private int _numGroups_sphereData;
        private bool _useSecondClothIfNeeded = false;
        private int _numClothParticles2 = 0;
        private int _selfAndAutoSpheresCount = 0;

        private int _grid_ID = Shader.PropertyToID("_grid");
        private int _scaled_ID = Shader.PropertyToID("_scaled");

        private int _lastParticleSum_ID = Shader.PropertyToID("_lastParticleSum");
        private int _selfAndAutoSpheresCount_ID = Shader.PropertyToID("_selfAndAutoSpheresCount");
        private int _useTrisMesh_ID = Shader.PropertyToID("_useTrisMesh");
        private int _useSelfCollision_ID = Shader.PropertyToID("_useSelfCollision");
        private int _useSelfCollisionTriangles_ID = Shader.PropertyToID("_useSelfCollisionTriangles");

        private int _cubeMinVec_ID = Shader.PropertyToID("_cubeMinVec");
        private int _sphereDataLength_ID = Shader.PropertyToID("_sphereDataLength");
        private int _numClothParticles_ID = Shader.PropertyToID("_numClothParticles");
        private int _numClothParticles2_ID = Shader.PropertyToID("_numClothParticles2");
        private int _collisionSize_ID = Shader.PropertyToID("_collisionSize");
        private int _selfCollisionScale_ID = Shader.PropertyToID("_selfCollisionScale");
        private int _secondClothScale_ID = Shader.PropertyToID("_secondClothScale");

        private int _usedVoxelListBuffer_ID = Shader.PropertyToID("_usedVoxelListBuffer");
        private int _usedVoxelListInverseBuffer_ID = Shader.PropertyToID("_usedVoxelListInverseBuffer");
        private int _usedVoxelListInverseBufferR_ID = Shader.PropertyToID("_usedVoxelListInverseBufferR");
        private int _usedVoxelListInverseBuffer2_ID = Shader.PropertyToID("_usedVoxelListInverseBuffer2");
        private int _counterPerVoxelBuffer_ID = Shader.PropertyToID("_counterPerVoxelBuffer");
        private int _counterPerVoxelBuffer2_ID = Shader.PropertyToID("_counterPerVoxelBuffer2");

        private int _lastCounterPerVoxelBuffer_ID = Shader.PropertyToID("_lastCounterPerVoxelBuffer");
        private int _lastCounterPerVoxelBuffer2_ID = Shader.PropertyToID("_lastCounterPerVoxelBuffer2");

        private int _voxelDataBuffer_ID = Shader.PropertyToID("_voxelDataBuffer");
        private int _voxelDataBuffer2_ID = Shader.PropertyToID("_voxelDataBuffer2");

        private int _vertsLength_ID = Shader.PropertyToID("_vertsLength");
        private int _localToWorldMatrix_ID = Shader.PropertyToID("_localToWorldMatrix");
        private int _trisData_ID = Shader.PropertyToID("_trisData");
        private int _positionbuffer_ID = Shader.PropertyToID("_positionbuffer");
        private int _sphereDataBufferRW_ID = Shader.PropertyToID("_sphereDataBufferRW");

        private int _meshTrisLength_ID = Shader.PropertyToID("_meshTrisLength");
        private int _skinnerVertexTexWidth_ID = Shader.PropertyToID("_skinnerVertexTexWidth");
        private int _normalScale_ID = Shader.PropertyToID("_normalScale");
        private int _positionBufferTex_ID = Shader.PropertyToID("_positionBufferTex");
        private int _normalBufferTex_ID = Shader.PropertyToID("_normalBufferTex");

        private int _skinned_tex_width_ID = Shader.PropertyToID("_skinned_tex_width");
        private int _meshMatrix_ID = Shader.PropertyToID("_meshMatrix");
        private int _skinned_data_1_ID = Shader.PropertyToID("_skinned_data_1");
        private int _skinned_data_2_ID = Shader.PropertyToID("_skinned_data_2");

        private int _meshVertsOut_ID = Shader.PropertyToID("_meshVertsOut");

        private int _numAutoSpheres_ID = Shader.PropertyToID("_numAutoSpheres");
        private int _autoSphereSize_ID = Shader.PropertyToID("_autoSphereSize");
        private int _autoBonesBuffer_ID = Shader.PropertyToID("_autoBonesBuffer");
        private int _autoSphereBuffer_ID = Shader.PropertyToID("_autoSphereBuffer");

        private static readonly int _rtArrayID = Shader.PropertyToID("_rtArray");
        private static readonly int _rtArrayWidthID = Shader.PropertyToID("_rtArrayWidth");

        private const float epsilon = 1e-7f;

        #endregion // Private Properties

        internal void InitNeighbourFinder(GPUClothDynamics clothSim, ref Transform[] meshObjects, ref bool useTrisMesh)
        {
            //var timer = Stopwatch.StartNew();
            _clothSim = clothSim;
            _clothSolver = _clothSim._clothSolver;
            _meshObjects = meshObjects;
            _useTrisMesh = useTrisMesh;

            if (_meshObjects == null || _meshObjects.Length < 1)
            {
                _meshObjects = new Transform[] { clothSim.transform };
            }

            if (_meshObjects.Any(x => x == clothSim.transform) && _meshObjects[0] != clothSim.transform)
            {
                var list = _meshObjects.ToList();
                list.Insert(0, clothSim.transform);
                _meshObjects = list.ToArray();
            }
            _meshObjects = _meshObjects.Distinct().ToArray();

            for (int i = 0; i < 2; i++)
            {
                var list = _meshObjects.ToList();
                list.Sort(new ComponentComparer());
                _meshObjects = list.ToArray();
            }

            _numparticles = 0;
            _vertexCounts = new int[_meshObjects.Length];
            for (int i = 0; i < _meshObjects.Length; i++)
            {
                Transform meshObject = _meshObjects[i];

                if ((meshObject.GetComponent<SkinnedMeshRenderer>() || meshObject.GetComponent<MeshFilter>()) && !meshObject.GetComponent<AutomaticBoneSpheres>() && !meshObject.GetComponent<GPUSkinning>() && !meshObject.GetComponent<DualQuaternionSkinner>() && !meshObject.GetComponent<GPUClothDynamics>())
                {
                    if (meshObject.GetComponent<SkinnedMeshRenderer>())
                    {
                        var gs = meshObject.gameObject.GetOrAddComponent<GPUSkinning>();
                        gs.OnEnable();
                    }
                    else
                    {
                        var gs = meshObject.gameObject.GetOrAddComponent<GPUMesh>();
                        gs.OnEnable();
                    }
                }
                int vertexCount =
                    meshObject.GetComponent<GPUClothDynamics>() != null ? (i == 0 && _clothSim._selfCollisionTriangles ? meshObject.GetComponent<MeshFilter>().mesh.triangles.Length / 3 : meshObject.GetComponent<MeshFilter>().mesh.vertexCount)
                    //: meshObject.GetComponent<SkinnerSource>() != null && meshObject.GetComponent<SkinnerSource>().enabled ? meshObject.GetComponent<SkinnerSource>().vertexCount
                    : (meshObject.GetComponent<DualQuaternionSkinner>() != null || meshObject.GetComponent<GPUSkinning>() != null) ? (_useTrisMesh ? (meshObject.GetComponent<SkinnedMeshRenderer>() ? meshObject.GetComponent<SkinnedMeshRenderer>().sharedMesh : meshObject.GetComponent<MeshFilter>().sharedMesh).triangles.Length / 3 : (meshObject.GetComponent<SkinnedMeshRenderer>() ? meshObject.GetComponent<SkinnedMeshRenderer>().sharedMesh : meshObject.GetComponent<MeshFilter>().sharedMesh).vertexCount)
                    : meshObject.GetComponent<AutomaticBoneSpheres>() != null ? meshObject.GetComponent<AutomaticBoneSpheres>()._spheresBuffer.count
                    : meshObject.GetComponent<GPUMesh>() != null ? (_useTrisMesh ? meshObject.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3 : meshObject.GetComponent<MeshFilter>().sharedMesh.vertexCount)
                    : 0;

                _vertexCounts[i] = vertexCount;
                _numparticles += vertexCount;
            }

            for (int i = 0; i < _vertexCounts.Length; i++)
            {
                if (_meshObjects[i].GetComponent<GPUClothDynamics>()) _numClothParticles2 += _vertexCounts[i];
            }

            _selfAndAutoSpheresCount = _numClothParticles2;

            if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color>" + _clothSim.name + " collision finder -> num particles: " + _numparticles);

            if (_meshObjects.Any(x => x.GetComponent<AutomaticBoneSpheres>()) && !_meshObjects.Any(x => (x.GetComponent<GPUSkinning>() || x.GetComponent<DualQuaternionSkinner>()) && x.GetComponent<GPUClothDynamics>() == null))
            {
                _useAutoSpheresOnly = true;
            }

            if (!_meshObjects.Any(x => x.GetComponent<GPUSkinning>() || x.GetComponent<DualQuaternionSkinner>() || x.GetComponent<GPUMesh>()))
            {
                _useTrisMesh = false;
            }

            if (_meshObjects[0] != _clothSim.transform && _meshObjects.Any(x => x.GetComponent<GPUClothDynamics>()))
            {
                _useSecondClothIfNeeded = true;
            }

            _sphereData = new sData[_numparticles];
            float clothMaskValue = _clothMaskValue; //Cloth mask

            for (int i = 0; i < _numparticles; i++)
            {
                _sphereData[i].pr.w = _unifiedSphereSize > 0 ? _unifiedSphereSize : clothMaskValue;
                _sphereData[i].nId.w = i;
            }

            List<int> trisList = new List<int>();

            int lastCount = 0;
            for (int m = 0; m < _meshObjects.Length; m++)
            {
                var meshObj = _meshObjects[m];

                if (meshObj.GetComponent<GPUClothDynamics>())
                {
                    var mesh = meshObj.GetComponent<MeshFilter>().mesh;
                    var verts = mesh.vertices;
                    var tris = mesh.triangles;
                    bool selfCollision = m == 0 && _clothSim._selfCollisionTriangles;
                    int count = _vertexCounts[m];
                    for (int i = 0; i < count; i++)
                    {
                        if (selfCollision)
                        {
                            _sphereData[lastCount + i].pr.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 0]]);
                            _sphereData[lastCount + i].nId.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 1]]);
                            _sphereData[lastCount + i].temp.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 2]]);
                        }
                        else
                        {
                            _sphereData[lastCount + i].pr.xyz = meshObj.TransformPoint(verts[i]);
                        }
                        _sphereData[lastCount + i].pr.w = _unifiedSphereSize > 0 ? _unifiedSphereSize : clothMaskValue;
                    }
                    if (selfCollision)
                    {
                        _selfTrisDataBuffer = new ComputeBuffer(tris.Length, sizeof(int));
                        _selfTrisDataBuffer.SetData(tris);
                    }
                    _useSelfCollision = true;
                    //if (m < _meshObjects.Length - 1) _lastParticleSum += _vertexCounts[m];

                    if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color><color=lime>" + meshObj.name + " is using GPUClothDynamics for (self) collision</color>");

                }
                else if (meshObj.GetComponent<DualQuaternionSkinner>() || meshObj.GetComponent<GPUSkinning>() || meshObj.GetComponent<GPUMesh>())
                {
                    Mesh mesh = meshObj.GetComponent<GPUMesh>() ? meshObj.GetComponent<MeshFilter>().sharedMesh : (meshObj.GetComponent<SkinnedMeshRenderer>() ? meshObj.GetComponent<SkinnedMeshRenderer>().sharedMesh : meshObj.GetComponent<MeshFilter>().sharedMesh);
                    //SetMeshReadable(mesh);
                    var verts = mesh.vertices;
                    //var normals = mesh.normals;

                    CalcDistConnectionsForMask(meshObj, mesh, lastCount, _useTrisMesh);

                    if (_useTrisMesh)
                    {
                        var tris = mesh.triangles;
                        trisList.AddRange(tris);

                        //_trisDataBuffer = new ComputeBuffer(tris.Length, sizeof(int));
                        //_trisDataBuffer.SetData(tris);

                        int length = tris.Length / 3;
                        for (int i = 0; i < length; i++)
                        {
                            int index = lastCount + i;
                            _sphereData[index].pr.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 0]]);
                            _sphereData[index].nId.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 1]]);
                            _sphereData[index].temp.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 2]]);
                        }
                    }
                    //_useTrisMesh = true;
                    //if (m < _meshObjects.Length - 1) _lastParticleSum += _vertexCounts[m];

                    if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color><color=lime>" + meshObj.name + " is using " + (meshObj.GetComponent<GPUMesh>() ? "GPUMesh" : meshObj.GetComponent<GPUSkinnerBase>().GetType().ToString()) + " for collision</color>");

                }
                else if (meshObj.GetComponent<AutomaticBoneSpheres>())
                {
                    var spheres = meshObj.GetComponent<AutomaticBoneSpheres>()._spheresList;
                    var bones = meshObj.GetComponent<AutomaticBoneSpheres>()._bonesData;

                    int count = spheres.Count;
                    for (int i = 0; i < count; i++)
                    {
                        AutomaticBoneSpheres.BonesStruct bone = bones[spheres[i].boneId];
                        float3 spherePos = bone.pos.xyz + Rotate(bone.rot, spheres[i].offset.xyz);
                        _sphereData[lastCount + i].pr.xyz = spherePos;
                        _sphereData[lastCount + i].pr.w = spheres[i].offset.w * _clothSim._autoSphereScale;
                        //GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        //go.transform.position = spherePos;
                        //go.transform.localScale *= spheres[i].offset.w * _clothSim._autoSphereSize * 2;
                    }
                    //if (m < _meshObjects.Length - 1) _lastParticleSum += _vertexCounts[m];
                    _selfAndAutoSpheresCount += _vertexCounts[m];

                    if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color><color=lime>" + meshObj.name + " is using AutoBoneSpheres for collision</color>");

                }

                var maskPaintObject = meshObj.GetComponent<PaintObject>();
                if (meshObj.GetComponent<GPUClothDynamics>() == null && maskPaintObject != null) // m > 0 -> do not use cloth mask for self collision
                {
                    int[] tris = null;
                    if (_useTrisMesh)
                    {
                        Mesh mesh = meshObj.GetComponent<SkinnedMeshRenderer>() != null ? meshObj.GetComponent<SkinnedMeshRenderer>().sharedMesh : meshObj.GetComponent<MeshFilter>().sharedMesh;
                        tris = mesh.triangles;
                    }
                    Color[] vColors = maskPaintObject.vertexColors;
                    int nextCount = _vertexCounts[m];
                    if (!_useTrisMesh && nextCount != vColors.Length)
                    {
                        //Debug.Log("nextCount != vColors.Length: " + nextCount + " != " + vColors.Length);
                        nextCount = vColors.Length;
                    }
                    //var skinner = meshObj.GetComponent<SkinnerSource>();
                    //bool useSkinner = false;
                    //if (skinner != null && skinner.enabled && skinner._model != null && skinner._model._mapVertsBack != null) useSkinner = true;

                    float maxValue = 0;
                    bool exceedCount = false;
                    for (int n = 0; n < nextCount; n++)
                    {
                        int k = n;
                        float mask = clothMaskValue;
                        int index = _useTrisMesh ? tris[n * 3 + 0] : n;
                        if (index < vColors.Length)
                            mask = vColors[index].r;
                        if (lastCount + k < _sphereData.Length)
                        {
                            mask = mask > 0.5f ? _sphereData[lastCount + k].pr.w : 0; // 50% cut off
                            _sphereData[lastCount + k].pr.w = mask;
                        }
                        else exceedCount = true;
                        maxValue = math.max(mask, maxValue);
                    }
                    if (exceedCount) Debug.Log("<color=blue>CD: </color><color=red>" + meshObj.name + " is using PaintObject with wrong data! Cloth will not work! Update the PaintObject!</color>");

                    if (maxValue == 0)
                    {
                        Debug.Log("<color=blue>CD: </color><color=red>" + meshObj.name + " is using PaintObject only with black colors! Cloth will not work! Repaint or Remove PaintObject!</color>");
                        _clothSim._runSim = false;
                        _clothSim.enabled = false;
                        return;
                    }
                    else
                        if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color><color=lime>" + meshObj.name + " is using PaintObject</color>");

                }

                lastCount += _vertexCounts[m];
            }

            if (_useTrisMesh)
            {
                //Debug.Log("trisList.Count " + trisList.Count);
                _trisDataBuffer = new ComputeBuffer(trisList.Count, sizeof(int));
                _trisDataBuffer.SetData(trisList);
            }

            //if(_selfTrisDataBuffer!=null) _selfTrisDataBuffer.Release();
            //_selfTrisDataBuffer = new ComputeBuffer(1, sizeof(int));
            if (_selfTrisDataBuffer == null) _selfTrisDataBuffer = new ComputeBuffer(1, sizeof(int));

            _sphereDataBuffer = new ComputeBuffer(_sphereData.Length, sizeof(float) * 12);
            _sphereDataBuffer.SetData(_sphereData);
            _numGroups_sphereData = _sphereDataBuffer.count.GetComputeShaderThreads(256);

            _updateAllParticlesKernel = _sphereFinderCS.FindKernel("UpdateAllParticles");
            _updateAllParticleTrisKernel = _sphereFinderCS.FindKernel("UpdateAllParticleTris");
            _updateSkinnerParticlesKernel = _sphereFinderCS.FindKernel("UpdateSkinnerParticles");
            _autoSpheresKernel = _sphereFinderCS.FindKernel("SetupAutoSpheres");
            //_scanInBucketExclusiveKernel = _scanCS.FindKernel("ScanInBucketExclusive");
            _scanInBucketInclusiveKernel = _scanCS.FindKernel("ScanInBucketInclusive");
            _scanBucketResultKernel = _scanCS.FindKernel("ScanBucketResult");
            _scanAddBucketResultKernel = _scanCS.FindKernel("ScanAddBucketResult");

            _sphereFinderCS.SetInt("_gridCount", _gridCount);
            _sphereFinderCS.SetInt(_selfAndAutoSpheresCount_ID, _selfAndAutoSpheresCount);
            _clothSolver.SetInt(_selfAndAutoSpheresCount_ID, _selfAndAutoSpheresCount);

            //UpdateParticles(clothSim._objBuffers[0].positionsBuffer);
            StartDebug(_numparticles);

            SetupVoxelCube();

            meshObjects = _meshObjects;
            useTrisMesh = _useTrisMesh;

        }

        [BurstCompile]
        public struct SpheresProcessJob : IJobParallelFor
        {
            [ReadOnly] internal NativeArray<sData> sphereData;
            public bool useSelfCollision;
            public int vertexCounts;
            public float selfCollisionScale;
            public int selfAndAutoSpheresCount;
            public int numClothParticles2;
            public float secondClothScale;
            public float vertexScale;
            public bool selfCollisionTriangles;
            public bool useTrisMesh;

            public int grid;
            public float voxelSize;
            public float voxelExtend;
            public float4 bbox;
            public float3 cubeMinPos;
            [NativeDisableParallelForRestriction] public NativeArray<int> usedVoxelList;
            [NativeDisableParallelForRestriction] public NativeArray<int> usedVoxelListInverse;
            [NativeDisableParallelForRestriction] public NativeArray<int> counterPerVoxel;

            float dot2(in float3 v) { return math.dot(v, v); }
            bool PointInTriangle(float3 p, float3 pA, float3 pB, float3 pC)
            {
                float3 a = pA - p;
                float3 b = pB - p;
                float3 c = pC - p;

                float3 normPBC = math.cross(b, c); // Normal of PBC (u)
                float3 normPCA = math.cross(c, a); // Normal of PCA (v)
                float3 normPAB = math.cross(a, b); // Normal of PAB (w)

                if (math.dot(normPBC, normPCA) < 0.0f)
                {
                    return false;
                }
                else if (math.dot(normPBC, normPAB) < 0.0f)
                {
                    return false;
                }
                return true;
            }
            float3 ClosestPointToLine(float3 start, float3 end, float3 pos)
            {
                //float3 lVec = end - start;
                //float t = math.dot(pos - start, lVec) / math.dot(lVec, lVec);
                //t = math.max(t, 0.0f);
                //t = math.min(t, 1.0f);
                //return start + lVec * t;
                float3 lVec = end - start;
                float t = math.clamp(math.dot(pos - start, lVec) / (epsilon + dot2(lVec)), 0.0f, 1.0f);
                return start + lVec * t;
            }
            float3 ClosestPointToTri(float3 pA, float3 pB, float3 pC, float3 pos)
            {
                float3 normal = math.normalize(math.cross(pB - pA, pC - pA));
                float surfaceDist = math.dot(normal, pos - pA);
                pos = pos - normal * surfaceDist;

                if (PointInTriangle(pos, pA, pB, pC))
                {
                    return pos;
                }

                float3 c1 = ClosestPointToLine(pA, pB, pos);
                float3 c2 = ClosestPointToLine(pB, pC, pos);
                float3 c3 = ClosestPointToLine(pC, pA, pos);

                float mag1 = dot2(pos - c1);
                float mag2 = dot2(pos - c2);
                float mag3 = dot2(pos - c3);

                float minValue = math.min(mag1, mag2);
                minValue = math.min(minValue, mag3);

                if (minValue == mag1)
                {
                    return c1;
                }
                else if (minValue == mag2)
                {
                    return c2;
                }
                return c3;
            }
            float dbox(float3 p, float b)
            {
                return math.length(math.max(math.abs(p) - b, 0.0f));
            }

            public void Execute(int i)
            {
                sData sphere;
                sphere = sphereData[i];
                float scale = useSelfCollision && i < vertexCounts ? selfCollisionScale : i < selfAndAutoSpheresCount ? 1.0f : i < numClothParticles2 ? secondClothScale : vertexScale;
                sphere.pr.w *= scale;

                int voxelCount = grid * grid * grid;
                for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                {
                    float3 vPos = new float3(voxelIndex % grid, (voxelIndex / grid) % grid, voxelIndex / (grid * grid));
                    bbox.xyz = cubeMinPos + vPos * voxelSize + new float3(1, 1, 1) * voxelExtend;

                    float4 pr = sphere.pr;
                    if ((selfCollisionTriangles && i < vertexCounts) || (useTrisMesh && i >= selfAndAutoSpheresCount))
                    {
                        pr.xyz = ClosestPointToTri(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bbox.xyz);
                    }

                    if (dbox(bbox.xyz - pr.xyz, voxelExtend) < pr.w)
                    {
                        int dataIndex = usedVoxelList[voxelIndex + 1] - 1;
                        if (dataIndex < 0)
                        {
                            dataIndex = usedVoxelList[0];
                            usedVoxelList[voxelIndex + 1] = dataIndex + 1;
                            usedVoxelListInverse[dataIndex + 1] = voxelIndex;
                            usedVoxelList[0]++;
                        }
                        counterPerVoxel[voxelIndex]++;
                    }
                }
            }
        }

        private void RunJobSystemOnVoxelGrid(int grid, float voxelSize, float voxelExtend, float4 bbox, float3 cubeMinPos, int[] counterPerVoxel, int[] usedVoxelListInverse)
        {
            var tempData1 = new NativeArray<sData>(_sphereData, Allocator.TempJob);
            var tempData2 = new NativeArray<int>(_usedVoxelList, Allocator.TempJob);
            var tempData3 = new NativeArray<int>(usedVoxelListInverse, Allocator.TempJob);
            var tempData4 = new NativeArray<int>(counterPerVoxel, Allocator.TempJob);

            var job = new SpheresProcessJob
            {
                sphereData = tempData1,
                useSelfCollision = _useSelfCollision,
                vertexCounts = _vertexCounts[0],
                selfCollisionScale = _clothSim._selfCollisionScale,
                selfAndAutoSpheresCount = _selfAndAutoSpheresCount,
                numClothParticles2 = _numClothParticles2,
                secondClothScale = _clothSim._secondClothScale,
                vertexScale = _clothSim._vertexScale,
                selfCollisionTriangles = _clothSim._selfCollisionTriangles,
                useTrisMesh = _useTrisMesh,
                grid = grid,
                bbox = bbox,
                cubeMinPos = cubeMinPos,
                voxelSize = voxelSize,
                voxelExtend = voxelExtend,
                usedVoxelList = tempData2,
                usedVoxelListInverse = tempData3,
                counterPerVoxel = tempData4
            };
            JobHandle handle = job.Schedule(_sphereData.Length, 512);
            handle.Complete();

            tempData2.CopyTo(_usedVoxelList);
            tempData3.CopyTo(usedVoxelListInverse);
            tempData4.CopyTo(counterPerVoxel);

            tempData1.Dispose();
            tempData2.Dispose();
            tempData3.Dispose();
            tempData4.Dispose();
        }

        private void SetupVoxelCube()
        {
            if (_gridCount < 16) _gridCount = 16;
            if (_gridCount > 16 && _gridCount < 64) _gridCount = 64;
            if (_gridCount > 64) _gridCount = 256;

            //if (_clothSim.GetComponent<Renderer>())
            //{
            //    _clothSim.GetVoxelCubePosQuick();
            //}
            int grid = _voxelCubeGridSize;
            float voxelSize = _clothSim._voxelCubeScale / ((float)grid);
            float voxelExtend = voxelSize * 0.5f;
            float4 bbox = new float4(float3.zero, voxelSize);
            float3 cubeMinPos = _clothSim._voxelCubePos - Vector3.one * _clothSim._voxelCubeScale * 0.5f;

            int[] counterPerVoxel = new int[_gridCount * _gridCount * _gridCount];
            _counterPerVoxel2 = new int[_gridCount * _gridCount * _gridCount];

            _usedVoxelList = new int[_counterPerVoxel2.Length + 512];
            int[] usedVoxelListInverse = new int[_usedVoxelList.Length];
            int[] usedVoxelListInverse2 = new int[_usedVoxelList.Length];

            int texWidth = (int)math.sqrt(counterPerVoxel.Length);
            _counterPerVoxelBuffer = new ComputeBuffer(counterPerVoxel.Length, sizeof(int));
            //_counterPerVoxelBuffer = new RenderTexture(texWidth, texWidth, 0, RenderTextureFormat.RInt);
            //_counterPerVoxelBuffer.filterMode = FilterMode.Point;
            //_counterPerVoxelBuffer.enableRandomWrite = true;
            //_counterPerVoxelBuffer.Create();

            _counterPerVoxelBuffer2 = new ComputeBuffer(counterPerVoxel.Length, sizeof(int));
            //_counterPerVoxelBuffer2 = new RenderTexture(texWidth, texWidth, 0, RenderTextureFormat.RInt);
            //_counterPerVoxelBuffer2.filterMode = FilterMode.Point;
            //_counterPerVoxelBuffer2.enableRandomWrite = true;
            //_counterPerVoxelBuffer2.Create();

            _counterPerVoxelThread = _counterPerVoxelBuffer.count.GetComputeShaderThreads(512);

            _sphereFinderCS.SetInt("_texWidth", texWidth);
            _clothSolver.SetInt("_texWidth", texWidth);

            texWidth += 2;

            _sphereFinderCS.SetInt("_texWidthExtra", texWidth);
            _clothSolver.SetInt("_texWidthExtra", texWidth);

            _usedVoxelListBuffer = new ComputeBuffer(_usedVoxelList.Length, sizeof(int));
            _usedVoxelListBufferDispatch = _usedVoxelListBuffer.count.GetComputeShaderThreads(512);
            //_usedVoxelListBuffer = new RenderTexture(texWidth, texWidth, 0, RenderTextureFormat.RInt);
            //_usedVoxelListBuffer.filterMode = FilterMode.Point;
            //_usedVoxelListBuffer.enableRandomWrite = true;
            //_usedVoxelListBuffer.Create();

            _usedVoxelListInverseBuffer = new ComputeBuffer(_usedVoxelList.Length, sizeof(int));
            //_usedVoxelListInverseBuffer = new RenderTexture(texWidth, texWidth, 0, RenderTextureFormat.RInt);
            //_usedVoxelListInverseBuffer.filterMode = FilterMode.Point;
            //_usedVoxelListInverseBuffer.enableRandomWrite = true;
            //_usedVoxelListInverseBuffer.Create();

            _usedVoxelListInverseBuffer2 = new ComputeBuffer(_usedVoxelList.Length, sizeof(int));
            //_usedVoxelListInverseBuffer2 = new RenderTexture(texWidth, texWidth, 0, RenderTextureFormat.RInt);
            //_usedVoxelListInverseBuffer2.filterMode = FilterMode.Point;
            //_usedVoxelListInverseBuffer2.enableRandomWrite = true;
            //_usedVoxelListInverseBuffer2.Create();

            _lastVoxelCountBuffer = new ComputeBuffer(2, sizeof(int));
            _lastVoxelCountBuffer.SetData(new int[2]);

            _argsBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(new int[] { 1, 1, 1 });
            _argsBuffer2 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            _argsBuffer2.SetData(new int[] { 1, 1, 1 });

            _lastCounterPerVoxel = new int[counterPerVoxel.Length];
            _lastCounterPerVoxelBuffer = new ComputeBuffer(_lastCounterPerVoxel.Length, sizeof(int));

            _lastCounterPerVoxel2 = new int[_counterPerVoxel2.Length];
            _lastCounterPerVoxelBuffer2 = new ComputeBuffer(_lastCounterPerVoxel2.Length, sizeof(int));

            _scanCountBuffer = new ComputeBuffer(_lastCounterPerVoxel.Length, sizeof(int));

            InitBuffers();

            //if (_initWithGPU)
            //{
            //    float4 cubeMinVec = new float4(cubeMinPos, voxelExtend);
            //    VoxelSubDivZero(cubeMinVec, true);
            //}
            //else
            {
                //var timer = System.Diagnostics.Stopwatch.StartNew();
                //int maxCounter = 0;
                //Debug.Log("_sphereData " + _sphereData.Length);
                for (int i = 0; i < _sphereData.Length; i++)
                {
                    sData sphere = _sphereData[i];
                    float scale = _useSelfCollision && i < _vertexCounts[0] ? _clothSim._selfCollisionScale : i < _selfAndAutoSpheresCount ? 1.0f : i < _numClothParticles2 ? _clothSim._secondClothScale : _clothSim._vertexScale;
                    sphere.pr.w *= scale;
                    int voxelCount = grid * grid * grid;
                    for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                    {
                        float3 vPos = new float3(voxelIndex % grid, (voxelIndex / grid) % grid, voxelIndex / (grid * grid));
                        bbox.xyz = cubeMinPos + vPos * voxelSize + new float3(1, 1, 1) * voxelExtend;
                        float4 pr = sphere.pr;
                        //if (_useTrisMesh && i >= _selfAndAutoSpheresCount)
                        if ((_clothSim._selfCollisionTriangles && i < _vertexCounts[0]) || (_useTrisMesh && i >= _selfAndAutoSpheresCount))
                        {
                            //float dist = udTriangle(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bbox.xyz);// - _triThickness;
                            //collide = dist < voxelSize && sphere.pr.w > 0;
                            pr.xyz = ClosestPointToTri(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bbox.xyz);
                        }
                        if (dbox(bbox.xyz - pr.xyz, voxelExtend) < pr.w)
                        {
                            int dataIndex = _usedVoxelList[voxelIndex + 1] - 1;
                            if (dataIndex < 0)
                            {
                                dataIndex = _usedVoxelList[0];
                                _usedVoxelList[voxelIndex + 1] = dataIndex + 1;
                                usedVoxelListInverse[dataIndex + 1] = voxelIndex;
                                _usedVoxelList[0]++;
                            }
                            //int index = voxelIndex * _spheresPerVoxel + counterPerVoxel[voxelIndex];
                            //voxelBufferData[index] = sphere;
                            //if (counterPerVoxel[voxelIndex] < _spheresPerVoxel) 
                            counterPerVoxel[voxelIndex]++;
                            //maxCounter = math.max(maxCounter, counterPerVoxel[voxelIndex]);
                        }
                    }
                }

                //RunJobSystemOnVoxelGrid(grid, voxelSize, voxelExtend, bbox, cubeMinPos, counterPerVoxel, usedVoxelListInverse);

                //timer.Stop();
                //TimeSpan timespan = timer.Elapsed;
                //Debug.Log(String.Format("ClothSim {0:00}:{1:00}:{2:00}", timespan.Minutes, timespan.Seconds, timespan.Milliseconds / 10));


                int rCount = 0;
                for (int n = 0; n < _usedVoxelList[0]; n++)
                {
                    _lastCounterPerVoxel[n] = rCount;
                    rCount += counterPerVoxel[usedVoxelListInverse[n + 1]];
                }

                //_spheresPerVoxel = maxCounter;
                //if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color>maxCounter " + maxCounter);
                if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color>sData Size " + rCount);

                //voxelBufferData = new sData[16 * 16 * 16 * _spheresPerVoxel];
                //voxelBufferData2 = new sData[16 * 16 * 16 * _spheresPerVoxel];
                _voxelBufferData = new sData[(int)(rCount * _clothSim._bufferScale)];
                _voxelBufferData2 = new sData[(int)(rCount * _clothSim._bufferScale)];

                _voxelDataBuffer = new ComputeBuffer(_voxelBufferData.Length, sizeof(float) * 12);
                _voxelDataBuffer.SetData(_voxelBufferData);
                _voxelDataBuffer2 = new ComputeBuffer(_voxelBufferData2.Length, sizeof(float) * 12);
                _voxelDataBuffer2.SetData(_voxelBufferData2);

                if (_initWithGPU)
                {
                    _lastCounterPerVoxelBuffer.SetData(_lastCounterPerVoxel);
                    //SetDataToTex(_counterPerVoxelBuffer, ref counterPerVoxel);
                    _counterPerVoxelBuffer.SetData(counterPerVoxel);
                    //SetDataToTex(_usedVoxelListBuffer, ref _usedVoxelList);
                    _usedVoxelListBuffer.SetData(_usedVoxelList);
                    //SetDataToTex(_usedVoxelListInverseBuffer, ref usedVoxelListInverse);
                    _usedVoxelListInverseBuffer.SetData(usedVoxelListInverse);

                    //Graphics.SetRenderTarget(_counterPerVoxelBuffer);
                    //GL.Clear(false, true, Color.clear);
                    _sphereFinderCS.SetBuffer(3, _usedVoxelListBuffer_ID, _counterPerVoxelBuffer);
                    _sphereFinderCS.Dispatch(3, _counterPerVoxelThread, 1, 1);

                    int kernel = 8;
                    _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
                    _sphereFinderCS.Dispatch(kernel, 1, 1, 1);

                    kernel = 11;
                    _sphereFinderCS.SetBuffer(11, _lastCounterPerVoxelBuffer_ID, _lastCounterPerVoxelBuffer);
                    _sphereFinderCS.SetBuffer(11, "_sphereDataBuffer", _sphereDataBuffer);
                    _sphereFinderCS.SetBuffer(11, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
                    _sphereFinderCS.SetBuffer(11, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
                    _sphereFinderCS.SetBuffer(11, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
                    _sphereFinderCS.SetBuffer(11, _voxelDataBuffer_ID, _voxelDataBuffer);
                    _sphereFinderCS.Dispatch(11, _numGroups_sphereData, 1, 1);
                }
                else
                {
                    for (int i = 0; i < counterPerVoxel.Length; i++)
                        counterPerVoxel[i] = 0;

                    for (int i = 0; i < _sphereData.Length; i++)
                    {
                        sData sphere = _sphereData[i];
                        //float scale = i < vertexCounts[0] && vertexCounts.Length > 1 ? _clothSim._selfCollisionScale : i < lastParticleSum ? 1.0f : _clothSim._collisionSize;
                        float scale = _useSelfCollision && i < _vertexCounts[0] ? _clothSim._selfCollisionScale : i < _selfAndAutoSpheresCount ? 1.0f : i < _numClothParticles2 ? _clothSim._secondClothScale : _clothSim._vertexScale;
                        sphere.pr.w *= scale;


                        //int voxelCount = grid * grid * grid;
                        //for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                        for (int dataIndex = 0; dataIndex < _usedVoxelList[0]; dataIndex++)
                        {
                            int voxelIndex = usedVoxelListInverse[dataIndex + 1];
                            float3 vPos = new float3(voxelIndex % grid, (voxelIndex / grid) % grid, voxelIndex / (grid * grid));
                            bbox.xyz = cubeMinPos + vPos * voxelSize + new float3(1, 1, 1) * voxelExtend;

                            float4 pr = sphere.pr;
                            if ((_clothSim._selfCollisionTriangles && i < _vertexCounts[0]) || (_useTrisMesh && i >= _selfAndAutoSpheresCount))
                            {
                                //float dist = udTriangle(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bbox.xyz);// - _triThickness;
                                //collide = dist < voxelSize && sphere.pr.w > 0;
                                pr.xyz = ClosestPointToTri(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bbox.xyz);
                            }

                            if (dbox(bbox.xyz - pr.xyz, voxelExtend) < pr.w)
                            {
                                int index = _lastCounterPerVoxel[dataIndex] + counterPerVoxel[voxelIndex];
                                _voxelBufferData[index] = sphere;
                                ////if (counterPerVoxel[voxelIndex] < _spheresPerVoxel) 
                                counterPerVoxel[voxelIndex]++;
                                //maxCounter = math.max(maxCounter, counterPerVoxel[voxelIndex]);

                            }
                        }
                    }
                }
                //Debug.Log("<color=blue>CD: </color>maxCounter " + maxCounter);
            }
            if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color>" + _clothSim.name + " voxelBufferData.Length: " + _voxelBufferData.Length);

            int totalRealCount = 0;
            int lastVoxelCount = 0;

            if (_initWithGPU)
            {
                UpdateTotalCountAndClear(_counterPerVoxelBuffer);
                var data = new int[2];
                _lastVoxelCountBuffer.GetData(data);
                totalRealCount = data[1];
            }
            else
            {
                lastVoxelCount = _usedVoxelList[0];
                for (int i = 0; i < lastVoxelCount; i++)
                {
                    totalRealCount += counterPerVoxel[usedVoxelListInverse[i + 1]];
                }
                //totalRealCount = _lastCounterPerVoxel[_usedVoxelList[0] - 1];
                _usedVoxelList = new int[_usedVoxelList.Length];
            }
            //Debug.Log("totalRealCount " + totalRealCount);


            VoxelSubdivWithCPU(1, totalRealCount, lastVoxelCount, grid, voxelSize, voxelExtend, cubeMinPos, counterPerVoxel, _counterPerVoxel2, usedVoxelListInverse, usedVoxelListInverse2);

            for (int n = 0; n < 3; n++)
            {
                grid *= _voxelCubeGridSize;
                if (grid >= _gridCount)
                    break;

                if (_initWithGPU)
                {
                    SwitchUpdateCountAndClear();

                    var data = new int[2];
                    _lastVoxelCountBuffer.GetData(data);
                    totalRealCount = data[1];
                }
                else
                {
                    //_spheresPerVoxel = _newSpheresPerVoxel;
                    //_newSpheresPerVoxel = _newSpheresPerVoxel / 2;

                    _lastCounterPerVoxel = new List<int>(_lastCounterPerVoxel2).ToArray();
                    //_lastUsedVoxelList = _usedVoxelList;

                    SwitchArrays(ref usedVoxelListInverse, ref usedVoxelListInverse2);

                    totalRealCount = 0;
                    lastVoxelCount = _usedVoxelList[0];
                    for (int i = 0; i < lastVoxelCount; i++)
                    {
                        totalRealCount += _counterPerVoxel2[usedVoxelListInverse[i + 1]];
                    }
                    //totalRealCount = _lastCounterPerVoxel2[_usedVoxelList[0] - 1];
                    //Debug.Log("total buffer Count " + _usedVoxelList[0] * 64 * _newSpheresPerVoxel);

                    _usedVoxelList = new int[_usedVoxelList.Length];

                    SwitchArrays(ref counterPerVoxel, ref _counterPerVoxel2);
                    SwitchArrays(ref _voxelBufferData, ref _voxelBufferData2);

                    for (int i = 0; i < _counterPerVoxel2.Length; i++)
                        _counterPerVoxel2[i] = 0;
                }
                //Debug.Log("totalRealCount " + totalRealCount);

                VoxelSubdivWithCPU(n + 2, totalRealCount, lastVoxelCount, grid, voxelSize, voxelExtend, cubeMinPos, counterPerVoxel, _counterPerVoxel2, usedVoxelListInverse, usedVoxelListInverse2);

            }

            if (_initWithGPU)
            {
                _lastCounterPerVoxelBuffer2.GetData(_lastCounterPerVoxel2);

                //GetDataFromTex(_usedVoxelListBuffer, ref _usedVoxelList);
                _usedVoxelListBuffer.GetData(_usedVoxelList);

                //GetDataFromTex(_counterPerVoxelBuffer2, ref _counterPerVoxel2);
                _counterPerVoxelBuffer2.GetData(_counterPerVoxel2);

                _voxelDataBuffer.GetData(_voxelBufferData);
                _voxelDataBuffer2.GetData(_voxelBufferData2);
                //GetDataFromTex(_usedVoxelListInverseBuffer2, ref usedVoxelListInverse2);
                _usedVoxelListInverseBuffer2.GetData(usedVoxelListInverse2);
            }
            //Debug.Log("_spheresPerVoxel " + _newSpheresPerVoxel);
            int usedVoxelCount = math.max(1, _usedVoxelList[0]);
            int totalSphereCount = _lastCounterPerVoxel2[usedVoxelCount - 1] + 1;
            if (_voxelBufferData2.Length < totalSphereCount)
            {
                float scaleBuffer = math.min(8, (totalSphereCount / (float)_voxelBufferData2.Length));
                Debug.Log("scaleBuffer " + scaleBuffer);
                if (_clothSim._bufferScale < 32) _clothSim._bufferScale *= scaleBuffer * 1.6f;
                //_clothSim._bufferScale = math.min(1024, _clothSim._bufferScale);
                if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color><color=lime>Dynamic Buffer Scale: " + _clothSim._bufferScale + " scaledBuffer: " + scaleBuffer + "</color>");
                int newLength = Mathf.CeilToInt(_voxelBufferData2.Length * scaleBuffer) + 64;
                System.Array.Resize(ref _voxelBufferData, newLength);
                System.Array.Resize(ref _voxelBufferData2, newLength);
                _voxelDataBuffer.Release();
                _voxelDataBuffer = new ComputeBuffer(_voxelBufferData.Length, sizeof(float) * 12);
                _voxelDataBuffer.SetData(_voxelBufferData);
                _voxelDataBuffer2.Release();
                _voxelDataBuffer2 = new ComputeBuffer(_voxelBufferData2.Length, sizeof(float) * 12);
                _voxelDataBuffer2.SetData(_voxelBufferData2);
            }

            //Debug.Log("totalSphereCount " + totalSphereCount);

        }

        private void InitBuffers()
        {
            int kernel = 0;
            _sphereFinderCS.SetBuffer(kernel, "_sphereDataBuffer", _sphereDataBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);

            kernel = 1;
            _sphereFinderCS.SetBuffer(kernel, _lastCounterPerVoxelBuffer_ID, _lastCounterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_lastVoxelCountBuffer", _lastVoxelCountBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBufferR_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            //_sphereFinderCS.SetBuffer(kernel, _voxelDataBuffer_ID, _voxelDataBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer2_ID, _usedVoxelListInverseBuffer2);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer2_ID, _counterPerVoxelBuffer2);

            kernel = 2;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_lastVoxelCountBuffer", _lastVoxelCountBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_argsBuffer", _argsBuffer);

            kernel = 8;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);

            kernel = 10;
            _sphereFinderCS.SetBuffer(kernel, "_lastVoxelCountBuffer", _lastVoxelCountBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBufferR_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _lastCounterPerVoxelBuffer_ID, _lastCounterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, _lastCounterPerVoxelBuffer2_ID, _lastCounterPerVoxelBuffer2);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer2_ID, _counterPerVoxelBuffer2);
            //_sphereFinderCS.SetBuffer(kernel, _voxelDataBuffer_ID, _voxelDataBuffer);
            //_sphereFinderCS.SetBuffer(kernel, _voxelDataBuffer2_ID, _voxelDataBuffer2);

            kernel = 11;
            _sphereFinderCS.SetBuffer(kernel, _lastCounterPerVoxelBuffer_ID, _lastCounterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_sphereDataBuffer", _sphereDataBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            //_sphereFinderCS.SetBuffer(kernel, _voxelDataBuffer_ID, _voxelDataBuffer);

            kernel = 12;
            _sphereFinderCS.SetBuffer(kernel, "_argsBuffer2", _argsBuffer2);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);

            kernel = 13;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_scanCountBuffer", _scanCountBuffer);

            kernel = 19;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_lastVoxelCountBuffer", _lastVoxelCountBuffer);

            kernel = 20;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);

        }

        private void GetDataFromTex(RenderTexture texBuffer, ref int[] data)
        {
            int threadSize = texBuffer.width * texBuffer.height;
            var counterPerVoxelBuffer2Copy = new ComputeBuffer(threadSize, sizeof(int));
            int kernel = 17;
            _sphereFinderCS.SetInt("_threadSize", threadSize);
            _sphereFinderCS.SetInt("_texWidthCopy", texBuffer.width);
            _sphereFinderCS.SetTexture(kernel, _counterPerVoxelBuffer2_ID, texBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_counterPerVoxelBuffer2Copy", counterPerVoxelBuffer2Copy);
            _sphereFinderCS.Dispatch(kernel, threadSize.GetComputeShaderThreads(512), 1, 1);
            counterPerVoxelBuffer2Copy.GetData(data);
            counterPerVoxelBuffer2Copy.Release();
        }

        private void SetDataToTex(RenderTexture texBuffer, ref int[] data)
        {
            int threadSize = data.Length;
            var counterPerVoxelBuffer2Copy = new ComputeBuffer(threadSize, sizeof(int));
            counterPerVoxelBuffer2Copy.SetData(data);
            int kernel = 18;
            _sphereFinderCS.SetInt("_threadSize", threadSize);
            _sphereFinderCS.SetInt("_texWidthCopy", texBuffer.width);
            _sphereFinderCS.SetTexture(kernel, _counterPerVoxelBuffer2_ID, texBuffer);
            _sphereFinderCS.SetBuffer(kernel, "_counterPerVoxelBuffer2Copy", counterPerVoxelBuffer2Copy);
            _sphereFinderCS.Dispatch(kernel, threadSize.GetComputeShaderThreads(512), 1, 1);
            counterPerVoxelBuffer2Copy.Release();
        }

        private void VoxelSubdivWithCPU(int level, int totalRealCount, int lastVoxelCount, int grid, float voxelSize, float voxelExtend, float3 cubeMinPos, int[] counterPerVoxel, int[] counterPerVoxel2, int[] usedVoxelListInverse, int[] usedVoxelListInverse2)
        {
            if (_initWithGPU)
            {
                float4 cubeMinVec = new float4(cubeMinPos, voxelExtend);
                VoxelSubdiv(level, grid, cubeMinVec);
            }
            else
            {
                //int lastParticleSum = _lastParticleSum;
                //for (int i = 0; i < vertexCounts.Length - 1; i++)
                //{
                //    lastParticleSum += vertexCounts[i];
                //}
                int maxCounter = 0;
                float scaled = 1.0f / (float)grid;
                for (int i = 0; i < totalRealCount; i++)
                {
                    int dataIndex = 0;
                    int realCount = 0;
                    for (int n = 0; n < lastVoxelCount; n++)
                    {
                        realCount += counterPerVoxel[usedVoxelListInverse[n + 1]];
                        if (i < realCount)
                        {
                            dataIndex = n;
                            break;
                        }
                    }

                    int voxelIndex = usedVoxelListInverse[dataIndex + 1];
                    int counter = i - (realCount - counterPerVoxel[voxelIndex]);
                    //int index = voxelIndex * _spheresPerVoxel + counter;
                    int index = _lastCounterPerVoxel[dataIndex] + counter;
                    var sphere = _voxelBufferData[index];
                    int nId = (int)sphere.nId.w;

                    float3 voxelPos = new float3(voxelIndex % grid, (voxelIndex / grid) % grid, voxelIndex / (grid * grid));
                    float3 minPos = cubeMinPos + voxelPos * voxelSize * scaled * 4;
                    bool collide = false;

                    for (int vIndex = 0; vIndex < 64; vIndex++)
                    {
                        float3 vPos = new float3(vIndex % 4, (vIndex / 4) % 4, vIndex / (4 * 4));
                        float3 voxelPos2 = voxelPos * 4 + vPos;
                        int newVoxelNum = (int)(voxelPos2.x + voxelPos2.y * (4 * grid) + voxelPos2.z * (4 * grid) * (4 * grid));
                        float3 bb = minPos + vPos * voxelSize * scaled + new float3(1, 1, 1) * voxelExtend * scaled;

                        float4 pr = sphere.pr;
                        if ((_clothSim._selfCollisionTriangles && nId < _vertexCounts[0]) || (_useTrisMesh && nId >= _selfAndAutoSpheresCount))
                        {
                            //float dist = udTriangle(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bb);// - _triThickness;
                            //collide = dist < voxelSize * scaled && sphere.pr.w > 0;
                            pr.xyz = ClosestPointToTri(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bb);
                        }

                        collide = dbox(bb - pr.xyz, voxelExtend * scaled) < pr.w;

                        if (collide)
                        {
                            int newDataIndex = _usedVoxelList[newVoxelNum + 1] - 1;
                            if (newDataIndex < 0)
                            {
                                newDataIndex = _usedVoxelList[0];
                                _usedVoxelList[newVoxelNum + 1] = newDataIndex + 1;
                                usedVoxelListInverse2[newDataIndex + 1] = newVoxelNum;
                                _usedVoxelList[0]++;
                            }
                            //int index2 = (level == 2 ? (dataIndex * 64 + vIndex) : newVoxelNum) * _newSpheresPerVoxel + counterPerVoxel2[newVoxelNum];
                            //if (index2 < voxelBufferData2.Length) 
                            //voxelBufferData2[index2] = sphere;
                            //if (counterPerVoxel2[newVoxelNum] < _newSpheresPerVoxel)
                            counterPerVoxel2[newVoxelNum]++;
                            maxCounter = math.max(maxCounter, counterPerVoxel2[newVoxelNum]);
                        }
                    }
                }

                int rCount = 0;
                for (int n = 0; n < _usedVoxelList[0]; n++)
                {
                    _lastCounterPerVoxel2[n] = rCount;
                    rCount += counterPerVoxel2[usedVoxelListInverse2[n + 1]];
                }

                for (int i = 0; i < counterPerVoxel2.Length; i++)
                    counterPerVoxel2[i] = 0;

                //_newSpheresPerVoxel = maxCounter;
                if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color>maxCounter " + maxCounter);


                //int maxBufferLength = 0;
                for (int i = 0; i < totalRealCount; i++)
                {
                    int dataIndex = 0;
                    int realCount = 0;
                    for (int n = 0; n < lastVoxelCount; n++)
                    {
                        realCount += counterPerVoxel[usedVoxelListInverse[n + 1]];
                        if (i < realCount)
                        {
                            dataIndex = n;
                            break;
                        }
                    }

                    int voxelIndex = usedVoxelListInverse[dataIndex + 1];
                    int counter = i - (realCount - counterPerVoxel[voxelIndex]);
                    int index = _lastCounterPerVoxel[dataIndex] + counter;
                    var sphere = _voxelBufferData[index];
                    int nId = (int)sphere.nId.w;

                    float3 voxelPos = new float3(voxelIndex % grid, (voxelIndex / grid) % grid, voxelIndex / (grid * grid));
                    float3 minPos = cubeMinPos + voxelPos * voxelSize * scaled * 4;
                    bool collide = false;

                    for (int vIndex = 0; vIndex < 64; vIndex++)
                    {
                        float3 vPos = new float3(vIndex % 4, (vIndex / 4) % 4, vIndex / (4 * 4));
                        float3 voxelPos2 = voxelPos * 4 + vPos;
                        int newVoxelNum = (int)(voxelPos2.x + voxelPos2.y * (4 * grid) + voxelPos2.z * (4 * grid) * (4 * grid));
                        float3 bb = minPos + vPos * voxelSize * scaled + new float3(1, 1, 1) * voxelExtend * scaled;

                        float4 pr = sphere.pr;
                        //if (_useTrisMesh && sphere.nId.w >= _selfAndAutoSpheresCount)
                        if ((_clothSim._selfCollisionTriangles && nId < _vertexCounts[0]) || (_useTrisMesh && nId >= _selfAndAutoSpheresCount))
                        {
                            //float dist = udTriangle(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bb);// - _triThickness;
                            //collide = dist < voxelSize * scaled && sphere.pr.w > 0;
                            pr.xyz = ClosestPointToTri(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, bb);
                        }

                        collide = dbox(bb - pr.xyz, voxelExtend * scaled) < pr.w;
                        //collide = _usedVoxelList[newVoxelNum + 1] > 0;

                        if (collide)
                        {
                            int newDataIndex = _usedVoxelList[newVoxelNum + 1] - 1;
                            int index2 = _lastCounterPerVoxel2[newDataIndex] + counterPerVoxel2[newVoxelNum];
                            //maxBufferLength = math.max(maxBufferLength, index2);
                            if (index2 < _voxelBufferData2.Length)
                                _voxelBufferData2[index2] = sphere;
                            //else
                            //{
                            //    Debug.LogError("Too many collision points for buffer, increase buffer scale or decrease collision scale!");
                            //    break;
                            //}
                            //if (counterPerVoxel2[newVoxelNum] < _newSpheresPerVoxel) 
                            counterPerVoxel2[newVoxelNum]++;
                            maxCounter = math.max(maxCounter, counterPerVoxel2[newVoxelNum]);
                        }
                    }
                }
                //Debug.Log("maxBufferLength " + maxBufferLength);
            }
        }

        private void GPUCollisionMethod()
        {
            //var cube = _voxelCube;
            int grid = _voxelCubeGridSize;
            float voxelSize = _clothSim._voxelCubeScale / ((float)grid);
            float voxelExtend = voxelSize * 0.5f;
            float3 cubeMinPos = _clothSim._voxelCubePos - Vector3.one * _clothSim._voxelCubeScale * 0.5f;

            _sphereFinderCS.SetBuffer(4, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(4, _counterPerVoxelBuffer2_ID, _counterPerVoxelBuffer2);
            _sphereFinderCS.SetBuffer(4, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(4, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(4, _usedVoxelListInverseBuffer2_ID, _usedVoxelListInverseBuffer2);
            _sphereFinderCS.Dispatch(4, _usedVoxelListBufferDispatch, 1, 1);

            //5 mrts might not work on all platforms (vulkan has problems with different texture size)
            //Graphics.SetRenderTarget(new RenderBuffer[] { _usedVoxelListBuffer.colorBuffer, _usedVoxelListInverseBuffer.colorBuffer, _usedVoxelListInverseBuffer2.colorBuffer }, _usedVoxelListInverseBuffer2.depthBuffer);
            //GL.Clear(false, true, Color.clear);

            //Graphics.SetRenderTarget(new RenderBuffer[] { _counterPerVoxelBuffer.colorBuffer, _counterPerVoxelBuffer2.colorBuffer }, _counterPerVoxelBuffer.depthBuffer);
            //GL.Clear(false, true, Color.clear);

            float4 cubeMinVec = new float4(cubeMinPos, voxelExtend);
            VoxelSubDivZero(cubeMinVec);

            UpdateTotalCountAndClear(_counterPerVoxelBuffer);

            VoxelSubdiv(1, grid, cubeMinVec);

            for (int n = 0; n < 3; n++)
            {
                grid *= _voxelCubeGridSize;
                if (grid >= _gridCount)
                    break;

                SwitchUpdateCountAndClear();

                VoxelSubdiv(n + 2, grid, cubeMinVec);
            }

            if (_clothSim._updateBufferSize)
            {
                _lastCounterPerVoxelBuffer2.GetData(_lastCounterPerVoxel2);

                bool changed = false;
                int index = math.clamp(_usedVoxelList[0] - 1, 0, _lastCounterPerVoxel2.Length - 1);
                int totalSphereCount = _lastCounterPerVoxel2[index] + 1;
                if (_voxelDataBuffer2.count < totalSphereCount)
                {
                    float scaleBuffer = math.min(8, (totalSphereCount / (float)_voxelBufferData2.Length));
                    Debug.Log("scaleBuffer " + scaleBuffer);
                    if (_clothSim._bufferScale < 32) _clothSim._bufferScale *= scaleBuffer;
                    //_clothSim._bufferScale = math.min(1024, _clothSim._bufferScale);
                    if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color><color=lime>Dynamic Buffer Scale: " + _clothSim._bufferScale + " scaledBuffer: " + scaleBuffer + "</color>");

                    int newLength = Mathf.CeilToInt(_voxelDataBuffer2.count * scaleBuffer) + 64;

                    _voxelDataBuffer.GetData(_voxelBufferData);
                    _voxelDataBuffer2.GetData(_voxelBufferData2);

                    Array.Resize(ref _voxelBufferData, newLength);
                    Array.Resize(ref _voxelBufferData2, newLength);

                    _voxelDataBuffer.Release();
                    _voxelDataBuffer = new ComputeBuffer(newLength, sizeof(float) * 12);
                    _voxelDataBuffer.SetData(_voxelBufferData);
                    _voxelDataBuffer2.Release();
                    _voxelDataBuffer2 = new ComputeBuffer(newLength, sizeof(float) * 12);
                    _voxelDataBuffer2.SetData(_voxelBufferData2);

                    //PlayerPrefs.SetFloat(_clothSim.GetInstanceID().ToString(), _clothSim._bufferScale);
                    //PlayerPrefs.Save();

                    changed = true;
                    Debug.Log("<color=blue>CD: </color><color=lime>New Buffer Size " + _voxelDataBuffer2.count + "</color>");
                }

                if (!changed)
                {
                    //GetDataFromTex(_usedVoxelListBuffer, ref _usedVoxelList);
                    _usedVoxelListBuffer.GetData(_usedVoxelList);

                    //GetDataFromTex(_counterPerVoxelBuffer2, ref _counterPerVoxel2);
                    _counterPerVoxelBuffer2.GetData(_counterPerVoxel2);

                    _voxelDataBuffer2.GetData(_voxelBufferData2);
                }

            }
        }

        private void SwitchUpdateCountAndClear()
        {
            SwitchBuffers(ref _lastCounterPerVoxelBuffer, ref _lastCounterPerVoxelBuffer2);
            SwitchBuffers(ref _usedVoxelListInverseBuffer, ref _usedVoxelListInverseBuffer2);

            UpdateTotalCountAndClear(_counterPerVoxelBuffer2);

            SwitchBuffers(ref _counterPerVoxelBuffer, ref _counterPerVoxelBuffer2);
            SwitchBuffers(ref _voxelDataBuffer, ref _voxelDataBuffer2);

            //Graphics.SetRenderTarget(_counterPerVoxelBuffer2);
            //GL.Clear(false, true, Color.clear);
            _sphereFinderCS.SetBuffer(3, _usedVoxelListBuffer_ID, _counterPerVoxelBuffer2);
            _sphereFinderCS.Dispatch(3, _counterPerVoxelThread, 1, 1);

            int kernel = 8;//ClearCounterBuffer
                           //_sphereFinderCS.SetTexture(kernel, _counterPerVoxelBuffer2_ID, _counterPerVoxelBuffer2);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.Dispatch(kernel, 1, 1, 1);
        }

        private void GetBlockSizeForScan(ComputeBuffer usedVoxelListInverseBuffer, ComputeBuffer counterPerVoxelBuffer)
        {
            int kernel = 12;
            if (!_useDirectDispatch)
            {
                _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, usedVoxelListInverseBuffer);
                _sphereFinderCS.Dispatch(kernel, 1, 1, 1);
            }
            else
            {
                kernel = 20;
                _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, usedVoxelListInverseBuffer);
                _sphereFinderCS.Dispatch(kernel, 1, 1, 1);
            }

            kernel = 13;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, counterPerVoxelBuffer);
            //_sphereFinderCS.SetBuffer(kernel, "_scanCountBuffer", _scanCountBuffer);

            if (!_useDirectDispatch)
            {
                _sphereFinderCS.DispatchIndirect(kernel, _argsBuffer2);
            }
            else
            {
                var dispatch = Mathf.FloorToInt((usedVoxelListInverseBuffer.count - 1) / 512.0f);
                _sphereFinderCS.Dispatch(kernel, dispatch, 1, 1);
            }
        }

        private void VoxelSubDivZero(float4 cubeMinVec, bool init = false)
        {
            //int lastParticleSum = _lastParticleSum;
            //_clothSolver.SetInt(_lastParticleSum_ID, lastParticleSum);
            //_sphereFinderCS.SetInt(_lastParticleSum_ID, _lastParticleSum);
            _sphereFinderCS.SetBool(_useTrisMesh_ID, _useTrisMesh);
            _sphereFinderCS.SetBool(_useSelfCollision_ID, _useSelfCollision);
            _sphereFinderCS.SetBool(_useSelfCollisionTriangles_ID, _clothSim._selfCollisionTriangles);

            _sphereFinderCS.SetVector(_cubeMinVec_ID, cubeMinVec);
            _sphereFinderCS.SetInt(_sphereDataLength_ID, _sphereDataBuffer.count);
            _sphereFinderCS.SetInt(_numClothParticles_ID, _vertexCounts.Length > 0 ? _vertexCounts[0] : 0);
            _sphereFinderCS.SetInt(_numClothParticles2_ID, _vertexCounts.Length > 1 ? _numClothParticles2 : 0);
            _sphereFinderCS.SetFloat(_collisionSize_ID, _useTrisMesh ? _clothSim._triangleScale : _useAutoSpheresOnly ? 1.0f : _clothSim._vertexScale);
            _sphereFinderCS.SetFloat(_selfCollisionScale_ID, _useSecondClothIfNeeded ? _clothSim._secondClothScale : _clothSim._useSelfCollision ? _clothSim._selfCollisionScale : 0);
            _sphereFinderCS.SetFloat(_secondClothScale_ID, _clothSim._secondClothScale);

            //_sphereFinderCS.SetBuffer(0, "_sphereDataBuffer", _sphereDataBuffer);
            //_sphereFinderCS.SetTexture(0, "_usedVoxelListBuffer", _usedVoxelListBuffer);
            _sphereFinderCS.SetBuffer(0, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(0, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.Dispatch(0, _numGroups_sphereData, 1, 1);

            GetBlockSizeForScan(_usedVoxelListInverseBuffer, _counterPerVoxelBuffer);
            ScanBuffer(_scanCountBuffer, _lastCounterPerVoxelBuffer);

            if (init)
            {
                int[] data = new int[_usedVoxelListInverseBuffer.count];

                //GetDataFromTex(_usedVoxelListInverseBuffer, ref data);
                _usedVoxelListInverseBuffer.GetData(data);

                int size = math.max(1, data[0]);
                if (_clothSim._debugMode) Debug.Log("<color=blue>CD: </color>sData Size " + size);

                _voxelBufferData = new sData[(int)(size * _clothSim._bufferScale)];
                _voxelBufferData2 = new sData[(int)(size * _clothSim._bufferScale)];

                if (_voxelBufferData.Length == 0)
                {
                    Debug.Log("<color=blue>CD: </color><color=red>" + _clothSim.name + " has voxelBufferData.Length == 0! Buffers might not be released correctly! Restart Unity or try the ReleaseMode! Did you forget to add a mesh object?</color>");
                    return;
                }
                _voxelDataBuffer = new ComputeBuffer(_voxelBufferData.Length, sizeof(float) * 12);
                _voxelDataBuffer.SetData(_voxelBufferData);
                _voxelDataBuffer2 = new ComputeBuffer(_voxelBufferData2.Length, sizeof(float) * 12);
                _voxelDataBuffer2.SetData(_voxelBufferData2);
            }

            //Graphics.SetRenderTarget(_counterPerVoxelBuffer);
            //GL.Clear(false, true, Color.clear);
            _sphereFinderCS.SetBuffer(3, _usedVoxelListBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.Dispatch(3, _counterPerVoxelThread, 1, 1);

            int kernel = 8;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.Dispatch(kernel, 1, 1, 1);

            kernel = 11;
            _sphereFinderCS.SetBuffer(11, _lastCounterPerVoxelBuffer_ID, _lastCounterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(11, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(11, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(11, _voxelDataBuffer_ID, _voxelDataBuffer);
            _sphereFinderCS.Dispatch(11, _numGroups_sphereData, 1, 1);


        }

        private void UpdateTotalCountAndClear(ComputeBuffer cpvBuffer)
        {
            if (!_useDirectDispatch)
            {
                _sphereFinderCS.SetBuffer(2, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
                _sphereFinderCS.SetBuffer(2, _counterPerVoxelBuffer_ID, cpvBuffer);
                _sphereFinderCS.Dispatch(2, 1, 1, 1);
            }
            else
            {
                _sphereFinderCS.SetBuffer(19, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
                _sphereFinderCS.SetBuffer(19, _counterPerVoxelBuffer_ID, cpvBuffer);
                _sphereFinderCS.Dispatch(19, 1, 1, 1);
            }
            _sphereFinderCS.SetBuffer(3, _usedVoxelListBuffer_ID, _usedVoxelListBuffer);
            _sphereFinderCS.Dispatch(3, _usedVoxelListBufferDispatch, 1, 1);

            //Graphics.SetRenderTarget(_usedVoxelListBuffer);
            //GL.Clear(false, true, Color.clear);

        }

        private void VoxelSubdiv(int level, int grid, float4 cubeMinVec)
        {
            //_sphereFinderCS.SetInt("_level", level);
            _sphereFinderCS.SetInt(_grid_ID, grid);
            _sphereFinderCS.SetFloat(_scaled_ID, 1.0f / (float)grid);
            _sphereFinderCS.SetVector(_cubeMinVec_ID, cubeMinVec);

            int kernel = 1;
            _sphereFinderCS.SetBuffer(kernel, _lastCounterPerVoxelBuffer_ID, _lastCounterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBufferR_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, _voxelDataBuffer_ID, _voxelDataBuffer);
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer2_ID, _usedVoxelListInverseBuffer2);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer2_ID, _counterPerVoxelBuffer2);

            if (!_useDirectDispatch)
            {
                _sphereFinderCS.DispatchIndirect(kernel, _argsBuffer);
            }
            else
            {
                var dispatch = _sphereDataBuffer.count.GetComputeShaderThreads(256);
                _sphereFinderCS.Dispatch(kernel, dispatch, 1, 1);
            }
            GetBlockSizeForScan(_usedVoxelListInverseBuffer2, _counterPerVoxelBuffer2);
            ScanBuffer(_scanCountBuffer, _lastCounterPerVoxelBuffer2);

            //Graphics.SetRenderTarget(_counterPerVoxelBuffer2);
            //GL.Clear(false, true, Color.clear);
            _sphereFinderCS.SetBuffer(3, _usedVoxelListBuffer_ID, _counterPerVoxelBuffer2);
            _sphereFinderCS.Dispatch(3, _counterPerVoxelThread, 1, 1);

            kernel = 8;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBuffer_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.Dispatch(kernel, 1, 1, 1);

            kernel = 10;
            _sphereFinderCS.SetBuffer(kernel, _usedVoxelListInverseBufferR_ID, _usedVoxelListInverseBuffer);
            _sphereFinderCS.SetBuffer(kernel, _lastCounterPerVoxelBuffer_ID, _lastCounterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, _lastCounterPerVoxelBuffer2_ID, _lastCounterPerVoxelBuffer2);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer_ID, _counterPerVoxelBuffer);
            _sphereFinderCS.SetBuffer(kernel, _counterPerVoxelBuffer2_ID, _counterPerVoxelBuffer2);
            _sphereFinderCS.SetBuffer(kernel, _voxelDataBuffer_ID, _voxelDataBuffer);
            _sphereFinderCS.SetBuffer(kernel, _voxelDataBuffer2_ID, _voxelDataBuffer2);

            if (!_useDirectDispatch)
            {
                _sphereFinderCS.DispatchIndirect(kernel, _argsBuffer);
            }
            else
            {
                var dispatch = _sphereDataBuffer.count.GetComputeShaderThreads(256);
                _sphereFinderCS.Dispatch(kernel, dispatch, 1, 1);
            }

        }

        private void UpdateParticles()
        {
            _lastParticleSum = 0;
            int lastTrisSum = 0;
            MonoBehaviour behaviour = null;

            int meshLength = _meshObjects.Length;
            for (int i = 0; i < meshLength; i++)
            {
                //_clothSolver.SetInt(_lastParticleSum_ID, _lastParticleSum);
                _sphereFinderCS.SetInt(_lastParticleSum_ID, _lastParticleSum);

                var meshObject = _meshObjects[i];

                if (meshObject.GetComponent<GPUClothDynamics>().ExistsAndEnabled(out behaviour))
                {
                    var cloth = behaviour as GPUClothDynamics;
                    bool useSelfTris = i == 0 && _clothSim._selfCollisionTriangles;
                    var vertsLength = _vertexCounts[i];
                    _sphereFinderCS.SetInt(_vertsLength_ID, vertsLength);
                    _sphereFinderCS.SetMatrix(_localToWorldMatrix_ID, meshObject.localToWorldMatrix);
                    int kernel = useSelfTris ? _updateAllParticleTrisKernel : _updateAllParticlesKernel;
                    _sphereFinderCS.SetBuffer(kernel, _trisData_ID, _selfTrisDataBuffer);
                    _sphereFinderCS.SetBuffer(kernel, _positionbuffer_ID, cloth._objBuffers[0].positionsBuffer);
                    _sphereFinderCS.SetBuffer(kernel, _sphereDataBufferRW_ID, _sphereDataBuffer);
                    _sphereFinderCS.Dispatch(kernel, vertsLength.GetComputeShaderThreads(256), 1, 1);
                    /*if (i < meshLength - 1)*/ _lastParticleSum += vertsLength;
                }
                else if (meshObject.GetComponent<DualQuaternionSkinner>().ExistsAndEnabled(out behaviour))
                {
                    var dqs = behaviour as DualQuaternionSkinner;
                    int length = _vertexCounts[i];// _vertexCounts[_vertexCounts.Length - 1];
                    int kernelIndex = _useTrisMesh ? 6 : 14;
                    _sphereFinderCS.SetInt("_lastTrisSum", lastTrisSum);
                    _sphereFinderCS.SetFloat(_normalScale_ID, _useTrisMesh ? _clothSim._triangleNormalScale : _clothSim._vertexNormalScale);
                    _sphereFinderCS.SetInt(_numClothParticles_ID, _clothSim._numParticles);
                    _sphereFinderCS.SetInt(_meshTrisLength_ID, length);
                    _sphereFinderCS.SetInt(_skinned_tex_width_ID, dqs._textureWidth);
                    _sphereFinderCS.SetMatrix(_meshMatrix_ID, meshObject.localToWorldMatrix);
                    _sphereFinderCS.SetTexture(kernelIndex, _skinned_data_1_ID, dqs._rtSkinnedData_1);
                    _sphereFinderCS.SetTexture(kernelIndex, _skinned_data_2_ID, dqs._rtSkinnedData_2);
                    if (_useTrisMesh) _sphereFinderCS.SetBuffer(kernelIndex, _trisData_ID, _trisDataBuffer);
                    _sphereFinderCS.SetBuffer(kernelIndex, _sphereDataBufferRW_ID, _sphereDataBuffer);
                    _sphereFinderCS.Dispatch(kernelIndex, length.GetComputeShaderThreads(256), 1, 1);
                    /*if (i < meshLength - 1)*/ _lastParticleSum += length;
                    lastTrisSum += length;
                }
                else if (meshObject.GetComponent<GPUSkinning>().ExistsAndEnabled(out behaviour) || meshObject.GetComponent<GPUMesh>().ExistsAndEnabled(out behaviour))
                {
                    var meshVertsOut = behaviour.GetType() == typeof(GPUSkinning) ? (behaviour as GPUSkinning)._meshVertsOut : (behaviour as GPUMesh)._meshVertsOut;
                    if (meshVertsOut != null)
                    {
                        _sphereFinderCS.SetInt("_lastTrisSum", lastTrisSum);

                        bool morph = false;
                        if (meshObject.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                            morph = true;

                        int length = _vertexCounts[i];// _vertexCounts[_vertexCounts.Length - 1];
                        int kernelIndex = morph ? (_useTrisMesh ? 21 : 22) : (_useTrisMesh ? 7 : 15);
                        _sphereFinderCS.SetFloat(_normalScale_ID, _useTrisMesh ? _clothSim._triangleNormalScale : _clothSim._vertexNormalScale);
                        _sphereFinderCS.SetInt(_meshTrisLength_ID, length);
                        _sphereFinderCS.SetMatrix(_meshMatrix_ID, meshObject.localToWorldMatrix);
                        _sphereFinderCS.SetBuffer(kernelIndex, _meshVertsOut_ID, meshVertsOut);

                        if (morph)
                        {
                            var blendShapes = (GPUBlendShapes)monoMorph;
                            _sphereFinderCS.SetInt(_rtArrayWidthID, blendShapes._rtArrayCombined.width);
                            _sphereFinderCS.SetTexture(kernelIndex, _rtArrayID, blendShapes._rtArrayCombined);
                        }

                        if (_useTrisMesh) _sphereFinderCS.SetBuffer(kernelIndex, _trisData_ID, _trisDataBuffer);
                        _sphereFinderCS.SetBuffer(kernelIndex, _sphereDataBufferRW_ID, _sphereDataBuffer);
                        _sphereFinderCS.Dispatch(kernelIndex, length.GetComputeShaderThreads(256), 1, 1);
                        /*if (i < meshLength - 1)*/ _lastParticleSum += length;
                        lastTrisSum += length;
                    }
                }
                else if (meshObject.GetComponent<AutomaticBoneSpheres>().ExistsAndEnabled(out behaviour))
                {
                    var autoSpheres = behaviour as AutomaticBoneSpheres;
                    int length = autoSpheres._spheresBuffer.count;
                    _sphereFinderCS.SetInt(_numAutoSpheres_ID, length);
                    _sphereFinderCS.SetFloat(_autoSphereSize_ID, _clothSim._autoSphereScale);
                    _sphereFinderCS.SetBuffer(_autoSpheresKernel, _autoBonesBuffer_ID, autoSpheres._bonesBuffer);
                    _sphereFinderCS.SetBuffer(_autoSpheresKernel, _autoSphereBuffer_ID, autoSpheres._spheresBuffer);
                    _sphereFinderCS.SetBuffer(_autoSpheresKernel, _sphereDataBufferRW_ID, _sphereDataBuffer);
                    _sphereFinderCS.Dispatch(_autoSpheresKernel, length.GetComputeShaderThreads(8), 1, 1);
                    /*if (i < meshLength - 1)*/ _lastParticleSum += length;
                }

            }
        }

        internal void CollisionPointsUpdate()
        {
            if (_clothSim != null && _clothSim._debugMode) InitBuffers();
            UpdateParticles();
            GPUCollisionMethod();
        }

        internal void Update()
        {
            if (_renderDebugPoints && _indexBuffer != null)
            {
                //if(_clothSim._cullingLights!=null && _clothSim._cullingLights.Length > 0)
                //{
                //	_debugMat.SetVector("_lightDir", _clothSim._cullingLights[0].transform.forward);
                //}
                _debugMat.SetFloat("_scale", 2 * _debugVertexScale * (_useTrisMesh ? _clothSim._triangleScale : _useAutoSpheresOnly ? 1.0f : _clothSim._vertexScale));
                _debugMat.SetInt("_vertexCount", _debugMesh.vertexCount);
                _debugMat.SetBuffer("_vertexBuffer", debugVertexBuffer);
                _debugMat.SetBuffer("_normalBuffer", debugNormalBuffer);
                _debugMat.SetBuffer("_meshPosBuffer", _sphereDataBuffer);
                Graphics.DrawProcedural(_debugMat, _b, MeshTopology.Triangles, _indexBuffer, _indexBuffer.count, 0, null, _matBlock);
            }
        }

        void StartDebug(int pointsCount)
        {
            if (_renderDebugPoints)
            {
                if (_debugMesh == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _debugMesh = go.GetComponent<MeshFilter>().mesh;
                    UnityEngine.Object.Destroy(go);
                }

                if (_debugMat == null)
                {
                    _debugMat = new Material(Shader.Find("ClothDynamics/DebugUnlitShader"));
                }

                _matBlock = new MaterialPropertyBlock();

                int debugPointsCount = pointsCount;
                int[] tris = new int[_debugMesh.triangles.Length * debugPointsCount];
                int[] meshStartIndex = new int[debugPointsCount];
                _meshPos = new Vector4[debugPointsCount];
                Vector4 pos = new Vector3(1, 1, 1);
                pos.w = _debugVertexScale;
                int vertexCount = 0;

                var meshTris = _debugMesh.triangles;
                for (int nx = 0; nx < debugPointsCount; nx++)
                {
                    int startIndex = _debugMesh.triangles.Length * nx;
                    int maxCount = 0;
                    for (int i = 0; i < meshTris.Length; i++)
                    {
                        tris[startIndex + i] = _debugMesh.vertexCount * nx + meshTris[i];
                        maxCount = Mathf.Max(maxCount, meshTris[i]);
                    }
                    vertexCount += maxCount;
                    meshStartIndex[nx] = startIndex;
                    _meshPos[nx] = pos;
                }

                _indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, tris.Length, sizeof(int));
                _indexBuffer.SetData(tris);

                debugVertexBuffer = new ComputeBuffer(_debugMesh.vertexCount, sizeof(float) * 3);
                debugVertexBuffer.SetData(_debugMesh.vertices);

                debugNormalBuffer = new ComputeBuffer(_debugMesh.vertexCount, sizeof(float) * 3);
                debugNormalBuffer.SetData(_debugMesh.normals);

                _b = new Bounds();
                _b.center = _clothSim.transform.position;// _mesh.bounds.center;
                _b.size = _debugMesh.bounds.size * debugPointsCount * _debugVertexScale;
            }
        }

        internal void OnDrawGizmos(Transform clothObj)
        {
            if (_renderDebugPoints)
            {
                if (_voxelBufferData2 != null && _voxelBufferData2.Length > 0 && _debugObject != null)
                {
                    //GetDataFromTex(_usedVoxelListBuffer, ref _usedVoxelList);
                    _usedVoxelListBuffer.GetData(_usedVoxelList);

                    //GetDataFromTex(_counterPerVoxelBuffer2, ref _counterPerVoxel2);
                    _counterPerVoxelBuffer2.GetData(_counterPerVoxel2);

                    _lastCounterPerVoxelBuffer2.GetData(_lastCounterPerVoxel2);
                    _voxelDataBuffer2.GetData(_voxelBufferData2);

                    //Debug.Log("_usedVoxelList " + _usedVoxelList[0] + ", " + _usedVoxelList[1] + ", " + _usedVoxelList[2]);
                    //Debug.Log("_counterPerVoxel2 " + _counterPerVoxel2[0] + ", " + _counterPerVoxel2[1] + ", " + _counterPerVoxel2[2]);
                    //Debug.Log("_lastCounterPerVoxel2 " + _lastCounterPerVoxel2[0] + ", " + _lastCounterPerVoxel2[1] + ", " + _lastCounterPerVoxel2[2]);
                    //Debug.Log("_voxelBufferData2 " + _voxelBufferData2[0].pr.xyz + ", " + _voxelBufferData2[1].pr.xyz + ", " + _voxelBufferData2[2].pr.xyz);

                    //var cube = _voxelCube;
                    int grid = _gridCount;// _voxelCubeGridSize;
                    float voxelSize = _clothSim._voxelCubeScale / ((float)grid);
                    float voxelExtend = voxelSize * 0.5f;
                    Vector3 cubeMinPos = _clothSim._voxelCubePos - Vector3.one * _clothSim._voxelCubeScale * 0.5f;

                    Vector3 voxelPos = (_debugObject.position - cubeMinPos) * grid / _clothSim._voxelCubeScale;
                    voxelPos.x = Mathf.Clamp((int)voxelPos.x, 0, grid - 1);
                    voxelPos.y = Mathf.Clamp((int)voxelPos.y, 0, grid - 1);
                    voxelPos.z = Mathf.Clamp((int)voxelPos.z, 0, grid - 1);
                    Vector3Int voxelPosInt = new Vector3Int((int)voxelPos.x, (int)voxelPos.y, (int)voxelPos.z);
                    int voxelIndex = voxelPosInt.x + voxelPosInt.y * grid + voxelPosInt.z * grid * grid;

                    Vector3 center = cubeMinPos + (Vector3)voxelPosInt * voxelSize + Vector3.one * voxelExtend;
                    Vector3 size = Vector3.one * voxelSize;
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(center, size);

                    if (voxelIndex >= 0 && voxelIndex < grid * grid * grid && _usedVoxelList[voxelIndex + 1] > 0)
                    {
                        int dataIndex = _usedVoxelList[voxelIndex + 1] - 1;
                        var points = new Vector3[_counterPerVoxel2[voxelIndex] * 3];

                        //Debug.Log("dataIndex " + dataIndex);
                        //Debug.Log("points " + points[0] + ", " + points[1] + ", " + points[2]);

                        for (int i = 0; i < _counterPerVoxel2[voxelIndex]; i++)
                        {
                            //int index = (lastDataIndex * 64 + vIndex) * _newSpheresPerVoxel + i;
                            int index = _lastCounterPerVoxel2[dataIndex] + i;
                            var sphere = _voxelBufferData2[index];
                            if (_useTrisMesh && sphere.nId.w >= _selfAndAutoSpheresCount)
                            {
                                Gizmos.color = Color.magenta;
                                Gizmos.DrawLine(sphere.pr.xyz, sphere.nId.xyz);
                                Gizmos.DrawLine(sphere.nId.xyz, sphere.temp.xyz);
                                Gizmos.DrawLine(sphere.temp.xyz, sphere.pr.xyz);

                                Gizmos.color = Color.yellow;
                                var pr = ClosestPointToTri(sphere.pr.xyz, sphere.nId.xyz, sphere.temp.xyz, _debugObject.position);
                                Gizmos.DrawWireSphere(pr, sphere.pr.w);
                            }
                            else
                            {
                                Gizmos.color = Color.yellow;
                                Gizmos.DrawWireSphere(sphere.pr.xyz, sphere.pr.w);// index >= vertexCounts[0] ? sphere.w * _clothSim._collisionSize : sphere.w * _clothSim._selfCollisionScale);
                            }
                        }
                    }
                }

                //if (_sphereData != null)
                //    for (int i = 0; i < _sphereData.Length; i++)
                //    {
                //        sData sphere = _sphereData[i];
                //        Gizmos.DrawWireSphere(sphere.pr.xyz, sphere.pr.w);
                //    }
            }
        }

        internal void DestroyBuffers()
        {
            if (_indexBuffer != null) _indexBuffer.Release();
            _indexBuffer = null;

            _scanCountBuffer.ClearBuffer();
            debugVertexBuffer.ClearBuffer();
            debugNormalBuffer.ClearBuffer();
            _sphereDataBuffer.ClearBuffer();
            _voxelDataBuffer.ClearBuffer();
            _usedVoxelListBuffer.ClearBuffer();
            _counterPerVoxelBuffer.ClearBuffer();
            _usedVoxelListInverseBuffer.ClearBuffer();
            _usedVoxelListInverseBuffer2.ClearBuffer();
            _counterPerVoxelBuffer2.ClearBuffer();
            _voxelDataBuffer2.ClearBuffer();
            _argsBuffer.ClearBuffer();
            _argsBuffer2.ClearBuffer();
            _lastVoxelCountBuffer.ClearBuffer();
            _trisDataBuffer.ClearBuffer();
            _selfTrisDataBuffer.ClearBuffer();
            _lastCounterPerVoxelBuffer.ClearBuffer();
            _lastCounterPerVoxelBuffer2.ClearBuffer();
        }

        void ScanBuffer(ComputeBuffer scanCountBuffer, ComputeBuffer resultBuffer/*, bool exclusive = false*/)
        {
            int threadsPerGroup = 512;
            int count = scanCountBuffer.count;
            int threadGroupCount = 1;

            int scanInBucketKernel = _scanInBucketInclusiveKernel;// exclusive ? _scanInBucketExclusiveKernel : _scanInBucketInclusiveKernel;
            _scanCS.SetBuffer(scanInBucketKernel, _inputPropId, scanCountBuffer);
            _scanCS.SetBuffer(scanInBucketKernel, _resultPropId, resultBuffer);
            if (!_useDirectDispatch)
            {
                _scanCS.DispatchIndirect(scanInBucketKernel, _argsBuffer2);
            }
            else
            {
                threadGroupCount = count.GetComputeShaderThreads(threadsPerGroup);
                _scanCS.Dispatch(scanInBucketKernel, threadGroupCount, 1, 1);
            }

            _scanCS.SetBuffer(_scanBucketResultKernel, _inputPropId, resultBuffer);
            _scanCS.SetBuffer(_scanBucketResultKernel, _resultPropId, scanCountBuffer);
            _scanCS.Dispatch(_scanBucketResultKernel, 1, 1, 1);

            _scanCS.SetBuffer(_scanAddBucketResultKernel, _inputPropId, scanCountBuffer);
            _scanCS.SetBuffer(_scanAddBucketResultKernel, _resultPropId, resultBuffer);
            if (!_useDirectDispatch)
            {
                _scanCS.DispatchIndirect(_scanAddBucketResultKernel, _argsBuffer2);
            }
            else
            {
                _scanCS.Dispatch(_scanAddBucketResultKernel, threadGroupCount, 1, 1);
            }
        }

        float3 Rotate(float4 q, float3 v)
        {
            float3 t = 2.0f * math.cross(q.xyz, v);
            return v + q.w * t + math.cross(q.xyz, t); //changed q.w to -q.w;
        }

        private void CalcDistConnectionsForMask(Transform meshObj, Mesh pMesh, int lastVertsCount, bool tris = false)
        {
            //var pMesh = _mesh;
            Vector3[] vertices = pMesh.vertices;
            int[] faces = pMesh.triangles;
            int lastCount = 0;// verts.Count;
            List<Vector2> connectionInfo = new List<Vector2>();
            List<int> connectedVerts = new List<int>();
            Dictionary<Vector3, List<int>> dictTris = new Dictionary<Vector3, List<int>>();

            List<Vector3> normals = new List<Vector3>();

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> uniqueVerts = new List<Vector3>();
            Dictionary<Vector3, int> dictVertsIndex = new Dictionary<Vector3, int>();
            int index = 0;
            int globali = 0;
            int offset = 0;
            Vector3[] norm = pMesh.normals;
            Vector2[] uv = pMesh.uv;
            List<Vector2> uvsTemp = new List<Vector2>();
            List<int> mapVertsBack = new List<int>();

            int vertexCount = pMesh.vertexCount;

            for (int i = 0; i < vertexCount; i++)
            {
                verts.Add(vertices[i]);

                if (dictVertsIndex.TryGetValue(vertices[i], out index))
                {
                    mapVertsBack.Add(index);
                    offset++;
                }
                else
                {
                    dictVertsIndex.Add(vertices[i], uniqueVerts.Count);
                    uniqueVerts.Add(vertices[i]);
                    normals.Add(norm[i]);
                    if (i < uv.Length) uvsTemp.Add(uv[i]);
                    mapVertsBack.Add(globali - offset);
                }
                globali++;
            }
            for (int f = 0; f < faces.Length; f += 3)
            {
                if (dictTris.ContainsKey(vertices[faces[f]]))
                {
                    var list = dictTris[vertices[faces[f]]];
                    list.Add(mapVertsBack[lastCount + faces[f + 1]]);
                    list.Add(mapVertsBack[lastCount + faces[f + 2]]);
                }
                else
                {
                    dictTris.Add(vertices[faces[f]], new List<int>(new[] {
                                                mapVertsBack  [lastCount + faces [f + 1]],
                                                mapVertsBack  [lastCount + faces [f + 2]]
                                            }));
                }
                if (dictTris.ContainsKey(vertices[faces[f + 1]]))
                {
                    var list = dictTris[vertices[faces[f + 1]]];
                    list.Add(mapVertsBack[lastCount + faces[f + 2]]);
                    list.Add(mapVertsBack[lastCount + faces[f]]);
                }
                else
                {
                    dictTris.Add(vertices[faces[f + 1]], new List<int>(new[] {
                                                mapVertsBack  [lastCount + faces [f + 2]],
                                                mapVertsBack  [lastCount + faces [f]]
                                            }));
                }
                if (dictTris.ContainsKey(vertices[faces[f + 2]]))
                {
                    var list = dictTris[vertices[faces[f + 2]]];
                    list.Add(mapVertsBack[lastCount + faces[f]]);
                    list.Add(mapVertsBack[lastCount + faces[f + 1]]);
                }
                else
                {
                    dictTris.Add(vertices[faces[f + 2]], new List<int>(new[] {
                                                mapVertsBack  [lastCount + faces [f]],
                                                mapVertsBack  [lastCount + faces [f + 1]]
                                            }));
                }
            }

            int currentNumV = uniqueVerts.Count;
            //Debug.Log("currentNumV: " + currentNumV);
            //Debug.Log("numParticles: " + vertices.Length);

            var meshData = meshObj.GetComponent<GPUMeshData>();
            var customScale = meshData._vertexCollisionScale;

            //Vector3[] vd = uniqueVerts.ToArray();
            float[] maskList = new float[vertexCount];
            int maxVertexConnection = 0;
            for (int v = 0; v < vertexCount; v++)
            {
                int n = mapVertsBack[v];
                var list = dictTris[uniqueVerts[n]];
                int start = connectedVerts.Count;
                float dist = float.MinValue;
                float average = 0;
                int count = list.Count;
                int counter = 0;
                for (int i = 0; i < count; i++)
                {
                    connectedVerts.Add(list[i]);
                    float d = Vector3.Distance(uniqueVerts[n], uniqueVerts[list[i]]);
                    if (n != list[i] || d > float.Epsilon)
                    {
                        dist = Mathf.Max(dist, d);
                        average += d;
                        counter++;
                    }
                }
                average /= Mathf.Max(1.0f, (float)counter);
                dist = average;

                float mask = math.max(0.05f, dist * 2);
                index = lastVertsCount + v;
                maskList[v] = mask;
                if (!tris)
                {
                    _sphereData[index].pr.xyz = meshObj.TransformPoint(uniqueVerts[n]) - normals[n] * _clothSim._vertexNormalScale * Mathf.Max(0.05f, mask * 5);
                    _sphereData[index].pr.w = customScale * (_unifiedSphereSize > 0 ? _unifiedSphereSize : mask);
                }
                //_sphereData[index].nId.w = index;

                int end = connectedVerts.Count;
                maxVertexConnection = Mathf.Max(maxVertexConnection, end - start);
                connectionInfo.Add(new Vector2(start, end));
            }

            if (tris)
            {
                int length = faces.Length / 3;
                for (int i = 0; i < length; i++)
                {
                    index = lastVertsCount + i;
                    var mask = _sphereData[index].pr.w = _unifiedSphereSize > 0 ? _unifiedSphereSize : math.max(maskList[faces[i * 3 + 0]], math.max(maskList[faces[i * 3 + 1]], maskList[faces[i * 3 + 2]]));
                    _sphereData[index].pr.xyz = meshObj.TransformPoint(vertices[faces[i * 3 + 0]]) - norm[faces[i * 3 + 0]] * _clothSim._vertexNormalScale * Mathf.Max(0.05f, mask * 5);
                }
            }
        }

        private static void SwitchArrays<Type>(ref Type[] bufferA, ref Type[] bufferB)
        {
            var temp = bufferA;
            bufferA = bufferB;
            bufferB = temp;
        }

        private static void SwitchBuffers<Type>(ref Type bufferA, ref Type bufferB)
        {
            var temp = bufferA;
            bufferA = bufferB;
            bufferB = temp;
        }

        //private static void SetMeshReadable(Mesh mesh)
        //{
        //    #if UNITY_EDITOR
        //    var filePath = AssetDatabase.GetAssetPath(mesh);
        //    filePath = filePath.Replace("/", "\\");
        //    string fileText = File.ReadAllText(filePath);
        //    fileText = fileText.Replace("m_IsReadable: 0", "m_IsReadable: 1");
        //    File.WriteAllText(filePath, fileText);
        //    AssetDatabase.Refresh();
        //    #endif
        //}

        float dbox(float3 p, float b)
        {
            return math.length(math.max(math.abs(p) - b, 0.0f));
        }

        float dot2(in float3 v) { return math.dot(v, v); }

        //float udTriangle(in float3 v1, in float3 v2, in float3 v3, in float3 p)
        //{
        //    float3 v21 = v2 - v1; float3 p1 = p - v1;
        //    float3 v32 = v3 - v2; float3 p2 = p - v2;
        //    float3 v13 = v1 - v3; float3 p3 = p - v3;
        //    float3 nor = math.cross(v21, v13);

        //    return math.sqrt((math.sign(math.dot(math.cross(v21, nor), p1)) +
        //                      math.sign(math.dot(math.cross(v32, nor), p2)) +
        //                      math.sign(math.dot(math.cross(v13, nor), p3)) < 2.0f)
        //                      ?
        //                       math.min(math.min(
        //                      dot2(v21 * math.clamp(math.dot(v21, p1) / dot2(v21), 0.0f, 1.0f) - p1),
        //                      dot2(v32 * math.clamp(math.dot(v32, p2) / dot2(v32), 0.0f, 1.0f) - p2)),
        //                      dot2(v13 * math.clamp(math.dot(v13, p3) / dot2(v13), 0.0f, 1.0f) - p3))
        //                      :
        //                       math.dot(nor, p1) * math.dot(nor, p1) / dot2(nor));
        //}

        bool PointInTriangle(float3 p, float3 pA, float3 pB, float3 pC)
        {
            float3 a = pA - p;
            float3 b = pB - p;
            float3 c = pC - p;

            float3 normPBC = math.cross(b, c); // Normal of PBC (u)
            float3 normPCA = math.cross(c, a); // Normal of PCA (v)
            float3 normPAB = math.cross(a, b); // Normal of PAB (w)

            if (math.dot(normPBC, normPCA) < 0.0f)
            {
                return false;
            }
            else if (math.dot(normPBC, normPAB) < 0.0f)
            {
                return false;
            }
            return true;
        }

        float3 ClosestPointToLine(float3 start, float3 end, float3 pos)
        {
            //float3 lVec = end - start;
            //float t = math.dot(pos - start, lVec) / math.dot(lVec, lVec);
            //t = math.max(t, 0.0f);
            //t = math.min(t, 1.0f);
            //return start + lVec * t;
            float3 lVec = end - start;
            float t = math.clamp(math.dot(pos - start, lVec) / (epsilon + dot2(lVec)), 0.0f, 1.0f);
            return start + lVec * t;
        }

        float3 ClosestPointToTri(float3 pA, float3 pB, float3 pC, float3 pos)
        {
            float3 normal = math.normalize(math.cross(pB - pA, pC - pA));
            float surfaceDist = math.dot(normal, pos - pA);
            pos = pos - normal * surfaceDist;

            if (PointInTriangle(pos, pA, pB, pC))
            {
                return pos;
            }

            float3 c1 = ClosestPointToLine(pA, pB, pos);
            float3 c2 = ClosestPointToLine(pB, pC, pos);
            float3 c3 = ClosestPointToLine(pC, pA, pos);

            float mag1 = dot2(pos - c1);
            float mag2 = dot2(pos - c2);
            float mag3 = dot2(pos - c3);

            float minValue = math.min(mag1, mag2);
            minValue = math.min(minValue, mag3);

            if (minValue == mag1)
            {
                return c1;
            }
            else if (minValue == mag2)
            {
                return c2;
            }
            return c3;
        }

        public class ComponentComparer : IComparer<Transform>
        {
            public int Compare(Transform first, Transform second)
            {
                if (first != null && second != null)
                {
                    bool clothFirst = first.GetComponent<GPUClothDynamics>() != null;
                    bool clothSecond = second.GetComponent<GPUClothDynamics>() != null;
                    bool skinningFirst = first.GetComponent<DualQuaternionSkinner>() != null || first.GetComponent<GPUSkinning>() != null;
                    bool skinningSecond = second.GetComponent<DualQuaternionSkinner>() != null || second.GetComponent<GPUSkinning>() != null;

                    if (clothFirst)
                        return -1;

                    if (clothSecond)
                        return 1;

                    if (skinningFirst)
                        return 1;

                    if (skinningSecond)
                        return -1;

                    if (first.GetComponent<AutomaticBoneSpheres>() != null && !skinningSecond)
                        return 1;

                    if (second.GetComponent<AutomaticBoneSpheres>() != null && skinningFirst)
                        return 1;

                    if (second.GetComponent<MeshFilter>() != null)
                        return 1;

                    return 0;
                }

                if (first == null && second == null)
                {
                    // We can't compare any properties, so they are essentially equal.
                    return 0;
                }

                if (first != null)
                {
                    // Only the first instance is not null, so prefer that.
                    return -1;
                }

                // Only the second instance is not null, so prefer that.
                return 1;
            }
        }
    }

#if UNITY_EDITOR
    public class ReadOnlyAttribute : PropertyAttribute
    {

    }

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property,
                                                GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
}

