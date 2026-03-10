using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace ClothDynamics
{
    //[DefaultExecutionOrder(15150)] //When using Final IK
    [System.Serializable]
    [DefaultExecutionOrder(15200)] //When using Final IK
    public class CollisionMeshesGPU
    {
        [Tooltip("List of all body meshes, with or without skinning, will be added automatically if only one CD2 solver exists.")]
        public Transform[] _meshObjects;

        private int _lastParticleSum;
        private ComputeShader _cs;
        private GPUClothDynamicsV2 _clothSim;
        internal int[] _vertexCounts;
        internal bool _useTrisMesh = false;
        //private int _autoSpheresKernel;
        private int _selfAndAutoSpheresCount = 0;

        private static readonly int _selfAndAutoSpheresCount_ID = Shader.PropertyToID("_selfAndAutoSpheresCount");
        private static readonly int _lastParticleSum_ID = Shader.PropertyToID("_lastParticleSum");
        private static readonly int _trisData_ID = Shader.PropertyToID("_trisData");
        private static readonly int _sphereDataBufferRW_ID = Shader.PropertyToID("_sphereDataBufferRW");
        private static readonly int _normalScale_ID = Shader.PropertyToID("_normalScale");
        private static readonly int _numClothParticles_ID = Shader.PropertyToID("_numClothParticles");
        private static readonly int _skinned_tex_width_ID = Shader.PropertyToID("_skinned_tex_width");
        private static readonly int _meshMatrix_ID = Shader.PropertyToID("_meshMatrix");
        private static readonly int _skinned_data_1_ID = Shader.PropertyToID("_skinned_data_1");
        private static readonly int _skinned_data_2_ID = Shader.PropertyToID("_skinned_data_2");
        private static readonly int _meshTrisLength_ID = Shader.PropertyToID("_meshTrisLength");
        private static readonly int _meshVertsOut_ID = Shader.PropertyToID("_meshVertsOut");
        private static readonly int _rtArrayID = Shader.PropertyToID("_rtArray");
        private static readonly int _rtArrayWidthID = Shader.PropertyToID("_rtArrayWidth");
        private static readonly int _numAutoSpheres_ID = Shader.PropertyToID("_numAutoSpheres");
        private static readonly int _autoSphereSize_ID = Shader.PropertyToID("_autoSphereSize");
        private static readonly int _autoBonesBuffer_ID = Shader.PropertyToID("_autoBonesBuffer");
        private static readonly int _autoSphereBuffer_ID = Shader.PropertyToID("_autoSphereBuffer");

        public ComputeBuffer _sphereDataBuffer;
        //internal ComputeBuffer _spherePosPredicted;
        internal ComputeBuffer _trisDataBuffer;

        private GraphicsBuffer _indexBuffer;
        private ComputeBuffer debugVertexBuffer;
        private ComputeBuffer debugNormalBuffer;

        internal struct sData
        {
            internal float4 pr;
            internal float4 nId;
            internal float4 temp;
            internal float mask;
            internal float3 color;
        }
        private sData[] _sphereData;

        [Tooltip("The the minimal particles collision size. Call \"mask\", because you can affect this value by the red color channel of a PaintObject and mask out the unused particles with black.")]
        private float _clothMaskValue = 1.0f;
        [Tooltip("This is an experimental feature. If you set it higher than zero, it will replace the collision sphere size of all mesh objects. (Default = 0)")]
        private float _unifiedSphereSize = 1.0f;
        [Tooltip("This renders the debug points of the cloth sim. Turn this on before play mode starts.")]
        [SerializeField] public bool _debugMeshPoints = false;
        [Tooltip("This is set automatically and renders the debug mesh with this material.")]
        /*[SerializeField] */
        private Material _debugMat;
        [Tooltip("This scales the Debug Points during runtime.")]
        [SerializeField] private float _debugVertexScale = 1.0f;
        [Tooltip("This is set automatically and used to display the particles as a mesh object (use a cube or a low res sphere).")]
        /*[SerializeField]*/
        private Mesh _debugMesh;
        private Bounds _b;
        private MaterialPropertyBlock _matBlock;
        internal int _numparticles = 0;
        //[SerializeField]
        private int innerVertexCount = 1;

        private int _initmeshObjectsLength = 0;

        public void AddMesh(GameObject go)
        {
            var list = _meshObjects.ToList();
            list.Add(go.transform);
            _meshObjects = list.ToArray();
            Extensions.CleanupList(ref _meshObjects);
            _clothSim.StartCoroutine(DelayedUpdate());
        }

        public void RemoveMesh(GameObject go)
        {
            var list = _meshObjects.ToList();
            list.Remove(go.transform);
            _meshObjects = list.ToArray();
            Extensions.CleanupList(ref _meshObjects);
            _clothSim.StartCoroutine(DelayedUpdate());
        }

        IEnumerator DelayedUpdate()
        {
            for (int i = 0; i < 10; i++) yield return null;
            _clothSim._solver.UpdateBuffers();
        }

        public void Init(GPUClothDynamicsV2 dynamics)
        {
            if (_debugMat == null)
            {
                _debugMat = new Material(Shader.Find("ClothDynamics/DebugUnlitShaderV2"));
            }

            _clothSim = dynamics;
            if (_cs == null)
            {
                //_sphereFinderCS = Resources.Load("Shaders/Compute/V2/CollisionMeshesGPU") as ComputeShader;
                _cs = GraphicsUtilities.LoadComputeShaderAt("Shaders/Compute/V2/CollisionMeshesGPU");
            }
            //_autoSpheresKernel = _cs.FindKernel("SetupAutoSpheres");

            var meshes = _meshObjects.ToList();
            if (!dynamics._solver._manualSetup && GPUClothDynamicsV2.FindObjectsOfType<GPUClothDynamicsV2>().Length < 2)
            {
                var skinners = GPUClothDynamicsV2.FindObjectsOfType<GPUSkinning>().Where(x => x.enabled && !x.GetComponent<ClothSkinningGPU>()).Select(x => x.transform);
                var staticMeshes = GPUClothDynamicsV2.FindObjectsOfType<GPUMesh>().Where(x => x.enabled && !x.GetComponent<ClothSkinningGPU>()).Select(x => x.transform);
                meshes.AddRange(skinners);
                meshes.AddRange(staticMeshes);
            }
            Extensions.CleanupList(ref meshes);
            _meshObjects = meshes.ToArray();

            if (_meshObjects == null || _meshObjects.Length == 0)
            {
                if (_sphereDataBuffer != null) _sphereDataBuffer.Release();
                _sphereDataBuffer = new ComputeBuffer(1, Marshal.SizeOf(new sData()));
                //_spherePosPredicted = new ComputeBuffer(1, sizeof(float) * 3);
                _initmeshObjectsLength = _meshObjects.Length;
                return;
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

                if ((meshObject.GetComponent<SkinnedMeshRenderer>() || meshObject.GetComponent<MeshFilter>()) && !meshObject.GetComponent<AutomaticBoneSpheres>() && !meshObject.GetComponent<GPUSkinning>() && !meshObject.GetComponent<DualQuaternionSkinner>() && !meshObject.GetComponent<ClothObjectGPU>())
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
                    (meshObject.GetComponent<DualQuaternionSkinner>() != null || meshObject.GetComponent<GPUSkinning>() != null) ?
                    (_useTrisMesh ? ((meshObject.GetComponent<SkinnedMeshRenderer>() ? meshObject.GetComponent<SkinnedMeshRenderer>().sharedMesh : meshObject.GetComponent<MeshFilter>().sharedMesh).triangles.Length / 3) : (meshObject.GetComponent<SkinnedMeshRenderer>() ? meshObject.GetComponent<SkinnedMeshRenderer>().sharedMesh : meshObject.GetComponent<MeshFilter>().sharedMesh).vertexCount)
                    //: meshObject.GetComponent<AutomaticBoneSpheres>() != null ? meshObject.GetComponent<AutomaticBoneSpheres>()._spheresBuffer.count
                    : meshObject.GetComponent<GPUMesh>() != null ? (_useTrisMesh ? (meshObject.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3) : meshObject.GetComponent<MeshFilter>().sharedMesh.vertexCount)
                    : 0;

                _vertexCounts[i] = vertexCount;
                _numparticles += vertexCount * innerVertexCount;
            }

            _sphereData = new sData[_numparticles];
            float clothMaskValue = _clothMaskValue; //Cloth mask

            for (int i = 0; i < _numparticles; i++)
            {
                _sphereData[i].mask = _unifiedSphereSize > 0 ? _unifiedSphereSize : clothMaskValue;
                _sphereData[i].nId.w = i;
            }

            List<int> trisList = new List<int>();

            int lastCount = 0;
            for (int m = 0; m < _meshObjects.Length; m++)
            {
                var meshObj = _meshObjects[m];

                if (meshObj.GetComponent<DualQuaternionSkinner>() || meshObj.GetComponent<GPUSkinning>() || meshObj.GetComponent<GPUMesh>())
                {
                    Mesh mesh = meshObj.GetComponent<GPUMesh>() ? meshObj.GetComponent<MeshFilter>().sharedMesh : (meshObj.GetComponent<SkinnedMeshRenderer>() ? meshObj.GetComponent<SkinnedMeshRenderer>().sharedMesh : meshObj.GetComponent<MeshFilter>().sharedMesh);
                    //SetMeshReadable(mesh);
                    var verts = mesh.vertices;
                    //var normals = mesh.normals;

                    for (int id = 0; id < innerVertexCount; id++)
                    {
                        var last = lastCount + id * _vertexCounts[m];
                        if (!_useTrisMesh || _unifiedSphereSize == 0)
                            CalcDistConnectionsForMask(meshObj, mesh, last, id, _useTrisMesh);

                        if (_useTrisMesh)
                        {
                            var tris = mesh.triangles;
                            trisList.AddRange(tris);

                            int length = tris.Length / 3;
                            for (int i = 0; i < length; i++)
                            {
                                int index = last + i;
                                _sphereData[index].pr.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 0]]);
                                _sphereData[index].nId.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 1]]);
                                _sphereData[index].temp.xyz = meshObj.TransformPoint(verts[tris[i * 3 + 2]]);
                            }
                        }
                    }
                    //_useTrisMesh = true;
                    //if (m < _meshObjects.Length - 1) _lastParticleSum += _vertexCounts[m];

                    if (_clothSim._debugEvents) Debug.Log("<color=blue>CD: </color><color=lime>" + meshObj.name + " is using " + (meshObj.GetComponent<GPUMesh>() ? "GPUMesh" : meshObj.GetComponent<GPUSkinnerBase>().GetType().ToString()) + " for collision</color>");

                }
                _lastParticleSums = new int[_meshObjects.Length];

                var maskPaintObject = meshObj.GetComponent<PaintObject>();
                if (meshObj.GetComponent<ClothObjectGPU>() == null && maskPaintObject != null) // m > 0 -> do not use cloth mask for self collision
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

                    float maxValue = 0;
                    bool exceedCount = false;
                    for (int n = 0; n < nextCount; n++)
                    {
                        int k = n;
                        float mask = clothMaskValue;
                        float blueMask = 0;
                        int index = _useTrisMesh ? tris[n * 3 + 0] : n;
                        if (index < vColors.Length)
                        {
                            mask = vColors[index].r;
                            blueMask = vColors[index].b;
                        }
                        for (int id = 0; id < innerVertexCount; id++)
                        {
                            var last = lastCount + id * _vertexCounts[m];
                            if (last + k < _sphereData.Length)
                            {
                                mask = mask > 0.5f ? _sphereData[last + k].mask : 0; // 50% cut off
                                _sphereData[last + k].mask = mask;
                                _sphereData[last + k].color = blueMask;
                            }
                            else exceedCount = true;
                        }
                        maxValue = math.max(mask, maxValue);
                    }
                    if (exceedCount) Debug.Log("<color=blue>CD: </color><color=red>" + meshObj.name + " is using PaintObject with wrong data! Cloth will not work! Update the PaintObject!</color>");

                    if (maxValue == 0)
                    {
                        Debug.Log("<color=blue>CD: </color><color=red>" + meshObj.name + " is using PaintObject only with black colors! Cloth will not work! Repaint or Remove PaintObject!</color>");
                        _clothSim.enabled = false;
                        return;
                    }
                    else
                        if (_clothSim._debugEvents) Debug.Log("<color=blue>CD: </color><color=lime>" + meshObj.name + " is using PaintObject</color>");

                }
                lastCount += _vertexCounts[m] * innerVertexCount;
            }

            if (_useTrisMesh)
            {
                //Debug.Log("trisList.Count " + trisList.Count);
                if (_trisDataBuffer != null) _trisDataBuffer.Release();
                _trisDataBuffer = new ComputeBuffer(trisList.Count, sizeof(int));
                _trisDataBuffer.SetData(trisList);
            }

            if (_sphereDataBuffer != null) _sphereDataBuffer.Release();
            _sphereDataBuffer = new ComputeBuffer(_sphereData.Length, Marshal.SizeOf(new sData()));
            _sphereDataBuffer.SetData(_sphereData);

            //_spherePosPredicted = new ComputeBuffer(_sphereData.Length, sizeof(float) * 3);
            //_spherePosPredicted.SetData(_sphereData.Select(d => d.pr.xyz).ToArray());

            _cs.SetInt(_selfAndAutoSpheresCount_ID, _selfAndAutoSpheresCount);

            _initmeshObjectsLength = _meshObjects.Length;
        }

        public void LateUpdate()
        {
            UpdateDebug();
        }

        private float _invDT = 1;
        internal int[] _lastParticleSums;
        public void UpdateParticles(float dt, bool localSetup = false)
        {
            if (_meshObjects != null)
            {
                if (_initmeshObjectsLength != _meshObjects.Length && _clothSim != null) Init(_clothSim);

                _lastParticleSum = 0;
                MonoBehaviour behaviour = null;

                int meshLength = _meshObjects.Length;

                Extensions.CleanupList(ref _meshObjects);

                for (int i = 0; i < meshLength; i++)
                {
                    //_clothSolver.SetInt(_lastParticleSum_ID, _lastParticleSum);
                    _cs.SetInt(_lastParticleSum_ID, _lastParticleSum);
                    _lastParticleSums[i] = _lastParticleSum;

                    var meshObject = _meshObjects[i];
                    if (meshObject == null) continue;

                    _invDT = 1.0f / math.max(float.Epsilon, dt);
                    _cs.SetFloat("_invDT", _invDT);

                    if (meshObject.GetComponent<DualQuaternionSkinner>().ExistsAndEnabled(out behaviour))
                    {
                        var dqs = behaviour as DualQuaternionSkinner;
                        int vertexCount = _vertexCounts[i];
                        int length = vertexCount * innerVertexCount;
                        int kernelIndex = _useTrisMesh ? 0 : 1;
                        _cs.SetInt("_vertexCount", vertexCount);
                        _cs.SetFloat(_normalScale_ID, _clothSim._globalSimParams._normalOffsetScale);
                        _cs.SetInt(_numClothParticles_ID, _clothSim._globalSimParams.numParticles);//TODO numParticles?
                        _cs.SetInt(_meshTrisLength_ID, length);
                        _cs.SetInt(_skinned_tex_width_ID, dqs._textureWidth);
                        _cs.SetMatrix(_meshMatrix_ID, meshObject.localToWorldMatrix);
                        _cs.SetTexture(kernelIndex, _skinned_data_1_ID, dqs._rtSkinnedData_1);
                        _cs.SetTexture(kernelIndex, _skinned_data_2_ID, dqs._rtSkinnedData_2);
                        if (_useTrisMesh) _cs.SetBuffer(kernelIndex, _trisData_ID, _trisDataBuffer);
                        _cs.SetBuffer(kernelIndex, _sphereDataBufferRW_ID, _sphereDataBuffer);
                        _cs.SetBool("_localSetup", localSetup);
                        if (localSetup && meshObject.parent != null)
                        {
                            _cs.SetMatrix("_bodyWorldToLocalMatrix", meshObject.parent.worldToLocalMatrix);
                        }
                        _cs.Dispatch(kernelIndex, length.GetComputeShaderThreads(256), 1, 1);
                        _lastParticleSum += length;
                    }
                    else if (meshObject.GetComponent<GPUSkinning>().ExistsAndEnabled(out behaviour) || meshObject.GetComponent<GPUMesh>().ExistsAndEnabled(out behaviour))
                    {
                        var meshVertsOut = behaviour.GetType() == typeof(GPUSkinning) ? (behaviour as GPUSkinning)._meshVertsOut : (behaviour as GPUMesh)._meshVertsOut;
                        if (meshVertsOut != null)
                        {
                            bool morph = false;
                            if (meshObject.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                                morph = true;

                            int vertexCount = _vertexCounts[i];
                            int length = vertexCount * innerVertexCount;
                            int kernelIndex = morph ? (_useTrisMesh ? 4 : 5) : (_useTrisMesh ? 2 : 3);
                            _cs.SetFloat(_normalScale_ID, _clothSim._globalSimParams._normalOffsetScale);
                            _cs.SetInt(_meshTrisLength_ID, length);
                            _cs.SetInt("_vertexCount", vertexCount);
                            _cs.SetMatrix(_meshMatrix_ID, meshObject.localToWorldMatrix);
                            _cs.SetBuffer(kernelIndex, _meshVertsOut_ID, meshVertsOut);

                            _cs.SetBool("_localSetup", localSetup);
                            if (localSetup && meshObject.parent != null)
                            {
                                _cs.SetMatrix("_bodyWorldToLocalMatrix", meshObject.parent.worldToLocalMatrix);
                            }

                            if (morph)
                            {
                                var blendShapes = (GPUBlendShapes)monoMorph;
                                if (blendShapes._rtArrayCombined != null)
                                {
                                    _cs.SetInt(_rtArrayWidthID, blendShapes._rtArrayCombined.width);
                                    _cs.SetTexture(kernelIndex, _rtArrayID, blendShapes._rtArrayCombined);
                                }
                            }

                            if (_useTrisMesh) _cs.SetBuffer(kernelIndex, _trisData_ID, _trisDataBuffer);
                            _cs.SetBuffer(kernelIndex, _sphereDataBufferRW_ID, _sphereDataBuffer);
                            _cs.Dispatch(kernelIndex, length.GetComputeShaderThreads(256), 1, 1);
                            _lastParticleSum += length;
                        }
                    }
                    //else if (meshObject.GetComponent<AutomaticBoneSpheres>().ExistsAndEnabled(out behaviour))
                    //{
                    //    var autoSpheres = behaviour as AutomaticBoneSpheres;
                    //    int length = autoSpheres._spheresBuffer.count;
                    //    _sphereFinderCS.SetInt(_numAutoSpheres_ID, length);
                    //    _sphereFinderCS.SetFloat(_autoSphereSize_ID, _clothSim._autoSphereScale);
                    //    _sphereFinderCS.SetBuffer(_autoSpheresKernel, _autoBonesBuffer_ID, autoSpheres._bonesBuffer);
                    //    _sphereFinderCS.SetBuffer(_autoSpheresKernel, _autoSphereBuffer_ID, autoSpheres._spheresBuffer);
                    //    _sphereFinderCS.SetBuffer(_autoSpheresKernel, _sphereDataBufferRW_ID, _sphereDataBuffer);
                    //    _sphereFinderCS.Dispatch(_autoSpheresKernel, length.GetComputeShaderThreads(8), 1, 1);
                    //    if (i < meshLength - 1) _lastParticleSum += length;
                    //}

                }
            }
        }



        private void CalcDistConnectionsForMask(Transform meshObj, Mesh pMesh, int lastVertsCount, int id, bool tris = false)
        {
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
                    mask = customScale * (_unifiedSphereSize > 0 ? (id + _unifiedSphereSize) : mask);
                    _sphereData[index].pr.xyz = meshObj.TransformPoint(uniqueVerts[n]) - normals[n] * _clothSim._globalSimParams._normalOffsetScale * mask * id * 0.01f;
                    _sphereData[index].mask = mask;
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
                    var mask = customScale * (_unifiedSphereSize > 0 ? (id + _unifiedSphereSize) : math.max(maskList[faces[i * 3 + 0]], math.max(maskList[faces[i * 3 + 1]], maskList[faces[i * 3 + 2]])));
                    _sphereData[index].pr.xyz = meshObj.TransformPoint(vertices[faces[i * 3 + 0]]) - norm[faces[i * 3 + 0]] * _clothSim._globalSimParams._normalOffsetScale * mask * id * 0.01f;
                    _sphereData[index].mask = mask;
                }
            }
        }



        internal void UpdateDebug()
        {
            if (_debugMeshPoints)
            {
                if (_indexBuffer == null || _lastDebugPointsCount != _numparticles) StartDebug(_numparticles);
                if (_indexBuffer != null)
                {
                    _debugMat.SetFloat(_normalScale_ID, _clothSim._globalSimParams._normalOffsetScale);
                    _debugMat.SetFloat("_trisMode", _clothSim._solver._trisMode ? 1 : 0);

                    _debugMat.SetFloat("_scale", _clothSim._globalSimParams.particleDiameter * _debugVertexScale); //TODO right scale?
                    _debugMat.SetInt("_vertexCount", _debugMesh.vertexCount);
                    _debugMat.SetBuffer("_vertexBuffer", debugVertexBuffer);
                    _debugMat.SetBuffer("_normalBuffer", debugNormalBuffer);
                    _debugMat.SetBuffer("_meshPosBuffer", _sphereDataBuffer);
                    if (_clothSim._extensions != null && _clothSim._extensions.Length > 0)
                        _debugMat.SetInt("_showDebugColors", _clothSim._extensions[0]._showDebugColors ? 1 : 0);

                    Graphics.DrawProcedural(_debugMat, _b, MeshTopology.Triangles, _indexBuffer, _indexBuffer.count, 0, null, _matBlock);
                }
            }
        }

        private int _lastDebugPointsCount = 0;


        void StartDebug(int pointsCount)
        {
            if (_debugMeshPoints && pointsCount > 0)
            {
                _lastDebugPointsCount = pointsCount;

                if (_debugMesh == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _debugMesh = go.GetComponent<MeshFilter>().mesh;
                    UnityEngine.Object.Destroy(go);
                }

                if (_debugMat == null)
                {
                    _debugMat = new Material(Shader.Find("ClothDynamics/DebugUnlitShaderV2"));
                }

                _matBlock = new MaterialPropertyBlock();

                int debugPointsCount = pointsCount;
                int[] tris = new int[_debugMesh.triangles.Length * debugPointsCount];
                int[] meshStartIndex = new int[debugPointsCount];
                var meshPos = new Vector4[debugPointsCount];
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
                    meshPos[nx] = pos;
                }

                if (_indexBuffer != null) _indexBuffer.Release();
                _indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, tris.Length, sizeof(int));
                _indexBuffer.SetData(tris);

                if (debugVertexBuffer != null) debugVertexBuffer.Release();
                debugVertexBuffer = new ComputeBuffer(_debugMesh.vertexCount, sizeof(float) * 3);
                debugVertexBuffer.SetData(_debugMesh.vertices);

                if (debugNormalBuffer != null) debugNormalBuffer.Release();
                debugNormalBuffer = new ComputeBuffer(_debugMesh.vertexCount, sizeof(float) * 3);
                debugNormalBuffer.SetData(_debugMesh.normals);

                _b = new Bounds();
                _b.center = _clothSim.transform.position;// _mesh.bounds.center;
                _b.size = _debugMesh.bounds.size * debugPointsCount * _debugVertexScale; //TODO
            }
        }


        float3 Rotate(float4 q, float3 v)
        {
            float3 t = 2.0f * math.cross(q.xyz, v);
            return v + q.w * t + math.cross(q.xyz, t); //changed q.w to -q.w;
        }

        public void OnDestroy()
        {
            //_spherePosPredicted.ClearBuffer();
            _sphereDataBuffer.ClearBuffer();
            _trisDataBuffer.ClearBuffer();
            debugVertexBuffer.ClearBuffer();
            debugNormalBuffer.ClearBuffer();
            _indexBuffer.ClearBuffer();
        }

        public class ComponentComparer : IComparer<Transform>
        {
            public int Compare(Transform first, Transform second)
            {
                if (first != null && second != null)
                {
                    bool clothFirst = first.GetComponent<ClothObjectGPU>() != null;
                    bool clothSecond = second.GetComponent<ClothObjectGPU>() != null;
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
}