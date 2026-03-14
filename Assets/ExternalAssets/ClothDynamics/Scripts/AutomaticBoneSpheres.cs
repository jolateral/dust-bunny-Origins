using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ClothDynamics
{
    [ExecuteInEditMode]
    public class AutomaticBoneSpheres : MonoBehaviour
    {
        public enum BoneShape
        {
            Line,
            Pyramid,
            Box
        };

        [System.Serializable]
        public struct TransformPair
        {
            public Transform first;
            public Transform second;
        };

        [Tooltip("This affects the shape of the bone visualization.")]
        public BoneShape _boneShape = BoneShape.Pyramid;

        [Tooltip("This visualizes the collision spheres.")]
        public bool _drawSpheres = true;
        [Tooltip("This visualizes the bones.")]
        public bool _drawBones = true;
        [Tooltip("This visualizes the tripods.")]
        public bool _drawTripods = false;
        [Tooltip("This visualizes the round cones. A Round cone is basically two connected spheres, that can have different sizes.")]
        public bool _drawRoundCones = false;
        [Tooltip("The tolerance level of the difference between the sphere sizes until a new RoundCone gets created.")]
        public float _roundConeTolerance = 0.1f;
        [Tooltip("The distance between the spheres until a new RoundCone gets created.")]
        public float _roundConeDist = 1.0f;

        [Range(0.01f, 5.0f)]
        [Tooltip("The size of each visualized bone.")]
        public float _boneSize = 1.0f;

        [Range(0.01f, 5.0f)]
        [Tooltip("The size of each visualized tripod.")]
        public float _tripodSize = 1.0f;

        [Tooltip("The color of each visualized bone.")]
        public Color _boneColor = new Color(0f, 0f, 1f, 0.5f);

        [Tooltip("The source collider mesh that is used to determine the sizes of the spheres.")]
        public List<GameObject> _meshColliderSource = new List<GameObject>();
        [Tooltip("Instead of ray tracing use the nearest vertex. Works only with a valid _meshColliderSource.")]
        public bool _useNearestVertex = true; //TODO set to true when mesh Collider is vaild
        [Tooltip("The tracing distance from the center of a sphere to the surface of the source collider mesh.")]
        public float _traceDist = 0.15f;
        [Tooltip("The minimal radius of a sphere.")]
        public float _minRadius = 0.01f;
        [Tooltip("The sphere count per bone.")]
        public int _boneCount = 5;
        [Tooltip("The outward offset for the spheres positions.")]
        public float _outwardsPush = 1.25f;
        [Tooltip("The outward spine offset.")]
        public float _outwardsSpine = 0.6f;
        [Tooltip("The spine split count.")]
        public int _spineSplitCount = 1;
        [Tooltip("The spine forward offset.")]
        public float _spineForward = 0.03f;
        [Tooltip("The extra hip offset.")]
        public Vector3 _hipOffset = new Vector3(0, -0.05f, -0.02f);
        [Tooltip("The collision layer of the source mesh.")]
        public LayerMask _collisionLayer;
        [Tooltip("The global sphere scaling.")]
        public float _sphereScale = 1.2f;
        [Tooltip("The selected bones.")]
        public List<int> _selectedBones = new List<int>();

        [System.Serializable]
        public class PerBoneData
        {
            public float scale;
            public Vector3 offset;
            public int addBones;
        }
        [Tooltip("The per bone settings.")]
        public SerializableDictionary<int, PerBoneData> _perBoneScale = new SerializableDictionary<int, PerBoneData>();
        public ComputeBuffer _bonesBuffer;
        public ComputeBuffer _spheresBuffer;

        [System.Serializable]
        public struct SphereStruct
        {
            public float4 offset;
            public int boneId;
        }
        [SerializeField]
        public List<SphereStruct> _spheresList = new List<SphereStruct>();

        [System.Serializable]
        public struct RoundConeStruct
        {
            public float4 offset;
            public float4 otherOffset;
            public int boneId;
        }

        [SerializeField]
        public List<RoundConeStruct> _roundConeList = new List<RoundConeStruct>();

        [System.Serializable]
        public struct BonesStruct
        {
            public float4 pos;
            public float4 rot;
        }
        [SerializeField]
        public BonesStruct[] _bonesData;

        [System.Serializable]
        public class BoneNames
        {
            public List<string> hipBones = new List<string>(new string[] { "hip", "pelvis" });
            public List<string> spineBones = new List<string>(new string[] { "abdomen", "chest", "spine", "breast" });
            public List<string> armBones = new List<string>(new string[] { "arm", "shldr", "shoulder" });
            public List<string> handBones = new List<string>(new string[] { "hand" });
            public List<string> headBones = new List<string>(new string[] { "head" });
            public List<string> footBones = new List<string>(new string[] { "foot" });
            public List<string> legBones = new List<string>(new string[] { "leg", "thigh" });
        }
        [Tooltip("The bone names that are needed for the algorthim to work properly.")]
        public BoneNames _boneNamesList = new BoneNames();
        [Tooltip("The bone names that will be ignored. Currently only meshes set as humanoids work properly.")]
        public List<string> _ignoreBonesList = new List<string>(new string[] { "ABS", "foot", "hand", "head" });

        [SerializeField]
        [Tooltip("All the transforms that were found.")]
        private Transform[] _iTransforms;

        [SerializeField]
        [Tooltip("All the bones that are used.")]
        private TransformPair[] _iBones;

        private bool _initMeshArrays = true;
        private Vector3[] _mcVerts = null;
        private Vector3[] _mcNormals = null;
        private GameObject[] _cmGo;

        public Transform[] transforms
        {
            get { return _iTransforms; }
            set
            {
                _iTransforms = value;
                ExtractBones();
            }
        }

        public TransformPair[] _bones { get => _iBones; }

        [Tooltip("All tips that were found.")]
        private Transform[] _iTips;
        public Transform[] _tips { get => _iTips; }

        public delegate void OnAddBoneRendererCallback(AutomaticBoneSpheres boneRenderer);
        public delegate void OnRemoveBoneRendererCallback(AutomaticBoneSpheres boneRenderer);

        public static OnAddBoneRendererCallback _onAddBoneRenderer;
        public static OnRemoveBoneRendererCallback _onRemoveBoneRenderer;
        private bool _InUse = false;

        internal bool _updateSync = false;

        public bool _createCollidersAndRenderers = false;


        void OnEnable()
        {
            //this.OnVariableChange += VariableChangeHandler;
            //if (!Application.isPlaying)
            {
                if (_meshColliderSource == null || _meshColliderSource.Count < 1 || _meshColliderSource[0] == null)
                {
                    if (_meshColliderSource == null) _meshColliderSource = new List<GameObject>();

                    var skins = this.GetComponentsInChildren<SkinnedMeshRenderer>();
                    int lastVertexCount = 0;
                    int c = 0;
                    foreach (var item in skins)
                    {
                        int vCount = item.sharedMesh.vertexCount;
                        if (item.enabled /*&& vCount > lastVertexCount*/)
                        {
                            lastVertexCount = vCount;
                            if (_meshColliderSource.Count < c + 1)
                                _meshColliderSource.Add(item.gameObject);
                            else
                                _meshColliderSource[c] = item.gameObject;
                            //break;
                            _collisionLayer.value = 1 << item.gameObject.layer;
                            c++;
                        }
                    }
                }
                CreateBoneNamesList();
                //if (m_Transforms == null || m_Transforms.Length == 0) 
                ClothDynamics.AnimationRiggingEditorUtils.BoneRendererSetup(this.transform, _boneNamesList, _ignoreBonesList);
                ExtractBones();
            }
#if UNITY_EDITOR
            _onAddBoneRenderer?.Invoke(this);
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            _onRemoveBoneRenderer?.Invoke(this);
#endif
            //this.OnVariableChange -= VariableChangeHandler;
            DestroyCmGos();
        }

        private void DestroyCmGos()
        {
            if (_cmGo != null)
            {
                for (int i = _cmGo.Length - 1; i >= 0; --i)
                {
                    if (_cmGo[i] != null) DestroyImmediate(_cmGo[i]);
                }
            }
        }

        public void Awake()
        {
            if (Application.isPlaying && enabled)
            {
                _InUse = false;
                var gpuSims = this.GetComponentsInChildren<GPUClothDynamics>();
                foreach (var gpuSim in gpuSims)
                {
                    if ((gpuSim._meshObjects != null && gpuSim._meshObjects.Contains(this.transform)) || (gpuSim._collidableObjects != null && gpuSim._collidableObjects.Any(x => _iBones.Any(y => y.first.gameObject == x))))
                        _InUse = true;
                }
                _InUse = this.GetComponentsInChildren<ClothObjectGPU>() != null;
                if (_InUse)
                {
                    if (_iBones != null)
                    {
                        _bonesData = new BonesStruct[_iBones.Length];

                        int length = _iBones.Length;
                        for (int i = 0; i < length; ++i)
                        {
                            var bone = _iBones[i].first;
                            _bonesData[i].pos.xyz = bone.position;
                            _bonesData[i].rot = QuatToVec(bone.rotation);
                        }
                    }
                    if (_bonesData != null)
                    {
                        _bonesBuffer = new ComputeBuffer(_bonesData.Length, sizeof(float) * 8);
                        _bonesBuffer.SetData(_bonesData);
                    }
                    //print("_spheresList.Count " + _spheresList.Count);
                    if (_spheresList.Count > 0)
                    {
                        _spheresBuffer = new ComputeBuffer(_spheresList.Count, sizeof(float) * 5);
                        _spheresBuffer.SetData(_spheresList);
                    }
                    else this.enabled = false;
                }
                else this.enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!_updateSync) UpdateSync();
        }

        public void UpdateSync()
        {
            if (Application.isPlaying && _InUse)
            {
                if (_iBones != null)
                {
                    int length = _iBones.Length;
                    for (int i = 0; i < length; ++i)
                    {
                        var bone = _iBones[i].first;
                        if (i < _iBones.Length && i < _bonesData.Length)
                        {
                            _bonesData[i].pos.xyz = bone.position;
                            _bonesData[i].rot = QuatToVec(bone.rotation);
                        }
                    }
                }
                if(_bonesData!=null && _bonesBuffer != null) _bonesBuffer.SetData(_bonesData);
            }
        }

        private void OnDestroy()
        {
            _bonesBuffer?.Release();
            _spheresBuffer?.Release();
            DestroyCmGos();
        }

        public void Invalidate()
        {
            ExtractBones(updateSpheres: false);
        }

        public void Reset()
        {
            ClearBones();
        }

        public void ClearBones()
        {
            _iBones = null;
            DestroyCmGos();
        }

        IEnumerator DelayedEnable()
        {
            yield return null;
            this.enabled = true;
        }

        public void BoneRendererSetupChanged()
        {
            this.enabled = false;
            StartCoroutine(DelayedEnable());
        }

        public void ExtractBones(bool updateSpheres = true)
        {
            //if (Application.isPlaying) return;

            if (_iTransforms == null || _iTransforms.Length == 0)
            {
                ClearBones();
                return;
            }

            var transformsHashSet = new HashSet<Transform>(_iTransforms);

            var bonesList = new List<TransformPair>(_iTransforms.Length);
            var tipsList = new List<Transform>(_iTransforms.Length);

            for (int i = 0; i < _iTransforms.Length; ++i)
            {
                bool hasValidChildren = false;

                var transform = _iTransforms[i];
                if (transform == null)
                    continue;
                //#if UNITY_EDITOR
                //                if (SceneVisibilityManager.instance.IsHidden(transform.gameObject, false))
                //                    continue;
                //#endif
                if (transform.childCount > 0)
                {
                    for (var k = 0; k < transform.childCount; ++k)
                    {
                        var childTransform = transform.GetChild(k);

                        if (transformsHashSet.Contains(childTransform))
                        {
                            bonesList.Add(new TransformPair() { first = transform, second = childTransform });
                            hasValidChildren = true;
                        }
                    }
                }

                if (!hasValidChildren)
                {
                    tipsList.Add(transform);
                }
            }

            _iBones = bonesList.ToArray();
            _iTips = tipsList.ToArray();

            if (updateSpheres) CreateSpheres();
        }

        void TraceAxis(Vector3 center, Vector3 dir, ref float averageRadius, ref float hitCount)
        {
            var normal = Vector3.Cross(dir, Vector3.up);
            if (Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) == 1)
                normal = Vector3.Cross(dir, Vector3.right);
            normal.Normalize();
            int layer = _collisionLayer.value;
            for (int d = 0; d < 36; ++d)
            {
                var nVec = Quaternion.AngleAxis(d * 10, dir) * normal;// + Vector3.one * (Random.value * 2 - 1) * 0.1f;
                var hits = Physics.RaycastAll(center + nVec * _traceDist, -nVec, _traceDist, layer);
                float nearestDist = float.MaxValue;
                int nearestNum = 0;
                for (int m = 0; m < hits.Length; m++)
                {
                    var pos = hits[m].point;
                    float dist = Vector3.Distance(pos, center);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestNum = m;
                    }
                }
                if (hits != null && hits.Length > 0)
                {
                    averageRadius = Mathf.Min(averageRadius, Vector3.Distance(hits[nearestNum].point, center));
                    hitCount++;
                }
            }
        }

        void TraceCenter(Vector3 center, Vector3 dir, ref Vector3 averagePoint, ref float hitCount)
        {
            var centerMesh = this.transform.position;// + _centerOffset;

            var normal = Vector3.Cross(dir, Vector3.up);
            if (Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) == 1)
                normal = Vector3.Cross(dir, Vector3.right);
            normal.Normalize();

            int layer = _collisionLayer.value;
            for (int d = 0; d < 72; ++d)
            {
                var nVec = Quaternion.AngleAxis(d * 5, dir) * normal;// + Vector3.one * (Random.value * 2 - 1) * 0.1f;
                var hits = Physics.RaycastAll(center + nVec * _traceDist, -nVec, _traceDist, layer);

                if (hits != null && hits.Length > 0)
                {
                    Vector3 average = Vector3.zero;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var point = hits[i].point;
                        centerMesh.y = point.y;
                        average += point + (point - center) * Vector3.Distance(point, centerMesh) * _outwardsPush;
                        //float dotLen = Mathf.Max(0,Vector3.Dot((point - center).normalized, (point - centerMesh).normalized));
                        //average += point + (point - center) * dotLen * _outwardsPush;
                    }
                    average /= (float)hits.Length;

                    centerMesh.y = average.y;
                    averagePoint += average + (average - center) * Vector3.Distance(average, centerMesh) * _outwardsPush;
                    //float dx = Mathf.Max(0, Vector3.Dot((average - center).normalized, (average - centerMesh).normalized));
                    //averagePoint += average + (average - center) * dx * _outwardsPush;
                    hitCount++;
                }
            }
        }

        float CreateConstraint(Vector3 center, Vector3 dir)
        {
            float averageRadius = _traceDist <= 0 ? 1 : _traceDist;
            float hitCount = 0;
            TraceAxis(center, dir, ref averageRadius, ref hitCount);
            return averageRadius;
        }

        Vector3 CreateCenter(Vector3 center, Vector3 dir)
        {
            Vector3 averageCenter = Vector3.zero;
            float hitCount = 0;

            TraceCenter(center, dir, ref averageCenter, ref hitCount);
            if (averageCenter.sqrMagnitude > 0 && hitCount > 0)
            {
                averageCenter = averageCenter / hitCount;
            }
            else
            {
                averageCenter = center;
            }
            return averageCenter;
        }

        public float NearestVertexTo(Vector3 point)
        {
            float finalRadius = float.MaxValue;
            int count = _meshColliderSource.Count;
            for (int n = 0; n < count; n++)
            {
                var mcs = _meshColliderSource[n];
                point = mcs.transform.InverseTransformPoint(point);

                if (_initMeshArrays)
                {
                    Mesh mesh = null;
                    if (mcs.GetComponent<MeshCollider>())
                    {
                        mesh = mcs.GetComponent<MeshCollider>().sharedMesh;
                    }
                    else if (mcs.GetComponent<SkinnedMeshRenderer>())
                    {
                        //mesh = mcs.GetComponent<SkinnedMeshRenderer>().sharedMesh;
                        mesh = new Mesh();
                        mcs.GetComponent<SkinnedMeshRenderer>().BakeMesh(mesh);
                    }
                    else if (mcs.GetComponent<MeshFilter>())
                    {
                        mesh = mcs.GetComponent<MeshFilter>().sharedMesh;
                    }
                    else
                    {
                        Debug.LogError("_meshColliderSource " + mcs + " has no mesh applied!");
                        return 0;
                    }
                    _mcVerts = mesh.vertices;
                    _mcNormals = mesh.normals;
                    _initMeshArrays = false;
                }

                int nearestIndex = 0;
                float minDistanceSqr = Mathf.Infinity;

                for (int i = 0; i < _mcVerts.Length; ++i)
                {
                    var vertex = _mcVerts[i];
                    Vector3 diff = point - vertex;
                    float distSqr = diff.sqrMagnitude;
                    if (distSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        nearestIndex = i;
                    }
                }
                var d = _mcNormals != null && nearestIndex < _mcNormals.Length ? Vector3.Dot(_mcNormals[nearestIndex], point - _mcVerts[nearestIndex]) : 0;
                var radius = d > -0.01f ? 0 : Mathf.Sqrt(minDistanceSqr);
                if (radius < finalRadius)
                {
                    finalRadius = radius;
                }
            }
            return finalRadius;
        }

        void CreateBoneNamesList()
        {
            var animator = this.GetComponent<Animator>();
            if (animator != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    var list =
                        i == 0 ? _boneNamesList.hipBones :
                        i == 1 ? _boneNamesList.spineBones :
                        i == 2 ? _boneNamesList.armBones :
                        i == 3 ? _boneNamesList.handBones :
                        i == 4 ? _boneNamesList.headBones :
                                 _boneNamesList.footBones
                        ;
                    var humanBoneId =
                        i == 0 ? HumanBodyBones.Hips :
                        i == 1 ? HumanBodyBones.Spine :
                        i == 2 ? HumanBodyBones.LeftLowerArm :
                        i == 3 ? HumanBodyBones.LeftHand :
                        i == 4 ? HumanBodyBones.Head :
                                 HumanBodyBones.LeftFoot
                        ;
                    var bone = animator.GetBoneTransform(humanBoneId);

                    if (bone == null && _iBones != null && _iBones.Length > 0)
                    {
                        bone = _iBones.Where(x => x.first != null && x.first.name.CheckIfContains(list)).FirstOrDefault().first;
                    }

                    if (bone != null)
                    {
                        var hipName = bone.name.ToLower();
                        bool skip = false;
                        foreach (var boneName in list)
                        {
                            if (hipName.Contains(boneName) || boneName.Contains(hipName))
                                skip = true;
                        }
                        if (!skip)
                        {
                            list.Add(hipName);
                        }
                    }
                }
            }
        }

        private void CreateSpheres()
        {
            _initMeshArrays = true;
            bool autoAddedCollider = false;
            bool autoEnableCollider = false;
            int mcsLength = _meshColliderSource.Count;
            GameObject[] oldColliderSource = new GameObject[mcsLength];
            MeshCollider[] mc = new MeshCollider[mcsLength];
            _cmGo = new GameObject[mcsLength];
            for (int i = 0; i < mcsLength; i++)
            {
                var mcs = _meshColliderSource[i];
                if (mcs != null)
                {
                    _collisionLayer.value = 1 << mcs.layer;
                    if (!mcs.GetComponent<MeshCollider>())
                    {
                        if (_cmGo[i] == null)
                        {
                            _cmGo[i] = new GameObject("TempColliderSourceObject");
                            _cmGo[i].layer = mcs.layer;
                            _cmGo[i].hideFlags = HideFlags.HideAndDontSave;
                            _cmGo[i].transform.SetPositionAndRotation(mcs.transform.position, mcs.transform.rotation);
                            //_cmGo.transform.localScale = mcs.transform.localScale;
                            var skin = mcs.gameObject.GetComponent<SkinnedMeshRenderer>();
                            var baked = new Mesh();
                            if (skin) skin.BakeMesh(baked);
                            var col = _cmGo[i].AddComponent<MeshCollider>();
                            col.sharedMesh = baked;
                            col.enabled = true;
                        }
                        oldColliderSource[i] = mcs;
                        mcs = _cmGo[i];
                        autoAddedCollider = true;
                    }
                    else
                    {
                        AddMissingMesh(mcs.GetComponent<MeshCollider>());
                    }

                    mc[i] = mcs.GetComponent<MeshCollider>();
                    if (mc[i])
                    {
                        autoEnableCollider = mc[i].enabled;
                        mc[i].enabled = true;
                    }
                }
            }
            _spheresList.Clear();
            _roundConeList.Clear();
            float radius = _traceDist;

            if (_perBoneScale == null) _perBoneScale = new SerializableDictionary<int, PerBoneData>();

            while (_perBoneScale.Count < _iBones.Length)
            {
                _perBoneScale.Add(_perBoneScale.Count, new PerBoneData { scale = 1.0f, offset = Vector3.zero, addBones = 0 });
            }
            while (_perBoneScale.Count > _iBones.Length)
            {
                _perBoneScale.Remove(_perBoneScale.Count - 1);
            }

            if (_iBones != null && _iBones.Length > 0)
            {
                for (int i = 0; i < _iBones.Length; i++)
                {
                    var pos0 = _iBones[i].first.position;
                    var pos1 = _iBones[i].second.position;

                    int bCount = _boneCount + _perBoneScale[i].addBones;
                    float step = 1.0f / Mathf.Max(1.0f, bCount - 1);

                    int maxSplitCount = math.max(1, _spineSplitCount);
                    float[] lastRadius = new float[maxSplitCount];
                    float[] biggerRadius = new float[maxSplitCount];
                    float[] smallerRadius = new float[maxSplitCount];
                    Vector3[] lastCenter = new Vector3[maxSplitCount];
                    for (int n = 0; n < bCount; n++)
                    {
                        var center = pos0 + (pos1 - pos0) * (step * n);
                        int splitCount = 1;

                        //if (!m_Bones[i].first.name.ToLower().Contains("arm") && !m_Bones[i].first.name.ToLower().Contains("shldr"))
                        if (!_iBones[i].first.name.CheckIfContains(_boneNamesList.armBones))
                            center = CreateCenter(center, pos1 - pos0);

                        //if (m_Bones[i].first.name.ToLower().Contains("abdomen") || m_Bones[i].first.name.ToLower().Contains("chest"))
                        if (_iBones[i].first.name.CheckIfContains(_boneNamesList.spineBones))
                        {
                            splitCount = _spineSplitCount;
                            center += this.transform.forward * _spineForward;
                        }

                        //if (m_Bones[i].first.name.ToLower().Contains("hip") || m_Bones[i].first.name.ToLower().Contains("pelvis"))
                        if (_iBones[i].first.name.CheckIfContains(_boneNamesList.hipBones))
                        {
                            center += _hipOffset;
                        }

                        center += _perBoneScale[i].offset;

                        float splitStep = 1.0f / Mathf.Max(1.0f, splitCount - 1);

                        for (int p = 0; p < splitCount; p++)
                        {
                            var newCenter = center;
                            if (splitCount > 1)
                            {
                                var legTransforms = _iBones.Where(x => x.first.name.CheckIfContains(_boneNamesList.legBones));

                                var rLeg = legTransforms.FirstOrDefault(x => x.first.name.ToLower().Contains("r")).first;//this.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightUpperLeg);
                                var lLeg = legTransforms.FirstOrDefault(x => x.first.name.ToLower().Contains("l")).first;//this.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftUpperLeg);

                                var center0 = center * (1.0f - _outwardsSpine);
                                if (rLeg) center0 += rLeg.position * _outwardsSpine;
                                var center1 = center * (1.0f - _outwardsSpine);
                                if (lLeg) center1 += lLeg.position * _outwardsSpine;

                                newCenter = center0 + (center1 - center0) * (splitStep * p);
                                newCenter.y = center.y;
                            }

                            if (_meshColliderSource != null && mcsLength > 0 && _useNearestVertex)
                                radius = NearestVertexTo(newCenter);
                            else
                                radius = CreateConstraint(newCenter, pos1 - pos0);

                            radius = math.max(radius, _minRadius);

                            radius *= _sphereScale;
                            radius *= _perBoneScale[i].scale;

                            if (radius > 0)
                            {
                                var offset = Rotate(QuatToVec(Inverse(_iBones[i].first.rotation)), newCenter - pos0);
                                //if (n == bCount - 1) offset = Rotate(QuatToVec(Inverse(m_Bones[i].first.rotation)), pos1 - pos0);

                                _spheresList.Add(new SphereStruct() { offset = new float4(offset.x, offset.y, offset.z, radius), boneId = i });

                                if (lastRadius[p] > 0 && (lastRadius[p] != radius || n == bCount - 1))
                                {
                                    if (Vector3.Distance(newCenter, lastCenter[p]) > _roundConeDist || radius < biggerRadius[p] - _roundConeTolerance || radius > smallerRadius[p] + _roundConeTolerance || n == bCount - 1)
                                    {
                                        var lastOffset = Rotate(QuatToVec(Inverse(_iBones[i].first.rotation)), lastCenter[p] - pos0);
                                        _roundConeList.Add(new RoundConeStruct() { offset = new float4(offset.x, offset.y, offset.z, radius), otherOffset = new float4(lastOffset.x, lastOffset.y, lastOffset.z, lastRadius[p]), boneId = i });
                                        lastRadius[p] = radius;
                                        biggerRadius[p] = radius;
                                        smallerRadius[p] = radius;
                                        lastCenter[p] = newCenter;
                                    }
                                    if (radius > lastRadius[p])
                                        biggerRadius[p] = radius;
                                    if (radius < lastRadius[p])
                                        smallerRadius[p] = radius;
                                }
                            }
                            if (n == 0)
                            {
                                lastRadius[p] = radius;
                                lastCenter[p] = newCenter;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < mcsLength; i++)
            {
                if (mc[i]) mc[i].enabled = autoEnableCollider;

                if (autoAddedCollider && _meshColliderSource != null)
                {
                    _meshColliderSource[i] = oldColliderSource[i];
                    if (_cmGo[i] != null) StartCoroutine(DestoryDelayed(_cmGo[i]));
                }
            }
        }

        IEnumerator DestoryDelayed(GameObject cmGo)
        {
            yield return null;
            DestroyImmediate(cmGo);
        }

        private void AddMissingMesh(MeshCollider mc)
        {
            int length = _meshColliderSource.Count;
            for (int i = 0; i < length; i++)
            {
                var mcs = _meshColliderSource[i];
                if (mc.sharedMesh == null && mcs.GetComponent<SkinnedMeshRenderer>())
                {
                    //mc.sharedMesh = mcs.GetComponent<SkinnedMeshRenderer>().sharedMesh;
                    mc.sharedMesh = new Mesh();
                    mcs.GetComponent<SkinnedMeshRenderer>().BakeMesh(mc.sharedMesh);
                }
                if (mc.sharedMesh == null && mcs.GetComponent<MeshFilter>())
                    mc.sharedMesh = mcs.GetComponent<MeshFilter>().sharedMesh;
            }
        }

        public void ConvertToColliders(bool useCones = false, bool prompt = false)
        {
            List<GameObject> sphereColliders = new List<GameObject>();
            int length = useCones ? _roundConeList.Count : _spheresList.Count;
            bool exists = false;
            bool addMode = false;
            var colliderName = useCones ? "ABS_RoundConeCollider_" : "ABS_SphereCollider_";
            if (length > 0 && _iBones != null)
            {
                for (int i = 0; i < length; i++)
                {
                    var boneId = useCones ? _roundConeList[i].boneId : _spheresList[i].boneId;
                    if (boneId < _iBones.Length)
                    {
                        var bone = _iBones[boneId].first;
                        if (!exists)
                        {
                            var children = bone.GetComponentsInChildren<Transform>();
                            for (int n = children.Length - 1; n >= 0; --n)
                            {
                                if (children[n].name.StartsWith(colliderName))
                                {
#if UNITY_EDITOR
                                    if (prompt || !EditorUtility.DisplayDialog("Found ABS Colliders In Bones Hierarchy", "ABS Colliders are already in the bones hierarchy, do you still want to add new onces ?", "Yes(Not Recommended)", "No"))
                                    {
                                        exists = true;
                                    }
                                    else
                                    {
                                        addMode = true;
                                    }
#else
                                    exists = true;
#endif
                                    break;
                                }
                            }
                        }
                        if (exists || addMode) break;
                    }
                }
            }
            if (!exists)
            {
                if (length > 0 && _iBones != null)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var boneId = useCones ? _roundConeList[i].boneId : _spheresList[i].boneId;
                        var offset = useCones ? _roundConeList[i].offset : _spheresList[i].offset;
                        if (boneId < _iBones.Length)
                        {
                            var bone = _iBones[boneId].first;
                            var spherePos = bone.position + (Vector3)Rotate(QuatToVec(bone.rotation), offset.xyz);
                            GameObject go = _createCollidersAndRenderers ? GameObject.CreatePrimitive(PrimitiveType.Sphere) : new GameObject();
                            go.name = colliderName + i;
                            go.transform.position = spherePos;
                            go.transform.localScale = Vector3.one * offset.w * 2;
                            go.transform.parent = bone;
                            if (go.GetComponent<Renderer>()) go.GetComponent<Renderer>().enabled = false;

                            if (useCones)
                            {
                                var rcc = go.AddComponent<RoundConeCollider>();
                                var otherOffset = _roundConeList[i].otherOffset;
                                spherePos = bone.position + (Vector3)Rotate(QuatToVec(bone.rotation), otherOffset.xyz);
                                GameObject go2 = _createCollidersAndRenderers ? GameObject.CreatePrimitive(PrimitiveType.Sphere) : new GameObject();
                                go2.name = "ABS_RoundConeColliderPartner_" + i;
                                go2.transform.position = spherePos;
                                go2.transform.localScale = Vector3.one * otherOffset.w * 2;
                                go2.transform.parent = bone;
                                if (go2.GetComponent<Renderer>()) go2.GetComponent<Renderer>().enabled = false;
                                rcc.otherSphere = go2.transform;
                            }
                            sphereColliders.Add(go);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    var boneId = useCones ? _roundConeList[i].boneId : _spheresList[i].boneId;
                    if (boneId < _iBones.Length)
                    {
                        var bone = _iBones[boneId].first;
                        var children = bone.GetComponentsInChildren<Transform>();
                        var list = children.Where(x => x.name.StartsWith(colliderName)).Select(x => x.gameObject);
                        sphereColliders.AddRange(list);
                    }
                }
            }

            sphereColliders = sphereColliders.Distinct().ToList();

            //Debug.Log("Colliders " + sphereColliders.Count);

            var gpuSims = this.GetComponentsInChildren<GPUClothDynamics>();
            foreach (var gpuSim in gpuSims)
            {
                bool write = true;
#if UNITY_EDITOR
                if (!prompt && !EditorUtility.DisplayDialog("Copy colliders to cloth list", "Do you want to add the colliders to the \"" + gpuSim.name + "\" cloth ?", "Yes", "No"))
                {
                    write = false;
                }
#endif
                if (write)
                {
                    gpuSim._useCollidableObjectsList = true;
                    Extensions.CleanupList(ref gpuSim._collidableObjects);
                    foreach (var item in sphereColliders)
                    {
                        if (!gpuSim._collidableObjects.Contains(item)) gpuSim._collidableObjects.Add(item);
                    }
                }
            }

            if (!_createCollidersAndRenderers)
            {
                var cds = FindObjectsOfType<GPUClothDynamicsV2>();
                foreach (var cd in cds)
                {
                    bool write = true;
#if UNITY_EDITOR
                    if (!prompt && !EditorUtility.DisplayDialog("Copy colliders to cloth list", "Do you want to add the colliders to the \"" + cd.name + "\" solver ?", "Yes", "No"))
                    {
                        write = false;
                    }
#endif
                    if (write)
                    {
                        //gpuSim._useCollidableObjectsList = true;
                        List<GameObject> collidableObjects = null;
                        if (cd._solver._absColliders != null)
                            collidableObjects = cd._solver._absColliders.ToList();
                        else
                            collidableObjects = new List<GameObject>();

                        Extensions.CleanupList(ref collidableObjects);

                        foreach (var item in sphereColliders)
                        {
                            if (!collidableObjects.Contains(item)) collidableObjects.Add(item);
                        }
                        cd._solver._absColliders = collidableObjects.ToArray();
                    }
                }
            }
        }

        public void ClearABSCollidersInBonesHierarchy()
        {
            var gpuSims = this.GetComponentsInChildren<GPUClothDynamics>();
            var cds = FindObjectsOfType<GPUClothDynamicsV2>();

            var children = this.transform.GetComponentsInChildren<Transform>();
            for (int n = children.Length - 1; n >= 0; --n)
            {
                if (children[n].name.StartsWith("ABS_SphereCollider") || children[n].name.StartsWith("ABS_RoundConeCollider"))
                {
                    var go = children[n].gameObject;
                    foreach (var gpuSim in gpuSims)
                    {
                        gpuSim._collidableObjects.Remove(go);
                    }
                    foreach (var cd in cds)
                    {
                        var collidableObjects = cd._solver._absColliders.ToList();
                        collidableObjects.Remove(go);
                        cd._solver._absColliders = collidableObjects.ToArray();
                    }
                    DestroyImmediate(go);
                }
            }

            int length = _spheresList.Count;
            if (length > 0 && _iBones != null)
            {
                for (int i = 0; i < length; i++)
                {
                    var data = _spheresList[i];
                    if (data.boneId < _iBones.Length)
                    {
                        var bone = _iBones[data.boneId].first;
                        children = bone.GetComponentsInChildren<Transform>();
                        for (int n = children.Length - 1; n >= 0; --n)
                        {
                            if (children[n].name.StartsWith("ABS_SphereCollider") || children[n].name.StartsWith("ABS_RoundConeCollider"))
                            {
                                var go = children[n].gameObject;
                                foreach (var gpuSim in gpuSims)
                                {
                                    gpuSim._collidableObjects.Remove(go);
                                }
                                foreach (var cd in cds)
                                {
                                    var collidableObjects = cd._solver._absColliders.ToList();
                                    collidableObjects.Remove(go);
                                    cd._solver._absColliders = collidableObjects.ToArray();
                                }
                                DestroyImmediate(go);
                            }
                        }
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (this.enabled)
            {
                //CreateSpheres();
                if (_drawSpheres)
                {
                    int length = _spheresList.Count;
                    if (length > 0 && _iBones != null)
                    {
                        Gizmos.color = Color.white;
                        for (int i = 0; i < length; i++)
                        {
                            var data = _spheresList[i];
                            if (data.boneId < _iBones.Length)
                            {
                                var bone = _iBones[data.boneId].first;
                                var spherePos = bone.position + (Vector3)Rotate(QuatToVec(bone.rotation), data.offset.xyz);
                                Gizmos.DrawWireSphere(spherePos, data.offset.w);
                            }
                        }
                    }
                }
                if (_drawRoundCones)
                {
                    int length = _roundConeList.Count;
                    if (length > 0 && _iBones != null)
                    {
                        Gizmos.color = Color.green;
                        for (int i = 0; i < length; i++)
                        {
                            var data = _roundConeList[i];
                            if (data.boneId < _iBones.Length)
                            {
                                var bone = _iBones[data.boneId].first;
                                var spherePos = bone.position + (Vector3)Rotate(QuatToVec(bone.rotation), data.offset.xyz);
                                var spherePos2 = bone.position + (Vector3)Rotate(QuatToVec(bone.rotation), data.otherOffset.xyz);
                                var r1 = data.offset.w;
                                var r2 = data.otherOffset.w;

                                Gizmos.DrawWireSphere(spherePos, data.offset.w);
                                Gizmos.DrawWireSphere(spherePos2, data.otherOffset.w);
                                Gizmos.DrawLine(spherePos + bone.right * r1, spherePos2 + bone.right * r2);
                                Gizmos.DrawLine(spherePos - bone.right * r1, spherePos2 - bone.right * r2);
                                Gizmos.DrawLine(spherePos + bone.forward * r1, spherePos2 + bone.forward * r2);
                                Gizmos.DrawLine(spherePos - bone.forward * r1, spherePos2 - bone.forward * r2);

                            }
                        }
                    }

                }
            }

        }

        Quaternion Inverse(Quaternion quaternion)
        {
            Quaternion quaternion2;
            float num2 = quaternion.x * quaternion.x + quaternion.y * quaternion.y + quaternion.z * quaternion.z + quaternion.w * quaternion.w;
            float num = 1f / num2;
            quaternion2.x = -quaternion.x * num;
            quaternion2.y = -quaternion.y * num;
            quaternion2.z = -quaternion.z * num;
            quaternion2.w = quaternion.w * num;
            return quaternion2;
        }

        float4 QuatToVec(Quaternion rot)
        {
            float4 rotVec;
            rotVec.x = rot.x;
            rotVec.y = rot.y;
            rotVec.z = rot.z;
            rotVec.w = rot.w;
            return rotVec;
        }

        float3 Rotate(float4 q, float3 v)
        {
            float3 t = 2.0f * math.cross(q.xyz, v);
            return v + q.w * t + math.cross(q.xyz, t); //changed q.w to -q.w;
        }

        [System.Serializable]
        public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
        {
            [SerializeField]
            private List<TKey> keys = new List<TKey>();

            [SerializeField]
            private List<TValue> values = new List<TValue>();

            // save the dictionary to lists
            public void OnBeforeSerialize()
            {
                keys.Clear();
                values.Clear();
                foreach (KeyValuePair<TKey, TValue> pair in this)
                {
                    keys.Add(pair.Key);
                    values.Add(pair.Value);
                }
            }

            // load dictionary from lists
            public void OnAfterDeserialize()
            {
                this.Clear();

                if (keys.Count != values.Count)
                    throw new System.Exception(string.Format("there are {0} keys and {1} values after deserialization. Make sure that both key and value types are serializable."));

                for (int i = 0; i < keys.Count; i++)
                    this.Add(keys[i], values[i]);
            }
        }

    }

    public static class AnimationRiggingEditorUtils
    {
        public static bool CheckIfContains(this string name, List<string> list)
        {
            string tempName = name.ToLower();
            foreach (var item in list)
            {
                string str = item.ToLower();
                if (!string.IsNullOrEmpty(str) && !string.IsNullOrWhiteSpace(str) && tempName.Contains(str))
                    return true;
            }
            return false;
        }

        public static void BoneRendererSetup(Transform transform, AutomaticBoneSpheres.BoneNames boneNamesList, List<string> ignoreBonesList)
        {
            var boneRenderer = transform.GetComponent<AutomaticBoneSpheres>();
#if UNITY_EDITOR
            if (boneRenderer == null)
                boneRenderer = Undo.AddComponent<AutomaticBoneSpheres>(transform.gameObject);
            else
                Undo.RecordObject(boneRenderer, "Bone renderer setup.");
#else
            if (boneRenderer == null)
                boneRenderer = transform.gameObject.AddComponent<AutomaticBoneSpheres>();
#endif
            var animator = transform.GetComponent<Animator>();
            var renderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bones = new List<Transform>();
            if (animator != null && renderers != null && renderers.Length > 0)
            {
                for (int i = 0; i < renderers.Length; ++i)
                {
                    var renderer = renderers[i];
                    for (int j = 0; j < renderer.bones.Length; ++j)
                    {
                        var bone = renderer.bones[j];
                        if (bone.GetComponent<SphereCollider>() == null)
                        {
                            var parentBones = bone.GetComponentsInParent<Transform>(true);
                            if (!parentBones.Any(x => x.name.CheckIfContains(ignoreBonesList)))
                            {
                                if (!bones.Contains(bone))
                                {
                                    bones.Add(bone);

                                    for (int k = 0; k < bone.childCount; k++)
                                    {
                                        var child = bone.GetChild(k);
                                        if (!child.name.CheckIfContains(ignoreBonesList))
                                            if (child.GetComponent<SphereCollider>() == null)
                                                if (!bones.Contains(child))
                                                    bones.Add(child);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                bones.AddRange(transform.GetComponentsInChildren<Transform>());
            }

            boneRenderer.transforms = bones.ToArray();
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabInstance(boneRenderer))
                EditorUtility.SetDirty(boneRenderer);
#endif

        }
    }
}