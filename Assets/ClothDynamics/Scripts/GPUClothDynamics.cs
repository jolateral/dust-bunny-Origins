using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace ClothDynamics
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(15200)] //When using Final IK
    public partial class GPUClothDynamics : GPUClothBase
    {
        #region Global Properties

        [HideInInspector]
        public Texture _logo;
        [HideInInspector]
        public bool _advancedMode = false;

        [Header("Simulation Parameters")]
        [Tooltip("This can be used to stop/start the simulation or debug the start sequence by turning it off before playing. (You find the debug options in the ExtraSettings.)")]
        [SerializeField] public bool _runSim = true;
        [Range(0.001f, 0.05f)]
        [Tooltip("Lower values makes the collision more accurate, but also slows down everything.")]
        [SerializeField] public float _timestep = 0.005f;
        [Tooltip("This multiplies the simulation's deltaTime. (Default = 1)")]
        [SerializeField] public float _timeMultiplier = 1;
        [Range(2, 32)]
        [Tooltip("Higher values improve the collision quality, but will likewise slow down the system. Try also to adjust the timestep if you need more performance or accuracy.")]
        [SerializeField] public int _iterationNum = 2;
        [Range(1, 16)]
        [Tooltip("Sub Steps will also lower the performance, but improve the collision quality. Use this with Damping to avoid cloth \"explosions\"! This is an experimental feature! (Default = 1)")]
        [SerializeField] public int _subSteps = 1;
        [Tooltip("This set the main particle buffer by multiplying it's length, it will be changed on startup if the values is too low.")]
        //[Range(1,1024)]
        [SerializeField] public float _bufferScale = 16;
        [Tooltip("This updates the main particle buffer during runtime and should be turned on once at the beginning of an animation and turned off, when the animation restarts. The resulting value needs to be copied manually (for safety reasons) when the playmode ends.")]
        [SerializeField] internal bool _updateBufferSize = true;
        [Tooltip("This let the init phase delay for a set amount of frames e.g. 3 = three frames until init phase starts, so loading async stuff can happen before. If you get ComputeBuffer disposal warnings you should increase this value!")]
        public int _delayInit = 0;

        [Header("Forces")]
        [Tooltip("This controls the gravity amount. If you set it to zero you can simulate space or underwater behaviour. Zero values are also good for debugging collisions.")]
        [SerializeField] public Vector3 _gravity = new Vector3(0, -9.81f, 0);
        [Tooltip("You can drag and drop an unity wind zone object here. Currently only one directional force per cloth is supported.")]
        [SerializeField] public WindZone _wind = null;
        [Tooltip("This controls the wind intensity for this object only.")]
        [SerializeField] public float _windIntensity = 0;
        [Tooltip("Automatically find wind.")]
        [SerializeField] public bool _findWind = true;
        [Tooltip("Vertex mass gets used as inverse mass in the simulation, so basically the forces are multiplied by 1/vertexMass. This leads to slower movement when the value increases.")]
        [SerializeField] private float _vertexMass = 1;
        [Range(0.0f, 1.0f)]
        [Tooltip("Every particle velocity gets multiplied by the inverse of this static friction when it hits an object surface.")]
        [SerializeField] public float _staticFriction = 0.9f;
        [Range(0.0f, 1.0f)]
        [Tooltip("This will only affect the CollidableObjectsList. If these object translate, the friction will affect the movement of the cloth.")]
        [SerializeField] public float _dynamicFriction = 0.1f;
        [Range(0.0f, 10.0f)]
        [Tooltip("This is the global impact on the particles when the parent object changes the position.")]
        [SerializeField] public float _worldPositionImpact = 0.5f;
        [Range(0.0f, 10.0f)]
        [Tooltip("This is the global impact on the particles when the parent object changes the rotation.")]
        [SerializeField] public float _worldRotationImpact = 0.5f;

        [Header("Constraints")]
        [Tooltip("This tries to  push the particles away, so they don't get compressed by the other forces.")]
        [SerializeField] public float _distanceCompressionStiffness = 0.0f;
        [Tooltip("This tries to keep the distance of the particles as the original distance dictates.")]
        [SerializeField] public float _distanceStretchStiffness = 2.0f;
        [Tooltip("This tries to keep the angle between the triangles like the original model. Higher values result in more smooth cloth with less wrinkles.")]
        [SerializeField] public float _bendingStiffness = 0.0f;
        [Tooltip("This multiplies the stiffness values. Default value is 1.")]
        [SerializeField] public float _deltaScale = 1.0f;
        [Tooltip("This clamps the constraint forces per delta time frame.")]
        [SerializeField] public float _clampDelta = 0.05f;

        [Header("Point Constraints")]
        [Tooltip("This constraints the vertices to their start position, if you are using blue vertex colors as a mask.")]
        [SerializeField] public PointConstraintType _pointConstraintType;
        [Tooltip("Here you can set custom vertex IDs as a constraint. Basically it's the same as the vertex colors, but you need to know the vertex ID number.")]
        [SerializeField] public int[] _pointConstraintCustomIndices;
        [Tooltip("Turn this on to use your mouse cursor as a cloth picker. You can drag and drop the cloth vertex points with it. Also tested on mobile (Android).")]
        [SerializeField] public bool _enableMouseInteraction;
        [Tooltip("This set the intensity of your mouse dragging.")]
        [SerializeField] public float _deltaConstraintMul = 0.001f;
        [Tooltip("The \"GetDepthPos\" class detects the vertex picking point via depth map.")]
        [SerializeField] public GetDepthPos _getDepthPos = new GetDepthPos();
        internal Vector4[] _pointConstraintsVec;
        internal GPUClothConstraint[] _transformConstraints;

        [Header("Velocity Damping")]
        [Tooltip("These methods will damp the velocity of the cloth movement. Smart damping is currently experimental and gets jittery when using high values.")]
        [SerializeField] public DampingMethod _dampingMethod = DampingMethod.simpleDamping;
        [Tooltip("This value is multiplied with the velocity, so lower values will decelerate faster.")]
        [SerializeField] public float _dampingVel = 0.999f;
        [Tooltip("This clamps the current velocity. It's the max velocity value.")]
        [SerializeField] public float _clampVel = 10;
        [Tooltip("This is experimental and cloth gets jittery when using high values.")]
        [SerializeField] public float _dampingStiffness = 1.0f;

        [Header("Pre-Cache Data")]
        [Tooltip("This can be used to cache some slow parts of the preload process. Use it only if you don't edit the mesh anymore!")]
        [SerializeField] public bool _usePreCache = false;
        [Tooltip("This will be saved on disk to be reused for the next run to increase loading time. (Will be assigned automatically)")]
        [SerializeField] public UnityEngine.Object _preCacheFile;
        [Tooltip("This will scale the Cache Vertex and Length Data by the parent.parent object.")]
        public bool _scaleCacheByParent = true;

        [Header("Mesh Collisions")]
        [Tooltip("The Collision Finder creates the voxel based collision system, to find colliding sphere on the voxel grid.")]
        [SerializeField] public bool _useCollisionFinder = false;
        [Tooltip("This let you use a custom voxel center position.")]
        [SerializeField] public bool _useCustomVoxelCenter = false;
        [Tooltip("The custom voxel center transforms the voxel grid. This is helpful go get a more stable collision, however it only works with cloth that does not move too far away.")]
        [SerializeField] public Transform _customVoxelCenter;
        [Tooltip("This scales the size of the collision voxel cube. The (green) cube holds a voxel grid within to detect collisions per voxel-sphere basis.")]
        [SerializeField] public float _voxelCubeScale = 0.01f;
        [Tooltip("The cube steps are the division of the voxel cubes until it follows the next movement of the cloth. This prevents the voxel cube from getting too jittery.")]
        [SerializeField] private float _voxelCubeSteps = 32;
        [Tooltip("The mesh objects define the surfaces that will collide with the cloth object.")]
        [SerializeField] public float _vertexScale = 0.5f;
        [Tooltip("This pushes the colliding vertex more inside the mesh. Negative values push it outwards. It makes sense to push the colliding vertex spheres inside to compensate the radius and the shape of the sphere collision.")]
        [SerializeField] public float _vertexNormalScale = 0.05f;
        [Tooltip("This will activate the triangle collision system for the mesh objects. It's still a sphere-to-sphere collision, however the mesh will have dynamic sphere positions at the zenith of the colliding cloth point on the triangle surface.")]
        [SerializeField] public bool _useTriangleMesh = true;
        [Tooltip("This scales the size of the colliding sphere on the triangle.")]
        [SerializeField] public float _triangleScale = 0.2f;
        [Tooltip("This moves the colliding sphere of the triangle inside the mesh via invsere normal direction.")]
        [SerializeField] public float _triangleNormalScale = 0.0f;
        [Tooltip("This scales the AutomaticBoneSpheres.")]
        [SerializeField] public float _autoSphereScale = 1.1f;
        [Tooltip("This scales the additional colliding cloth object spheres. (Cloth-to-cloth only.)")]
        [SerializeField] public float _secondClothScale = 1.0f;
        [Tooltip("This helps to earlier detect collisions by increasing the collision radius with this static value.")]
        [SerializeField] public float _staticCollisionRadius = 0.005f;
        [Tooltip("The GPUNeighbourFinder contains the voxel based collision system.")]
        [SerializeField] public GPUNeighbourFinder _collisionFinder = new GPUNeighbourFinder();
        [Tooltip("The GridOffset moves the collision voxel cube.")]
        [SerializeField] public float3 _gridOffset;

        [Tooltip("This is an experimental feature which uses velocity vectors to predict a collision. Can be faster in some simulation scenarios.")]
        [SerializeField] public bool _predictiveContact = false;

        [Header("Mesh Self-Collision")]
        [Tooltip("This activates self cloth collision. You need to drag and drop this cloth object in the mesh objects list.")]
        [SerializeField] public bool _useSelfCollision = false;
        [Tooltip("This affects the self collision and will normally show visible result between 8-16. (Depending on your mesh size.)")]
        [SerializeField] public float _selfCollisionScale = 1.0f;
        [Tooltip("This activates self cloth collision with the triangles, admittedly it's a dynamic sphere-to-sphere collision on the triangle surface.")]
        [SerializeField] public bool _selfCollisionTriangles = false;
        [Tooltip("This reduces jittering of self collision by ignoring neighbour vertices, but can be very performance-heavy. Works better with Predictive Contact!")]
        [SerializeField] public bool _useNeighbourCheck = false;

        [Header("Primitive Collisions")]
        [Tooltip("Use this to turn on collision detection with primitive objects. (Spheres, Cubes, Capsules,...)")]
        [SerializeField] public bool _useCollidableObjectsList = false;
        [Tooltip("Add primitive objects here. But be aware that a long list will result in a performance drop.")]
        [SerializeField] public List<GameObject> _collidableObjects = new List<GameObject>();
        [Tooltip("This bias adds a distance buffer between the primitive objects and the cloth vertices.")]
        [SerializeField] public float _collidableObjectsBias = 0.02f;
        [Tooltip("This will use a separate sphere collision algorthim. If it's turned off, spheres will also be converted to SDF collisions, like the other primitives.")]
        [SerializeField] public bool _useDefaultSphereCollision = true;
        [Tooltip("This feature is currently not stable and needs more testing.")]
        [SerializeField] public bool _usePredictiveContactColliders = false;
        [Tooltip("If you turn this on, you can use the y-scaling of your plane mesh to increase the bias between plane and cloth.")]
        [SerializeField] public bool _usePlaneScaleY = false;

        [Header("Cloth Skinning")]
        [Tooltip("The cloth skinning should always be used when the cloth is placed on characters.")]
        [SerializeField] public bool _useClothSkinning = true;
        [Tooltip("This could be a GPUSkinning or DualQuaternionSkinner component. Automatically detected by the cloth, if it has a component like GPUSkinning or DualQuaternionSkinner attached.")]
        [SerializeField] private GPUSkinnerBase _skinComponent;
        [Range(0.0f, 1.0f)]
        [Tooltip("This blends how strong the skinning will affect the cloth. Also the red vertex color channel will affect the results.")]
        [SerializeField] public float _blendSkinning = 1.0f;
        [Range(0.0f, 1.0f)]
        [Tooltip("This will always blend to the skinned vertex position independent of the red vertex color channel. It can be used to reset the cloth original position e.g. beaming characters from A to B.")]
        [SerializeField] public float _minBlend = 0.01f;
        [Tooltip("Activate this to push the cloth outside of the colliding mesh surface. You need to add a mesh (e.g. a character) to the MeshObjects list.")]
        [SerializeField] public bool _useSurfacePush = false;
        [Tooltip("This is the intensity of the push force. A too high value can create a jitter effect.")]
        [SerializeField] public float _surfacePush = 50;
        [Tooltip("This is the offset for the surface position in the negative normal direction. So positive values will move the push-surface further inside the mesh.")]
        [SerializeField] public float _surfaceOffset = 0.0f;
        [Range(0.0f, 1.0f)]
        [Tooltip("This says how much the skinning should affect the surface push. A Value of 1 will try to reset the cloth vertex to the skinned position, a value of 0 will reset the cloth vertex to the last position outside the mesh.")]
        [SerializeField] public float _skinningForSurfacePush = 0.8f;
        [Tooltip("This forces the surface push system to use the CollidableObjects as a surface objects instead of a mesh. If true, you can't use the mesh collision!")]
        [SerializeField] public bool _forceSurfacePushColliders = false;

        [Header("LOD System")]
        [Tooltip("Use this if you want a LOD behaviour for your cloth. It will blend to the skinned mesh movement, if you are moving further away with the camera.")]
        [SerializeField] public bool _useLOD = false;
        [Tooltip("This set the distance of the LOD in Unity units.")]
        [SerializeField] public float _distLod = 8;
        [Tooltip("This curve affects the blending from cloth movement to skinned mesh movement.")]
        [SerializeField] public AnimationCurveData _lodCurve;
        [Tooltip("This should be your MainCamera object and is automatically detected if you don't select one.")]
        [SerializeField] private Camera _cam;
        [Tooltip("This will be blend-added to the timeStep when the camera moves away from the cloth object.")]
        [SerializeField] public float _lodTimeStepAdd = 0.002f;

        [Header("Proxy Skin")]
        //[Tooltip("Add a HighRes mesh object here, which should be controlled by this cloth object. (The cloth object will be invisible.)")]
        //[SerializeField] public GameObject _meshProxy;
        //[Tooltip("Here you can toggle if the MeshProxy will be used, however you need to add a mesh to make this work.")]
        //[SerializeField] public bool _useMeshProxy = true;
        [Tooltip("This curve controls the weighting of the skinned MeshProxy.")]
        [SerializeField] public AnimationCurveData _weightsCurve;
        [Tooltip("This is the tolerance distance for the vertices that affect the weighting.")]
        [SerializeField] public float _weightsToleranceDistance = 0.05f;
        [Tooltip("This scales the weighting.")]
        [SerializeField] public float _scaleWeighting = 50;
        [Tooltip("This is the minimal radius between the connected vertices.")]
        [SerializeField] public float _minRadius = 0.001f;
        [Tooltip("This skin is automatically generated the first time you run the cloth sim. It will be saved on disk to be reused for the next run to increase loading time.")]
        [SerializeField] public UnityEngine.Object _skinPrefab;

        [Header("Bounds Parameters")]
        [Tooltip("This is a simple frustum culling based on the bounding box of the mesh to stop simulation for offscreen cloth objects.")]
        [SerializeField] public bool _useFrustumClipping = true;
        [Tooltip("This adds an offset to the culling frustum.")]
        [SerializeField] public float _camCullDist = 1.0f;
        [Tooltip("This will use the light's direction to keep your cloth sim alive if you only see the cloth shadow in the camera view. Currently only works with directional lights properly.")]
        [SerializeField] public bool _useShadowCulling = true;
        [Tooltip("Here you can add directional lights for shadow culling. It will automatically search for a directional light if none is selected.")]
        [SerializeField] public Light[] _cullingLights;
        [Tooltip("This will move the bounding box to the center of the cloth sim if turned on. This should only be used for free falling cloth. Normally your cloth should be attached to a parent object that controls the animation and the bounding box position of your cloth.")]
        [SerializeField] public bool _updateTransformCenter = false;
        [Tooltip("This scales the bounding box like a cube if the value is higher than zero. The cube size uses the max length of all sides compared.")]
        [SerializeField] public float _scaleBoundingBox = 0;
        [Tooltip("This uses a simple more efficient algorithm to calculate the center position of the voxel cube. If you turn it off, it will be slower but might be a bit more accurate (not recommended).")]
        [SerializeField] public bool _calcVoxelPosRoughly = true;
        [Tooltip("This reads back the voxel cube (and custom bounding box) position from GPU to CPU. Normally this will be delayed for a few frames, because it runs async.")]
        [SerializeField] public bool _useReadbackCenter = true;

        [Header("Vertex Parameters")]
        [Tooltip("This will sew (weld) the edges that lie visually on each other but are not connected. This is also affected by the WeldThreshold.")]
        [SerializeField] public bool _sewEdges = true;
        [Tooltip("This will remove double vertices from the cloth sim and move them with their associates separately. This is also affected by the WeldThreshold. (It's better to remove doubles directly in your mesh.)")]
        [SerializeField] public bool _fixDoubles = true;
        [Range(2, 8)]
        [Tooltip("This set the uv id that is needed for the SRP setup. If your mesh needs the uv2 use a higher number. If you change this you need to change it in the Shader Graph too!")]
        [SerializeField] public int _vertexIdsToUvId = 2;
        [Tooltip("This will weld the vertices of the cloth mesh. (Does not work with every mesh!)")]
        [SerializeField] public bool _weldVertices = false;
        internal bool _weldDone = false;
        [Tooltip("This is the threshold that will be used for welding and sewing.")]
        [SerializeField] public float _weldThreshold = 1e-10f;

        [Header("Readback Vertices For Collision")]
        [Tooltip("This reads back the vertex positions as sphere colliders. Normally this will be delayed, because it runs async.")]
        [SerializeField] public bool _useReadbackVertices = false;
        [Tooltip("This selects the vertices every x times. So you can skip some vertices to improve performance. A value of 1 will select every vertex of the cloth object.")]
        [SerializeField] public int _readbackVertexEveryX = 10;
        [Tooltip("This scales the radius of the generated sphere colliders.")]
        [SerializeField] public float _readbackVertexScale = 0.01f;

        [Header("Garment Setup")]
        [Tooltip("This uses the garment function. So your cloth can be pulled together, if the vertices of the stretched edges have green vertex colors. The name \"garment\" is used for meshes that were created with the CreateGarment component from splines, but can also be used for any other mesh.")]
        [SerializeField] public bool _useGarmentMesh = false;
        [Tooltip("This updates the garment values during runtime. If you turn it off the performance is better, but than you can't change the garment values anymore.")]
        [SerializeField] public bool _updateGarmentMesh = true;
        [Tooltip("This runs the seam setup process once at the beginning.")]
        [SerializeField] public bool _onlyAtStart = false;
        [Tooltip("This set the length of the stretched edges that have green vertex colors.")]
        [SerializeField] public float _garmentSeamLength = 0.02f;
        [Range(0.01f, 100)]
        [Tooltip("This blends the Garment from cloth movement to the static garment position.")]
        [SerializeField] public float _blendGarment = 20f;
        [Tooltip("This pushes the garment vertices away in normal direction.")]
        [SerializeField] public float _pushVertsByNormals = 0.0f;

        [Header("Debug")]
        [Tooltip("This shows the bounding box (in white) and the voxel cube (in green).")]
        [SerializeField] private bool _showBoundingBox = true;
        [Tooltip("This debugs the PointPointContactBuffers and is used if the starting values fit to the resulting input of the cloth sim. It might be that the buffers are to low and you need to increase them manually, if that is the case please contact the developer of this tool.")]
        [SerializeField] private bool _debugBuffers = false;
        [Tooltip("This will show the weld edges.")]
        [SerializeField] private bool _showWeldEdges = false;
        [Tooltip("This ID will specifically visualize the weld edge. A value of -1 will show all.")]
        [SerializeField] private int _weldEdgeId = -1;
        [Tooltip("This will show the bending constraints.")]
        [SerializeField] private bool _showBendingConstraints = false;
        [Tooltip("This ID will specifically visualize the bending constraints. A value of -1 will show all.")]
        [SerializeField] private int _debugBendingId = -1;
        [Tooltip("This will write the vertex positions back to the mesh. Extremely slow and is therefore not recommended.")]
        [SerializeField] private bool _slowCPUWriteBack = false;
        [Tooltip("If you turn this on you get a debug log for some events, but it is also a bit slower.")]
        [SerializeField] public bool _debugMode = false;
        //[SerializeField] private int _debugVertexId = 0;
        //public bool _finishedLoading = false;

        public bool _applyTransformAtExport = false;

        #endregion // Global Properties

        #region Private Properties

        private bool _useTransferData = false;
        private Transform _rootBone = null;

        internal Vector3 _voxelCubePos;
        internal ComputeShader _clothSolver;

        private bool _enableMouseInteractionLastState = false;
        private bool _useCollidableObjectsListLastState = false;
        private int _workGroupSize = 256;
        private GameObject[] _vertexColliders;
        private bool _runReadbackVertices = false;
        private Transform _readbackVertexParent;

        private enum SkinTypes
        {
            NoSkinning,
            GPUSkinning,
            DualQuaternionSkinner,
            SkinnerSource
        }
        private SkinTypes _skinTypeCloth = SkinTypes.NoSkinning;

        private PointConstraintType _lastPointConstraintType;
        private int _tempPointConstraint = -1;
        private Vector3 _deltaPointConstraint, _lastMousePos;
        internal int _tempPointVertexId = 0;
        private int _lastCollidableObjectsCount = 0;
        internal Texture _dummyTex;
        private bool _runReadback = false;

        // simulation data
        private float _nextFrameTime = 0f;
        internal Vector3[] _positions;
        private Color[] _colors;
        private Vector4[] _velocities;
        private float[] _frictions;
        private float _invMass;
        internal int _numParticles;
        private int _numDistanceConstraints, _numBendingConstraints;
        private CollidableSphereStruct[] _collidableSpheres;
        private CollidableSDFStruct[] _collidableSDFs;
        private int _numCollidableSpheres, _numCollidableSDFs, _numPointConstraints;

        //[System.Serializable]
        private struct TransformDynamics
        {
            public struct TransformPerFrame
            {
                public Vector3 position;
                public Quaternion rotation;
                public Vector3 scale;
                public TransformPerFrame(Vector3 position, Quaternion rotation, Vector3 scale)
                {
                    this.position = position;
                    this.rotation = rotation;
                    this.scale = scale;
                }
            }

            public TransformPerFrame frame;
            public TransformPerFrame prevFrame;
            public Vector3 velocity;
            public Vector3 rotVelocity;
            public Vector3 posAcceleration;
            public Vector3 rotAcceleration;
        }
        private List<TransformDynamics> _tds = new List<TransformDynamics>();

        // constraints
        private DistanceConstraintStruct[] _distanceConstraints;
        private BendingConstraintStruct[] _bendingConstraints;
        internal int[] _pointConstraints;
        private List<Vector2Int> _connectionInfo = null;
        private List<int> _connectedVerts = null;

        // compute buffers
        public class ObjectBuffers
        {
            internal ComputeBuffer positionsBuffer;
            internal ComputeBuffer normalsBuffer;
            internal ComputeBuffer connectionInfoBuffer;
            internal ComputeBuffer connectedVertsBuffer;
        }
        //internal ObjectBuffers[] _objBuffers;

        internal ComputeBuffer _projectedPositionsBuffer;
        private ComputeBuffer _velocitiesBuffer;
        private ComputeBuffer _deltaPositionsUIntBuffer;
        private ComputeBuffer _deltaPositionsUIntBuffer2;
        private ComputeBuffer _deltaCounterBuffer;
        private ComputeBuffer _distanceConstraintsBuffer;
        private ComputeBuffer _bendingConstraintsBuffer;
        private RenderTexture _gridCenterBuffer;
        private ComputeBuffer _collidableSpheresBuffer;
        private ComputeBuffer _collidableSDFsBuffer;
        private ComputeBuffer _pointConstraintsBuffer;
        private ComputeBuffer _pointConstraintsVecBuffer;
        private ComputeBuffer _frictionsBuffer;
        private ComputeBuffer _pointPointContactBuffer;
        private ComputeBuffer _pointPointContactBuffer2;
        private ComputeBuffer _pointPointContactBuffer3;
        private ComputeBuffer _countContactBuffer;
        private ComputeBuffer _countContactBuffer2;
        private ComputeBuffer _countContactBuffer3;

        // kernel IDs
        private int _applyExternalForcesKernel;
        private int _dampVelocitiesKernel;
        private int _applyExplicitEulerKernel;
        private int _projectConstraintDeltasKernel;
        private int _averageConstraintDeltasKernel;
        private int _satisfyPointConstraintsKernel;
        private int _satisfySphereCollisionsKernel;
        private int _satisfyVertexCollisionsKernel;
        private int _satisfySDFCollisionsKernel;
        private int _updatePositionsKernel;
        private int _updatePositions2Kernel;
        private int _updatePositions2BlendsKernel;
        private int _updatePositionsNoSkinningKernel;
        private int _updateWorldTransformKernel;
        private int _updateInverseWorldTransformKernel;
        private int _csNormalsKernel;
        private int _bendingConstraintDeltasKernel;
        private int _skinningHDKernel;
        private int _calcCubeCenterKernel;
        private int _calcCubeCenter2Kernel;
        private int _selfContactCollisionsKernel;
        private int _collidersContactCollisionsKernel;
        private int _otherSpheresContactCollisions2Kernel;
        private int _countContactStartKernel;
        private int _pointPointPredictiveContactKernel;
        private int _pointPointPredictiveContactCollidersKernel;

        private int _countContactSetupKernel;
        private int _transferDuplicateVertexDataKernel;
        private int _calcCubeCenterFastKernel;
        private int _surfacePushKernel;
        private int _surfacePushCollidersKernel;
        private int _surfacePushDQSKernel;
        private int _surfacePushCollidersDQSKernel;
        private int _surfacePushSkinningKernel;
        private int _surfacePushSkinningBlendsKernel;
        private int _surfacePushCollidersSkinningKernel;
        private int _surfacePushCollidersSkinningBlendsKernel;
        private int _blendGarmentOriginKernel;
        private int _computeCenterOfMassKernel;
        private int _finishCenterOfMassKernel;
        private int _sumAllMassAndMatrixKernel;
        private int _finishMatrixCalcKernel;
        private int _applyBackIntoVelocitiesKernel;
        private int _clearCenterOfMassKernel;

        // num of work groups
        private int _numGroups_Vertices;
        private int _numGroups_DistanceConstraints;
        private int _numGroups_BendingConstraints;
        //private int _numGroups_AllConstraints;
        private int _numGroups_PointConstraints;
        private int _numGroups_VerticesHD;

        // mesh data
        private Mesh _mesh;
        private Triangle[] _triangles;

        private Renderer _mr;
        private MaterialPropertyBlock _mpb;
        private MaterialPropertyBlock _mpbHD;

        private List<GameObject> _spheres;
        private List<GameObject> _sdfObjs;
        //private ComputeBuffer _connectionInfoBuffer;
        //private ComputeBuffer _connectedVertsBuffer;
        private List<Edge> _weldEdges;

        //private List<int> _mapVertsBack;
        private ComputeBuffer _bonesStartMatrixBuffer;
        private ComputeBuffer _vertexBlendsBuffer;
        private ComputeBuffer _connectionInfoTetBuffer;
        private ComputeBuffer _connectedVertsTetBuffer;
        private ComputeBuffer _startVertexBuffer;
        private ComputeBuffer _duplicateVerticesBuffer;

        //private TextAsset _newPrefabAsset;
        private List<int> _dupIngoreList;
        private List<BendingConstraintStruct> _extraBendingConstraints;

        private bool _saveVoxelPosState;
        private bool _saveLodState;
        private bool _saveClippingState;
        internal bool _foundInFrustum;
        private float _blendDist = 0;

        //private static int _id;
        private Plane[] _planes;
        //private bool _diffCams = false;
        private ComputeBuffer _baseVerticesBuffer;
        private ComputeBuffer _centerMassBuffer;

        private int _maxDebugData;
        private int _maxDebugData2;
        private int _maxDebugData3;
        internal bool _supportsAsyncGPUReadback;
        private Vector3[] _dataBufferPos;
        internal bool _updateSync = false;
        private static float[] _displacements = new float[12];

        private const float DELTA_SCALE = 0.001f;

        private int _cubeMinPos_ID = Shader.PropertyToID("_cubeMinPos");
        private int _worldToLocalMatrix_ID = Shader.PropertyToID("_worldToLocalMatrix");
        private int _localToWorldMatrix_ID = Shader.PropertyToID("_localToWorldMatrix");

        private int _staticFriction_ID = Shader.PropertyToID("_staticFriction");
        private int _dynamicFriction_ID = Shader.PropertyToID("_dynamicFriction");

        private int _gravity_ID = Shader.PropertyToID("_gravity");
        private int _invMass_ID = Shader.PropertyToID("_invMass");
        private int _windVec_ID = Shader.PropertyToID("_windVec");

        private int _stretchStiffness_ID = Shader.PropertyToID("_stretchStiffness");
        private int _compressionStiffness_ID = Shader.PropertyToID("_compressionStiffness");
        private int _bendingStiffness_ID = Shader.PropertyToID("_bendingStiffness");
        private int __worldPositionImpact_ID = Shader.PropertyToID("_worldPositionImpact");
        private int __worldRotationImpact_ID = Shader.PropertyToID("_worldRotationImpact");
        private int __collidableObjectsBias_ID = Shader.PropertyToID("_collidableObjectsBias");
        private int __minBlend_ID = Shader.PropertyToID("_minBlend");
        private int __blendSkinning_ID = Shader.PropertyToID("_blendSkinning");

        private int _dt_ID = Shader.PropertyToID("_dt");
        private int _deltaScale_ID = Shader.PropertyToID("_deltaScale");
        private int _clampDelta_ID = Shader.PropertyToID("_clampDelta");
        private int _clampVel_ID = Shader.PropertyToID("_clampVel");
        private int _dampingVel_ID = Shader.PropertyToID("_dampingVel");

        private int _trisData_ID = Shader.PropertyToID("_trisData");
        private int _collisionRadius_ID = Shader.PropertyToID("_collisionRadius");
        private int _deltaTime_ID = Shader.PropertyToID("_deltaTime");
        private int _usedVoxelListBuffer_ID = Shader.PropertyToID("_usedVoxelListBuffer");
        private int _counterPerVoxelBuffer2_ID = Shader.PropertyToID("_counterPerVoxelBuffer2");
        private int _lastCounterPerVoxelBuffer2_ID = Shader.PropertyToID("_lastCounterPerVoxelBuffer2");
        private int _voxelDataBuffer_ID = Shader.PropertyToID("_voxelDataBuffer");
        private int _numPointConstraints_ID = Shader.PropertyToID("_numPointConstraints");
        private int _hasTempPointConstraint_ID = Shader.PropertyToID("_hasTempPointConstraint");
        private int _deltaPointConstraint_ID = Shader.PropertyToID("_deltaPointConstraint");
        private int _tempPointConstraint_ID = Shader.PropertyToID("_tempPointConstraint");
        private int _duplicateVerticesCount_ID = Shader.PropertyToID("_duplicateVerticesCount");
        private int _delta_ID = Shader.PropertyToID("_delta");
        private int _mpb_normalsBuffer_ID = Shader.PropertyToID("normalsBuffer");
        private int _mpb_positionsBuffer_ID = Shader.PropertyToID("positionsBuffer");

        private int _blendGarment_ID = Shader.PropertyToID("_blendGarment");
        private int _pushVertsByNormals_ID = Shader.PropertyToID("_pushVertsByNormals");
        private int _normalsBuffer_ID = Shader.PropertyToID("_normalsBuffer");
        private int _baseVertices_ID = Shader.PropertyToID("_baseVertices");
        private int _projectedPositions_ID = Shader.PropertyToID("_projectedPositions");

        private int _maxVerts_ID = Shader.PropertyToID("_maxVerts");
        private int _positions_ID = Shader.PropertyToID("_positions");
        private int _connectionInfoBuffer_ID = Shader.PropertyToID("_connectionInfoBuffer");
        private int _connectedVertsBuffer_ID = Shader.PropertyToID("_connectedVertsBuffer");

        private int _skinningForSurfacePush_ID = Shader.PropertyToID("_skinningForSurfacePush");

        private int _skinned_tex_width_ID = Shader.PropertyToID("_skinned_tex_width");
        private int _skinned_data_1_ID = Shader.PropertyToID("_skinned_data_1");

        private int _meshVertsOut_ID = Shader.PropertyToID("_meshVertsOut");
        private int _surfacePush_ID = Shader.PropertyToID("_surfacePush");
        private int _surfaceOffset_ID = Shader.PropertyToID("_surfaceOffset");
        private int _normalScale_ID = Shader.PropertyToID("_normalScale");

        private int _dispatchDim_x_ID = Shader.PropertyToID("_dispatchDim_x");
        private int _numParticles_ID = Shader.PropertyToID("_numParticles");
        private int _gridCenterBuffer_ID = Shader.PropertyToID("_gridCenterBuffer");
        private int _gridCenterBufferCopy_ID = Shader.PropertyToID("_gridCenterBufferCopy");
        private int _deltaPosAsIntX_ID = Shader.PropertyToID("_deltaPosAsIntX");
        private int _collidableSpheres_ID = Shader.PropertyToID("_collidableSpheres");
        private int _collidableSDFs_ID = Shader.PropertyToID("_collidableSDFs");
        private int _numCollidableSDFs_ID = Shader.PropertyToID("_numCollidableSDFs");
        private int _numCollidableSpheres_ID = Shader.PropertyToID("_numCollidableSpheres");

        private int _frictions_ID = Shader.PropertyToID("_frictions");
        private int _velocities_ID = Shader.PropertyToID("_velocities");

        private int _worldToLocalMatrixHD_ID = Shader.PropertyToID("_worldToLocalMatrixHD");

        private int _deltaPosAsInt_ID = Shader.PropertyToID("_deltaPosAsInt");
        private int _deltaCount_ID = Shader.PropertyToID("_deltaCount");

        private int _pointPointContactBuffer_ID = Shader.PropertyToID("_pointPointContactBuffer");
        private int _pointPointContactBuffer2_ID = Shader.PropertyToID("_pointPointContactBuffer2");
        private int _pointPointContactBuffer3_ID = Shader.PropertyToID("_pointPointContactBuffer3");
        private int _countContactBuffer_ID = Shader.PropertyToID("_countContactBuffer");
        private int _countContactBuffer2_ID = Shader.PropertyToID("_countContactBuffer2");
        private int _countContactBuffer3_ID = Shader.PropertyToID("_countContactBuffer3");

        private int _centerMassBuffer_ID = Shader.PropertyToID("_centerMassBuffer");
        private int _dampingStiffness_ID = Shader.PropertyToID("_dampingStiffness");

        private int _posCloth_ID = Shader.PropertyToID("_posCloth");
        private int _posVel_ID = Shader.PropertyToID("_posVel");
        private int _rotVel_ID = Shader.PropertyToID("_rotVel");

        //private Animator _anim;
        private AutomaticBoneSpheres _abs;

        private struct Blends
        {
            public Vector4 bones;
            public Vector4 weights;
        };

        private System.Diagnostics.Stopwatch _debugTimer;
        private TimeSpan _debugTimespan;

        private List<byte> _byteDataPreCache;
        private byte[] _bufferPreCache;
        private int _bStepPreCache = 0;
        private bool _overwritePreCache = false;
        private Shader _shader = null;
        private Coroutine _initCoroutine = null;

        #endregion // Private Properties

        internal void OnEnable()
        {
            if (_customVoxelCenter == null) _customVoxelCenter = this.transform;

#if UNITY_EDITOR
            _logo = Resources.Load("Textures/Logo") as Texture;
            EditorApplication.playModeStateChanged += OnPlaymodeChanged;
#endif


            if (_delayInit > 0 || _weldVertices)
                _initCoroutine = StartCoroutine(DelayInit());
            else
                Init();
        }

        IEnumerator DelayInit()
        {
            for (int i = 0; i < _delayInit; i++) yield return null;
            if (_weldVertices)
            {
                while (this.GetComponent<MeshRenderer>() == null)
                    yield return null;
            }
            Init();
        }

        internal void Init()
        {
            if (!Application.isPlaying || !this.enabled) return;

            if (_skinComponent != null && _skinComponent.transform.parent != this.transform.parent)
                _skinComponent = null;

            if (!Application.isPlaying || !this.enabled) return;

            if (_cullingLights != null)
            {
                var lightsList = _cullingLights.ToList();
                if (lightsList != null)
                {
                    Extensions.CleanupList(ref lightsList);
                    _cullingLights = lightsList.ToArray();
                }
            }
            var foundLight = FindObjectsOfType<Light>().Where(x => x.type == LightType.Directional).FirstOrDefault();
            if (foundLight != null && (_cullingLights == null || _cullingLights.Length < 1)) _cullingLights = new Light[] { foundLight };

            var selfSkinned = this.GetComponent<GPUSkinnerBase>();
            if (selfSkinned) selfSkinned._render = false;

            _saveVoxelPosState = _calcVoxelPosRoughly;
            //if (!_useExistingMesh) _calcVoxelPosRoughly = false;
            _saveLodState = _useLOD;
            //_useLOD = false; //Coroutine does not always work, so just keep it as it is!
            _saveClippingState = _useFrustumClipping;
            _useFrustumClipping = false;

#if UNITY_2021_2_OR_NEWER
            var skinning = this.GetComponent<GPUSkinning>();
            if (skinning) _useTransferData = this.GetComponent<GPUSkinning>()._useTransferData;
#endif

            var t = new ComputeBuffer(1, 4);
            t.SetData(new int[] { 0 });
            _supportsAsyncGPUReadback = UniversalAsyncGPUReadbackRequest.Request(t).valid;//SystemInfo.supportsAsyncGPUReadback;
            if (!_supportsAsyncGPUReadback) Debug.Log("<color=blue>CD: </color><color=orange> SystemInfo.supportsAsyncGPUReadback == false !</color>");
            t.Release();

            //var anim = this.GetComponentInParent<Animator>();
            //if (anim != null && anim.GetComponent<ClothUpdateAnimator>() == null) anim.gameObject.AddComponent<ClothUpdateAnimator>();

            _mpb = new MaterialPropertyBlock();
            if (_mr == null)
            {
                _mr = _useTransferData ? this.GetComponent<Renderer>() : this.GetComponent<MeshRenderer>();
                if (_mr == null)
                {
                    _mr = _useTransferData ? (Renderer)this.gameObject.AddComponent<SkinnedMeshRenderer>() : (Renderer)this.gameObject.AddComponent<MeshRenderer>();
                    if (this.GetComponent<SkinnedMeshRenderer>())
                        _mr.materials = this.GetComponent<SkinnedMeshRenderer>().materials;
                }
            }
            if (!_slowCPUWriteBack)
            {
                SetClothShader(_mr);
            }

            //if (_initCoroutine != null) StopCoroutine(_initCoroutine);

            if (!_useCollisionFinder && !_useCollidableObjectsList)
            {
                var skinners = this.transform.parent.GetComponentsInChildren<GPUSkinning>();
                for (int i = 0; i < skinners.Length; i++)
                {
                    if (skinners[i].GetComponent<GPUClothDynamics>()) continue;
                    if (_meshObjects != null && (skinners[i].name.ToLower().Contains("body") || skinners[i].name.ToLower().Contains("skin") || skinners[i].name.ToLower().Contains("base") || !skinners[i].name.ToLower().Contains("cloth") || !skinners[i].name.ToLower().Contains("hair") || skinners[i].name.ToLower().Contains("dress") || !skinners[i].name.ToLower().Contains("skirt") || !skinners[i].name.ToLower().Contains("shirt")))
                    {
                        _useCollisionFinder = true;
                        var list = _meshObjects.ToList();
                        list.Add(skinners[i].transform);
                        _meshObjects = list.ToArray();
                        break;
                    }
                }
                if (_meshObjects != null && _meshObjects.Length == 0)
                {
                    var objects = this.transform.parent.GetComponentsInChildren<Transform>();
                    for (int i = 0; i < objects.Length; i++)
                    {
                        if (objects[i].GetComponent<GPUClothDynamics>()) continue;
                        if (objects[i].name.ToLower().Contains("body") || objects[i].name.ToLower().Contains("skin") || objects[i].name.ToLower().Contains("base") || objects[i].name.ToLower().Contains("bod") || objects[i].name.ToLower().Contains("corpus") || objects[i].name.ToLower().Contains("mesh"))
                        {
                            _useCollisionFinder = true;
                            var list = _meshObjects.ToList();
                            objects[i].gameObject.GetOrAddComponent<GPUSkinning>();
                            list.Add(objects[i]);
                            _meshObjects = list.ToArray();
                            break;
                        }
                    }
                }
                if (!_useCollisionFinder)
                {
                    var planeMeshes = FindObjectsOfType<MeshFilter>();
                    for (int i = 0; i < planeMeshes.Length; i++)
                    {
                        if (planeMeshes[i].mesh.name.ToLower().Contains("plane") && (planeMeshes[i].name.ToLower().Contains("plane") || planeMeshes[i].name.ToLower().Contains("ground") || planeMeshes[i].name.ToLower().Contains("floor") || planeMeshes[i].name.ToLower().Contains("base")))
                        {
                            _useCollidableObjectsList = true;
                            _collidableObjects.Add(planeMeshes[i].gameObject);
                            break;
                        }
                    }
                }
            }


            if (!_useTransferData)
            {
                if (this.GetComponent<SkinnedMeshRenderer>())
                    this.GetComponent<SkinnedMeshRenderer>().enabled = false;

                var mf = this.GetComponent<MeshFilter>();
                if (mf == null)
                {
                    mf = this.gameObject.AddComponent<MeshFilter>();
                    if (this.GetComponent<SkinnedMeshRenderer>())
                        mf.sharedMesh = this.GetComponent<SkinnedMeshRenderer>().sharedMesh;
                }
                _mesh = mf.sharedMesh;
            }
            else
            {
                var skin = this.GetComponent<SkinnedMeshRenderer>();
                if (skin != null && _rootBone == null)
                {
                    _rootBone = skin.rootBone;
                }
                if (skin != null) _mesh = skin.sharedMesh;
            }

            if (_mesh == null)
            {
                Debug.LogError("Mesh missing at " + this.name);
                return;
            }

            //SetSecondUVsForVertexID(_mesh);//TODO
            _mesh.RecalculateBounds();
            if (_slowCPUWriteBack) _mesh.MarkDynamic();


            if (_debugMode)
            {
                _debugTimer = System.Diagnostics.Stopwatch.StartNew();
                Destroy();
            }
            //_id++;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) _cam = FindObjectOfType<Camera>();

            var cloths = FindObjectsOfType<GPUClothDynamics>();
            //if (cloths.Any(x => x._cam != _cam)) _diffCams = true;

            if (_findWind) _wind = FindObjectOfType<WindZone>();

            if (_useClothSkinning)
            {
                if ((this.GetComponent<SkinnedMeshRenderer>() || (this.GetComponent<AutomaticSkinning>() && this.GetComponent<AutomaticSkinning>().enabled))
                     && !this.GetComponent<GPUSkinning>() && !this.GetComponent<DualQuaternionSkinner>())
                {
                    var skinner = this.gameObject.AddComponent<GPUSkinning>();
                    skinner.Initialize(renderSetup: false);
                }
                if (_skinComponent != null && _skinComponent.transform.parent != this.transform.parent)
                    _skinComponent = null;
                _skinComponent = this.GetComponent<GPUSkinning>();
                if (_skinComponent == null)
                    _skinComponent = this.GetComponent<DualQuaternionSkinner>();
                if (_skinComponent != null && !_skinComponent.isActiveAndEnabled)
                    _skinComponent = null;
            }
            else _skinComponent = null;

            if (_skinComponent != null)
            {
                if (_skinComponent.GetType() == typeof(GPUSkinning))
                    _skinTypeCloth = SkinTypes.GPUSkinning;
                if (_skinComponent.GetType() == typeof(DualQuaternionSkinner))
                    _skinTypeCloth = SkinTypes.DualQuaternionSkinner;
                //if (_skinComponent.GetType() == typeof(SkinnerSource))
                //    _skinType = SkinTypes.SkinnerSource;
                if (_debugMode) Debug.Log("<color=blue>CD: </color><color=lime>" + this.name + " is using " + _skinComponent.GetType() + " for cloth (self) skinning</color>");
            }

            if (_clothSolver == null) _clothSolver = Resources.Load("Shaders/Compute/PBDClothSolver") as ComputeShader;
            // Instantiate CS for multi cloth objects
            if (cloths.Length > 1)
                _clothSolver = Instantiate(_clothSolver);

            if (!_useSelfCollision) _useNeighbourCheck = false;

            _clothSolver.SetInt("_indexOffset", 0); //This is a placeholder, used only by V2 in SkinningHD

#if UNITY_2020_1_OR_NEWER
            if (_useNeighbourCheck) _clothSolver.EnableKeyword("USE_NEIGHBOUR_CHECK");
            else _clothSolver.DisableKeyword("USE_NEIGHBOUR_CHECK");
            SetShaderKeyword(turnOn: false);
#else
			_clothSolver.SetBool("_useNeighbourCheck", _useNeighbourCheck);
			SetShaderKeyword(turnOn: true);
#endif
            if (_lodCurve == null) _lodCurve = Resources.Load("Curves/AnimationCurveLod") as AnimationCurveData;
            if (_weightsCurve == null) _weightsCurve = Resources.Load("Curves/AnimationCurveProxySkin") as AnimationCurveData;

            _spheres = new List<GameObject>();
            _sdfObjs = new List<GameObject>();

            if (_debugMode)
            {
                _debugTimer.Stop();
                _debugTimespan = _debugTimer.Elapsed;
                Debug.Log(String.Format("<color=blue> CD: </color>Init Data {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                _debugTimer.Restart();
            }

            WeldMesh();
            CheckPreCache(weld: true);

            _numParticles = _mesh.vertexCount;
            if (_numParticles == 0) { Debug.LogError("_numParticles == 0"); return; }

            Vector3[] baseVertices = _mesh.vertices;
            Color[] baseColors = _mesh.colors;

            bool useVertexColors = true;
            if (baseColors == null || baseColors.Length != _numParticles)
                useVertexColors = false;

            _positions = new Vector3[_numParticles];
            if (_useGarmentMesh && baseColors != null && baseColors.Length != 0) _colors = new Color[_numParticles];
            else if (_useGarmentMesh)
            {
                if (_debugMode) Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " mesh vertex colors are missing! Auto correction via script will set all vertex colors to red!</color>");
                _colors = new Color[_numParticles];
                baseColors = new Color[_colors.Length];
                for (int i = 0; i < baseColors.Length; i++)
                {
                    baseColors[i] = Color.red;
                }
                //_useGarmentMesh = false;
            }

            _velocities = new Vector4[_numParticles];
            _frictions = new float[_numParticles];

            if (_debugMode)
            {
                _debugTimer.Stop();
                _debugTimespan = _debugTimer.Elapsed;
                Debug.Log(String.Format("<color=blue> CD: </color>UseGarmentMesh {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                _debugTimer.Restart();
            }

            if (!_usePreCache || _preCacheFile == null)
            {
                // step 1-3: initialize position, velocity and weight
                var colorsTmp = _mesh.colors;
                int count = _numParticles;
                for (int i = 0; i < count; i++)
                {
                    _positions[i] = baseVertices[i];
                    if (_useGarmentMesh) _colors[i] = baseColors[i];
                    _velocities[i] = Vector4.zero;
                    _velocities[i].w = useVertexColors ? colorsTmp[i].r : 1;
                    _frictions[i] = 1;
                }
            }
            _invMass = 1.0f / _vertexMass;

            if (_debugMode)
            {
                _debugTimer.Stop();
                _debugTimespan = _debugTimer.Elapsed;
                Debug.Log(String.Format("<color=blue> CD: </color>Velocity and Weight {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                _debugTimer.Restart();
            }

            // initialize triangles
            int[] triangleIndices = _mesh.GetTriangles(0);
            _triangles = new Triangle[triangleIndices.Length / 3];
            for (int i = 0; i < _triangles.Length; i++)
            {
                _triangles[i] = new Triangle(triangleIndices[i * 3], triangleIndices[i * 3 + 1], triangleIndices[i * 3 + 2]);
            }

            List<Mesh> meshList = new List<Mesh>();
            meshList.Add(_mesh);
            bool useProxy = _useMeshProxy && _meshProxy != null;
            if (useProxy)
            {
                var mf = _meshProxy.GetComponent<MeshFilter>();
                Mesh meshHD = null;
                if (mf != null)
                    meshHD = mf.mesh;
                else
                {
                    var smr = _meshProxy.GetComponent<SkinnedMeshRenderer>();
                    if (meshHD == null && smr != null)
                    {
                        meshHD = smr.sharedMesh;
                        mf = _meshProxy.gameObject.AddComponent<MeshFilter>();
                        mf.sharedMesh = meshHD;
                        var mr = _meshProxy.GetComponent<MeshRenderer>();
                        if (mr == null) mr = _meshProxy.gameObject.AddComponent<MeshRenderer>();
                        Material[] materials = smr.sharedMaterials;
                        if (smr) DestroyImmediate(smr);
                        mr.sharedMaterials = materials;
                        if (mf.sharedMesh.subMeshCount < mr.sharedMaterials.Length)
                        {
                            var mats = mr.sharedMaterials;
                            Array.Resize(ref mats, mf.sharedMesh.subMeshCount);
                            mr.sharedMaterials = mats;
                        }
                    }
                }
                if (meshHD != null) meshList.Add(meshHD);
                else { useProxy = false; if (_meshProxy != null) _meshProxy.SetActive(false); }
            }
            else if (_meshProxy != null) _meshProxy.SetActive(false);


            _objBuffers = new ObjectBuffers[useProxy ? 2 : 1];
            for (int i = 0; i < _objBuffers.Length; i++)
            {
                _objBuffers[i] = new ObjectBuffers();
            }

            //if (_usePreCache) PreCacheClothData(meshList);

            for (int i = 0; i < _objBuffers.Length; i++)
            {
                if (!_usePreCache || _preCacheFile == null) CalcConnections(meshList[i], _objBuffers[i], i);
            }

            if (!_usePreCache || _preCacheFile == null)
            {
                // modify positions to world coordinates before calculating constraint restlengths
                for (int i = 0; i < _numParticles; i++)
                {
                    _positions[i] = transform.TransformPoint(_positions[i]);
                }

                if (_debugMode)
                {
                    _debugTimer.Stop();
                    _debugTimespan = _debugTimer.Elapsed;
                    Debug.Log(String.Format("<color=blue> CD: </color>Prepare Cloth Data {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                    _debugTimer.Restart();
                }

                // add constraints
                if (_sewEdges && _fixDoubles) ExtraConstraints();
                if (_debugMode)
                {
                    _debugTimer.Stop();
                    _debugTimespan = _debugTimer.Elapsed;
                    Debug.Log(String.Format("<color=blue> CD: </color>ExtraConstraints {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                    _debugTimer.Restart();
                }
                AddDistanceConstraints(baseColors);
                if (_debugMode)
                {
                    _debugTimer.Stop();
                    _debugTimespan = _debugTimer.Elapsed;
                    Debug.Log(String.Format("<color=blue> CD: </color>AddDistanceConstraints {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                    _debugTimer.Restart();
                }
                AddBendingConstraints();
                if (_debugMode)
                {
                    _debugTimer.Stop();
                    _debugTimespan = _debugTimer.Elapsed;
                    Debug.Log(String.Format("<color=blue> CD: </color>AddBendingConstraints {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                    _debugTimer.Restart();
                }
                // modify positions back to local coordinates
                for (int i = 0; i < _numParticles; i++)
                {
                    _positions[i] = transform.InverseTransformPoint(_positions[i]);
                }
            }

            if (_usePreCache) PreCacheClothData(meshList);

            SetupComputeBuffers();

            InitializeDiffFrames(transform.position, transform.lossyScale, transform.rotation);

            if (_useLOD && (_skinTypeCloth == SkinTypes.DualQuaternionSkinner || _skinTypeCloth == SkinTypes.GPUSkinning) && _cam != null && _minBlend < 0.01f)
                _minBlend = 0.01f;

            if (_skinComponent == null)
            {
                _minBlend = 0;
                _blendSkinning = 0;
                _dummyTex = new RenderTexture(4, 4, 0);
                _skinTypeCloth = SkinTypes.NoSkinning;
                _skinningForSurfacePush = 0;
            }

            var mesh = _useTransferData ? this.GetComponent<SkinnedMeshRenderer>().sharedMesh : GetComponent<MeshFilter>().mesh;
            mesh.RecalculateBounds();
            if (_scaleBoundingBox > 0)
            {
                var b = mesh.bounds;
                var maxSize = math.max(b.size.x, math.max(b.size.y, b.size.z));
                b.size = Vector3.one * maxSize * _scaleBoundingBox;
                if (_updateTransformCenter) b.center -= (_useTransferData ? (Renderer)this.GetComponent<SkinnedMeshRenderer>() : (Renderer)this.GetComponent<MeshRenderer>()).bounds.center - this.transform.position;
                mesh.bounds = b;
            }

            //if (!_useCollisionFinder && !_useCollidableObjectsList) _useCollisionFinder = true; //Better than nothing

            if (!_useCollisionFinder)
                _collisionFinder = null;

            if (_debugMode)
            {
                _debugTimer.Stop();
                _debugTimespan = _debugTimer.Elapsed;
                Debug.Log(String.Format("<color=blue> CD: </color>Before CollisionFinder {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                _debugTimer.Restart();
            }

            if (_meshObjects != null)
            {
                Extensions.CleanupList(ref _meshObjects);
            }
            else
            {
                _useCollisionFinder = false;
            }

#if UNITY_STANDALONE && (UNITY_ANDROID || UNITY_IPHONE)
			_useCollisionFinder = false;
#endif
            if (!_useCollisionFinder)
            {
                _useCustomVoxelCenter = true;
            }

            if (_useCollisionFinder && _collisionFinder != null)
            {
                GetVoxelCubePosQuick();

                if (_collisionFinder._scanCS == null) _collisionFinder._scanCS = Resources.Load("Shaders/Compute/Scan") as ComputeShader;
                if (_collisionFinder._sphereFinderCS == null) _collisionFinder._sphereFinderCS = Resources.Load("Shaders/Compute/SphereFinder") as ComputeShader;
                if (cloths.Length > 1)
                {
                    _collisionFinder._scanCS = Instantiate(_collisionFinder._scanCS);
                    _collisionFinder._sphereFinderCS = Instantiate(_collisionFinder._sphereFinderCS);
                }

                var tempList = _meshObjects.ToList();
                Extensions.CleanupList(ref tempList);
                foreach (var item in tempList)
                {
                    if (!item.gameObject.activeInHierarchy) tempList.Remove(item);
                }
                _meshObjects = tempList.ToArray();

                if (_useSelfCollision || _meshObjects.Length == 0)
                {
                    if (!_meshObjects.Contains(this.transform))
                    {
                        var list = _meshObjects.ToList();
                        list.Insert(0, this.transform);
                        _meshObjects = list.ToArray();
                    }
                }
                //if (_meshObjects.Contains(this.transform) && _meshObjects.Length == 1)
                //	_useSelfCollision = true; //This is helpful, but confusing for the user


                _collisionFinder.InitNeighbourFinder(this, ref _meshObjects, ref _useTriangleMesh);

                if (this.enabled)
                {
                    var vcs = _collisionFinder._vertexCounts;
                    int vSum = 0;
                    for (int i = 0; i < vcs.Length; i++)
                    {
                        vSum += vcs[i];
                    }
                    float weightSelfVertsCollision = math.min(0.6f, (vcs[0] / (float)vSum) * 2);
                    if (_meshObjects.Length < 1 || !_meshObjects[0].GetComponent<GPUClothDynamics>())
                        weightSelfVertsCollision = 0.01f;
                    if (_meshObjects.Length == 1 && _meshObjects[0].GetComponent<GPUClothDynamics>())
                        weightSelfVertsCollision = 0.99f;

                    //Debug.Log("weightSelfVertsCollision " + weightSelfVertsCollision);
#if !UNITY_EDITOR
					if (_predictiveContact)
					{
#endif
                    _pointPointContactBuffer = new ComputeBuffer((int)math.max(1, _collisionFinder._voxelDataBuffer2.count * weightSelfVertsCollision), sizeof(float) * 4 + sizeof(int) * 4);
                    _pointPointContactBuffer2 = new ComputeBuffer((int)math.max(1, _collisionFinder._voxelDataBuffer2.count * (1.0f - weightSelfVertsCollision)), sizeof(float) * 4 + sizeof(int) * 4);

                    if (_debugBuffers)
                    {
                        Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " start value: _pointPointContactBuffer " + _pointPointContactBuffer.count + "</color>");
                        Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " start value: _pointPointContactBuffer2 " + _pointPointContactBuffer2.count + "</color>");
                    }
#if !UNITY_EDITOR
					}
#endif
                    _countContactBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
                    _countContactBuffer.SetData(new int[] { 1, 1, 1 });
                    _countContactBuffer2 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
                    _countContactBuffer2.SetData(new int[] { 1, 1, 1 });
                    _countContactBuffer3 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
                    _countContactBuffer3.SetData(new int[] { 1, 1, 1 });
                }
                else return;

                SetCollisionFinderBuffers();

            }

            if (_debugMode)
            {
                _debugTimer.Stop();
                _debugTimespan = _debugTimer.Elapsed;
                Debug.Log(String.Format("<color=blue> CD: </color>CollisionFinder {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                _debugTimer.Restart();
            }

            _getDepthPos?.Init(this, _cam);

            if (!_useTransferData && this.GetComponent<SkinnedMeshRenderer>())
                Destroy(this.GetComponent<SkinnedMeshRenderer>());

            SetupCollisionComputeBuffers();

            SetupSkinningHD();

            StartCoroutine(DelayVoxelPosState());

            if (_useGarmentMesh)
            {
                _blendGarmentOriginKernel = _clothSolver.FindKernel("BlendGarmentOrigin");
                _baseVerticesBuffer = new ComputeBuffer(baseVertices.Length, sizeof(float) * 3);
                _baseVerticesBuffer.SetData(baseVertices);
            }

            if (_meshObjects != null)
            {
                //if (_meshObjects.Contains(this.transform)) _useSelfCollision = true;
                //if (_meshObjects.All(x => x.GetComponent<GPUClothDynamics>())) _useSurfacePush = false;

                if (_meshObjects.Length < 1 || !_meshObjects[0].GetComponent<GPUClothDynamics>())
                    _clothSolver.SetInt("_numSelfParticles", 0);
                else
                    _clothSolver.SetInt("_numSelfParticles", _numParticles);
            }

            if (_useTransferData && _rootBone != null)
            {
                _mpb.SetVector("g_RootRot", QuatToVec(_rootBone.localRotation));
                _mpb.SetVector("g_RootPos", _rootBone.localPosition);
            }
            _mpb.SetBuffer(_mpb_normalsBuffer_ID, _objBuffers[0].normalsBuffer);
            _mpb.SetBuffer(_mpb_positionsBuffer_ID, _objBuffers[0].positionsBuffer);
            if (_mr != null) _mr.SetPropertyBlock(_mpb);

            if (_abs == null) { _abs = this.GetComponentInParent<AutomaticBoneSpheres>(); if (_abs) _abs._updateSync = true; }

            if (_debugMode)
            {
                _debugTimer.Stop();
                _debugTimespan = _debugTimer.Elapsed;
                Debug.Log(String.Format("<color=blue> CD: </color>Finished Cloth Setup <color=lime>" + this.name + "</color> {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
            }

            _finishedLoading = true;
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || !this.enabled || _blendDist >= 1 || !_finishedLoading) return;

            if (_useFrustumClipping)
            {
                //if (_id == 1 || _diffCams || _planes == null)

                if (_mesh != null)
                {
                    _planes = GeometryUtility.CalculateFrustumPlanes(_cam);

                    var frustumBounds = new Bounds(this.transform.TransformPoint(_mesh.bounds.center), this.transform.localRotation * _mesh.bounds.size); //transform.TransformVector(_mesh.bounds.size)
                                                                                                                                                          //Debug.DrawLine(frustumBounds.min, frustumBounds.min + Vector3.up * (frustumBounds.max.y - frustumBounds.min.y));
                                                                                                                                                          //Debug.DrawLine(frustumBounds.max, frustumBounds.max + Vector3.down * (frustumBounds.max.y - frustumBounds.min.y));
                                                                                                                                                          //Debug.DrawLine(frustumBounds.min, frustumBounds.max);

                    //_foundInFrustum = false;
                    //if (GeometryUtility.TestPlanesAABB(_planes, frustumBounds))
                    //    _foundInFrustum = true;

                    float3 extents = frustumBounds.extents;
                    int inside = 0;
                    for (int p = 0; p < 4; ++p)
                    {
                        float d = _planes[p].distance + _camCullDist + math.dot(frustumBounds.center, _planes[p].normal);
                        float r = math.dot(extents, math.abs(_planes[p].normal));
                        inside += (int)math.sign(d + r);
                    }

                    _foundInFrustum = false;

                    float sphereRadius = frustumBounds.size.magnitude;// * 0.5f;

                    foreach (var light in _cullingLights)
                    {
                        if (_useShadowCulling && light != null && TestSweptSphere(frustumBounds.center, sphereRadius, light.transform.forward, _planes))
                        {
                            _foundInFrustum = true;
                        }
                    }
                    if (inside >= 4)
                        _foundInFrustum = true;
                }
                else _foundInFrustum = true;
            }
            else _foundInFrustum = true;

            if (_foundInFrustum)
            {
                _abs?.UpdateSync();

                // step 8: update collisions
                UpdateCollisionComputeBuffers(Time.fixedDeltaTime);
            }

            //if (Input.GetKeyDown(KeyCode.E))
            //    ExportMesh();
        }

        private void Update()
        {
            if (!Application.isPlaying || !this.enabled || _blendDist >= 1 || !_finishedLoading) return;

            if (_foundInFrustum)
            {
                if (_useCollisionFinder && _collisionFinder != null) _collisionFinder.Update();
                if (_enableMouseInteraction) _getDepthPos?.Update();
            }
        }

        private void LateUpdate()
        {
            if (this.enabled && !_updateSync && _foundInFrustum && _runSim && Application.isPlaying && _finishedLoading)
                ClothUpdate();
        }

        internal void UpdateSync()
        {
            if (this.enabled && _updateSync && _foundInFrustum && _runSim && Application.isPlaying && _finishedLoading)
                ClothUpdate();
        }

        private void ClothUpdate()
        {
            if (this == null) return;

            _blendDist = 0;
            if (_useLOD && (_skinTypeCloth == SkinTypes.DualQuaternionSkinner || _skinTypeCloth == SkinTypes.GPUSkinning) && _cam != null)
            {
                var dist = Vector3.Distance(this.transform.position, _cam.transform.position);
                var linearBlendDist = math.saturate(dist / _distLod);
                _blendDist = _distLod == 0 ? 1 : math.saturate(_lodCurve._curve.Evaluate(linearBlendDist)); // math.saturate(math.exp((blendDist - 1.5f) * 4) * 8 - 0.016f);
                                                                                                            //if (_minBlend < 0.01f) _minBlend = 0.01f;
            }

            if (_skinComponent != null && _blendDist >= 1) //Switch to GPU Skinning only
            {
                if (_mr.material.shader != _skinComponent._shader)
                {
                    if (_skinTypeCloth == SkinTypes.GPUSkinning)
                    {
                        var skinning = _skinComponent as GPUSkinning;
                        skinning.SetShader(force: true);
                        var mats = _mr.materials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i].shader = skinning._shader;
                            mats[i].EnableKeyword("USE_BUFFERS");
                            if (skinning._meshVertsOut != null) mats[i].SetBuffer(skinning._propID, skinning._meshVertsOut);
                        }
                    }
                    else if (_skinTypeCloth == SkinTypes.DualQuaternionSkinner)
                    {
                        var skinning = _skinComponent as DualQuaternionSkinner;
                        skinning.SetShader(force: true);
                        var mats = _mr.materials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i].shader = skinning._shader;
                            mats[i].EnableKeyword("USE_BUFFERS");
                            skinning.UpdateMaterialPropertyBlock();
                        }
                    }
                }
            }
            else
            {
                if (_mr != null && _skinComponent != null && _mr.material.shader == _skinComponent._shader)
                {
                    SetClothShader(_mr);

                    if (_useTransferData && _rootBone != null)
                    {
                        _mpb.SetVector("g_RootRot", QuatToVec(_rootBone.localRotation));
                        _mpb.SetVector("g_RootPos", _rootBone.localPosition);
                    }
                    _mpb.SetBuffer(_mpb_normalsBuffer_ID, _objBuffers[0].normalsBuffer);
                    _mpb.SetBuffer(_mpb_positionsBuffer_ID, _objBuffers[0].positionsBuffer);
                    if (_mr != null) _mr.SetPropertyBlock(_mpb);
                }
                if (_enableMouseInteraction != _enableMouseInteractionLastState || _lastPointConstraintType != _pointConstraintType || _useCollidableObjectsList != _useCollidableObjectsListLastState || (_useCollidableObjectsList && _lastCollidableObjectsCount != _collidableObjects.Count))
                {
                    _enableMouseInteractionLastState = _enableMouseInteraction;
                    _useCollidableObjectsListLastState = _useCollidableObjectsList;
                    SetupCollisionComputeBuffers();
                }

#if UNITY_EDITOR //For Debugging
                if (_debugMode || (_useGarmentMesh && _updateGarmentMesh && !_onlyAtStart)) SetupComputeBuffers(false);
#endif

                if (_blendDist < 1 && _useCollisionFinder && _collisionFinder != null)
                {
                    _collisionFinder.CollisionPointsUpdate();

                    float voxelCubeScale = _voxelCubeScale;
                    Vector4 cubeMinPos = _voxelCubePos - Vector3.one * voxelCubeScale * 0.5f;
                    cubeMinPos.w = voxelCubeScale;
                    _clothSolver.SetVector(_cubeMinPos_ID, cubeMinPos);
                }

                _invMass = 1.0f / _vertexMass;
                _clothSolver.SetFloat(_invMass_ID, _invMass);
                _clothSolver.SetFloat(_staticFriction_ID, math.saturate(1.0f - _staticFriction));
                _clothSolver.SetFloat(_dynamicFriction_ID, _dynamicFriction);
                _clothSolver.SetFloat(_stretchStiffness_ID, _distanceStretchStiffness);
                _clothSolver.SetFloat(_compressionStiffness_ID, _distanceCompressionStiffness);
                _clothSolver.SetFloat(_bendingStiffness_ID, _bendingStiffness);
                _clothSolver.SetFloat(__worldPositionImpact_ID, _worldPositionImpact);
                _clothSolver.SetFloat(__worldRotationImpact_ID, _worldRotationImpact);
                _clothSolver.SetFloat(__collidableObjectsBias_ID, _collidableObjectsBias);
                _clothSolver.SetFloat(__minBlend_ID, _minBlend + _blendDist);
                _clothSolver.SetFloat(__blendSkinning_ID, _blendSkinning);
                _clothSolver.SetVector(_gravity_ID, _gravity);

                if (_wind != null)
                {
                    //Don't know what the Unity WindZone algorithm does, so we're guessing here, free to edit:
                    var windVec = _wind.transform.forward * _windIntensity * _wind.windMain * 0.3333f * (1 + (UnityEngine.Random.value * _wind.windTurbulence) + GetWindTurbulence(Time.fixedTime, _wind.windPulseFrequency, _wind.windPulseMagnitude));
                    _clothSolver.SetVector(_windVec_ID, windVec);
                }

                _clothSolver.SetMatrix(_worldToLocalMatrix_ID, this.transform.worldToLocalMatrix);
                _clothSolver.SetMatrix(_localToWorldMatrix_ID, this.transform.localToWorldMatrix);
                _clothSolver.Dispatch(_updateWorldTransformKernel, _numGroups_Vertices, 1, 1);

                if (_blendDist < 1)
                {
                    //if (_usePredictiveContactColliders)
                    //{
                    //    int _pPPCClearKernel = _clothSolver.FindKernel("PPPCClear");
                    //    _clothSolver.SetBuffer(_pPPCClearKernel, _pointPointContactBuffer3_ID, _pointPointContactBuffer3);
                    //    _clothSolver.Dispatch(_pPPCClearKernel, _pointPointContactBuffer3.count.GetComputeShaderThreads(256), 1, 1); //TODO
                    //}

                    // calculate the timestep 

                    _nextFrameTime += Time.deltaTime * _timeMultiplier;
                    int iter = 0;

                    float timeStep = _timestep + _blendDist * _lodTimeStepAdd;

                    while (_nextFrameTime > 0)
                    {
                        if (_nextFrameTime < timeStep)
                        {
                            break;
                        }
                        float dt = Mathf.Min(_nextFrameTime, timeStep);
                        _nextFrameTime -= dt;
                        iter++;

                        for (int i = 0; i < _subSteps; i++)
                        {

                            dt = dt / (float)_subSteps;

                            UpdateDiffFrames(dt);

                            // send the dt data to the GPU
                            _clothSolver.SetFloat(_dt_ID, dt);
                            _clothSolver.SetFloat(_deltaScale_ID, _deltaScale);
                            _clothSolver.SetFloat(_clampDelta_ID, _clampDelta);

                            // step 5: apply external forces
                            _clothSolver.Dispatch(_applyExternalForcesKernel, _numGroups_Vertices, 1, 1);

                            // step 6: damp velocity
                            if (_dampingMethod == DampingMethod.smartDamping || _dampingMethod == DampingMethod.smartAndSimpleDamping)
                            {
                                SmartDamping();
                            }
                            if (_dampingMethod == DampingMethod.simpleDamping || _dampingMethod == DampingMethod.smartAndSimpleDamping)
                            {
                                _clothSolver.SetFloat(_clampVel_ID, _clampVel);
                                _clothSolver.SetFloat(_dampingVel_ID, _dampingVel);
                                _clothSolver.Dispatch(_dampVelocitiesKernel, _numGroups_Vertices, 1, 1);
                            }

                            // step 7: apply explicit Euler to positions based on velocity
                            _clothSolver.Dispatch(_applyExplicitEulerKernel, _numGroups_Vertices, 1, 1);

                            if (iter == 1)
                            {

                                if (_useCollisionFinder && _collisionFinder != null || _usePredictiveContactColliders)
                                {
                                    _clothSolver.Dispatch(_countContactStartKernel, 1, 1, 1);

                                    if (_predictiveContact || _usePredictiveContactColliders) _clothSolver.SetFloat(_collisionRadius_ID, _staticCollisionRadius);
                                    _clothSolver.SetFloat(_deltaTime_ID, dt);
                                }

                                if (_useCollisionFinder && _collisionFinder != null)
                                {
                                    if (_predictiveContact)
                                    {
                                        //#if UNITY_EDITOR
                                        if (_useNeighbourCheck)
                                        {
                                            _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _connectionInfoBuffer_ID, _objBuffers[0].connectionInfoBuffer);
                                            _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _connectedVertsBuffer_ID, _objBuffers[0].connectedVertsBuffer);
                                        }
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _velocities_ID, _velocitiesBuffer);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _pointPointContactBuffer_ID, _pointPointContactBuffer);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _pointPointContactBuffer2_ID, _pointPointContactBuffer2);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _countContactBuffer_ID, _countContactBuffer);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _countContactBuffer2_ID, _countContactBuffer2);
                                        //#endif
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _usedVoxelListBuffer_ID, _collisionFinder._usedVoxelListBuffer);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _counterPerVoxelBuffer2_ID, _collisionFinder._counterPerVoxelBuffer2);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _lastCounterPerVoxelBuffer2_ID, _collisionFinder._lastCounterPerVoxelBuffer2);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                                        _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _trisData_ID, _collisionFinder._selfTrisDataBuffer);
                                        _clothSolver.Dispatch(_pointPointPredictiveContactKernel, _numGroups_Vertices, 1, 1);
                                    }
                                    else
                                    {
                                        if (_useNeighbourCheck)
                                        {
                                            _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _connectionInfoBuffer_ID, _objBuffers[0].connectionInfoBuffer);
                                            _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _connectedVertsBuffer_ID, _objBuffers[0].connectedVertsBuffer);
                                        }
                                        //#if UNITY_EDITOR
                                        _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                                        _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _frictions_ID, _frictionsBuffer);
                                        //#endif
                                        _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _usedVoxelListBuffer_ID, _collisionFinder._usedVoxelListBuffer);
                                        _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _counterPerVoxelBuffer2_ID, _collisionFinder._counterPerVoxelBuffer2);
                                        _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _lastCounterPerVoxelBuffer2_ID, _collisionFinder._lastCounterPerVoxelBuffer2);
                                        _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                                        _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _trisData_ID, _collisionFinder._selfTrisDataBuffer);
                                    }
                                }

                                if (_usePredictiveContactColliders)
                                {
                                    _clothSolver.Dispatch(_pointPointPredictiveContactCollidersKernel, _numGroups_Vertices, 1, 1);
                                }

                                if (_useCollisionFinder && _collisionFinder != null || _usePredictiveContactColliders)
                                {
                                    _clothSolver.Dispatch(_countContactSetupKernel, 1, 1, 1);
                                }

                                if (_debugBuffers)
                                {
                                    if (_useCollisionFinder && _collisionFinder != null)
                                    {
                                        int[] data = new int[_countContactBuffer.count];
                                        _countContactBuffer.GetData(data);
                                        _maxDebugData = math.max(data[0] * 256, _maxDebugData);
                                        Debug.Log("<color=blue> CD: </color>_countContactBuffer " + _maxDebugData);

                                        int[] data2 = new int[_countContactBuffer2.count];
                                        _countContactBuffer2.GetData(data2);
                                        _maxDebugData2 = math.max(data2[0] * 256, _maxDebugData2);
                                        Debug.Log("<color=blue>CD: </color>_countContactBuffer2 " + _maxDebugData2);
                                    }
                                    if (_usePredictiveContactColliders)
                                    {
                                        int[] data3 = new int[_countContactBuffer3.count];
                                        _countContactBuffer3.GetData(data3);
                                        _maxDebugData3 = math.max(data3[0] * 256, _maxDebugData3);
                                        Debug.Log("<color=blue>CD: </color>_countContactBuffer3 " + _maxDebugData3);
                                    }
                                }
                            }

                            // step 9-11: project constraints iterationNum times
                            int lodIterationNum = _useLOD ? Mathf.Max(1, Mathf.RoundToInt(math.saturate(1 - _blendDist * 4) * _iterationNum)) : _iterationNum;
                            //Debug.Log("lodIterationNum " + lodIterationNum + " _blendDist " + _blendDist);

                            //_clothSolver.SetFloat(_deltaTime_ID, dt/(float)lodIterationNum);

                            for (int j = 0; j < lodIterationNum; j++)
                            {
                                // distance constraints
                                _clothSolver.Dispatch(_projectConstraintDeltasKernel, _numGroups_DistanceConstraints, 1, 1);
                                _clothSolver.Dispatch(_averageConstraintDeltasKernel, _numGroups_Vertices, 1, 1);
                                _clothSolver.Dispatch(_bendingConstraintDeltasKernel, _numGroups_BendingConstraints, 1, 1);
                                _clothSolver.Dispatch(_averageConstraintDeltasKernel, _numGroups_Vertices, 1, 1);

                                if (!_usePredictiveContactColliders)
                                {
                                    if (_useCollidableObjectsList)
                                    {
                                        if (_numCollidableSpheres > 0)
                                        {
                                            _clothSolver.Dispatch(_satisfySphereCollisionsKernel, _numGroups_Vertices, 1, 1);
                                        }
                                        if (_numCollidableSDFs > 0)
                                        {
                                            _clothSolver.Dispatch(_satisfySDFCollisionsKernel, _numGroups_Vertices, 1, 1);
                                        }
                                    }
                                }

                                if (_useCollisionFinder && _collisionFinder != null)
                                {

                                    if (_predictiveContact)
                                    {
                                        _clothSolver.SetBuffer(_selfContactCollisionsKernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                                        _clothSolver.DispatchIndirect(_selfContactCollisionsKernel, _countContactBuffer);

                                        //_clothSolver.Dispatch(averageConstraintDeltasKernel, numGroups_Vertices, 1, 1);

                                        _clothSolver.SetBuffer(_otherSpheresContactCollisions2Kernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                                        _clothSolver.DispatchIndirect(_otherSpheresContactCollisions2Kernel, _countContactBuffer2);

                                        if (!_usePredictiveContactColliders) _clothSolver.Dispatch(_averageConstraintDeltasKernel, _numGroups_Vertices, 1, 1);
                                    }
                                    else
                                    {
                                        //_clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _deltaPosAsInt_ID, _deltaPositionsUIntBuffer);
                                        //_clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _deltaCount_ID, _deltaCounterBuffer);
                                        _clothSolver.Dispatch(_satisfyVertexCollisionsKernel, _numGroups_Vertices, 1, 1);

                                        //_clothSolver.Dispatch(_averageConstraintDeltasKernel, _numGroups_Vertices, 1, 1);
                                    }
                                }

                                if (_usePredictiveContactColliders)
                                {
                                    _clothSolver.DispatchIndirect(_collidersContactCollisionsKernel, _countContactBuffer3);
                                    _clothSolver.Dispatch(_averageConstraintDeltasKernel, _numGroups_Vertices, 1, 1);
                                }

                                // satisfy pointConstraints
                                if (_numPointConstraints > 0)
                                {
                                    if (_transformConstraints != null && _pointConstraintsVec != null)
                                    {
                                        for (int n = 0; n < _transformConstraints.Length; n++)
                                        {
                                            if (n < _pointConstraintsVec.Length)
                                            {
                                                var constraint = _transformConstraints[n];
                                                _pointConstraintsVec[n] = constraint.transform.position;
                                                _pointConstraintsVec[n].w = constraint._intensity + 1;
                                                if (constraint._vertexId >= 0)
                                                {
                                                    _pointConstraints[n] = constraint._vertexId;
                                                }
                                            }
                                        }
                                        _pointConstraintsVecBuffer.SetData(_pointConstraintsVec);
                                    }
                                    if (_enableMouseInteraction && _tempPointConstraint != -1)
                                    {
                                        _pointConstraints[_pointConstraints.Length - 1] = _tempPointConstraint;
                                        _clothSolver.SetInt(_numPointConstraints_ID, _numPointConstraints);
                                        _clothSolver.SetBool(_hasTempPointConstraint_ID, true);
                                        _clothSolver.SetVector(_deltaPointConstraint_ID, _deltaPointConstraint);
                                        _clothSolver.SetInt(_tempPointConstraint_ID, _tempPointConstraint);
                                    }
                                    else
                                    {
                                        if (_enableMouseInteraction) _clothSolver.SetInt(_numPointConstraints_ID, _numPointConstraints - 1);
                                        else _clothSolver.SetInt(_numPointConstraints_ID, _numPointConstraints);
                                        _clothSolver.SetBool(_hasTempPointConstraint_ID, false);
                                        _clothSolver.SetInt(_tempPointConstraint_ID, -1);
                                    }
                                    _pointConstraintsBuffer.SetData(_pointConstraints);

                                    _clothSolver.Dispatch(_satisfyPointConstraintsKernel, _numGroups_PointConstraints, 1, 1);
                                }
                            }

                            if (_useSurfacePush) SurfacePush();

                            // step 13 & 14: apply projected positions to actual vertices
                            ApplySkinningAndPositions();
                        }
                    }

                    if (_fixDoubles && _duplicateVerticesBuffer != null)
                    {
                        _clothSolver.SetInt(_duplicateVerticesCount_ID, _duplicateVerticesBuffer.count);
                        _clothSolver.Dispatch(_transferDuplicateVertexDataKernel, _duplicateVerticesBuffer.count.GetComputeShaderThreads(8), 1, 1);
                    }

                    // handle mouse drag inputs
                    if (_enableMouseInteraction)
                    {
                        if (Input.GetMouseButtonDown(0) && _tempPointConstraint == -1 && _tempPointVertexId >= 0)
                        {
                            _tempPointConstraint = _tempPointVertexId;
                            _lastMousePos = InputEx.mousePosition;
                            _deltaPointConstraint = Vector3.zero;
                        }
                        else if (Input.GetMouseButtonUp(0))
                        {
                            _tempPointConstraint = -1;
                        }
                        if (_tempPointConstraint != -1)
                        {
                            _deltaPointConstraint = InputEx.mousePosition - _lastMousePos;
                            _deltaPointConstraint *= _deltaConstraintMul;
                            _deltaPointConstraint = _cam.transform.TransformDirection(_deltaPointConstraint);
                            _lastMousePos = InputEx.mousePosition;
                        }
                    }

                }
                else
                {
                    ApplySkinningAndPositions();
                }

                // get voxelCube center by vertices
                if (_useCollisionFinder && !_useCustomVoxelCenter) DispatchVoxelCubeCenter(_numParticles, _gridCenterBuffer, _objBuffers[0].positionsBuffer, _deltaPositionsUIntBuffer2);

                if (_useCollisionFinder && !_useCustomVoxelCenter && _useReadbackCenter && !_runReadback) StartCoroutine(ReadbackVoxelCubeCenter());
                else if (_useCustomVoxelCenter) _voxelCubePos = _customVoxelCenter.position;

                if (_useReadbackVertices && !_runReadbackVertices) StartCoroutine(ReadbackVertexData());

                // recalculate the center of the mesh
                Vector3 newCenter = Vector3.zero;
                Vector3 delta = Vector3.zero;
                if (_updateTransformCenter)
                {
                    newCenter = _voxelCubePos;
                    delta = newCenter - transform.position;
                }

                // modify data to local coordinates
                _clothSolver.SetVector(_delta_ID, delta);
                _clothSolver.Dispatch(_updateInverseWorldTransformKernel, _numGroups_Vertices, 1, 1);

                if (_updateTransformCenter)
                {
                    transform.position = newCenter;
                }


                if (_slowCPUWriteBack)
                {
                    // get data from GPU back to CPU
                    _objBuffers[0].positionsBuffer.GetData(_positions);
                    _mesh.vertices = _positions;
                    _mesh.RecalculateNormals();
                    GetComponent<MeshFilter>().mesh = _mesh;
                    //_mesh.RecalculateBounds();
                    //if (GetComponent<MeshCollider>()) GetComponent<MeshCollider>().sharedMesh = _mesh;
                }
                else
                {
                    ComputeNormals(0);

                    if (_debugMode)
                    {
                        if (_useTransferData && _rootBone != null)
                        {
                            _mpb.SetVector("g_RootRot", QuatToVec(_rootBone.localRotation));
                            _mpb.SetVector("g_RootPos", _rootBone.localPosition);
                        }
                        _mpb.SetBuffer(_mpb_normalsBuffer_ID, _objBuffers[0].normalsBuffer);
                        _mpb.SetBuffer(_mpb_positionsBuffer_ID, _objBuffers[0].positionsBuffer);
                        if (_mr != null) _mr.SetPropertyBlock(_mpb);
                    }
                }

                ComputeSkinningHD();
            }
        }

        private void ApplySkinningAndPositions()
        {
            if (_useGarmentMesh)
            {
                bool prewarm = Time.frameCount < 30;
                if (prewarm || (!_onlyAtStart && _baseVerticesBuffer != null))
                {
                    _clothSolver.SetFloat(_blendGarment_ID, prewarm ? 0.01f : _blendGarment * 0.01f);
                    _clothSolver.SetFloat(_pushVertsByNormals_ID, _pushVertsByNormals);
                    _clothSolver.SetBuffer(_blendGarmentOriginKernel, _normalsBuffer_ID, _objBuffers[0].normalsBuffer);
                    _clothSolver.SetBuffer(_blendGarmentOriginKernel, _baseVertices_ID, _baseVerticesBuffer);
                    //_clothSolver.SetBuffer(blendGarmentOriginKernel, _positions_ID, _objBuffers[0].positionsBuffer);
                    _clothSolver.SetBuffer(_blendGarmentOriginKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                    _clothSolver.Dispatch(_blendGarmentOriginKernel, _numGroups_Vertices, 1, 1);
                }
            }
            if (_skinTypeCloth == SkinTypes.DualQuaternionSkinner)
                _clothSolver.Dispatch(_updatePositionsKernel, _numGroups_Vertices, 1, 1);
            else if (_skinTypeCloth == SkinTypes.GPUSkinning)
            {
                bool morph = false;
                if (this.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                    morph = true;

                int kernel = morph ? _updatePositions2BlendsKernel : _updatePositions2Kernel;

                if (morph)
                {
                    //print("blendShapes " + blendShapes.gameObject.name);
                    var blendShapes = (GPUBlendShapes)monoMorph;
                    _clothSolver.SetInt("_rtArrayWidth", blendShapes._rtArrayCombined.width);
                    _clothSolver.SetTexture(kernel, "_rtArray", blendShapes._rtArrayCombined);
                }

                _clothSolver.Dispatch(kernel, _numGroups_Vertices, 1, 1);
            }
            else
                _clothSolver.Dispatch(_updatePositionsNoSkinningKernel, _numGroups_Vertices, 1, 1);
        }

        private void ComputeNormals(int index)
        {
            _clothSolver.SetInt(_maxVerts_ID, _objBuffers[index].positionsBuffer.count);
            _clothSolver.SetBuffer(_csNormalsKernel, _positions_ID, _objBuffers[index].positionsBuffer);
            _clothSolver.SetBuffer(_csNormalsKernel, _connectionInfoBuffer_ID, _objBuffers[index].connectionInfoBuffer);
            _clothSolver.SetBuffer(_csNormalsKernel, _connectedVertsBuffer_ID, _objBuffers[index].connectedVertsBuffer);
            _clothSolver.SetBuffer(_csNormalsKernel, _normalsBuffer_ID, _objBuffers[index].normalsBuffer);
            _clothSolver.Dispatch(_csNormalsKernel, _objBuffers[index].positionsBuffer.count.GetComputeShaderThreads(256), 1, 1);
        }

        private void SurfacePush()
        {
            bool useCF = _forceSurfacePushColliders ? false : _useCollisionFinder && _collisionFinder != null;
            if (!useCF) _forceSurfacePushColliders = true;

            int kernel = useCF ? _surfacePushKernel : _surfacePushCollidersKernel;

            if (_skinningForSurfacePush > 0)
            {
                _clothSolver.SetFloat(_skinningForSurfacePush_ID, _skinningForSurfacePush);
                if (_skinTypeCloth == SkinTypes.DualQuaternionSkinner)
                {
                    var dqs = _skinComponent as DualQuaternionSkinner;
                    if (dqs.gameObject.activeInHierarchy)
                    {
                        kernel = useCF ? _surfacePushDQSKernel : _surfacePushCollidersDQSKernel;
                        //_clothSolver.SetInt(_skinned_tex_width_ID, dqs ? dqs._textureWidth : 4);//should be set already
                        _clothSolver.SetTexture(kernel, _skinned_data_1_ID, dqs ? dqs._rtSkinnedData_1 : _dummyTex);
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
                            _clothSolver.SetInt("_rtArrayWidth", blendShapes._rtArrayCombined.width);
                            _clothSolver.SetTexture(kernel, "_rtArray", blendShapes._rtArrayCombined);
                        }

                        if (skinning._meshVertsOut != null) _clothSolver.SetBuffer(kernel, _meshVertsOut_ID, skinning._meshVertsOut);
                    }
                }
            }
            if (useCF)
            {
                _clothSolver.SetFloat(_normalScale_ID, _collisionFinder._useTrisMesh ? _triangleNormalScale : _vertexNormalScale);
                _clothSolver.SetBuffer(kernel, _usedVoxelListBuffer_ID, _collisionFinder._usedVoxelListBuffer);
                _clothSolver.SetBuffer(kernel, _counterPerVoxelBuffer2_ID, _collisionFinder._counterPerVoxelBuffer2);
                _clothSolver.SetBuffer(kernel, _lastCounterPerVoxelBuffer2_ID, _collisionFinder._lastCounterPerVoxelBuffer2);
                _clothSolver.SetBuffer(kernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
            }
            else
            {
                _clothSolver.SetBuffer(kernel, _collidableSpheres_ID, _collidableSpheresBuffer);
                _clothSolver.SetBuffer(kernel, _collidableSDFs_ID, _collidableSDFsBuffer);
            }
            _clothSolver.SetFloat(_surfacePush_ID, _surfacePush);
            _clothSolver.SetFloat(_surfaceOffset_ID, _surfaceOffset);
            _clothSolver.SetBuffer(kernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(kernel, _projectedPositions_ID, _projectedPositionsBuffer);
            _clothSolver.Dispatch(kernel, _numGroups_Vertices, 1, 1);
        }

        private void ExtraConstraints()
        {
            var aMesh = _mesh;// this.GetComponent<MeshFilter>().sharedMesh;
            var verts = aMesh.vertices;
            List<int> newVerts = new List<int>();
            List<int> newDupVerts = new List<int>();
            _dupIngoreList = new List<int>();

            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                bool duplicate = false;
                for (int i2 = 0; i2 < newVerts.Count; i2++)
                {
                    int index = newVerts[i2];
                    if ((verts[index] - p).sqrMagnitude <= _weldThreshold)
                    {
                        duplicate = true;
                        newDupVerts.Add(index);
                        newDupVerts.Add(i);
                        _dupIngoreList.Add(i);
                        break;
                    }
                }
                if (!duplicate)
                {
                    newVerts.Add(i);
                }
            }

            _extraBendingConstraints = new List<BendingConstraintStruct>();

            if (_showWeldEdges) _weldEdges = new List<Edge>();
            List<int> noDupConList = new List<int>();
            HashSet<int> wingCorners = new HashSet<int>();

            int count = newDupVerts.Count;
            for (int i = 0; i < count; i += 2)
            {
                int index = newDupVerts[i];
                if (_connectedVerts != null)
                {
                    int n = index;
                    Vector2Int info = _connectionInfo[n];
                    int start = info.x;
                    int end = info.y;

                    int connectedIndex = 0;
                    noDupConList.Clear();
                    for (int c = start; c < end; ++c)
                    {
                        int conId = _connectedVerts[c];
                        if (newDupVerts.Contains(conId))
                        {
                            connectedIndex = conId;
                        }
                        else
                        {
                            noDupConList.Add(conId);
                        }
                    }

                    if (_showWeldEdges)
                    {
                        _weldEdges.Add(new Edge(index, connectedIndex));
                    }

                    n = connectedIndex;
                    info = _connectionInfo[n];
                    start = info.x;
                    end = info.y;
                    wingCorners.Clear();
                    for (int c = start; c < end; ++c)
                    {
                        int conId = _connectedVerts[c];
                        if (noDupConList.Contains(conId))
                        {
                            wingCorners.Add(conId);
                        }
                    }

                    if (wingCorners.Count < 2) continue;

                    int4 indices = 0;
                    indices[0] = wingCorners.ElementAt(0);
                    indices[1] = wingCorners.ElementAt(1);
                    indices[2] = index;
                    indices[3] = connectedIndex;

                    var bendingConstraint = new BendingConstraintStruct();
                    bendingConstraint.index0 = indices[0];
                    bendingConstraint.index1 = indices[1];
                    bendingConstraint.index2 = indices[2];
                    bendingConstraint.index3 = indices[3];
                    Vector3 p0 = _positions[indices[0]];
                    Vector3 p1 = _positions[indices[1]];
                    Vector3 p2 = _positions[indices[2]];
                    Vector3 p3 = _positions[indices[3]];

                    Vector3 n1 = (Vector3.Cross(p2 - p0, p3 - p0)).normalized;
                    Vector3 n2 = (Vector3.Cross(p3 - p1, p2 - p1)).normalized;

                    float d = Vector3.Dot(n1, n2);
                    d = Mathf.Clamp(d, -1.0f, 1.0f);
                    bendingConstraint.restAngle = Mathf.Acos(d);

                    _extraBendingConstraints.Add(bendingConstraint);
                }
            }

            if (count > 0)
            {
                _duplicateVerticesBuffer = new ComputeBuffer(count / 2, sizeof(int) * 2);
                _duplicateVerticesBuffer.SetData(newDupVerts.ToArray());
                //Debug.Log("_duplicateVerticesBufferCount " + _duplicateVerticesBuffer.count);
            }
        }

        private void AddDistanceConstraints(Color[] baseColors)
        {
            // use a set to get unique edges
            HashSet<Edge> edgeSet = new HashSet<Edge>(new EdgeComparer());

            if (_sewEdges)
            {
                for (int n = 0; n < _positions.Length; n++)
                {
                    Vector2Int info = _connectionInfo[n];
                    int start = info.x;
                    int end = info.y;
                    for (int c = start; c < end; ++c)
                    {
                        //if (!_dupIngoreList.Contains(n) && !_dupIngoreList.Contains(_connectedVerts[c]))
                        if (!_fixDoubles || !_dupIngoreList.Contains(n))
                            edgeSet.Add(new Edge(n, _connectedVerts[c]));
                    }
                }
                if (_fixDoubles)
                {
                    _dupIngoreList.Clear();
                    _dupIngoreList = null;
                }
            }
            else
            {
                for (int i = 0; i < _triangles.Length; i++)
                {
                    edgeSet.Add(new Edge(_triangles[i].vertices[0], _triangles[i].vertices[1]));
                    edgeSet.Add(new Edge(_triangles[i].vertices[0], _triangles[i].vertices[2]));
                    edgeSet.Add(new Edge(_triangles[i].vertices[1], _triangles[i].vertices[2]));
                };
            }

            _numDistanceConstraints = edgeSet.Count;
            _distanceConstraints = new DistanceConstraintStruct[_numDistanceConstraints];
            int j = 0;
            foreach (Edge e in edgeSet)
            {
                EdgeStruct edge;
                edge.startIndex = e.startIndex;
                edge.endIndex = e.endIndex;
                _distanceConstraints[j].edge = edge;
                var dist = (_positions[edge.startIndex] - _positions[edge.endIndex]).magnitude;
                if (edge.startIndex < baseColors.Length && edge.endIndex < baseColors.Length)
                    _distanceConstraints[j].restLength = _useGarmentMesh && baseColors[edge.startIndex].g + baseColors[edge.endIndex].g > 1.5f && dist > _garmentSeamLength ? _garmentSeamLength : dist;
                else
                    _distanceConstraints[j].restLength = dist;
                j++;
            }

            //Debug.Log("_numDistanceConstraints " + _numDistanceConstraints);

        }

        private void AddBendingConstraints()
        {
            Dictionary<Edge, List<Triangle>> wingEdges = new Dictionary<Edge, List<Triangle>>(new EdgeComparer());

            // map edges to all of the faces to which they are connected

            foreach (Triangle tri in _triangles)
            {
                Edge e1 = new Edge(tri.vertices[0], tri.vertices[1]);
                if (wingEdges.ContainsKey(e1) && !wingEdges[e1].Contains(tri))
                {
                    wingEdges[e1].Add(tri);
                }
                else
                {
                    List<Triangle> tris = new List<Triangle>();
                    tris.Add(tri);
                    if (!wingEdges.ContainsKey(e1)) wingEdges.Add(e1, tris);
                }

                Edge e2 = new Edge(tri.vertices[0], tri.vertices[2]);
                if (wingEdges.ContainsKey(e2) && !wingEdges[e2].Contains(tri))
                {
                    wingEdges[e2].Add(tri);
                }
                else
                {
                    List<Triangle> tris = new List<Triangle>();
                    tris.Add(tri);
                    if (!wingEdges.ContainsKey(e2)) wingEdges.Add(e2, tris);
                }

                Edge e3 = new Edge(tri.vertices[1], tri.vertices[2]);
                if (wingEdges.ContainsKey(e3) && !wingEdges[e3].Contains(tri))
                {
                    wingEdges[e3].Add(tri);
                }
                else
                {
                    List<Triangle> tris = new List<Triangle>();
                    tris.Add(tri);
                    if (!wingEdges.ContainsKey(e3)) wingEdges.Add(e3, tris);
                }

            }

            // wingEdges are edges with 2 occurences,
            // so we need to remove the lower frequency ones
            List<Edge> keyList = wingEdges.Keys.ToList();
            foreach (Edge e in keyList)
            {
                if (wingEdges[e].Count < 2)
                {
                    wingEdges.Remove(e);
                }
            }

            _numBendingConstraints = wingEdges.Count;
            _bendingConstraints = new BendingConstraintStruct[_numBendingConstraints];
            int j = 0;
            foreach (Edge wingEdge in wingEdges.Keys)
            {
                /* wingEdges are indexed like in the Bridson,
                 * Simulation of Clothing with Folds and Wrinkles paper
                 *    3
                 *    ^
                 * 0  |  1
                 *    2
                 */

                int[] indices = new int[4];
                indices[2] = wingEdge.startIndex;
                indices[3] = wingEdge.endIndex;

                int b = 0;
                foreach (Triangle tri in wingEdges[wingEdge])
                {
                    for (int i = 0; i < 3; i++)
                    {
                        int point = tri.vertices[i];
                        if (point != indices[2] && point != indices[3])
                        {
                            //tri #1
                            if (b == 0)
                            {
                                indices[0] = point;
                                break;
                            }
                            //tri #2
                            else if (b == 1)
                            {
                                indices[1] = point;
                                break;
                            }
                        }
                    }
                    b++;
                }

                _bendingConstraints[j].index0 = indices[0];
                _bendingConstraints[j].index1 = indices[1];
                _bendingConstraints[j].index2 = indices[2];
                _bendingConstraints[j].index3 = indices[3];
                Vector3 p0 = _positions[indices[0]];
                Vector3 p1 = _positions[indices[1]];
                Vector3 p2 = _positions[indices[2]];
                Vector3 p3 = _positions[indices[3]];

                Vector3 n1 = (Vector3.Cross(p2 - p0, p3 - p0)).normalized;
                Vector3 n2 = (Vector3.Cross(p3 - p1, p2 - p1)).normalized;

                float d = Vector3.Dot(n1, n2);
                d = Mathf.Clamp(d, -1.0f, 1.0f);
                _bendingConstraints[j].restAngle = Mathf.Acos(d);

                j++;
            }

            if (_sewEdges && _fixDoubles)
            {
                var list = _bendingConstraints.ToList();
                list.AddRange(_extraBendingConstraints);
                _bendingConstraints = list.ToArray();
            }
            _numBendingConstraints = _bendingConstraints.Length;
            //Debug.Log("_numBendingConstraints " + _numBendingConstraints);

        }

        private void AddPointConstraints()
        {
            _lastPointConstraintType = _pointConstraintType;

            List<int> points = new List<int>();
            if (_pointConstraintType == PointConstraintType.blueVertexColor)
            {
                Color[] baseColors = _mesh.colors;
                for (int i = 0; i < baseColors.Length; i++)
                {
                    if (baseColors[i].b > 0.1f)
                    {
                        points.Add(i);
                    }
                }
                //var po = this.GetComponent<PaintObject>();
                //if (po != null)
                //{
                //    var colors = po.vertexColors;
                //    for (int i = 0; i < colors.Length; i++)
                //    {
                //        if (colors[i].b > 0.1f)
                //        {
                //            points.Add(i);
                //        }
                //    }
                //}
            }
            //else if (_pointConstraintType == PointConstraintType.topCorners)
            //{
            //    points.Add(_rows * (_columns + 1));
            //    points.Add((_rows + 1) * (_columns + 1) - 1);
            //}
            //else if (_pointConstraintType == PointConstraintType.topRow)
            //{
            //    for (int i = 0; i <= _columns; i++)
            //    {
            //        points.Add(_rows * (_columns + 1) + i);
            //    }
            //}
            //else if (_pointConstraintType == PointConstraintType.leftCorners)
            //{
            //    points.Add(0);
            //    points.Add(_rows * (_columns + 1));
            //}
            //else if (_pointConstraintType == PointConstraintType.leftRow)
            //{
            //    for (int i = 0; i <= _columns; i++)
            //    {
            //        points.Add(i * (_columns + 1));
            //    }
            //}

            if (_transformConstraints != null)
            {
                for (int i = 0; i < _transformConstraints.Length; i++)
                {
                    points.Add(_transformConstraints[i]._vertexId);
                }
            }

            if (_pointConstraintCustomIndices != null)
            {
                for (int i = 0; i < _pointConstraintCustomIndices.Length; i++)
                {
                    int index = _pointConstraintCustomIndices[i];
                    if (index >= 0 && index < _numParticles)
                    {
                        points.Add(index);
                    }
                }
            }

            //if (tempPointConstraint != -1)
            if (_enableMouseInteraction)
            {
                points.Add(_tempPointConstraint);
            }

            _numPointConstraints = points.Count;
            if (_numPointConstraints > 0)
            {
                _pointConstraints = new int[_numPointConstraints];
                _pointConstraintsVec = new Vector4[_numPointConstraints];
                for (int i = 0; i < _numPointConstraints; i++)
                {
                    _pointConstraints[i] = points[i];
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_showBoundingBox)
            {
                Gizmos.color = Color.white;
                var sMesh = GetComponent<MeshFilter>() != null ? GetComponent<MeshFilter>().sharedMesh :
                    GetComponent<SkinnedMeshRenderer>() != null ? GetComponent<SkinnedMeshRenderer>().sharedMesh : null;
                if (sMesh)
                {
                    Gizmos.DrawWireCube(transform.TransformPoint(sMesh.bounds.center), transform.localRotation * sMesh.bounds.size);//
                }

                if (!Application.isPlaying && this.GetComponent<Renderer>())
                {
                    GetVoxelCubePosQuick();
                }
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_voxelCubePos, Vector3.one * _voxelCubeScale);

                if (_collisionFinder != null)
                {
                    var gridCount = _collisionFinder._gridCount;
                    var startPos = _voxelCubePos - Vector3.one * _voxelCubeScale * 0.5f;
                    var voxelSize = _voxelCubeScale / (float)gridCount;
                    startPos += Vector3.one * voxelSize * 0.5f;
                    for (int i = 0; i < gridCount; i++)
                    {
                        Gizmos.DrawWireCube(startPos + Vector3.one * voxelSize * i, Vector3.one * voxelSize);
                    }
                }
            }
            if (Application.isPlaying && _showWeldEdges && _weldEdges != null)
            {
                Gizmos.color = Color.green;
                int count = _weldEdges.Count;
                _weldEdgeId = math.clamp(_weldEdgeId, -1, count - 1);
                if (_weldEdgeId >= 0)
                {
                    int i = _weldEdgeId;
                    Gizmos.DrawLine(transform.TransformPoint(_positions[_weldEdges[i].startIndex]), transform.TransformPoint(_positions[_weldEdges[i].endIndex]));
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        Gizmos.DrawLine(transform.TransformPoint(_positions[_weldEdges[i].startIndex]), transform.TransformPoint(_positions[_weldEdges[i].endIndex]));
                    }
                }

                //if (_connectedVerts != null)
                //{
                //    int n = _debugVertexId;
                //    Vector2Int info = _connectionInfo[n];
                //    int start = info.x;
                //    int end = info.y;

                //    for (int c = start; c < end; ++c)
                //    {
                //        var neighbour = _positions[_connectedVerts[c]];
                //        Gizmos.DrawLine(transform.TransformPoint(_positions[n]), transform.TransformPoint(neighbour));
                //    }
                //}
            }

            if (Application.isPlaying && _showBendingConstraints && _bendingConstraints != null)
            {
                int j = _debugBendingId;
                _debugBendingId = math.clamp(_debugBendingId, -1, _bendingConstraints.Length - 1);
                if (_debugBendingId >= 0)
                //for (int j = 0; j < _bendingConstraints.Length; j++)
                {
                    int indices0 = _bendingConstraints[j].index0;
                    int indices1 = _bendingConstraints[j].index1;
                    int indices2 = _bendingConstraints[j].index2;
                    int indices3 = _bendingConstraints[j].index3;
                    Vector3 p0 = transform.TransformPoint(_positions[indices0]);
                    Vector3 p1 = transform.TransformPoint(_positions[indices1]);
                    Vector3 p2 = transform.TransformPoint(_positions[indices2]);
                    Vector3 p3 = transform.TransformPoint(_positions[indices3]);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(p0, p2);
                    Gizmos.DrawLine(p2, p1);
                    Gizmos.DrawLine(p1, p3);
                    Gizmos.DrawLine(p3, p0);
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(p2, p3);

                }
            }

            if (Application.isPlaying && _useCollisionFinder && _collisionFinder != null) _collisionFinder.OnDrawGizmos(this.transform);
            _getDepthPos?.OnDrawGizmos();

            //if (_objBuffers != null && _objBuffers[0].colorsBuffer != null)
            //{
            //    Color[] colors = new Color[]{
            //        new Color(1,0,0),
            //        new Color(0,1,0),
            //        new Color(0,0,1),
            //        new Color(1,1,0),
            //        new Color(0,1,1),
            //        new Color(1,0,1),
            //        new Color(1,0.5f,0),
            //        new Color(0.5f,1,0),
            //        new Color(0.5f,0,1),
            //        new Color(1,1,0.5f),
            //        new Color(0.5f,1,1),
            //        new Color(1,0.5f,1)
            //    };
            //    int[] cData = new int[_objBuffers[0].colorsBuffer.count];
            //    _objBuffers[0].colorsBuffer.GetData(cData);
            //    for (int n = 0; n < _mapIndices.Length; n++)
            //    {
            //        var indices = _mapIndices[n];
            //        int start = indices.x;
            //        int end = indices.y;
            //        Gizmos.color = colors[n];
            //        for (int i = start; i < end; i++)
            //        {
            //            int k = _colorKeyList[i];
            //            Gizmos.DrawCube(this.transform.TransformPoint(_positions[k]), Vector3.one * 0.005f);
            //        } 
            //    }
            //}
        }

        private void Destroy()
        {
            _projectedPositionsBuffer.ClearBuffer();
            _velocitiesBuffer.ClearBuffer();
            _frictionsBuffer.ClearBuffer();
            _deltaPositionsUIntBuffer.ClearBuffer();
            _deltaPositionsUIntBuffer2.ClearBuffer();
            _deltaCounterBuffer.ClearBuffer();
            _distanceConstraintsBuffer.ClearBuffer();
            _bendingConstraintsBuffer.ClearBuffer();
            _pointConstraintsBuffer.ClearBuffer();
            _pointConstraintsVecBuffer.ClearBuffer();
            _collidableSpheresBuffer.ClearBuffer();
            _collidableSDFsBuffer.ClearBuffer();
            //_connectionInfoBuffer.ClearBuffer();
            //_connectedVertsBuffer.ClearBuffer();
            _bonesStartMatrixBuffer.ClearBuffer();
            _vertexBlendsBuffer.ClearBuffer();
            _connectionInfoTetBuffer.ClearBuffer();
            _connectedVertsTetBuffer.ClearBuffer();
            _startVertexBuffer.ClearBuffer();
            _duplicateVerticesBuffer.ClearBuffer();
            //if (_gridCenterBuffer != null) _gridCenterBuffer.Release();

            if (_objBuffers != null)
            {
                for (int i = 0; i < _objBuffers.Length; i++)
                {
                    _objBuffers[i].positionsBuffer.ClearBuffer();
                    _objBuffers[i].normalsBuffer.ClearBuffer();
                    _objBuffers[i].connectionInfoBuffer.ClearBuffer();
                    _objBuffers[i].connectedVertsBuffer.ClearBuffer();
                }
            }
            _pointPointContactBuffer.ClearBuffer();
            _pointPointContactBuffer2.ClearBuffer();
            _pointPointContactBuffer3.ClearBuffer();
            _countContactBuffer.ClearBuffer();
            _countContactBuffer2.ClearBuffer();
            _countContactBuffer3.ClearBuffer();
            _baseVerticesBuffer.ClearBuffer();
            _centerMassBuffer.ClearBuffer();

            _collisionFinder?.DestroyBuffers();
            _getDepthPos?.OnDestroy();

            _mesh = null;
        }
    }


}