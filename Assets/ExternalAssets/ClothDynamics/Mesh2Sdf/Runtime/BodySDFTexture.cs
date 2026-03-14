using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;


namespace ClothDynamics
{
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
    public class BodySDFTexture : SDFTexture
#else
    public class BodySDFTexture : MonoBehaviour
#endif
    {
        [SerializeField]
        [Tooltip("Either a static 3DTexture asset containing an SDF, or a 3D RenderTexture. A 3D RenderTexture is where MeshToSDF writes the SDF.")]
        Texture _SDF;
        [SerializeField]
        [Tooltip("Size of the volume. The effective size of the volume will be rounded off to the nearest full voxel, to keep voxels cubic.")]
        Vector3 _Size = Vector3.one;
        [SerializeField]
        [Tooltip("Voxel count along each axis. Y and Z resolutions are calculated automatically from X and proportions of the volume. Voxel counts above 64^3 might lead to poor performance.")]
        int _Resolution = 64;
        [SerializeField]
        [Tooltip("This Transform is optional and will center the sdf texture to the center transform position. Only the position is use!")]
        public Transform _center;

        public new Texture sdf { get { ValidateTexture(); return _SDF; } set { _SDF = value; } }


        RenderTexture _SDFrev = null;

        RenderTexture _myTexture = null;

        public RenderTexture sdfPrev { get { if (_SDFrev == null && sdf.GetType() == typeof(RenderTexture)) _SDFrev = new RenderTexture(((RenderTexture)sdf).descriptor); return _SDFrev; } set { _SDFrev = value; } }

        public new Vector3 size { get { return _Size; } set { _Size = value; ValidateSize(); } }
        public new int resolution { get { return _Resolution; } set { _Resolution = value; ValidateResolution(); } }

        // Max 3D texture resolution in any dimension is 2048
        int _kMaxResolution = 2048;
        // Max compute buffer size
        int _kMaxVoxelCount = 1024 * 1024 * 1024 / 2;

        private void FixedUpdate() //TODO check if FixedUpdate is the best solution
        {
            ValidateCenter();
        }

#if !HAS_PACKAGE_DEMOTEAM_MESHTOSDF
        public enum Mode
        {
            None,
            Static,
            Dynamic
        }
#endif
        public Mode _mode = Mode.Dynamic;
        public new Mode mode
        {
            get
            {
                if ((_SDF as Texture3D) != null)
                    return Mode.Static;

                RenderTexture rt = _SDF as RenderTexture;
                if (rt != null && rt.dimension == TextureDimension.Tex3D)
                    return Mode.Dynamic;

                return Mode.None;
            }
            set
            {
                _mode = value;
            }
        }

        public new Vector3Int voxelResolution
        {
            get
            {
                Texture3D tex3D = _SDF as Texture3D;
                if (tex3D != null)
                    return new Vector3Int(tex3D.width, tex3D.height, tex3D.depth);

                Vector3Int res = new Vector3Int();
                res.x = _Resolution;
                res.y = (int)(_Resolution * _Size.y / _Size.x);
                res.z = (int)(_Resolution * _Size.z / _Size.x);
                res.y = Mathf.Clamp(res.y, 1, _kMaxResolution);
                res.z = Mathf.Clamp(res.z, 1, _kMaxResolution);
                return res;
            }
        }

        public new Bounds voxelBounds
        {
            get
            {
                Vector3Int voxelRes = voxelResolution;
                if (voxelRes == Vector3Int.zero)
                    return new Bounds(Vector3.zero, Vector3.zero);

                // voxelBounds is m_Size, but adjusted to be filled by uniformly scaled voxels
                // voxelResolution quantizes to integer counts, so we just need to multiply by voxelSize
                Vector3 extent = new Vector3(voxelRes.x, voxelRes.y, voxelRes.z) * voxelSize;
                return new Bounds(Vector3.zero, extent);
            }
        }

        public new float voxelSize
        {
            get
            {
                if (mode == Mode.Dynamic)
                    return _Size.x / _Resolution;

                int resX = voxelResolution.x;
                return resX != 0 ? 1f / (float)resX : 0f;
            }
        }

        public new Matrix4x4 worldToSDFTexCoords
        {
            get
            {
                Vector3 scale = voxelBounds.size;
                Matrix4x4 localToSDFLocal = Matrix4x4.Scale(new Vector3(1.0f / scale.x, 1.0f / scale.y, 1.0f / scale.z));
                Matrix4x4 worldToSDFLocal = localToSDFLocal * transform.worldToLocalMatrix;
                return Matrix4x4.Translate(Vector3.one * 0.5f) * worldToSDFLocal;
            }
        }

        public new Matrix4x4 sdflocalToWorld
        {
            get
            {
                Vector3 scale = voxelBounds.size;
                return transform.localToWorldMatrix * Matrix4x4.Scale(scale);
            }
        }

        public new int maxResolution
        {
            get
            {
                // res * (res * size.y / size.x) * (res * size.z / size.x) = voxel_count
                // res^3 = voxel_count * size.x * size.x / (size.y * size.z)
                int maxResolution = (int)(Mathf.Pow(_kMaxVoxelCount * _Size.x * _Size.x / (_Size.y * _Size.z), 1.0f / 3.0f));
                return Mathf.Clamp(maxResolution, 1, _kMaxResolution);
            }
        }

        void ValidateSize()
        {
            _Size.x = Mathf.Max(_Size.x, 0.001f);
            _Size.y = Mathf.Max(_Size.y, 0.001f);
            _Size.z = Mathf.Max(_Size.z, 0.001f);
        }


        void ValidateResolution()
        {
            _Resolution = Mathf.Clamp(_Resolution, 1, maxResolution);
        }

        void ValidateTexture()
        {
            if (mode == Mode.Static || _mode == Mode.Static)
                return;

            RenderTexture rt = _SDF as RenderTexture;
            if (rt == null)
                return;

            Vector3Int res = voxelResolution;
            bool serializedPropertyChanged = rt.depth != 0 || rt.width != res.x || rt.height != res.y || rt.volumeDepth != res.z || rt.format != RenderTextureFormat.RHalf || rt.dimension != TextureDimension.Tex3D;

            if (!rt.enableRandomWrite || serializedPropertyChanged)
            {
                rt.Release();
                if (serializedPropertyChanged)
                {
                    rt.depth = 0;
                    rt.width = res.x;
                    rt.height = res.y;
                    rt.volumeDepth = res.z;
                    rt.format = RenderTextureFormat.RHalf;
                    rt.dimension = TextureDimension.Tex3D;
                }

                // For some reason this flag gets lost (not serialized?), so we don't want to write and dirty other properties if just this doesn't match
                rt.enableRandomWrite = true;
                rt.Create();
            }

            if (rt.wrapMode != TextureWrapMode.Clamp)
                rt.wrapMode = TextureWrapMode.Clamp;
        }

        void ValidateCenter()
        {
            if (_center != null)
            {
                this.transform.position = _center.position;
            }
        }

        public new void OnValidate()
        {
            //Debug.Log("OnValidate");
            ApplyToBase();

            ValidateCenter();
            ValidateSize();
            ValidateResolution();
            ValidateTexture();

#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
            base.OnValidate();
#endif
        }

        private void ApplyToBase()
        {
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
            var obj = this.GetComponent<SDFTexture>() as SDFTexture;
            var field1 = obj.GetType().BaseType.GetField("m_SDF", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field1 != null)
            {
                field1.SetValue(obj, _SDF);
            }
            var field2 = obj.GetType().BaseType.GetField("m_Size", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field2 != null)
            {
                field2.SetValue(obj, _Size);
            }
            var field3 = obj.GetType().BaseType.GetField("m_Resolution", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field3 != null)
            {
                field3.SetValue(obj, _Resolution);
            }
            var field4 = obj.GetType().BaseType.GetField("kMaxResolution", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field4 != null)
            {
                field4.SetValue(obj, _kMaxResolution);
            }
            var field5 = obj.GetType().BaseType.GetField("kMaxVoxelCount", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field5 != null)
            {
                field5.SetValue(obj, _kMaxVoxelCount);
            }
#endif
        }

        void CreateSdfRT()
        {
            if (_SDF == null)
            {
                _myTexture = new RenderTexture(32, 32, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
                _myTexture.dimension = TextureDimension.Tex3D;
                _myTexture.volumeDepth = 32;
                _myTexture.enableRandomWrite = true;
                _myTexture.wrapMode = TextureWrapMode.Clamp;
                _myTexture.name = this.name + this.GetInstanceID();
                _SDF = _myTexture;
                ApplyToBase();
            }
        }

        private void OnEnable()
        {
            CreateSdfRT();
        }

        private void OnDisable()
        {
            if (_myTexture != null)
            {
                _myTexture.Release();
                _myTexture = null;
            }
        }
    }
}