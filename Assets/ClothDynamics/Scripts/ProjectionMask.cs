using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
//using UnityEngine.Rendering.HighDefinition;

namespace ClothDynamics
{
    [DefaultExecutionOrder(15300)] //When using Final IK
    public class ProjectionMask : MonoBehaviour
    {
        [Tooltip("You can leave this blank, use a prefab or a scene camera. If you want to use one camera for many cloth you can make a copy of a MaskCam and add it here manually.")]
        public Camera _maskCam;
        [Tooltip("This will check if there is a MaskCam in the scene and will use it if this is true.")]
        public bool _searchForMaskCam = false;
        [Tooltip("This is the layer that is needed to render the cloth separatly. Choose an empty layer or add a new one.")]
        public LayerMask _maskLayer;
        [Tooltip("This depth threshold helps to hide unwanted render overlays. Try 0.05 or higher to cut off overlapping body parts.")]
        [Range(0, 1)]
        public float _depthThreshold = 0.05f;
        [Tooltip("This scales the RenderTexture down, which is used as mask.")]
        [Range(1, 8)]
        public int _downScaleRT = 1;
        [Tooltip("Dilation adds extra pixels to the mask's edges so you can use  a low-res RT. If you set _downScaleRT to 1 you can set this to 0.")]
        public int _dilationSteps = 0;
        [Tooltip("These are the Body meshes from the cloth component of the mesh objects list, you can add your own e.g. if you are not using the CollisionFinder.")]
        public List<GameObject> _maskBodies = new List<GameObject>();
        [Tooltip("If you want to shared one mask renderer, you can set one here from another ProjectionMask, so this one will not extra render the mask.")]
        public ProjectionMask _otherMaskRenderer = null;
        [Tooltip("If this is true, you should see a blue cloth copy in the scene view at the same position as the original cloth mesh.")]
        public bool _debugRenderer = false;

        public bool _runUpdate = true;
        private RenderTexture _rt;
        private Shader _shader;
        private GPUClothBase _cloth;
        private Shader _dilationShader;
        private Material _dilationMat;
        private WaitForSeconds _waitForSeconds = new WaitForSeconds(1);

        private int _maskTexID = Shader.PropertyToID("_MaskTex");
        private int _maxStepsID = Shader.PropertyToID("_MaxSteps");
        private int _maskThresholdID = Shader.PropertyToID("_MaskThreshold");
        private int _positionsBufferID = Shader.PropertyToID("positionsBuffer");
        private int _normalsBufferID = Shader.PropertyToID("normalsBuffer");
        internal GameObject _copy;

        internal void SetShader(string addon = "")
        {
            if (GetComponentInChildren<ClothObjectGPU>() == null) //TODO maybe find better solution
            {
                if (GraphicsSettings.currentRenderPipeline)
                {
                    if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                    {
                        //Debug.Log("HDRP active");
                        if (_shader == null || !string.IsNullOrEmpty(addon)) _shader = Resources.Load("Shaders/SRP/ClothMaskGraph" + addon, typeof(Shader)) as Shader;
                    }
                    else // assuming here we only have HDRP or URP options here
                    {
                        //Debug.Log("URP active");
#if UNITY_2020_1_OR_NEWER
                        if (_shader == null || !string.IsNullOrEmpty(addon)) _shader = Resources.Load("Shaders/SRP/URP_ClothMaskGraph" + addon, typeof(Shader)) as Shader;
#else
					if (_shader == null || !string.IsNullOrEmpty(addon)) _shader = Resources.Load("Shaders/SRP/URP_ClothMaskGraph_2019" + addon, typeof(Shader)) as Shader;
#endif
                    }
                }
                else
                {
                    //Debug.Log("Built-in RP active");
                    if (_shader == null || !string.IsNullOrEmpty(addon)) _shader = Resources.Load("Shaders/ClothMask" + addon) as Shader;
                }
            }
            else
            {
                if (_shader == null || !string.IsNullOrEmpty(addon)) _shader = Resources.Load("Shaders/V2/ClothMaskV2" + addon, typeof(Shader)) as Shader;
            }
        }


        private void OnEnable()
        {
            SetShader();

            if (_cloth == null) _cloth = GetComponentInChildren<GPUClothBase>();
            if (_dilationShader == null) _dilationShader = Resources.Load("Shaders/DilationEdges", typeof(Shader)) as Shader;
            if (_dilationMat == null && _dilationShader != null) _dilationMat = new Material(_dilationShader);

            if (_otherMaskRenderer == null || _otherMaskRenderer == this)
            {

                if (_rt == null)
                {
                    _rt = new RenderTexture(Screen.width / _downScaleRT, Screen.height / _downScaleRT, 32, RenderTextureFormat.ARGBFloat);
                    _rt.name = "ClothMaskRT";
                    _rt.Create();
                }

                if (_rt.width != Screen.width / _downScaleRT || _rt.height != Screen.height / _downScaleRT)
                {
                    _rt.Release();
                    _rt.width = Screen.width / _downScaleRT;
                    _rt.height = Screen.height / _downScaleRT;
                    _rt.name = "ClothMaskRT";
                    _rt.Create();
                }

                GameObject sceneObj = null;
                if (_maskCam != null)
                {

                    sceneObj = GameObject.Find(_maskCam.gameObject.name);
                    if (sceneObj != null && sceneObj.GetInstanceID() != _maskCam.gameObject.GetInstanceID()) sceneObj = null;
                }

                if (_searchForMaskCam)
                {
                    var cams = FindObjectsOfType<Camera>();
                    foreach (var cam in cams)
                    {
                        if (cam != null && cam.name.ToLower().Contains("maskcam")) { _maskCam = cam; sceneObj = _maskCam.gameObject; }
                    }
                }

                if (_maskCam == null || sceneObj == null)
                {
                    _maskCam = _maskCam != null ? Instantiate(_maskCam).GetComponent<Camera>() : new GameObject("MaskCam").AddComponent<Camera>();
                }

                if (_maskCam != null)
                {
                    if (Camera.main != null)
                    {
                        _maskCam.CopyFrom(Camera.main);
                        _maskCam.transform.SetParent(Camera.main.transform);
                        Camera.main.cullingMask &= ~_maskLayer.value;
                    }
                    if (_cloth != null) _maskCam.cullingMask = _maskLayer.value;
                    _maskCam.clearFlags = CameraClearFlags.SolidColor;
                    _maskCam.backgroundColor = Color.white;

                    //HDAdditionalCameraData hdCam = _maskCam.GetComponent<HDAdditionalCameraData>();
                    //hdCam.backgroundColorHDR = Color.white;
                    //hdCam.volumeLayerMask = 0;

                    _maskCam.targetTexture = _rt;
                    _maskCam.depth = -99;
                    _maskCam.enabled = false;

                    //if (GraphicsSettings.currentRenderPipeline)
                    //{
                    //	if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                    //	{
                    //		StartCoroutine(DelayCamUpdate());
                    //	}
                    //}
                    //else _maskCam.enabled = false;
                }

            }
            else _runUpdate = false;

            StartCoroutine(DelayStart());
        }

        private ComputeBuffer _tempBuffer;

        IEnumerator DelayStart()
        {
            if (_cloth != null)
            {
                while (!_cloth._finishedLoading)
                    yield return null;

                //yield return _waitForSeconds; //TODO needed?

                if (_otherMaskRenderer != null && _otherMaskRenderer != this)
                {
                    this._maskCam = _otherMaskRenderer._maskCam;
                    this._rt = _otherMaskRenderer._rt;
                }

                var _meshObjects = _cloth._meshObjects;

                foreach (var mesh in _meshObjects)
                {
                    if (mesh != null)
                    {
                        if (mesh == _cloth.transform || mesh.GetComponent<GPUSkinnerBase>() == null) continue;
                        if (!_maskBodies.Contains(mesh.gameObject)) _maskBodies.Add(mesh.gameObject);
                    }
                }

                foreach (var mesh in _maskBodies)
                {
                    if (mesh == null) continue;

                    if (mesh.GetComponent<GPUSkinning>() != null) mesh.GetComponent<GPUSkinning>().SetShader("Mask");
                    if (mesh.GetComponent<DualQuaternionSkinner>() != null) mesh.GetComponent<DualQuaternionSkinner>().SetShader("Mask");

                    yield return null;

                    var mats = mesh.GetComponent<Renderer>().materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i].SetTexture(_maskTexID, _rt);
                        mats[i].SetFloat(_maskThresholdID, -_depthThreshold);
                        if (GraphicsSettings.currentRenderPipeline && mats[i].shader.name.ToLower().Contains("mask"))
                        {
                            if (mats[i].GetFloat("_AlphaCutoff") <= 0) mats[i].SetFloat("_AlphaCutoff", 0.5f);
                            mats[i].SetFloat("_AlphaCutoffEnable", 1);
                        }
                    }

                    if (mesh.GetComponent<SkinnedMeshRenderer>() && mesh.GetComponent<MeshRenderer>()) Destroy(mesh.GetComponent<MeshRenderer>());
                }

                if (_copy == null)
                {
                    _copy = new GameObject("ClothCopy");
                    if (!_debugRenderer) _copy.hideFlags = HideFlags.HideAndDontSave;
                    _copy.transform.SetParent(_cloth.transform.parent, false);
                    _copy.layer = _maskLayer.FirstSetLayer();
                    var mesh = _copy.AddComponent<MeshFilter>().mesh = (_cloth._useMeshProxy && _cloth._meshProxy != null) ? _cloth._meshProxy.GetComponent<MeshFilter>().sharedMesh : _cloth.GetComponent<MeshFilter>().sharedMesh;
                    _copy.AddComponent<MeshRenderer>().materials = (_cloth._useMeshProxy && _cloth._meshProxy != null) ? _cloth._meshProxy.GetComponent<Renderer>().materials : _copy.GetComponent<Renderer>().materials;
                    _cloth.SetSecondUVsForVertexID(mesh);
                    _copy.transform.localPosition = this.transform.localPosition;
                    _copy.transform.localRotation = this.transform.localRotation;
                    _copy.transform.localScale = this.transform.localScale;

                    foreach (var item in _copy.GetComponentsInChildren<Transform>())
                    {
                        if (item.transform != _copy.transform)
                            Destroy(item.gameObject);
                    }

                    var mats = _copy.GetComponent<Renderer>().materials;
                    foreach (var mat in mats)
                    {
                        mat.shader = _shader;
                        if (_tempBuffer == null) _tempBuffer = new ComputeBuffer(1, sizeof(float) * 3);
                        mat.SetBuffer("positionsBuffer", _tempBuffer);
                        mat.SetBuffer("normalsBuffer", _tempBuffer);
                        mat.EnableKeyword("USE_BUFFERS");
                        //mat.SetFloat("_AlphaCutoffEnable", 1);
                    }

                    bool useProxy = _cloth._useMeshProxy && _cloth._meshProxy != null;

                    var mpb = new MaterialPropertyBlock();
                    mpb.SetBuffer(_positionsBufferID, useProxy ? _cloth._objBuffers[1].positionsBuffer : _cloth.GetPositionsBuffer());
                    mpb.SetBuffer(_normalsBufferID, useProxy ? _cloth._objBuffers[1].normalsBuffer : _cloth.GetNormalsBuffer());
                    _cloth.SetCustomProperties(mpb);
                    if(useProxy) mpb.SetInt("prevNumParticles", 0);
                    var mr = _copy.GetComponent<Renderer>();
                    if (mr != null) mr.SetPropertyBlock(mpb);
                }
            }
        }

        private void OnDisable()
        {
            if (_rt != null) _rt.Release();
            _rt = null;

            if (_copy != null) Destroy(_copy);

            _tempBuffer.ClearBuffer();
        }

        void LateUpdate()
        {
            if (_runUpdate && _cloth._finishedLoading)
            {
#if UNITY_EDITOR
                if (_rt.width != Screen.width / _downScaleRT || _rt.height != Screen.height / _downScaleRT)
                {
                    _rt.Release();
                    _rt.width = Screen.width / _downScaleRT;
                    _rt.height = Screen.height / _downScaleRT;
                    _rt.Create();
                }

                foreach (var mesh in _maskBodies)
                {
                    if (mesh != null)
                    {
                        var mats = mesh.GetComponent<Renderer>().materials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i].SetFloat(_maskThresholdID, -_depthThreshold);
                        }
                    }
                }

#endif

                //_maskCam.RenderWithShader(_shader, "");
                //if (!GraphicsSettings.currentRenderPipeline)
                _maskCam.Render();

                if (_dilationSteps > 0)
                {
                    var tempRT = RenderTexture.GetTemporary(_rt.width, _rt.height, _rt.depth, _rt.format);
                    Graphics.CopyTexture(_rt, tempRT);
                    _dilationMat.SetInt(_maxStepsID, _dilationSteps);
                    Graphics.Blit(tempRT, _rt, _dilationMat);
                    RenderTexture.ReleaseTemporary(tempRT);
                }
            }
        }

        private void OnGUI()
        {
            if (_debugRenderer && _maskCam != null && _maskCam.activeTexture != null)
                GUI.DrawTexture(new Rect(0, Screen.height - (Screen.height / 3), Screen.width / 3, Screen.height / 3), _maskCam.activeTexture);
        }

    }

    public static class LayerMaskExtensions
    {
        public static int FirstSetLayer(this LayerMask mask)
        {
            int value = mask.value;
            if (value == 0) return 0;  // Early out
            for (int l = 1; l < 32; l++)
                if ((value & (1 << l)) != 0) return l;  // Bitwise
            return -1;  // This line won't ever be reached but the compiler needs it
        }
    }

}