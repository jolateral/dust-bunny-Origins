using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ClothDynamics
{
    [ExecuteInEditMode]
    public class GPUBlendShapes : MonoBehaviour
    {
        private int _fameNum = 0;
        private ComputeShader _csDataToRt;
        private static readonly int _dataBufferID = Shader.PropertyToID("dataBuffer");
        private static readonly int _dataBufferNID = Shader.PropertyToID("dataBufferN");
        private static readonly int _rtID = Shader.PropertyToID("rt");
        private static readonly int _rtNID = Shader.PropertyToID("rtN");
        private static readonly int _rtTID = Shader.PropertyToID("rtT");
        private static readonly int _widthID = Shader.PropertyToID("width");

        private static readonly int _rtArrayID = Shader.PropertyToID("_rtArray");
        private static readonly int _rtArrayNID = Shader.PropertyToID("_rtArrayN");
        private static readonly int _rtArrayTID = Shader.PropertyToID("_rtArrayT");
        private static readonly int _rtArrayWidthID = Shader.PropertyToID("_rtArrayWidth");
        private static readonly int _blendWeightArrayID = Shader.PropertyToID("_blendWeightArray");
        private static readonly int _remapArrayID = Shader.PropertyToID("_remapArray");
        private static readonly int _shapeCountID = Shader.PropertyToID("_shapeCount");


        private string _clothPath = "Assets/ClothDynamics";

        public List<string> _blendShapeNames = new List<string>();
        public List<float> _blendWeightArray = new List<float>();
        private Dictionary<string, int> _mapBlendShapeNames = new Dictionary<string, int>();

        [Header("File Settings")]
        [Tooltip("This will write/read the blend shapes data to/from the StreamingAssets folder.")]
        [SerializeField] private bool _useStreamingPath = true;
        [Tooltip("This can be used to make the data unique, but normally all your meshes, which have the same name, have the same blend shapes. If not, please use this to create a new cache folder with this ID.")]
        public int _uniqueID = 0;

        private bool _runUpdate = true;
#if UNITY_EDITOR
        [ReadOnly]
#endif
        [SerializeField] private int _SHAPE_COUNT = 1;

        [Tooltip("This creates delta normal data and is recommended to be true!")]
        public bool _useNormalFiles = true;
        [Tooltip("This creates delta tangents data and is recommended to be false! Because you will not see much difference normally.")]
        public bool _useTangentFiles = false;
        private bool _useNormalFilesLast = true;
        private bool _useTangentFilesLast = false;

        private Texture2D[] bytesTex = new Texture2D[2]; // four textures so gpu texture write is less expensive (but more memory intense)
        private Renderer _rend;
        private string _FILE_NAME = "";
        private string _PATH_NAME = "";
        private string _ROOT_NAME = "";
        internal RenderTexture _rtArray;
        internal RenderTexture _rtArrayCombined;
        private RenderTexture _rtArrayN;
        private RenderTexture _rtArrayNCombined;
        private RenderTexture _rtArrayT;
        private RenderTexture _rtArrayTCombined;
        private MaterialPropertyBlock _mpb;
        private WaitForSeconds _waitForSeconds = new WaitForSeconds(1);

        private static Shader _copyShader;
        private static Material _matCopyTex;

        [Tooltip("This is only needed for GPUBlendShapes that are applied to a cloth and will be controlled by the body blend shapes. It should set the right body object automatically.")]
        [SerializeField] public GPUBlendShapes _externalController;

        IEnumerator Start()
        {
            yield return _waitForSeconds;
            _useNormalFilesLast = !_useNormalFiles;
            _useTangentFilesLast = !_useTangentFiles;
        }

        protected void OnEnable()
        {
            var skin = this.GetComponent<SkinnedMeshRenderer>();
            if (skin != null)
            {
                var count = skin.sharedMesh.blendShapeCount;
                if (count != _blendShapeNames.Count)
                {
                    _blendShapeNames.Clear();
                    _blendWeightArray.Clear();

                    for (int i = 0; i < count; i++)
                    {
                        string shapename = skin.sharedMesh.GetBlendShapeName(i);
                        _blendShapeNames.Add(shapename);
                        _blendWeightArray.Add(0);
                        _mapBlendShapeNames.Add(shapename, i);
                    }
                }

                if (Application.isPlaying)
                {
                    _mapBlendShapeNames.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        string shapename = skin.sharedMesh.GetBlendShapeName(i);
                        _mapBlendShapeNames.Add(shapename, i);
                    }
                }
            }

            if (Application.isPlaying)
            {
                Init();
                ReadData();
                UpdateMaterials();
                _runUpdate = true;
            }
        }

        private void Init()
        {
#if UNITY_EDITOR
            _clothPath = Directory.GetParent(Directory.GetParent(Directory.GetParent(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(this)))).FullName).FullName).FullName;
#endif

            _copyShader = Resources.Load("BlendShaders/CopyTex", typeof(Shader)) as Shader;
            _matCopyTex = new Material(_copyShader); //TODO set mat

            _mpb = new MaterialPropertyBlock();
            _rend = this.GetComponent<Renderer>();

            _FILE_NAME = this.name;
            _PATH_NAME = _FILE_NAME + "_" + _uniqueID;
            _ROOT_NAME = "BlendShapes";

            _useNormalFilesLast = !_useNormalFiles;
            _useTangentFilesLast = !_useTangentFiles;

            if (this.GetComponent<GPUClothDynamics>().ExistsAndEnabled(out MonoBehaviour monoCloth))
            {
                var cloth = (GPUClothDynamics)monoCloth;
                foreach (var item in cloth._meshObjects)
                {
                    if (!item.GetComponent<GPUClothDynamics>() && item.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                    {
                        _externalController = (GPUBlendShapes)monoMorph;
                        break;
                    }
                }
            }
            //if (this.GetComponent<ClothObjectGPU>().ExistsAndEnabled(out MonoBehaviour mCloth))
            foreach (var cd in FindObjectsOfType<GPUClothDynamicsV2>())
            {
                if (cd.ExistsAndEnabled(out MonoBehaviour mCloth))
                {
                    var cloth = ((GPUClothDynamicsV2)mCloth)._collisionMeshes;
                    if (cloth != null)
                    {
                        foreach (var item in cloth._meshObjects)
                        {
                            if (item == this.transform) continue;
                            //print("item " + item);
                            if (item.parent == this.transform.parent && item.GetComponent<ClothObjectGPU>() == null && item.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour mMorph))
                            {
                                _externalController = (GPUBlendShapes)mMorph;
                                break;
                            }
                        }
                    }
                } 
            }
        }

        internal List<int> _vertexMap = null;
        internal BoneWeight[] _weights = null;

        //internal Mesh _mesh = null;
        void ReadData()
        {
            var skin = this.GetComponent<SkinnedMeshRenderer>();
            if (skin != null)
            {
                _vertexMap = this.GetComponent<GPUSkinning>() != null ? this.GetComponent<GPUSkinning>()._vertexMap : null;
                //print("_vertexMap " + _vertexMap);

                var cloth = this.GetComponent<GPUClothDynamics>();
                var cloth2 = this.GetComponent<ClothObjectGPU>();
                var mesh = skin.sharedMesh;
                _weights = mesh.boneWeights;


                var count = skin.sharedMesh.blendShapeCount;
                Vector3[][] deltaVerticesTemp = new Vector3[count][];
                Vector3[][] deltaNormalsTemp = new Vector3[count][];
                Vector3[][] deltaTangentsTemp = new Vector3[count][];

                for (int i = 0; i < count; i++)
                {
                    deltaVerticesTemp[i] = new Vector3[skin.sharedMesh.vertexCount];
                    deltaNormalsTemp[i] = new Vector3[skin.sharedMesh.vertexCount];
                    deltaTangentsTemp[i] = new Vector3[skin.sharedMesh.vertexCount];

                    string shapename = skin.sharedMesh.GetBlendShapeName(i);
                    int shapeId = skin.sharedMesh.GetBlendShapeIndex(shapename);
                    skin.sharedMesh.GetBlendShapeFrameVertices(shapeId, _fameNum, deltaVerticesTemp[i], deltaNormalsTemp[i], deltaTangentsTemp[i]);
                }

                if (_vertexMap == null && (cloth != null || cloth2 != null))
                {
                    if ((cloth != null && cloth._weldVertices && !cloth._weldDone) || (cloth2 != null && cloth2._weldVertices && !cloth2._weldDone))
                    {
                        mesh = Instantiate(mesh);
                        if (skin != null) skin.sharedMesh = mesh;
                        else this.GetComponent<MeshFilter>().sharedMesh = mesh;
                        GPUClothDynamics.WeldVerticesOld(mesh, out _vertexMap);
                        //_mesh = mesh;
                        if (cloth != null) cloth._weldDone = true;
                        if (cloth2 != null) cloth2._weldDone = true;
                    }
                }

                int vCount = skin.sharedMesh.vertexCount;
                int width = Mathf.CeilToInt(Mathf.Sqrt(vCount));
                RenderTexture rt = new RenderTexture(width, width, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                {
                    enableRandomWrite = true
                };
                rt.Create();

                RenderTexture rtN = null;
                if (_useNormalFiles)
                {
                    rtN = new RenderTexture(width, width, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                    {
                        enableRandomWrite = true
                    };
                    rtN.Create();
                }

                RenderTexture rtT = null;
                if (_useTangentFiles)
                {
                    rtT = new RenderTexture(width, width, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                    {
                        enableRandomWrite = true
                    };
                    rtT.Create();
                }

                ComputeBuffer dataBuffer = new ComputeBuffer(vCount, sizeof(float) * 3);
                ComputeBuffer dataBufferN = new ComputeBuffer(vCount, sizeof(float) * 3);
                ComputeBuffer dataBufferT = new ComputeBuffer(vCount, sizeof(float) * 3);

                int widthKernel = Mathf.CeilToInt(width / 8.0f);

                if (_csDataToRt == null)
                {
                    _csDataToRt = Resources.Load<ComputeShader>("BlendShaders/WriteDataToRT");
                }

                _csDataToRt.SetInt(_widthID, width);
                _csDataToRt.SetInt("vertexCount", vCount);
                Vector3[] deltaVertices = new Vector3[skin.sharedMesh.vertexCount];
                Vector3[] deltaNormals = new Vector3[skin.sharedMesh.vertexCount];
                Vector3[] deltaTangents = new Vector3[skin.sharedMesh.vertexCount];


                if (_vertexMap == null)
                {
                    _vertexMap = new List<int>();
                    for (int n = 0; n < deltaVertices.Length; n++) _vertexMap.Add(n);
                }

                for (int i = 0; i < count; i++)
                {

                    //string shapename = skin.sharedMesh.GetBlendShapeName(i);
                    //int shapeId = skin.sharedMesh.GetBlendShapeIndex(shapename);
                    //skin.sharedMesh.GetBlendShapeFrameVertices(shapeId, _fameNum, deltaVerticesTemp, deltaNormalsTemp, deltaTangentsTemp);

                    for (int n = 0; n < deltaVertices.Length; n++)
                    {
                        deltaVertices[n] = deltaVerticesTemp[i][_vertexMap[n]];
                        deltaNormals[n] = deltaNormalsTemp[i][_vertexMap[n]];
                        deltaTangents[n] = deltaTangentsTemp[i][_vertexMap[n]];
                    }


                    dataBuffer.SetData(deltaVertices);

                    if (_useNormalFiles)
                    {
                        dataBufferN.SetData(deltaNormals);
                        _csDataToRt.SetBuffer(0, _dataBufferID, dataBuffer);
                        _csDataToRt.SetBuffer(0, _dataBufferNID, dataBufferN);
                        _csDataToRt.SetTexture(0, _rtID, rt);
                        _csDataToRt.SetTexture(0, _rtNID, rtN);
                        _csDataToRt.Dispatch(0, widthKernel, widthKernel, 1);
                    }
                    else
                    {
                        _csDataToRt.SetBuffer(1, _dataBufferID, dataBuffer);
                        _csDataToRt.SetTexture(1, _rtID, rt);
                        _csDataToRt.Dispatch(1, widthKernel, widthKernel, 1);
                    }

                    if (_useTangentFiles)
                    {
                        dataBufferT.SetData(deltaTangents);
                        _csDataToRt.SetBuffer(1, _dataBufferID, dataBufferT);
                        _csDataToRt.SetTexture(1, _rtID, rtT);
                        _csDataToRt.Dispatch(1, widthKernel, widthKernel, 1);
                    }

                    string path = (_useStreamingPath ? Application.streamingAssetsPath + "/Cache/" : _clothPath + "/Resources/") + _ROOT_NAME + "/" + _PATH_NAME + "/";
                    SaveRenderTexture(rt, i, this.name, "_RT", path, _useStreamingPath);

                    if (_useNormalFiles)
                    {
                        SaveRenderTexture(rtN, i, name, "_RT_N", path, _useStreamingPath);
                    }

                    if (_useTangentFiles)
                    {
                        SaveRenderTexture(rtT, i, name, "_RT_T", path, _useStreamingPath);
                    }
                }

                dataBuffer.Release();
                dataBufferN.Release();
                dataBufferT.Release();

                if (_rtArray == null)
                {
                    if (_useStreamingPath)
                    {
                        var files = Directory.GetFiles(Application.streamingAssetsPath + $"/Cache/{_ROOT_NAME}/{_PATH_NAME}/_RT", "*.*", SearchOption.AllDirectories).Where(x => Path.GetExtension(x) != ".meta" && Path.GetExtension(x) != ".manifest").ToArray();
                        _SHAPE_COUNT = files.Length;
                    }
                    else
                    {
                        var files = Resources.LoadAll<Texture>($"{_ROOT_NAME}/{_PATH_NAME}/_RT");
                        _SHAPE_COUNT = files.Length;
                        //Resources.UnloadUnusedAssets();
                    }

                    _SHAPE_COUNT = Mathf.Max(1, _SHAPE_COUNT);
                    int fCount = _SHAPE_COUNT;
                    if (fCount != _blendWeightArray.Count)
                    {
                        _blendWeightArray.Clear();
                    }
                    _blendShapeNames.Clear();

                    for (int i = 0; i < fCount; i++)
                    {
                        var textures = ReadTextures(i);
                        if (textures != null)
                        {
                            CreateCacheTextures(i, fCount, textures);
                            string shapename = skin.sharedMesh.GetBlendShapeName(i);
                            _blendShapeNames.Add(shapename);
                            if (_blendWeightArray.Count <= i) _blendWeightArray.Add(0);

                            if (!_useStreamingPath)
                            {
                                for (int n = 0; n < textures.Length; n++)
                                {
                                    Resources.UnloadAsset(textures[n]);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void SaveRenderTexture(RenderTexture input, int num, string fileName, string extName, string resPath, bool useStreamingPath = false, bool useFloat = false)
        {
#if UNITY_EDITOR
            if (input != null)
            {
                int width = input.width;
                int height = input.height;

                Texture2D tex = new Texture2D(width, height, useFloat ? TextureFormat.RGBAFloat : TextureFormat.RGBAHalf, false, true);

                // Read screen contents into the texture
                Graphics.SetRenderTarget(input);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                ///string path = (useStreamingPath ? Application.streamingAssetsPath + "/Cache" : resPath) + $"/{fileName}/{extName}/";
                string path = resPath + $"/{extName}/";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                // Encode texture into the EXR
                Texture2D.EXRFlags flags = Texture2D.EXRFlags.None;
                if (useFloat) flags |= Texture2D.EXRFlags.OutputAsFloat;
                byte[] bytes = useStreamingPath ? tex.GetRawTextureData() : tex.EncodeToEXR(flags);
                File.WriteAllBytes($"{path}{fileName}{extName}_{num}.exr", bytes);

                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.Refresh();
            }
#endif
        }

        //private void OnDrawGizmos()
        //{
        //	var skin = this.GetComponent<SkinnedMeshRenderer>();
        //	if (skin != null)
        //	{
        //		//deltaVertices = new Vector3[skin.sharedMesh.vertexCount];
        //		//deltaNormals = new Vector3[skin.sharedMesh.vertexCount];
        //		//deltaTangents = new Vector3[skin.sharedMesh.vertexCount];
        //		//skin.sharedMesh.GetBlendShapeFrameVertices(0, 100, deltaVertices, deltaNormals, deltaTangents);
        //		string shapename = skin.sharedMesh.GetBlendShapeName(0);
        //		Debug.Log("shapename " + shapename);
        //		int shapeId = skin.sharedMesh.GetBlendShapeIndex(shapename);
        //		deltaVertices = new Vector3[skin.sharedMesh.vertexCount];
        //		deltaNormals = new Vector3[skin.sharedMesh.vertexCount];
        //		//deltaTangents = new Vector3[skin.sharedMesh.vertexCount];
        //		skin.sharedMesh.GetBlendShapeFrameVertices(shapeId, _fameNum, deltaVertices, deltaNormals, null);
        //		var weight = skin.GetBlendShapeWeight(shapeId);
        //		Debug.Log("weight " + weight);
        //		if (deltaVertices != null)
        //		{
        //			var verts = skin.sharedMesh.vertices;
        //			for (int i = 0; i < deltaVertices.Length; i++)
        //			{
        //				Gizmos.DrawWireCube(this.transform.TransformPoint(verts[i] + deltaVertices[i] * weight * 0.01f), Vector3.one * 0.001f);
        //			}
        //		}
        //	}
        //}



        //Texture CreateTempTex(Texture tex, Vector2Int size)
        //{
        //	var _tempN = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        //	Graphics.Blit(tex, _tempN);
        //	return _tempN;
        //}

        Texture LoadTexture(string path, int index = 0)
        {
            if (_useStreamingPath)
            {
                var bytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, $"Cache/" + path + ".exr"));
                var size = Mathf.CeilToInt(Mathf.Sqrt(bytes.Length / 8)); // TODO: 8 half, 16 float
                if (bytesTex[index] == null || bytesTex[index].width != size) bytesTex[index] = new Texture2D(size, size, TextureFormat.RGBAHalf, false, true);

                //tex.LoadRawTextureData(bytes);
                //tex.Apply();

                NativeArray<byte> nativeByteArray = bytesTex[index].GetRawTextureData<byte>();
                nativeByteArray.CopyFrom(bytes);
                bytesTex[index].Apply();
            }
            else
            {
                bytesTex[index] = Resources.Load<Texture2D>(path);
            }
            return bytesTex[index];
        }

        Texture[] ReadTextures(int i = 0)
        {
            Texture temp = null;
            Texture tempN = null;
            Texture tempT = null;
            Vector2Int size = Vector2Int.zero;

            var tex = LoadTexture($"{_ROOT_NAME}/{_PATH_NAME}/_RT/{_FILE_NAME}_RT_{i}");
            if (tex == null) return null;
            size = new Vector2Int(tex.width, tex.height);
            temp = tex;// CreateTempTex(tex, size);
                       //if (!_useStreamingPath) Resources.UnloadAsset(tex);

            if (_useNormalFiles)
            {
                var texN = LoadTexture($"{_ROOT_NAME}/{_PATH_NAME}/_RT_N/{_FILE_NAME}_RT_N_{i}", 1);
                tempN = texN;// CreateTempTex(texN, size);
                             //if (!_useStreamingPath) Resources.UnloadAsset(texN);
            }
            if (_useTangentFiles)
            {
                var texT = LoadTexture($"{_ROOT_NAME}/{_PATH_NAME}/_RT_T/{_FILE_NAME}_RT_T_{i}", 1);
                tempT = texT;// CreateTempTex(texT, size);
                             //if (!_useStreamingPath) Resources.UnloadAsset(texT);
            }
            return new Texture[] { temp, tempN, tempT };
        }

        void CreateCacheTextures(int i, int frames, Texture[] textures)
        {
            var tex = textures[0];
            var texN = textures[1];
            var texT = textures[2];


            if (tex != null && _rtArrayCombined == null)
            {
                _rtArrayCombined = new RenderTexture(tex.width, tex.width, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = $"{_FILE_NAME}_RTAC",
                    enableRandomWrite = true
                };
                _rtArrayCombined.Create();
            }
            if (tex != null && _rtArray == null)
            {
                _rtArray = new RenderTexture(tex.width, tex.width, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = $"{_FILE_NAME}_RTA",
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                    volumeDepth = frames
                };
                _rtArray.Create();
            }
            BlitSlice(tex, _rtArray, i);

            if (_useNormalFiles)
            {
                if (texN != null && _rtArrayNCombined == null)
                {
                    _rtArrayNCombined = new RenderTexture(tex.width, tex.width, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                    {
                        name = $"{_FILE_NAME}_RTAC_N",
                        enableRandomWrite = true
                    };
                    _rtArrayNCombined.Create();
                }
                if (texN != null && _rtArrayN == null)
                {
                    _rtArrayN = new RenderTexture(texN.width, texN.width, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                    {
                        name = $"{_FILE_NAME}_RTA_N",
                        dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                        volumeDepth = frames
                    };
                    _rtArrayN.Create();
                }
                BlitSlice(texN, _rtArrayN, i);
            }

            if (_useTangentFiles)
            {
                if (texT != null && _rtArrayTCombined == null)
                {
                    _rtArrayTCombined = new RenderTexture(tex.width, tex.width, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                    {
                        name = $"{_FILE_NAME}_RTAC_N",
                        enableRandomWrite = true
                    };
                    _rtArrayTCombined.Create();
                }
                if (texT != null && _rtArrayT == null)
                {
                    _rtArrayT = new RenderTexture(texN.width, texN.width, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                    {
                        name = $"{_FILE_NAME}_RTA_T",
                        dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                        volumeDepth = frames
                    };
                    _rtArrayT.Create();
                }
                BlitSlice(texT, _rtArrayT, i);
            }
        }

        enum UpdateType
        {
            FixedUpdate = 0,
            Update = 1,
            LateUpdate = 2
        }
        [SerializeField] private UpdateType _updateType = UpdateType.FixedUpdate;


        private void FixedUpdate()
        {
            if (_updateType == UpdateType.FixedUpdate)
                UpdateX();
        }

        private void Update()
        {
            if (_updateType == UpdateType.Update)
               UpdateX();
        }

        private void LateUpdate()
        {
            if (_updateType == UpdateType.LateUpdate)
               UpdateX();
        }

        private void UpdateX()
        {
#if UNITY_EDITOR
            var skin = this.GetComponent<SkinnedMeshRenderer>();
            if (skin != null && skin.sharedMesh != null)
            {
                var count = skin.sharedMesh.blendShapeCount;
                if (count != _blendShapeNames.Count)
                {
                    _blendShapeNames.Clear();
                    _blendWeightArray.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        string shapename = skin.sharedMesh.GetBlendShapeName(i);
                        _blendShapeNames.Add(shapename);
                        _blendWeightArray.Add(0);
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    string shapename = skin.sharedMesh.GetBlendShapeName(i);
                    int shapeId = skin.sharedMesh.GetBlendShapeIndex(shapename);
                    _blendWeightArray[i] = skin.GetBlendShapeWeight(shapeId);
                }
            }
#endif
            if (!Application.isPlaying || !_runUpdate || _rtArray == null || (_useNormalFiles && _rtArrayN == null) || (_useTangentFiles && _rtArrayT == null))
                return;

            ExternalControl();
            CombineTextures();
            UpdateMaterials();
        }

        private void ExternalControl()
        {
            if (_externalController != null)
            {
                int count = _externalController._blendShapeNames.Count;
                for (int i = 0; i < count; i++)
                {
                    var index = GetBlendShapeIndex(_externalController._blendShapeNames[i]);
                    if (index >= 0)
                    {
                        _blendWeightArray[index] = _externalController._blendWeightArray[i];
                    }
                }
            }
        }

        private void CombineTextures()
        {
            int width = _rtArray.width;
            int widthKernel = Mathf.CeilToInt(width / 8.0f);

            int count = _blendWeightArray.Count;
            //print("_blendWeightArray " + count);
            List<int> remapWeights = new List<int>();

            for (int k = 0; k < count; k++)
            {
                if (_blendWeightArray[k] > 0)
                    remapWeights.Add(k);
            }

            count = remapWeights.Count;
            var array = new float[count * 4];
            var arrayRemap = new int[count * 4];
            for (int k = 0; k < count; k++)
            {
                array[k * 4] = _blendWeightArray[remapWeights[k]];
                arrayRemap[k * 4] = remapWeights[k];
            }
            //print("arrayRemap " + count);

            //_csDataToRt.SetInt(_widthID, width); //Gets set at init
            _csDataToRt.SetInt(_shapeCountID, count /*_rtArray.volumeDepth*/);
            _csDataToRt.SetFloats(_blendWeightArrayID, array);
            _csDataToRt.SetInts(_remapArrayID, arrayRemap);

            if (_useTangentFiles)
            {
                int kernel = 4;
                _csDataToRt.SetTexture(kernel, _rtArrayID, _rtArray);
                _csDataToRt.SetTexture(kernel, _rtArrayNID, _rtArrayN);
                _csDataToRt.SetTexture(kernel, _rtArrayTID, _rtArrayT);
                _csDataToRt.SetTexture(kernel, _rtID, _rtArrayCombined);
                _csDataToRt.SetTexture(kernel, _rtNID, _rtArrayNCombined);
                _csDataToRt.SetTexture(kernel, _rtTID, _rtArrayTCombined);
                _csDataToRt.Dispatch(kernel, widthKernel, widthKernel, 1);
            }
            else if (_useNormalFiles)
            {
                int kernel = 3;
                _csDataToRt.SetTexture(kernel, _rtArrayID, _rtArray);
                _csDataToRt.SetTexture(kernel, _rtArrayNID, _rtArrayN);
                _csDataToRt.SetTexture(kernel, _rtID, _rtArrayCombined);
                _csDataToRt.SetTexture(kernel, _rtNID, _rtArrayNCombined);
                _csDataToRt.Dispatch(kernel, widthKernel, widthKernel, 1);
            }
            else
            {
                int kernel = 2;
                _csDataToRt.SetTexture(kernel, _rtArrayID, _rtArray);
                _csDataToRt.SetTexture(kernel, _rtID, _rtArrayCombined);
                _csDataToRt.Dispatch(kernel, widthKernel, widthKernel, 1);
            }
        }

        private void UpdateMaterials()
        {
            if (_rend == null) _rend = this.GetComponent<Renderer>();
            _rend.GetPropertyBlock(_mpb);
            if (_rtArray) _mpb.SetTexture(_rtArrayID, _rtArrayCombined);
            if (_rtArrayN && _useNormalFiles) _mpb.SetTexture(_rtArrayNID, _rtArrayNCombined);
            if (_rtArrayT && _useTangentFiles) _mpb.SetTexture(_rtArrayTID, _rtArrayTCombined);
            if (_rtArray) _mpb.SetInt(_rtArrayWidthID, _rtArrayCombined.width);
            //_mpb.SetFloatArray(_blendWeightArrayID, _blendWeightArray);
            //_mpb.SetInt(_shapeCountID, _SHAPE_COUNT);
            _rend.SetPropertyBlock(_mpb);

            if (_useNormalFiles != _useNormalFilesLast || _useTangentFiles != _useTangentFilesLast)
            {
                if (_useNormalFiles != _useNormalFilesLast) _useNormalFilesLast = _useNormalFiles;
                if (_useTangentFiles != _useTangentFilesLast) _useTangentFilesLast = _useTangentFiles;
                if (_rend != null)
                {
                    for (int i = 0; i < _rend.materials.Length; i++)
                    {
                        var material = _rend.materials[i];
                        material.EnableKeyword("USE_BLEND_SHAPES");
                        if (_useNormalFiles) material.EnableKeyword("USE_NORMALS");
                        else material.DisableKeyword("USE_NORMALS");
                        if (_useTangentFiles) material.EnableKeyword("USE_TANGENTS");
                        else material.DisableKeyword("USE_TANGENTS");
                    }
                }
            }
        }

        // Summary:
        //     Sets the weight of a BlendShape for this Renderer.
        //
        // Parameter:
        //   index:
        //     The index of the BlendShape to modify. Index must be smaller than the Mesh.blendShapeCount
        //     of the Mesh attached to this Renderer.
        //
        //   value:
        //     The weight for this BlendShape.
        public void SetBlendShapeWeight(int index, float value)
        {
            _blendWeightArray[index] = value;
        }

        // Summary:
        //     Returns the weight of a BlendShape for this Renderer.
        //
        // Parameter:
        //   index:
        //     The index of the BlendShape whose weight you want to retrieve. Index must be
        //     smaller than the Mesh.blendShapeCount of the Mesh attached to this Renderer.
        //
        // Return value:
        //     The weight of the BlendShape.
        public float GetBlendShapeWeight(int index)
        {
            return _blendWeightArray[index];
        }

        public int GetBlendShapeIndex(string blendShapeName)
        {
            if (_mapBlendShapeNames.TryGetValue(blendShapeName, out int value)) return value;
            return -1;
        }

        public string GetBlendShapeName(int shapeIndex)
        {
            var array = _mapBlendShapeNames.Keys.ToArray();
            if (shapeIndex >= 0 && shapeIndex < array.Length) return array[shapeIndex];
            return "";
        }

        private void OnDisable()
        {
            if (_rtArray) _rtArray.Release();
            if (_rtArrayN) _rtArrayN.Release();
            if (_rtArrayT) _rtArrayT.Release();
            _runUpdate = false;
        }

        public static void BlitSlice(Texture source, RenderTexture rt, int i)
        {
            if (_copyShader != null)
            {
                Graphics.SetRenderTarget(rt, 0, CubemapFace.Unknown, i);

                GL.PushMatrix();
                GL.LoadOrtho();

                if (_matCopyTex == null) _matCopyTex = new Material(_copyShader);
                _matCopyTex.SetTexture("_MainTex", source);
                _matCopyTex.SetPass(0);

                GL.Begin(GL.QUADS);
                GL.TexCoord2(0, 0);
                GL.Vertex3(0, 0, 0);
                GL.TexCoord2(1, 0);
                GL.Vertex3(1, 0, 0);
                GL.TexCoord2(1, 1);
                GL.Vertex3(1, 1, 0);
                GL.TexCoord2(0, 1);
                GL.Vertex3(0, 1, 0);
                GL.End();

                GL.PopMatrix();
            }
        }
    }

}
#if UNITY_EDITOR
namespace ClothDynamics
{
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class CDReadOnlyDrawer : PropertyDrawer
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
}
#endif
