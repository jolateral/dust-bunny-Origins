using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace ClothDynamics
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(15200)] //When using Final IK
    public class GPUClothDynamicsV2 : MonoBehaviour
    {
        [HideInInspector]
        public Texture _logo;
        [HideInInspector]
        public int _settingsView = 0;
        public enum DampingMethods { noDamping, simpleDamping/*, smartDamping, smartAndSimpleDamping*/ }

        [Serializable]
        public class SDFTextureList
        {
            [Tooltip("SDF Texture System.")]
            public BodySDFTexture tex;
            [Tooltip("SDF Intensity is a multiplier that is applied to the force if using an SDF Texture.")]
            public float _sdfIntensity;
            [Tooltip("SDF surface offset.")]
            public float _sdfOffset;
            public SDFTextureList()
            {
                _sdfIntensity = 1.0f;
                _sdfOffset = 0.01f;
            }
        }

        [Serializable]
        public class SimParams
        {
            public int numSubsteps = 8;
            [Tooltip("Number of solver iterations to perform per-substep.")]
            public int numIterations = 8;
            [Tooltip("Max number of neighbors a particle can have.")]
            public int maxNumNeighbors = 64;
            [Tooltip("The magnitude of particle velocity will be clamped to this value at the end of each step.")]
            public float maxSpeed = 50;

            [Header("Forces")]
            [Tooltip("Constant acceleration applied to all particles.")]
            public Vector3 gravity = new Vector3(0, -9.0f, 0);
            [Tooltip("Viscous drag force, applies a force proportional, and opposite to the particle velocity.")]
            public float damping = 0.25f;
            [Tooltip("Control the convergence rate of the parallel solver, default: 1, values greater than 1 may lead to instability.")]
            public float relaxationFactor = 1.0f;
            [Tooltip(" With Long Range Attachment, all particles are required to stay within a distance from the attached point.")]
            public float longRangeStretchiness = 1.2f;
            [Tooltip("You can drag and drop an unity wind zone object here. Currently only one directional force per cloth is supported.")]
            [SerializeField] public WindZone _wind = null;
            [Tooltip("This controls the wind intensity for this object only.")]
            [SerializeField] public float _windIntensity = 0;

            [Header("Collision")]
            [Tooltip("This only affects the sdf colliders. Distance particles maintain against shapes, note that for robust collision against triangle meshes this distance should be greater than zero.")]
            public float sdfCollisionMargin = 0.02f;
            [Tooltip("Coefficient of friction used when colliding against shapes.")]
            public float friction = 0.5f;
            [HideInInspector]
            public bool enableSelfCollision = true;
            [Tooltip("Hash once every n substeps. This can improves performance greatly.")]
            public int interleavedHash = 3;

            [Header("Velocity Damping")]
            [Tooltip("These methods will damp the velocity of the cloth movement. Smart damping is currently experimental and gets jittery when using high values.")]
            [SerializeField] public DampingMethods _dampingMethod = DampingMethods.noDamping;
            [Tooltip("This value is multiplied with the velocity, so lower values will decelerate faster.")]
            [SerializeField] public float _dampingVel = 0.999f;
            [Tooltip("This clamps the current velocity. It's the max velocity value.")]
            [SerializeField] public float _clampVel = 10;
            //[Tooltip("This is experimental and cloth gets jittery when using high values.")]
            //[SerializeField] public float _dampingStiffness = 1.0f;

            [Header("Misc")]
            [Tooltip("Multiply original stretch length by this scalar to obtain particle diameter.")]
            public float particleDiameterScalar = 1.5f;
            [Tooltip("multiply particle diameter by this scalar to obtain hash cell size.")]
            public float hashCellSizeScalar = 1.5f;
            [Tooltip("This pushes the colliding vertex more inside the mesh. Negative values push it outwards. It makes sense to push the colliding vertex spheres inside to compensate the radius and the shape of the sphere collision.")]
            [SerializeField] public float _normalOffsetScale = 1.0f;

            [Header("SDF Realtime Texture")]
            public SDFTextureList[] _sdfList;

            // runtime info
            [Header("Runtime Info")]
#if UNITY_EDITOR
            [ReadOnly]
#endif
            [Tooltip("The maximum interaction radius for particles.")]
            public float particleDiameter;
#if UNITY_EDITOR
            [ReadOnly]
#endif
            [Tooltip("Total number of particles.")]
            public int numParticles;
#if UNITY_EDITOR
            [HideInInspector]
#endif
            public float deltaTime;

        }
        public SimParams _globalSimParams;

        [SerializeField]
        public ClothSolverGPU _solver = new ClothSolverGPU();

        [SerializeField]
        public CollisionMeshesGPU _collisionMeshes = new CollisionMeshesGPU();

        [Tooltip("List of all cloth objects for this sim, will be added automatically!")]
        public List<GameObject> _clothList = new List<GameObject>();

        [Tooltip("If you turn this on you get a debug log for some events, but it is also a bit slower.")]
        [SerializeField] public bool _debugEvents = false;

        [Tooltip("External cloth systems and addons.")]
        [SerializeField] public ClothExtensionGPU[] _extensions;

        //public void OnPlaymodeChanged(PlayModeStateChange state)
        //{
        //}

        internal void OnEnable()
        {
#if UNITY_EDITOR
            _logo = Resources.Load("Textures/Logo2") as Texture;
            //EditorApplication.playModeStateChanged += OnPlaymodeChanged;
#endif
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                if (_solver == null) _solver = new ClothSolverGPU();// this.gameObject.GetComponent<ClothSolverGPU>();
                _solver.Initialize(this);
            }
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            //EditorApplication.playModeStateChanged -= OnPlaymodeChanged;
#endif
        }

        private void OnDestroy()
        {
            _solver.OnDestroy();
            _collisionMeshes.OnDestroy();
            _tempBuffer.ClearBuffer();
        }

        Mesh GenerateClothMesh(int resolution)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> indices = new List<int>();
            const float clothSize = 2.0f;

            for (int y = 0; y <= resolution; y++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    vertices.Add(clothSize * new Vector3((float)x / (float)resolution - 0.5f, -(float)y / (float)resolution, 0));
                    normals.Add(new Vector3(0, 0, 1));
                    uvs.Add(new Vector2((float)x / (float)resolution, (float)y / (float)resolution));
                }
            }

            int VertexIndexAt(int x, int y)
            {
                return x * (resolution + 1) + y;
            };

            for (int x = 0; x < resolution; x++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    indices.Add(VertexIndexAt(x, y));
                    indices.Add(VertexIndexAt(x + 1, y));
                    indices.Add(VertexIndexAt(x, y + 1));

                    indices.Add(VertexIndexAt(x, y + 1));
                    indices.Add(VertexIndexAt(x + 1, y));
                    indices.Add(VertexIndexAt(x + 1, y + 1));
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = indices.ToArray();
            return mesh;
        }

        private ComputeBuffer _tempBuffer;

        GameObject SpawnCloth(int resolution = 16, ClothSolverGPU solver = null)
        {
            var cloth = new GameObject("Cloth Generated");

            var mesh = GenerateClothMesh(resolution);
            var filter = cloth.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            var renderer = cloth.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Shader Graphs/ClothShaderV2"));
            renderer.material.color = UnityEngine.Random.ColorHSV() + 0.1f * Color.white;

            if (_tempBuffer == null) _tempBuffer = new ComputeBuffer(1, sizeof(float) * 3);
            renderer.material.SetBuffer("positionsBuffer", _tempBuffer);
            renderer.material.SetBuffer("normalsBuffer", _tempBuffer);

            //if (solver == null)
            //{
            //    solver = cloth.AddComponent<ClothSolverGPU>();
            //    solver.Initialize(this);
            //}

            var clothObj = cloth.AddComponent<ClothObjectGPU>();
            if (clothObj)
            {
                //if(!_clothList.Contains(clothObj.gameObject)) _clothList.Add(clothObj.gameObject);
                clothObj.Init(this, resolution, solver, generated: true);
                //if(clothObj.m_attachedIndices == null) clothObj.SetAttachedIndices(new List<int>());
            }
            return cloth;
        }

        public void FixedUpdate()
        {
            if (Application.isPlaying)
            {
                _solver.FixedUpdate();
            }
        }
        public void Update()
        {
            if (Application.isPlaying)
            {
                _solver.Update();
            }
        }

        public void LateUpdate()
        {
            if (Application.isPlaying)
            {
                _solver.LateUpdate();
                _collisionMeshes.LateUpdate();
            }
        }

        //private float counter = 0;
        //private void UpdateX()
        //{
        //    if (Input.GetKeyDown(KeyCode.C))
        //    {
        //        int clothResolution = 64;
        //        {
        //            var cloth = SpawnCloth(clothResolution, _solver);
        //            cloth.transform.position = new Vector3(0.0f, 2.1f + counter, 1.0f);
        //            cloth.transform.rotation = Quaternion.Euler(-90, 0, 0);
        //        }
        //        counter += 0.2f;
        //    }
        //}

        private void OnDrawGizmos()
        {
            _solver?.OnDrawGizmos();
        }

#if UNITY_EDITOR
        [MenuItem("ClothDynamics/Create ClothDynamics V2 Object", priority = 35)]
        private static void NewCDV2(MenuCommand command)
        {
            GameObject v2 = new GameObject("ClothDynamicsV2", typeof(GPUClothDynamicsV2));
            if (command.context)
                Undo.SetTransformParent(v2.transform, ((GameObject)command.context).transform, "Create ClothDynamics V2");
            Selection.activeTransform = v2.transform;
        }
#endif
        
    }
}