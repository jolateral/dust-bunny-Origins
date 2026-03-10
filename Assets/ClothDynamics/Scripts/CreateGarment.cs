using DelaunatorSharp;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Unity.Collections;
using Unity.Mathematics;
using andywiecko.BurstTriangulator;
using MeshTools;
//using HullDelaunayVoronoi.Delaunay;
//using HullDelaunayVoronoi.Primitives;
using RuntimeHandle;

namespace ClothDynamics
{
#if UNITY_EDITOR
    [CanEditMultipleObjects]
    [ExecuteInEditMode]
#endif
    public class CreateGarment : MonoBehaviour
    {
        [Tooltip("The spline that is needed to create one part of the garment mesh. It needs to be closed.")]
        [SerializeField] private BezierSpline _spline;
        [Tooltip("The distance between the triangle edges.")]
        [SerializeField] internal float _generationMinDistance = 10.0f;
        [Tooltip("If you want to use bezier shaped splines, however linear splines are recommended.")]
        [SerializeField] private bool _useBezierCurve = false;
        [Tooltip("This weights the positions of the points on the bezier curved edges.")]
        [SerializeField] private AnimationCurve _blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("This flips the normal. (See blue debug line in editor view.)")]
        [SerializeField] public bool _flipNormal = false;
        [Tooltip("This allows you to connect the same edge again with another one.")]
        [SerializeField] private bool _allowDoubles = false;
        [Tooltip("This removes triangles that are connected with extreme angles between their edges.")]
        [SerializeField] private bool _removeTrisByAngle = false;

        [HideInInspector] public bool _updateData = false;

        private float _scale = 0.01f;

        private float _internalScale = 1;
        private List<Vector2> _points = new List<Vector2>();
        private float _lastSplineLength = -1;
        private Vector3[] _newVerts;
        private int[] _newTris;
        private Vector2[] _egdePointsOnly;
        private int s_ButtonHash = "LineHandle".GetHashCode();
        private float _thresholdAngle = 135;
        private bool _moving = false;
        private Vector3 _lastPos = Vector3.zero;
        internal List<List<Vector2>> _pointsPerEdge = new List<List<Vector2>>();

        [System.Serializable]
        public class ConnectedEdges
        {
            //public List<Vector2> points;
            public BezierPoint p1;
            public BezierPoint p2;
            public ConnectedEdges(List<BezierPoint> points)
            {
                if (points != null)
                {
                    this.p1 = points[0];
                    this.p2 = points[1];
                }
            }
        }
        [Tooltip("This is the list of the connected edges. Each point has a corresponding edge.")]
        public List<ConnectedEdges> connectedEdgesList = new List<ConnectedEdges>();

        public List<Transform> _cutouts = new List<Transform>();

        public float _subDivs = 1;

        public (float2[], int[]) TriangulationWithHolesWithRefinement(Vector2[] _edgePoints, Vector2[] _holePoints)
        {
            var managedPositions = new float2[_edgePoints.Length + _holePoints.Length];
            var constraints = new int[managedPositions.Length * 2];

            for (int i = 0; i < _edgePoints.Length; i++)
            {
                managedPositions[i] = new float2(_edgePoints[i].x, _edgePoints[i].y) * 100;
                constraints[i * 2] = i;
                constraints[i * 2 + 1] = (i + 1) % _edgePoints.Length;
            }

            float2 center = 0;
            for (int n = 0; n < _holePoints.Length; n++)
            {
                int i = _edgePoints.Length + n;
                managedPositions[i] = new float2(_holePoints[n].x, _holePoints[n].y) * 100;
                center += managedPositions[i];
                constraints[i * 2] = i;
                constraints[i * 2 + 1] = _edgePoints.Length + ((n + 1) % _holePoints.Length);
            }
            center /= (float)math.max(1, _holePoints.Length);

            using var positions = new NativeArray<float2>(managedPositions, Allocator.Persistent);
            using var constraintEdges = new NativeArray<int>(constraints, Allocator.Persistent);
            using var holes = new NativeArray<float2>(new[] { center }, Allocator.Persistent);
            using var triangulator = new Triangulator(Allocator.Persistent)
            {
                Settings =
                {
                    ConstrainEdges = true,
                    RefineMesh = true,
                    RestoreBoundary = true,
                    MinimumArea = _generationMinDistance,
                    MaximumArea = _generationMinDistance * 1.5f,//TODO
                }
            };
            triangulator.Input.Positions = positions;
            triangulator.Input.ConstraintEdges = constraintEdges;
            if (_holePoints.Length > 0) triangulator.Input.HoleSeeds = holes;

            triangulator.Run();

            var actualTriangles = triangulator.Output.Triangles.ToArray();
            var actualVerts = triangulator.Output.Positions.ToArray();

            return (actualVerts, actualTriangles);
        }



        private void OnEnable()
        {
            if (_spline == null) _spline = this.GetComponent<BezierSpline>();
#if UNITY_EDITOR
            SceneView.duringSceneGui += DrawEdges;
#endif
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawEdges;
        }

        public static void HandleClickSelection(GameObject gameObject, Event evt)
        {
            if (evt.shift || EditorGUI.actionKey)
            {
                UnityEngine.Object[] existingSelection = Selection.objects;

                // For shift, we check if EXACTLY the active GO is hovered by mouse and then subtract. Otherwise additive.
                // For control/cmd, we check if ANY of the selected GO is hovered by mouse and then subtract. Otherwise additive.
                // Control/cmd takes priority over shift.
                bool subtractFromSelection = EditorGUI.actionKey ? Selection.Contains(gameObject) : Selection.activeGameObject == gameObject;
                if (subtractFromSelection)
                {
                    // subtract from selection
                    var newSelection = new UnityEngine.Object[existingSelection.Length - 1];

                    int index = System.Array.IndexOf(existingSelection, gameObject);

                    System.Array.Copy(existingSelection, newSelection, index);
                    System.Array.Copy(existingSelection, index + 1, newSelection, index, newSelection.Length - index);

                    Selection.objects = newSelection;
                }
                else
                {
                    // add to selection
                    var newSelection = new UnityEngine.Object[existingSelection.Length + 1];
                    System.Array.Copy(existingSelection, newSelection, existingSelection.Length);
                    newSelection[existingSelection.Length] = gameObject;

                    Selection.objects = newSelection;
                }
            }
            else
                Selection.activeObject = gameObject;


        }

        private void DoLineRender(BezierPoint p1, BezierPoint p2, Color color, float size)
        {
            Vector3 start = p1.position;
            Vector3 end = p2 != null ? p2.position : start;

            GameObject boneGO = p1.gameObject;

            float length = (end - start).magnitude;
            bool tipBone = (length < float.Epsilon);

            int id = GUIUtility.GetControlID(s_ButtonHash, FocusType.Passive);
            Event evt = Event.current;

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                    {
                        HandleUtility.AddControl(id, tipBone ? HandleUtility.DistanceToCircle(start, 1 * size * 0.5f) : HandleUtility.DistanceToLine(start, end));
                        break;
                    }
                case EventType.MouseMove:
                    if (id == HandleUtility.nearestControl)
                        HandleUtility.Repaint();
                    break;
                case EventType.MouseDown:
                    {
                        if (HandleUtility.nearestControl == id && evt.button == 0)
                        {
                            if (!SceneVisibilityManager.instance.IsPickingDisabled(boneGO, false))
                            {
                                GUIUtility.hotControl = id; // Grab mouse focus
                                HandleClickSelection(boneGO, evt);
                                evt.Use();
                            }
                        }
                        break;
                    }
                case EventType.MouseDrag:
                    {
                        if (!evt.alt && GUIUtility.hotControl == id)
                        {
                            if (!SceneVisibilityManager.instance.IsPickingDisabled(boneGO, false))
                            {
                                DragAndDrop.PrepareStartDrag();
                                DragAndDrop.objectReferences = new UnityEngine.Object[] { p1.transform };
                                DragAndDrop.StartDrag(ObjectNames.GetDragAndDropTitle(p1.transform));

                                GUIUtility.hotControl = 0;

                                evt.Use();
                            }
                        }
                        break;
                    }
                case EventType.MouseUp:
                    {
                        if (GUIUtility.hotControl == id && (evt.button == 0 || evt.button == 2))
                        {
                            GUIUtility.hotControl = 0;
                            evt.Use();
                        }
                        if (_moving) _moving = false;
                        break;
                    }
                case EventType.Repaint:
                    {
                        Color highlight = color;

                        bool hoveringBone = GUIUtility.hotControl == 0 && HandleUtility.nearestControl == id;
                        hoveringBone = hoveringBone && !SceneVisibilityManager.instance.IsPickingDisabled(p1.gameObject, false);

                        if (hoveringBone)
                        {
                            highlight = Handles.preselectionColor;
                        }
                        else if (Selection.Contains(boneGO) || Selection.activeObject == boneGO)
                        {
                            highlight = Handles.selectedColor;
                        }

                        Handles.color = highlight;
                        //Handles.DrawLine(start, end);

                        var startPoint = p1.position;
                        var endPoint = p2.position;
                        var startTangent = p1.followingControlPointPosition;
                        var endTangent = p2.precedingControlPointPosition;
                        Handles.DrawBezier(startPoint, endPoint, startTangent, endTangent, highlight, null, 2f);
                    }
                    break;
            }
        }

        private void DrawEdges(SceneView sceneview)
        {
            if (_spline)
            {
                var points = _spline.points.ToArray();
                for (int i = 0; i < points.Length; i++)
                {
                    DoLineRender(points[i], (i + 1) < points.Length ? points[i + 1] : points[0], Color.red, 0.1f);
                }
            }
        }
#endif
        private void Update()
        {
            if (_lastPos != this.transform.position)
            {
                _lastPos = this.transform.position;
                _moving = true;
            }
            //else_moving = false;
            if (Application.isPlaying)
            {
                if (_spline)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        RaycastHit raycastHit;
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out raycastHit, 100f))
                        {
                            if (raycastHit.transform != null)
                            {
                                CurrentClickedGameObject(raycastHit.transform.gameObject);
                            }
                        }
                    }

                    var points = _spline.points.ToArray();
                    var center = Vector3.zero;
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (_lines.Count < points.Length)
                        {
                            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            line.GetComponent<Renderer>().material.color = Color.white;
                            _lines.Add(line);
                        }
                        else if (_lines.Count > points.Length)
                        {
                            Destroy(_lines[_lines.Count - 1]);
                            _lines.RemoveAt(_lines.Count - 1);
                        }

                        var p1 = points[i];
                        var p2 = (i + 1) < points.Length ? points[i + 1] : points[0];
                        _lines[i].transform.parent = p1.transform;
                        _lines[i].transform.position = (p1.position + p2.position) * 0.5f;
                        _lines[i].transform.localScale = new Vector3(_scaleRadius * 2.0f, _scaleRadius * 2.0f, (p1.position - p2.position).magnitude) * 5; //TODO something wrong with the scaling
                        _lines[i].transform.forward = (p1.position - p2.position).normalized;

                        center += p1.position;
                    }

                    if (_centerObj == null)
                    {
                        center /= (float)math.max(1, points.Length);
                        _centerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        _centerObj.name = "Center_" + this.name;
                        _centerObj.transform.position = center;
                        _centerObjOffset = _centerObj.transform.position - _spline.transform.position;
                        _centerObj.transform.localScale = Vector3.one;
                        var handle = _centerObj.GetComponent<RuntimeTransformHandle>();
                        if (handle == null) handle = _centerObj.AddComponent<RuntimeTransformHandle>();
                        handle.autoScale = true;
                    }
                    //handle.target = _spline.transform;
                    var pos = _spline.transform.position;
                    pos = _centerObj.transform.position - _centerObjOffset;
                    //pos.z = _spline.transform.position.z;
                    _spline.transform.position = pos;
                }
            }
        }


        public GameObject _centerObj = null;
        Vector3 _centerObjOffset;
        List<GameObject> _lines = new List<GameObject>();
        public static List<GameObject> _selected = new List<GameObject>();

        public static BezierSpline _firstSelectedSpline;

        public void CurrentClickedGameObject(GameObject gameObject)
        {
            if (_lines.Contains(gameObject))
            {
                Debug.Log("Clicked on " + gameObject.name);
                if (_selected.Count == 2 && _selected[0].GetComponent<Renderer>())
                {
                    _selected[0].GetComponent<Renderer>().material.color = Color.white;
                    _selected.RemoveAt(0);
                }
                _selected.Add(gameObject);
                if (gameObject.GetComponent<Renderer>()) gameObject.GetComponent<Renderer>().material.color = Color.yellow;
            }
        }


        private Material lineMaterial;

        public List<Camera> _cameras;

        private void OnRenderObject()
        {
            //Debug.Log("OnRenderObject " + this.name);
            if (_cameras != null && _cameras.Contains(Camera.current) && _spline && _generationMinDistance > 0)
            {
                var cam = Camera.current;
                if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));

                var saveRot = this.transform.rotation;
                var pos = this.transform.position;

                CreateVertsAndTris(out int[] newTris, out Vector3[] newVerts, _updateData, !_flipNormal);

                if (newVerts != null)
                {
                    _newTris = newTris;
                    _newVerts = newVerts;
                }

                GL.PushMatrix();

                GL.LoadIdentity();
                GL.MultMatrix(cam.worldToCameraMatrix);
                GL.LoadProjectionMatrix(cam.projectionMatrix);

                lineMaterial.SetPass(0);
                GL.Begin(GL.LINES);
                GL.Color(Color.red);

                if (_newVerts != null)
                {
                    int newLength = _newTris.Length / 3;
                    for (int i = 0; i < newLength; i++)
                    {
                        var i1 = _newTris[i * 3];
                        var i2 = _newTris[i * 3 + 1];
                        var i3 = _newTris[i * 3 + 2];
                        var p1 = saveRot * (_newVerts[i1] - pos) / _internalScale + pos;
                        var p2 = saveRot * (_newVerts[i2] - pos) / _internalScale + pos;
                        var p3 = saveRot * (_newVerts[i3] - pos) / _internalScale + pos;

                        GL.Vertex3(p1.x, p1.y, p1.z);
                        GL.Vertex3(p2.x, p2.y, p2.z);

                        GL.Vertex3(p2.x, p2.y, p2.z);
                        GL.Vertex3(p3.x, p3.y, p3.z);

                        GL.Vertex3(p3.x, p3.y, p3.z);
                        GL.Vertex3(p1.x, p1.y, p1.z);
                    }
                }

                GL.Color(Color.green);

                foreach (var e in connectedEdgesList)
                {
                    int lengthP1 = CreateVertsAndTrisFromEdgesList(e, out int[] tris, out Vector3[] verts);
                    if (verts != null)
                    {
                        var pos1 = e.p1.Internal_Spline.transform.position;
                        var pos2 = e.p2.Internal_Spline.transform.position;
                        var rot1 = e.p1.Internal_Spline.transform == this.transform ? saveRot : e.p1.Internal_Spline.transform.rotation;
                        var rot2 = e.p2.Internal_Spline.transform == this.transform ? saveRot : e.p2.Internal_Spline.transform.rotation;
                        var p = Vector3.zero;
                        var r = Quaternion.identity;
                        int newLength = tris.Length / 3;
                        for (int i = 0; i < newLength; i++)
                        {
                            var i1 = tris[i * 3];
                            var i2 = tris[i * 3 + 1];
                            var i3 = tris[i * 3 + 2];

                            if (i1 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                            var p1 = r * (verts[i1] - p) / _internalScale + p;
                            if (i2 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                            var p2 = r * (verts[i2] - p) / _internalScale + p;
                            if (i3 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                            var p3 = r * (verts[i3] - p) / _internalScale + p;

                            GL.Vertex3(p1.x, p1.y, p1.z);
                            GL.Vertex3(p2.x, p2.y, p2.z);

                            GL.Vertex3(p2.x, p2.y, p2.z);
                            GL.Vertex3(p3.x, p3.y, p3.z);

                            GL.Vertex3(p3.x, p3.y, p3.z);
                            GL.Vertex3(p1.x, p1.y, p1.z);
                        }
                    }
                }

                GL.End();
                GL.PopMatrix();
                _updateData = false;
            }
        }

        private void OnDrawGizmos()
        {

            if (_spline && _generationMinDistance > 0 && !_moving)
            {
                var points = _spline.points;
                var vecZero = new Vector3(1, 1, 0);
                var centerPos = Vector3.zero;
                int count = points.Count;
                for (int i = 0; i < count; i++)
                {
                    centerPos += points[i].transform.localPosition = Vector3.Scale(points[i].transform.localPosition, vecZero);
                }
                if (count > 0)
                {
                    centerPos /= count;
                    centerPos = this.transform.TransformPoint(centerPos);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(centerPos, centerPos + (_flipNormal ? -1 : 1) * this.transform.forward * (points[0].transform.position - centerPos).magnitude * 0.5f);
                }
                var saveRot = this.transform.rotation;
                //this.transform.rotation = Quaternion.identity;

                //var tempScale = this.transform.localScale;
                //this.transform.localScale *= _internalScale;
                //var tempdist = _generationMinDistance;
                //_generationMinDistance *= _internalScale;
                var pos = this.transform.position;

                CreateVertsAndTris(out int[] newTris, out Vector3[] newVerts, _updateData, !_flipNormal);

                if (newVerts != null)
                {
                    _newTris = newTris;
                    _newVerts = newVerts;
                }

                count = _points.Count;
                for (int k = 0; k < count; k++)
                {
                    var p = _points[k];
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(p, _scaleRadius);
                }

                if (_newVerts != null)
                {
                    //foreach (var p in _newVerts)
                    //{
                    //    Gizmos.DrawSphere(p, _debugRadius);
                    //}
                    Gizmos.color = Color.white;
                    int newLength = _newTris.Length / 3;
                    for (int i = 0; i < newLength; i++)
                    {
                        var i1 = _newTris[i * 3];
                        var i2 = _newTris[i * 3 + 1];
                        var i3 = _newTris[i * 3 + 2];
                        var p1 = saveRot * (_newVerts[i1] - pos) / _internalScale + pos;
                        var p2 = saveRot * (_newVerts[i2] - pos) / _internalScale + pos;
                        var p3 = saveRot * (_newVerts[i3] - pos) / _internalScale + pos;
                        //var center = (p1 + p2 + p3) * 0.333333f;
                        //if (InsideCheck.Run(center, _egdePointsOnly))
                        //{
                        //    Gizmos.color = Color.green;
                        //}else Gizmos.color = Color.red;

                        //Gizmos.DrawSphere(center, _debugRadius);

                        Gizmos.DrawLine(p1, p2);
                        Gizmos.DrawLine(p2, p3);
                        Gizmos.DrawLine(p3, p1);
                    }
                }

                Gizmos.color = Color.green;


                //if (_pointsPerEdge != null && _pointsPerEdge.Count == 0 || (connectedEdgesList.Count > 0 && connectedEdgesList[0].p2.Internal_Spline?.GetComponent<CreateGarment>()?._pointsPerEdge == null))
                //{
                //    CreateVertsAndTris(out _, out _, true);
                //    BezierSpline otherSpline = null;
                //    if (connectedEdgesList.Count > 0 && connectedEdgesList[0].p2.Internal_Spline != connectedEdgesList[0].p1.Internal_Spline)
                //    {
                //        otherSpline = connectedEdgesList[0].p2.Internal_Spline;
                //        if (connectedEdgesList[0].p2.Internal_Spline == _spline) otherSpline = connectedEdgesList[0].p1.Internal_Spline;
                //        otherSpline?.GetComponent<CreateGarment>()?.CreateVertsAndTris(out _, out _, true);
                //    }
                //}
                foreach (var e in connectedEdgesList)
                {
                    int lengthP1 = CreateVertsAndTrisFromEdgesList(e, out int[] tris, out Vector3[] verts);
                    if (verts != null)
                    {
                        var pos1 = e.p1.Internal_Spline.transform.position;
                        var pos2 = e.p2.Internal_Spline.transform.position;
                        var rot1 = e.p1.Internal_Spline.transform == this.transform ? saveRot : e.p1.Internal_Spline.transform.rotation;
                        var rot2 = e.p2.Internal_Spline.transform == this.transform ? saveRot : e.p2.Internal_Spline.transform.rotation;
                        var p = Vector3.zero;
                        var r = Quaternion.identity;
                        int newLength = tris.Length / 3;
                        for (int i = 0; i < newLength; i++)
                        {
                            var i1 = tris[i * 3];
                            var i2 = tris[i * 3 + 1];
                            var i3 = tris[i * 3 + 2];

                            if (i1 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                            var p1 = r * (verts[i1] - p) / _internalScale + p;
                            if (i2 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                            var p2 = r * (verts[i2] - p) / _internalScale + p;
                            if (i3 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                            var p3 = r * (verts[i3] - p) / _internalScale + p;

                            //var pA = verts[i1];
                            //var pB = verts[i2];
                            //var pC = verts[i3];

                            //var center = (pA + pB + pC) * 0.333333f;
                            //if (!InsideCheck.Run(center, _egdePointsOnly))
                            //{
                            Gizmos.DrawLine(p1, p2);
                            Gizmos.DrawLine(p2, p3);
                            Gizmos.DrawLine(p3, p1);
                            //}
                        }
                    }
                }
                //this.transform.localScale = tempScale;
                //_generationMinDistance = tempdist;
                //this.transform.rotation = saveRot;
                _updateData = false;
            }
        }

        private int CreateVertsAndTrisFromEdgesList(ConnectedEdges e, out int[] tris, out Vector3[] verts)
        {
            int lengthP1 = 0;
            tris = null;
            verts = null;
            var bp1 = e.p1;
            var bp2 = e.p2;
            if (bp1 != null && bp2 != null)
            {
                //var tScale1 = bp1.Internal_Spline.transform.localScale;
                //bp1.Internal_Spline.transform.localScale *= _internalScale;
                //var tDist1 = bp1.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance;
                //bp1.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance *= _internalScale;

                //var tScale2 = bp2.Internal_Spline.transform.localScale;
                //bp2.Internal_Spline.transform.localScale *= _internalScale;
                //var tDist2 = bp2.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance;
                //bp2.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance *= _internalScale;

                var ppe1 = bp1.Internal_Spline?.GetComponent<CreateGarment>()?._pointsPerEdge;
                if (ppe1 == null) ppe1 = bp1.Internal_Spline?.GetComponent<CreateGarment>()?.CreateVertsAndTris(out _, out _, true);
                var ppe2 = bp2.Internal_Spline?.GetComponent<CreateGarment>()?._pointsPerEdge;
                if (ppe2 == null) ppe2 = bp2.Internal_Spline?.GetComponent<CreateGarment>()?.CreateVertsAndTris(out _, out _, true);

                //bp1.Internal_Spline.transform.transform.localScale = tScale1;
                //bp1.Internal_Spline.transform.GetComponent<CreateGarment>()._generationMinDistance = tDist1;

                //bp2.Internal_Spline.transform.transform.localScale = tScale2;
                //bp2.Internal_Spline.transform.GetComponent<CreateGarment>()._generationMinDistance = tDist2;

                if (ppe1 != null && ppe1.Count >= bp1.Internal_Spline.Count && ppe2 != null && ppe2.Count >= bp2.Internal_Spline.Count)
                {
                    //for (int i = 0; i < ppe1[bp1.Internal_Index].Count; i++)
                    //{
                    //    Vector3 vec = ppe1[bp1.Internal_Index][i];
                    //    vec.z = bp1.Internal_Spline.transform.position.z;
                    //    Gizmos.DrawSphere(vec, 0.01f);
                    //}

                    List<Vector2> edgePoints = new List<Vector2>();
                    List<Vector2> edgePointsOrigin = new List<Vector2>();

                    edgePoints.AddRange(ppe1[bp1.Internal_Index]);
                    edgePoints.Add(ppe1[bp1.Internal_Index < bp1.Internal_Spline.Count - 1 ? bp1.Internal_Index + 1 : 0][0]);
                    edgePointsOrigin.AddRange(edgePoints);
                    lengthP1 = edgePoints.Count;
                    for (int i = 0; i < lengthP1; ++i)
                    {
                        edgePoints[i] = edgePoints[i] + Vector2.one * _generationMinDistance * 0.001f;
                    }
                    edgePoints.AddRange(ppe2[bp2.Internal_Index]);
                    edgePoints.Add(ppe2[bp2.Internal_Index < bp2.Internal_Spline.Count - 1 ? bp2.Internal_Index + 1 : 0][0]);
                    edgePointsOrigin.AddRange(ppe2[bp2.Internal_Index]);
                    edgePointsOrigin.Add(ppe2[bp2.Internal_Index < bp2.Internal_Spline.Count - 1 ? bp2.Internal_Index + 1 : 0][0]);

                    var delaunatorEdges = new Delaunator(edgePoints.ToPoints().ToArray());
                    tris = delaunatorEdges.Triangles;

                    List<int> newTrisList = new List<int>();
                    for (int i = 0; i < tris.Length - 1; i += 3)
                    {
                        int t0 = tris[i + 0];
                        int t1 = tris[i + 1];
                        int t2 = tris[i + 2];
                        int found = 0;
                        if (t0 < lengthP1) found++;
                        if (t1 < lengthP1) found++;
                        if (t2 < lengthP1) found++;

                        if (found > 0 && found < 3)
                        {
                            newTrisList.Add(t0);
                            newTrisList.Add(t1);
                            newTrisList.Add(t2);
                        }
                    }
                    tris = newTrisList.ToArray();

                    verts = edgePointsOrigin.Select(point => new Vector3(point.x, point.y)).ToArray();
                    for (int i = 0; i < verts.Length; ++i)
                    {
                        verts[i].z = i < lengthP1 ? bp1.Internal_Spline.transform.position.z : bp2.Internal_Spline.transform.position.z;
                    }
                }
            }
            return lengthP1;
        }

        private bool IsPointOnLine(Vector2 lineStart, Vector2 lineEnd, Vector2 pointToCheck, float threshold = 0.01f)
        {
            Vector2 lineDirection = (lineEnd - lineStart).normalized;
            float distanceToPoint = Vector2.Dot((pointToCheck - lineStart), lineDirection);
            Vector2 pointOnLine = lineStart + (lineDirection * distanceToPoint);

            float distanceToLine = Vector2.Distance(pointToCheck, pointOnLine);

            float d1 = math.dot(lineDirection, (lineEnd - pointToCheck).normalized);
            float d2 = math.dot(lineDirection, (lineStart - pointToCheck).normalized);

            return distanceToLine < threshold && d1 > 0 && d2 < 0;
        }

        internal List<List<Vector2>> CreateVertsAndTris(out int[] newTris, out Vector3[] newVerts, bool force = false, bool flip = false, int vertexCount = 0)
        {
            newTris = null;
            newVerts = null;

            float totalLength = _spline.Length;
            if (_cutouts != null)
            {
                for (int i = 0; i < _cutouts.Count; i++)
                {
                    var parent = _cutouts[i];
                    var cutSpline = parent.GetComponent<BezierSpline>();
                    if (cutSpline != null) totalLength += cutSpline.Length;
                }
            }
            if (totalLength != _lastSplineLength || force)
            {
                _lastSplineLength = totalLength;

                _points.Clear();
                _pointsPerEdge.Clear();
                int count = _spline.points.Count;
                float minDist = _generationMinDistance / _subDivs;

                if (!_useBezierCurve)
                {
                    for (int n = 0; n < count; n++)
                    {
                        _pointsPerEdge.Add(new List<Vector2>());
                        _points.Add(_spline.points[n].position);
                        _pointsPerEdge[n].Add(_spline.points[n].position);
                        _pointsPerEdge[n].Add(_spline.points[(n + 1) % count].position);
                    }
                }
                else
                {
                    for (int n = 0; n < count; n++)
                    {
                        _pointsPerEdge.Add(new List<Vector2>());

                        float normPos1 = n / (float)count;
                        float normPos2 = (n + 1) / (float)count;
                        float sLength = _spline.GetLengthApproximately(normPos1, normPos2);

                        int stepLength = Mathf.CeilToInt(sLength / minDist);
                        Vector3 p;
                        for (int i = 0; i < stepLength; i++)
                        {
                            p = _spline.GetPoint(normPos1 + _blendCurve.Evaluate(((i * minDist) / sLength)) * (normPos2 - normPos1));
                            _points.Add(p);
                            _pointsPerEdge[n].Add(p);
                        }
                    }
                }
                _egdePointsOnly = _points.ToArray();

                List<Vector2> _holes = new List<Vector2>();
                if (_cutouts != null)
                {
                    for (int c = 0; c < _cutouts.Count; c++)
                    {
                        var parent = _cutouts[c];
                        var cutSpline = parent.GetComponent<BezierSpline>();

                        if (!_useBezierCurve)
                        {
                            for (int n = 0; n < count; n++)
                            {
                                //_pointsPerEdge.Add(new List<Vector2>());
                                _points.Add(cutSpline.points[n].position);
                                _holes.Add(cutSpline.points[n].position);
                                //_pointsPerEdge[n].Add(cutSpline.points[n].position);
                                //_pointsPerEdge[n].Add(cutSpline.points[(n + 1) % count].position);
                            }
                        }
                        else
                        {
                            for (int n = 0; n < count; n++)
                            {
                                //_pointsPerEdge.Add(new List<Vector2>());

                                float normPos1 = n / (float)count;
                                float normPos2 = (n + 1) / (float)count;
                                float sLength = cutSpline.GetLengthApproximately(normPos1, normPos2);

                                int stepLength = Mathf.CeilToInt(sLength / minDist);
                                Vector3 p;
                                for (int i = 0; i < stepLength; i++)
                                {
                                    p = cutSpline.GetPoint(normPos1 + _blendCurve.Evaluate(((i * minDist) / sLength)) * (normPos2 - normPos1));
                                    _points.Add(p);
                                    _holes.Add(p);
                                    //_pointsPerEdge[n].Add(p);
                                }
                            }
                        }

                    }
                }
                (var outputPositions, var outputTriangles) = TriangulationWithHolesWithRefinement(_egdePointsOnly, _holes.ToArray());

                var tris = outputTriangles;
                newVerts = outputPositions.Select(v => new Vector3(v.x, v.y) * _scale).ToArray();

                if (_removeTrisByAngle)
                {
                    List<int> newTriangles = new List<int>();
                    RemoveTrisByThresholdAngle(tris, newVerts, newTriangles, _thresholdAngle);
                    tris = newTriangles.ToArray();
                }

                var edgeList = EdgeHelpers.GetEdgesBoundary(tris).SortEdges();

                for (int i = 0; i < _pointsPerEdge.Count; i++)
                {
                    var edge = _pointsPerEdge[i];
                    var copy = edge.ToList();
                    for (int k = 0; k < edge.Count - 1; k++)
                    {
                        var p1 = edge[k];
                        var p2 = edge[k + 1];
                        for (int n = 0; n < edgeList.Count; n++)
                        {
                            var edge2 = edgeList[n];
                            var p3 = newVerts[edge2.v1];
                            var p4 = newVerts[edge2.v2];

                            bool isOnLine = IsPointOnLine(p1, p2, p3, _generationMinDistance * 0.001f);
                            if (isOnLine) copy.Insert(k + 1, p3);

                            isOnLine = IsPointOnLine(p1, p2, p4, _generationMinDistance * 0.001f);
                            if (isOnLine) copy.Insert(k + 2, p4);

                        }
                    }
                    //if (flip) copy.Reverse();
                    _pointsPerEdge[i] = copy;
                }


                for (int i = 0; i < newVerts.Length; ++i)
                {
                    newVerts[i].z = this.transform.position.z;
                }

                var newTrisList = new List<int>();

                int tLength = tris.Length / 3;
                for (int i = 0; i < tLength; i++)
                {
                    var i1 = tris[i * 3];
                    var i2 = tris[i * 3 + 1];
                    var i3 = tris[i * 3 + 2];
                    var p1 = newVerts[i1];
                    var p2 = newVerts[i2];
                    var p3 = newVerts[i3];
                    var center = (p1 + p2 + p3) * 0.333333f;
                    if (InsideCheck.Run(center, _egdePointsOnly))
                    {
                        if (flip)
                        {
                            newTrisList.Add(vertexCount + i3);
                            newTrisList.Add(vertexCount + i2);
                            newTrisList.Add(vertexCount + i1);
                        }
                        else
                        {
                            newTrisList.Add(vertexCount + i1);
                            newTrisList.Add(vertexCount + i2);
                            newTrisList.Add(vertexCount + i3);
                        }
                    }
                }
                newTris = newTrisList.ToArray();
            }
            return _pointsPerEdge;
        }

        public float _scaleRadius = 0.01f;

        bool CheckDoubles(ConnectedEdges edges)
        {
            int count = connectedEdgesList.Count;
            for (int i = 0; i < count; i++)
            {
                if (connectedEdgesList[i].p1 == edges.p1 || connectedEdgesList[i].p1 == edges.p2 || connectedEdgesList[i].p2 == edges.p1 || connectedEdgesList[i].p2 == edges.p2)
                    return false;
            }
            return true;
        }

        private static List<BezierPoint> ReadPointsFromEdges()
        {
#if UNITY_EDITOR
            var selected = Selection.objects;
            if (selected.Length == 2)
            {
                if (selected[0].GetType() == typeof(GameObject) && selected[1].GetType() == typeof(GameObject))
                {
                    var bp1 = ((GameObject)selected[0]).GetComponent<BezierPoint>();
                    var bp2 = ((GameObject)selected[1]).GetComponent<BezierPoint>();

                    if (bp1 != null && bp2 != null)
                    {
                        List<BezierPoint> edgePoints = new List<BezierPoint>();
                        edgePoints.Add(bp1);
                        edgePoints.Add(bp2);
                        return edgePoints;
                    }
                }
            }
#endif
            return null;
        }

        public void AddConnectedEdgeList()
        {
            ConnectedEdges edges = new ConnectedEdges(ReadPointsFromEdges());
            if (edges.p1 != null)
            {
                if (_allowDoubles || CheckDoubles(edges))
                    connectedEdgesList.Add(edges);
            }
        }

        private BezierSpline[] FindAllConnectedSplines()
        {
            var otherSplines = new List<BezierSpline>();
            otherSplines.Add(this._spline);
            foreach (var item in connectedEdgesList)
            {
                if (item.p1 != null && item.p1.Internal_Spline != null && !otherSplines.Contains(item.p1.Internal_Spline))
                    otherSplines.Add(item.p1.Internal_Spline);

                if (item.p2 != null && item.p2.Internal_Spline != null && !otherSplines.Contains(item.p2.Internal_Spline))
                    otherSplines.Add(item.p2.Internal_Spline);
            }
            otherSplines.Remove(this._spline);
            return otherSplines.ToArray();
        }

        public GameObject CreateMeshFromSplines()
        {
            if (_spline && _generationMinDistance > 0)
            {
                KDTree vertexTree = new KDTree(3);

                var saveRot = this.transform.rotation;
                this.transform.rotation = Quaternion.identity;

                var tempScale = this.transform.localScale;
                this.transform.localScale *= _internalScale;
                var tempdist = _generationMinDistance;
                _generationMinDistance *= _internalScale;
                Vector3 pos = this.transform.position;

                Vector3 tempScale2 = this.transform.localScale;
                float tempdist2 = _generationMinDistance;

                int[] newTris = null;
                Vector3[] newVerts = null;

                this.CreateVertsAndTris(out newTris, out newVerts, true, !_flipNormal);

                if (newVerts != null && newTris != null)
                {
                    var trisList = new List<int>(newTris);
                    var vertsList = new List<Vector3>(newVerts);
                    var colorList = new List<Color>();
                    int vertexCount = vertsList.Count;
                    pos = _spline.transform.position;
                    HashSet<Vector3> tempUniqueVerts = new HashSet<Vector3>();
                    for (int i = 0; i < vertexCount; i++)
                    {
                        vertsList[i] = saveRot * (vertsList[i] - pos) / _internalScale + pos;
                        colorList.Add(Color.clear);
                        if (tempUniqueVerts.Add(vertsList[i]))
                            vertexTree.insert(new double[] { vertsList[i].x, vertsList[i].y, vertsList[i].z }, i);
                    }
                    tempUniqueVerts.Clear();
                    if (connectedEdgesList.Count > 0)
                    {
                        var otherSplines = FindAllConnectedSplines();
                        foreach (var otherSpline in otherSplines)
                        {
                            if (otherSpline != null)
                            {
                                var saveRot2 = otherSpline.transform.rotation;
                                otherSpline.transform.rotation = Quaternion.identity;
                                tempScale2 = otherSpline.transform.localScale;
                                otherSpline.transform.localScale *= _internalScale;
                                tempdist2 = otherSpline.GetComponent<CreateGarment>()._generationMinDistance;
                                otherSpline.GetComponent<CreateGarment>()._generationMinDistance *= _internalScale;

                                int vCount = vertsList.Count;
                                otherSpline.GetComponent<CreateGarment>().CreateVertsAndTris(out newTris, out newVerts, true, !otherSpline.GetComponent<CreateGarment>()._flipNormal, vCount);

                                trisList.AddRange(newTris);
                                pos = otherSpline.transform.position;

                                for (int i = 0; i < newVerts.Length; i++)
                                {
                                    newVerts[i].z = pos.z;
                                    newVerts[i] = saveRot2 * (newVerts[i] - pos) / _internalScale + pos;
                                    colorList.Add(Color.clear);
                                    if (tempUniqueVerts.Add(newVerts[i]))
                                        vertexTree.insert(new double[] { newVerts[i].x, newVerts[i].y, newVerts[i].z }, vCount + i);
                                }
                                vertsList.AddRange(newVerts);
                                tempUniqueVerts.Clear();

                                otherSpline.transform.rotation = saveRot2;
                                otherSpline.transform.localScale = tempScale2;
                                otherSpline.GetComponent<CreateGarment>()._generationMinDistance = tempdist2;
                            }
                        }
                    }
                    vertexCount = vertsList.Count;
                    int frontVertexCount = vertexCount;
                    var trisArray = trisList.ToArray();
                    foreach (var e in connectedEdgesList)
                    {
                        int lengthP1 = CreateVertsAndTrisFromEdgesList(e, out int[] tris, out Vector3[] verts);
                        if (verts != null)
                        {
                            //var tScale1 = e.p1.Internal_Spline.transform.localScale;
                            //e.p1.Internal_Spline.transform.localScale *= _internalScale;
                            //var tDist1 = e.p1.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance;
                            //e.p1.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance *= _internalScale;

                            //var tScale2 = e.p2.Internal_Spline.transform.localScale;
                            //e.p2.Internal_Spline.transform.localScale *= _internalScale;
                            //var tDist2 = e.p2.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance;
                            //e.p2.Internal_Spline.GetComponent<CreateGarment>()._generationMinDistance *= _internalScale;

                            var normal1 = e.p1.Internal_Spline.transform.forward;
                            var normal2 = -e.p2.Internal_Spline.transform.forward;
                            var pos1 = e.p1.Internal_Spline.transform.position;
                            var pos2 = e.p2.Internal_Spline.transform.position;
                            var rot1 = e.p1.Internal_Spline.transform == this.transform ? saveRot : e.p1.Internal_Spline.transform.rotation;
                            var rot2 = e.p2.Internal_Spline.transform == this.transform ? saveRot : e.p2.Internal_Spline.transform.rotation;

                            var p = Vector3.zero;
                            var r = Quaternion.identity;

                            var zDiff = Vector3.Distance(pos1, pos2);

                            Vector3[] editVerts = new Vector3[verts.Length];
                            Color[] editColors = new Color[verts.Length];

                            int newLength = tris.Length / 3;
                            int tCount = trisList.Count / 3;
                            for (int i = 0; i < newLength; i++)
                            {
                                var i1 = tris[i * 3];
                                var i2 = tris[i * 3 + 1];
                                var i3 = tris[i * 3 + 2];
                                //var pA = verts[i1];
                                //var pB = verts[i2];
                                //var pC = verts[i3];

                                if (i1 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                                var p1 = r * (verts[i1] - p) / _internalScale + p;
                                if (i2 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                                var p2 = r * (verts[i2] - p) / _internalScale + p;
                                if (i3 < lengthP1) { p = pos1; r = rot1; } else { p = pos2; r = rot2; }
                                var p3 = r * (verts[i3] - p) / _internalScale + p;

                                editVerts[i1] = p1;
                                editVerts[i2] = p2;
                                editVerts[i3] = p3;
                                editColors[i1] = Color.green;
                                editColors[i2] = Color.green;
                                editColors[i3] = Color.green;

                                var middle = (p1 + p2 + p3) * 0.333333f;
                                var d1 = Mathf.Abs(Vector3.Dot(normal1, middle - pos1));
                                var d2 = Mathf.Abs(Vector3.Dot(normal2, middle - pos2));
                                if (d1 < d2 && d1 < zDiff * 0.01f) continue;
                                else if (d2 < zDiff * 0.01f) continue;

                                Vector3 normal = (Vector3.Cross(p2 - p1, p3 - p1)).normalized;
                                //pos = middle + normal * _generationMinDistance * 0.1f;

                                int nearest = (int)vertexTree.nearest(new double[] { middle.x, middle.y, middle.z });
                                int tIndex = (int)(Array.IndexOf(trisArray, nearest) / 3) * 3;
                                var tMiddle = (vertsList[trisList[tIndex]] + vertsList[trisList[tIndex + 1]] + vertsList[trisList[tIndex + 2]]) * 0.333333f;
                                var dist = Vector3.Dot(normal, middle - tMiddle);

                                //if (InsideCheck.Run(pos, _egdePointsOnly))
                                if (dist < 0)
                                {
                                    var temp = i1;
                                    i1 = i3;
                                    i3 = temp;
                                }
                                trisList.Add(vertexCount + i1);
                                trisList.Add(vertexCount + i2);
                                trisList.Add(vertexCount + i3);
                            }
                            vertsList.AddRange(editVerts);

                            int newTCount = trisList.Count / 3;
                            Vector3 averageNormal = Vector3.zero;
                            for (int i = tCount; i < newTCount; i++)
                            {
                                var a = vertsList[trisList[i * 3]];
                                var b = vertsList[trisList[i * 3 + 1]];
                                var c = vertsList[trisList[i * 3 + 2]];
                                averageNormal += (Vector3.Cross(b - a, c - a)).normalized;
                            }
                            averageNormal.Normalize();

                            for (int i = tCount; i < newTCount; i++)
                            {
                                var i1 = trisList[i * 3];
                                var i2 = trisList[i * 3 + 1];
                                var i3 = trisList[i * 3 + 2];
                                var a = vertsList[i1];
                                var b = vertsList[i2];
                                var c = vertsList[i3];
                                Vector3 n = (Vector3.Cross(b - a, c - a)).normalized;
                                if (Vector3.Dot(n, averageNormal) < 0.5f)
                                {
                                    var temp = i1;
                                    i1 = i3;
                                    i3 = temp;
                                    trisList[i * 3] = i1;
                                    trisList[i * 3 + 1] = i2;
                                    trisList[i * 3 + 2] = i3;
                                }
                            }
                            colorList.AddRange(editColors);

                            vertexCount += verts.Length;

                            //e.p1.Internal_Spline.transform.transform.localScale = tScale1;
                            //e.p1.Internal_Spline.transform.GetComponent<CreateGarment>()._generationMinDistance = tDist1;
                            //e.p2.Internal_Spline.transform.transform.localScale = tScale2;
                            //e.p2.Internal_Spline.transform.GetComponent<CreateGarment>()._generationMinDistance = tDist2;

                        }
                    }

                    //if (connectedEdgesList.Count > 0 && otherSpline != null)
                    //{
                    //    otherSpline.transform.localScale = tempScale2;
                    //    otherSpline.GetComponent<CreateGarment>()._generationMinDistance = tempdist2;
                    //}

                    Vector3 center = Vector3.zero;
                    int length = vertsList.Count;
                    for (int i = 0; i < length; i++)
                    {
                        center += vertsList[i];
                    }
                    center /= Mathf.Max(1.0f, (float)length);

                    KDTree vTree = new KDTree(3);
                    HashSet<Vector3> tempVerts = new HashSet<Vector3>();
                    for (int i = 0; i < length; i++)
                    {
                        vertsList[i] -= center;
                        if (i >= frontVertexCount)
                        {
                            if (tempVerts.Add(vertsList[i]))
                                vTree.insert(new double[] { vertsList[i].x, vertsList[i].y, vertsList[i].z }, i);
                        }
                    }
                    tempVerts.Clear();
                    Mesh mesh = new Mesh()
                    {
                        vertices = vertsList.ToArray(),
                        triangles = trisList.ToArray(),
                        uv = vertsList.Select(p => (Vector2)p).ToArray(),
                        colors = colorList.ToArray()
                    };

                    WeldVertices(mesh, vTree);

                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    DestroyImmediate(go.GetComponent<MeshCollider>());
                    go.name = "Garment_" + this.name;
                    go.transform.position = center;
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;

#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        var clothPath = Directory.GetParent(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(this))));

                        string path = clothPath + "/Export/Garment/" + this.name + "_mesh.asset";
                        bool exported = false;
                        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

                        var newMesh = mesh;
                        bool useProxy = false;
                        path = path.Replace('\\', '/');
                        if (path.StartsWith(Application.dataPath))
                        {
                            path = "Assets" + path.Substring(Application.dataPath.Length);
                        }

                        if (System.IO.File.Exists(path))
                        {
                            if (EditorUtility.DisplayDialog((useProxy ? "Proxy " : "") + "Mesh Exists", "Do you want to overwrite the existing mesh \"" + Path.GetFileName(path) + "\" ?", "Yes", "No"))
                            {
                                if (AssetDatabase.Contains(newMesh)) { AssetDatabase.SaveAssets(); }
                                else { AssetDatabase.CreateAsset(newMesh, path); }
                                exported = true;
                            }
                        }
                        else
                        {
                            if (AssetDatabase.Contains(newMesh)) { AssetDatabase.SaveAssets(); }
                            else { AssetDatabase.CreateAsset(newMesh, path); }
                            exported = true;
                        }

                        if (exported)
                        {
                            Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " exported to " + path + "</color>");
                            //AssetDatabase.SaveAssets();
                        }

                    }
#endif

                    this.transform.localScale = tempScale;
                    _generationMinDistance = tempdist;
                    this.transform.rotation = saveRot;

                    return go;
                }

                this.transform.localScale = tempScale;
                _generationMinDistance = tempdist;
                this.transform.rotation = saveRot;
            }

            return null;
        }

        private void WeldVertices(Mesh aMesh, KDTree vTree, float aMaxDelta = 1e-05f)
        {
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var colors = aMesh.colors;
            var uvs = aMesh.uv;
            List<int> newVerts = new List<int>();
            int[] map = new int[verts.Length];
            // create mapping and filter duplicates.
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                //var n = normals[i];
                //var uv = uvs[i];
                bool duplicate = false;
                for (int i2 = 0; i2 < newVerts.Count; i2++)
                {
                    int a = newVerts[i2];
                    if (
                        (verts[a] - p).sqrMagnitude <= aMaxDelta// && // compare position
                                                                //Vector3.Angle(normals[a], n) <= aMaxDelta //&& // compare normal
                                                                //(uvs[a] - uv).sqrMagnitude <= aMaxDelta // compare first uv coordinate
                        )
                    {
                        map[i] = i2;
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    map[i] = newVerts.Count;
                    newVerts.Add(i);
                }
            }
            // create new vertices
            int count = newVerts.Count;
            var verts2 = new Vector3[count];
            var normals2 = new Vector3[count];
            var colors2 = new Color[count];
            var uvs2 = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                //normals2[i] = normals[a];
                if (a < colors.Length)
                    colors2[i] = colors[a];
                if (vTree.m_count > 0)
                {
                    var id = vTree.nearest(new double[] { verts[a].x, verts[a].y, verts[a].z });
                    if (id != null)
                        colors2[i] = Vector3.Distance(verts[a], verts[(int)id]) < _generationMinDistance * 0.01f ? colors[(int)id] : Color.clear;
                }
                uvs2[i] = uvs[a];
            }
            // map the triangle to the new vertices
            var tris = aMesh.triangles;
            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = map[tris[i]];
            }
            //aMesh.Clear();
            //aMesh.vertices = verts2;
            //aMesh.normals = normals2;
            //
            //aMesh.triangles = tris;
            aMesh.triangles = tris;
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.colors = colors2;
            aMesh.uv = uvs2;

            aMesh.RecalculateBounds();
            aMesh.RecalculateNormals();

        }

        public static void RemoveTrisByThresholdAngle(int[] tris, Vector3[] verts, List<int> newTriangles, float _thresholdAngle)
        {
            KDTree treePart = new KDTree(2);
            HashSet<Vector2> tempUniqueVerts = new HashSet<Vector2>();
            for (int i = 0; i < verts.Length; i++)
            {
                if (tempUniqueVerts.Add(verts[i]))
                    treePart.insert(new double[] { verts[i].x, verts[i].y }, i);
            }
            tempUniqueVerts.Clear();

            int length = tris.Length / 3;
            for (int i = 0; i < length; i++)
            {
                int f = i * 3;

                int pA = tris[f + 0];
                int pB = tris[f + 1];
                int pC = tris[f + 2];
                var angle = Vector2.Angle(new Vector2(verts[pB].x - verts[pA].x, verts[pB].y - verts[pA].y), new Vector2(verts[pC].x - verts[pA].x, verts[pC].y - verts[pA].y));
                if (angle > _thresholdAngle || angle < 1) continue;

                pA = tris[f + 1];
                pB = tris[f + 2];
                pC = tris[f + 0];
                angle = Vector2.Angle(new Vector2(verts[pB].x - verts[pA].x, verts[pB].y - verts[pA].y), new Vector2(verts[pC].x - verts[pA].x, verts[pC].y - verts[pA].y));
                if (angle > _thresholdAngle || angle < 1) continue;

                pA = tris[f + 2];
                pB = tris[f + 0];
                pC = tris[f + 1];
                angle = Vector2.Angle(new Vector2(verts[pB].x - verts[pA].x, verts[pB].y - verts[pA].y), new Vector2(verts[pC].x - verts[pA].x, verts[pC].y - verts[pA].y));
                if (angle > _thresholdAngle || angle < 1) continue;

                Vector3 v0 = verts[tris[f + 0]];
                Vector3 v1 = verts[tris[f + 1]];
                Vector3 v2 = verts[tris[f + 2]];
                //Vector3 v0 = new Vector3(verts[tris[f + 1]].x, verts[tris[f + 1]].y);
                //Vector3 v1 = v0;
                //Vector3 v2 = v0;
                //for (int n = 0; n < 2; n++)
                //{
                //    bool flipDir = n == 0;
                //    v1 = new Vector3(verts[flipDir ? tris[f + 0] : tris[f + 2]].x, verts[flipDir ? tris[f + 0] : tris[f + 2]].y);
                //    v2 = new Vector3(verts[flipDir ? tris[f + 2] : tris[f + 0]].x, verts[flipDir ? tris[f + 2] : tris[f + 0]].y);
                //    Vector3 n1 = v1 - v0;
                //    Vector3 n2 = v2 - v0;

                //    var dot = Vector3.Dot(Vector3.forward, Vector3.Cross(n1, n2));
                //    if (dot > 0)
                //        break;
                //}

                int nearestNewTris = (int)treePart.nearest(new double[] { v0.x, v0.y });
                newTriangles.Add(nearestNewTris);

                nearestNewTris = (int)treePart.nearest(new double[] { v1.x, v1.y });
                newTriangles.Add(nearestNewTris);

                nearestNewTris = (int)treePart.nearest(new double[] { v2.x, v2.y });
                newTriangles.Add(nearestNewTris);
            }
        }


        //API stuff

        public void CreateSpline()
        {
            float scale = 0.5f;
            var ps = new Vector2[]{
            new Vector2(0f, 0f) * scale,
            new Vector2(0f, 1f) * scale,
            new Vector2(-0.3f, 0.9f) * scale,
            new Vector2(-0.3f, 1.3f) * scale,
            new Vector2(0f, 1.4f) * scale,
            new Vector2(0.3f, 1.4f) * scale,
            new Vector2(0.5f, 1.3f) * scale,
            new Vector2(0.7f, 1.4f) * scale,
            new Vector2(1f, 1.4f) * scale,
            new Vector2(1.3f, 1.3f) * scale,
            new Vector2(1.3f, 0.9f) * scale,
            new Vector2(1f, 1f) * scale,
            new Vector2(1f, 0f) * scale
                };
            CreateSpline(ps);
        }

        public BezierSpline CreateSpline(Vector2[] ps, bool flip = true, GameObject cutoutParent = null)
        {
            var spline = NewSpline();
            spline.Initialize(ps.Length);
            if (_firstSelectedSpline == null) _firstSelectedSpline = spline;
            for (int i = 0; i < ps.Length; i++)
            {
                spline.points[i].position = (Vector3)ps[i];
            }
            //var random = UnityEngine.Random.value;
            //var pos = spline.transform.position;
            //pos += new Vector3(0, 0, random * 0.01f);
            //spline.transform.position = pos;

            if (cutoutParent == null)
            {
                var garment = spline.gameObject.AddComponent<CreateGarment>();
                garment._cameras = new List<Camera>();
                var cams = FindObjectsOfType<Camera>();
                garment._cameras.AddRange(cams);
                garment._flipNormal = flip;
            }
            else
            {
                var garment = cutoutParent.GetComponent<CreateGarment>();
                garment._cutouts.Add(spline.transform);
            }

            var points = spline.GetComponentsInChildren<BezierPoint>();
            foreach (var p in points)
            {
                var handle = p.gameObject.AddComponent<RuntimeTransformHandle>();
                //handle.handleCamera = cams.Where(x => x.tag != "MainCamera").FirstOrDefault();
                handle.autoScale = true;
                handle.autoScaleFactor = 0.6f;
            }

            return spline;
        }

        private static BezierSpline NewSpline()
        {
            GameObject spline = new GameObject("BezierSpline", typeof(BezierSpline));
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(spline, "Create Spline");
#endif
            spline.transform.localPosition = new Vector3(0f, 0f, 0f);
            spline.transform.localRotation = Quaternion.identity;
            spline.transform.localScale = new Vector3(1f, 1f, 1f);
#if UNITY_EDITOR
            Selection.activeTransform = spline.transform;
#endif
            return spline.GetComponent<BezierSpline>();
        }

        public void ConnectEdges()
        {
            Debug.Log("ConnectEdges " + _selected.Count);
            if (_selected.Count == 2)
            {
                if (_selected[0].transform.parent.parent != _selected[1].transform.parent.parent)
                {
                    var garment = _firstSelectedSpline.GetComponent<CreateGarment>();
                    Debug.Log("garment " + garment.name);
                    garment.connectedEdgesList.Add(new ConnectedEdges(new List<BezierPoint>() { _selected[0].transform.parent.GetComponent<BezierPoint>(), _selected[1].transform.parent.GetComponent<BezierPoint>() }));
                }
                if (_selected[0].transform.parent != _selected[1].transform.parent)
                {
                    var garment = _firstSelectedSpline.GetComponent<CreateGarment>();
                    Debug.Log("garment " + garment.name);
                    garment.connectedEdgesList.Add(new ConnectedEdges(new List<BezierPoint>() { _selected[0].transform.GetComponent<BezierPoint>(), _selected[1].transform.GetComponent<BezierPoint>() }));
                }
            }
        }

        public void CreateMesh()
        {
            //if (connectedEdgesList.Count != 0 || EditorUtility.DisplayDialog("No connected edges found", "There are no connected edges in this garment's list, do you still want to create it ?", "Yes", "No"))
            if (_firstSelectedSpline != null)
            {
                var garment = _firstSelectedSpline.GetComponent<CreateGarment>();
                garment.CreateMeshFromSplines();
            }
        }

        public void TestScript()
        {
            float scale = 0.5f;

            //Points for the shape of the spline/garment
            var ps = new Vector2[]{
            new Vector2(0f, 0f) * scale,
            new Vector2(0f, 0.8f) * scale,
            new Vector2(-0.3f, 0.8f) * scale,
            new Vector2(-0.3f, 1.3f) * scale,
            new Vector2(0f, 1.4f) * scale,
            new Vector2(0.3f, 1.4f) * scale,
            new Vector2(0.5f, 1.3f) * scale,
            new Vector2(0.7f, 1.4f) * scale,
            new Vector2(1f, 1.4f) * scale,
            new Vector2(1.3f, 1.3f) * scale,
            new Vector2(1.3f, 0.8f) * scale,
            new Vector2(1f, 0.8f) * scale,
            new Vector2(1f, 0f) * scale
                };

            //for (int i = 0; i < ps.Length; i++)
            //{
            //    ps[i].y += 0.05f;
            //}

            //Config Spline1 with garment (front)
            var s1 = CreateSpline(ps, false);
            if (s1.GetComponent<CreateGarment>()) s1.GetComponent<CreateGarment>()._generationMinDistance = 1.5f;
            s1.transform.position = new Vector3(-0.25f, 0.9f, 0.3f * scale);

            //Config Spline2 with garment (back)
            var s2 = CreateSpline(ps, true);
            if (s2.GetComponent<CreateGarment>()) s2.GetComponent<CreateGarment>()._generationMinDistance = 1.5f;
            s2.transform.position = new Vector3(-0.25f, 0.9f, -0.4f * scale);

            //Add connections lines between splines
            for (int i = 0; i < s1.points.Count - 1; i++)
            {
                if (i == 2 || i == 9 || i >= 5 && i <= 6) continue;
                _selected.Clear();
                _selected.Add(s1.points[i].gameObject);
                _selected.Add(s2.points[i].gameObject);
                s1.GetComponent<CreateGarment>().ConnectEdges();
            }

            //Create Mesh
            if (_firstSelectedSpline != null)
            {
                var garment = _firstSelectedSpline.GetComponent<CreateGarment>();
                var go = garment.CreateMeshFromSplines();
                var cloth = go.AddComponent<ClothObjectGPU>();
                cloth._useGarmentMesh = true;
                cloth._garmentSeamLength = 0.03f;
                cloth._weldVertices = false;
                cloth._sewEdges = true;
                cloth._fixDoubles = true;
            }

            s1?.gameObject.SetActive(false);
            s2?.gameObject.SetActive(false);
            if (s1?.GetComponent<CreateGarment>()?._centerObj != null) s1.GetComponent<CreateGarment>()._centerObj.SetActive(false);
            if (s2?.GetComponent<CreateGarment>()?._centerObj != null) s2.GetComponent<CreateGarment>()._centerObj.SetActive(false);
        }

    }

    public static class ExtensionMethods
    {
        public static Vector2 ToMyVector2(this Vector3 v, int axis = 2)
        {
            if (axis == 0)
                return new Vector2(v.y, v.z);
            else if (axis == 1)
                return new Vector2(v.x, v.z);
            else
                return new Vector2(v.x, v.y);
        }

    }
}
