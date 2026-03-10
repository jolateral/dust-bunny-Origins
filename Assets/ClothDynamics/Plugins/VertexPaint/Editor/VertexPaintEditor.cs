using System.Collections;
using UnityEngine;
using UnityEditor;
using Unity.Burst;
using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace ClothDynamics
{
	//public struct SampledAnimationCurve : System.IDisposable
	//{
	//    NativeArray<float> sampledFloat;
	//    /// <param name="samples">Must be 2 or higher</param>
	//    public SampledAnimationCurve(AnimationCurve ac, int samples)
	//    {
	//        sampledFloat = new NativeArray<float>(samples, Allocator.Persistent);
	//        float timeFrom = ac.keys[0].time;
	//        float timeTo = ac.keys[ac.keys.Length - 1].time;
	//        float timeStep = (timeTo - timeFrom) / (samples - 1);

	//        for (int i = 0; i < samples; i++)
	//        {
	//            sampledFloat[i] = ac.Evaluate(timeFrom + (i * timeStep));
	//        }
	//    }

	//    public void Dispose()
	//    {
	//        sampledFloat.Dispose();
	//    }

	//    /// <param name="time">Must be from 0 to 1</param>
	//    public float EvaluateLerp(float time)
	//    {
	//        int len = sampledFloat.Length - 1;
	//        float clamp01 = time < 0 ? 0 : (time > 1 ? 1 : time);
	//        float floatIndex = (clamp01 * len);
	//        int floorIndex = (int)math.floor(floatIndex);
	//        if (floorIndex == len)
	//        {
	//            return sampledFloat[len];
	//        }

	//        float lowerValue = sampledFloat[floorIndex];
	//        float higherValue = sampledFloat[floorIndex + 1];
	//        return math.lerp(lowerValue, higherValue, math.frac(floatIndex));
	//    }
	//}

	public class VertexPaintEditor : ScriptableWizard
	{
		static VertexPaintEditor window;
		static int VertexPaintHash;
		static PaintObject deformObj;
		static bool drawActive = false;
		static Vector3 gizmoPos = Vector3.zero;
		static Vector3 gizmoPosStart = Vector3.zero;
		static GameObject character;
		static GameObject lastCharacter;
		static GameObject cmGo;
		static Mesh currentSelectionMesh;

		public bool showMeshPainter = true;
		public bool showListLayer = true;
		public bool showListMeshes = true;
		public bool showTexList = true;
		public bool showRenderList = true;
		public bool showPaintList = true;
		public bool showExtraList = true;

		public static int _selectedColorChannel = 0;
		public static int _lastSelectedColorChannel = -1;
		public static string[] _colorChannelOptions = new string[] { "All", "Red", "Green", "Blue", "Alpha" };

		static NativeArray<float3> _verts;
		static NativeArray<float3> _normals;
		static NativeArray<float4> _colors;
		static NativeArray<float> _sampledCurve;

		static Mesh lastMesh = null;

		[MenuItem("ClothDynamics/Vertex Paint Manager", priority = 1)]
		static public void Init()
		{
			if (window == null)
			{
				deformObj = null;
				window = (VertexPaintEditor)EditorWindow.GetWindow(typeof(VertexPaintEditor));
				//window.title = "VertexPainter";
				window.autoRepaintOnSceneChange = true;
				window.minSize = new Vector2(300, 460);
				VertexPaintHash = window.GetHashCode();
				SceneView.duringSceneGui += OnSceneGUI;
				Undo.undoRedoPerformed += MyUndoCallback;
			}
			if (window) window.Focus();
		}

		private static void UpdateVerticesAnColors()
		{
			if (currentSelectionMesh != null && currentSelectionMesh.vertexCount > 0 && (currentSelectionMesh != lastMesh || !_verts.IsCreated))
			{
				lastMesh = currentSelectionMesh;

				Vector3[] vertices = currentSelectionMesh.vertices;
				if (_verts.IsCreated) _verts.Dispose();
				_verts = new NativeArray<float3>(vertices.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

				Vector3[] normals = currentSelectionMesh.normals;
				if (_normals.IsCreated) _normals.Dispose();
				_normals = new NativeArray<float3>(normals.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

				Color[] colors = currentSelectionMesh.colors;
				if (_colors.IsCreated) _colors.Dispose();
				_colors = new NativeArray<float4>(colors.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

				if (vertices.Length != colors.Length)
					Debug.LogError("vertices.Length != colors.Length");
				else
				{
					for (int i = 0; i < vertices.Length; i++)
					{
						_verts[i] = new float3(vertices[i].x, vertices[i].y, vertices[i].z);
						_normals[i] = new float3(normals[i].x, normals[i].y, normals[i].z);
						_colors[i] = new float4(colors[i].r, colors[i].g, colors[i].b, colors[i].a);
					}
				}
				if (_sampledCurve.IsCreated) _sampledCurve.Dispose();

				_sampledCurve = SampledAnimationCurve(deformObj.paintFalloffCurve, 1024);
			}
		}

		static void MyUndoCallback()
		{
			GetColorsFromDeform();
			drawActive = drawActive == false;
			SetMeshMode(drawActive);
			drawActive = drawActive == false;
			SetMeshMode(drawActive);
			UpdateColorChannels();
		}

		void OnDestroy()
		{
			if (_verts.IsCreated) _verts.Dispose();
			if (_colors.IsCreated) _colors.Dispose();
			if (_sampledCurve.IsCreated) _sampledCurve.Dispose();

			//FillVolumeVertices();
			drawActive = false;
			SetMeshMode(drawActive);
			SceneView.duringSceneGui -= OnSceneGUI;
			Undo.undoRedoPerformed -= MyUndoCallback;
			SceneView.RepaintAll();
			deformObj = null;
			window = null;

		}

		static void GetColorsFromDeform()
		{
			if (currentSelectionMesh == null) return;
			Vector3[] vertices = currentSelectionMesh.vertices;
			if (currentSelectionMesh.colors == null || currentSelectionMesh.colors.Length != vertices.Length)
			{
				currentSelectionMesh.colors = new Color[vertices.Length];
			}
			Color[] colrs = currentSelectionMesh.colors;
			//var skinner = deformObj.GetComponent<SkinnerSource>();
			//bool useSkinner = false;
			//if (skinner != null && skinner._model != null && skinner._model._mapVertsBack != null) useSkinner = true;
			for (int i = 0; i < colrs.Length; i++)
			{
				int index = i;// useSkinner ? skinner._model._mapVertsBack[i] : i;
				if (index < deformObj.vertexColors.Length)
					colrs[i] = deformObj.vertexColors[index];
			}
			currentSelectionMesh.colors = colrs;
		}
		static void SetColorsToDeform()
		{
			if (currentSelectionMesh.colors == null || currentSelectionMesh.colors.Length != currentSelectionMesh.vertices.Length)
				return;
			Color[] colrs = currentSelectionMesh.colors;
			//var skinner = deformObj.GetComponent<SkinnerSource>();
			//bool useSkinner = false;
			//if (skinner != null && skinner._model != null && skinner._model._mapVertsBack != null) useSkinner = true;
			for (int i = 0; i < colrs.Length; i++)
			{
				int index = i;// useSkinner ? skinner._model._mapVertsBack[i] : i;
				if (index < deformObj.vertexColors.Length)
					deformObj.vertexColors[index] = colrs[i];
			}
		}

		static void DrawHandle(Event current)
		{
			Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
			RaycastHit hit;
			if (Physics.Raycast(r, out hit, Mathf.Infinity, 1 << 31))//deformObj.layerMask.value))
			{
				Handles.color = current.alt ? Color.red : Color.green;
				Handles.DrawWireDisc(hit.point, hit.normal, deformObj.paintRadius);
				Handles.DrawLine(hit.point, hit.point + hit.normal * deformObj.paintRadius);
				Handles.color = Color.white * 0.5f;
				var newNormal = Vector3.Cross(hit.normal, Vector3.up);
				Handles.DrawWireDisc(hit.point, newNormal, deformObj.paintRadius);
				Handles.DrawWireDisc(hit.point, Vector3.Cross(newNormal, hit.normal), deformObj.paintRadius);
				if (current.shift) Handles.Label(hit.point, "   Radius:" + deformObj.paintRadius);
				if (current.control) Handles.Label(hit.point, "   Blend:" + deformObj.paintBlendFactor);
			}
		}

		[BurstCompile]
		public struct PaintVerticesJob : IJobParallelFor
		{
			[ReadOnly] public int channel;
			[ReadOnly] public float paintRadius;
			[ReadOnly] public bool frontFacesOnly;
			[ReadOnly] public float paintBlendFactor;
			[ReadOnly] public float3 pos;
			[ReadOnly] public float3 camPos;
			[ReadOnly] public float4 color;
			[ReadOnly] public NativeArray<float3> vertices;
			[ReadOnly] public NativeArray<float3> normals;
			[ReadOnly] public NativeArray<float> samples;
			public NativeArray<float4> colors;

			public float EvaluateLerp(NativeArray<float> sampledFloat, float time)
			{
				if (sampledFloat != null && sampledFloat.Length > 1)
				{
					int len = sampledFloat.Length - 1;
					float clamp01 = time;
					float floatIndex = clamp01 * len;
					int floorIndex = (int)math.floor(floatIndex);
					if (floorIndex == len && len < sampledFloat.Length)
					{
						return sampledFloat[len];
					}

					if (floorIndex + 1 < sampledFloat.Length)
					{
						float lowerValue = sampledFloat[floorIndex];
						float higherValue = sampledFloat[floorIndex + 1];
						return math.lerp(lowerValue, higherValue, math.frac(floatIndex));
					}
					else
					{
						return time;
					}
				}
				else
				{
					return time;
				}
			}

			public void Execute(int i)
			{
				if (i < vertices.Length)
				{
					float mag = math.length(vertices[i] - pos);
					if (mag <= paintRadius)
					{
						float d = math.dot(normals[i], camPos - vertices[i]);
						if (!frontFacesOnly || d > 0)
						{
							float time = math.saturate(1.0f - mag / paintRadius);
							float falloff = time;
							//float falloff = EvaluateLerp(samples, time);
							if (i < colors.Length)
							{
								float4 col = colors[i];
								if (channel == 0)
								{
									col = math.lerp(col, color, paintBlendFactor * falloff);
								}
								else
								{
									int c = channel - 1;
									col[c] = math.lerp(col[c], color[c], paintBlendFactor * falloff);
								}
								colors[i] = col;
							}
						}
					}
				}
			}
		}

		/// <param name="samples">Must be 2 or higher</param>
		static NativeArray<float> SampledAnimationCurve(AnimationCurve ac, int samples)
		{
			_sampledCurve = new NativeArray<float>(samples, Allocator.Persistent);//new NativeArray<float>(samples, Allocator.TempJob);
			float timeFrom = ac.keys[0].time;
			float timeTo = ac.keys[ac.keys.Length - 1].time;
			float timeStep = (timeTo - timeFrom) / (samples - 1);

			for (int i = 0; i < samples; i++)
			{
				_sampledCurve[i] = ac.Evaluate(timeFrom + (i * timeStep));
			}
			return _sampledCurve;
		}



		static void DrawMeshes(int dir, bool savePos = false)
		{

			Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
			RaycastHit hit;
			if (Physics.Raycast(r, out hit, Mathf.Infinity, 1 << 31))//deformObj.layerMask.value))
			{
				if (savePos)
					gizmoPosStart = hit.point;
				gizmoPos = hit.point;

				UpdateVerticesAnColors();

				Color[] colors = currentSelectionMesh.colors;

				Vector3 pos = cmGo.transform.InverseTransformPoint(hit.point);
				var scc = _selectedColorChannel;
				//var pc = deformObj.paintColor;
				//var paint = scc == 0 ? pc : scc == 1 ? 
				var color = dir > 0 ? deformObj.paintColor : deformObj.paintClearColor;
				deformObj.paintRadius = Mathf.Max(0.0001f, Mathf.Abs(deformObj.paintRadius));

				bool frontFacesOnly = deformObj.frontFacesOnly;

				float3 camPos = Camera.current != null ? Camera.current.transform.position : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
				if (math.lengthsq(camPos) == 0) frontFacesOnly = false;

				var job = new PaintVerticesJob
				{
					channel = scc,
					paintRadius = deformObj.paintRadius,
					frontFacesOnly = frontFacesOnly,
					paintBlendFactor = deformObj.paintBlendFactor,
					pos = pos,
					camPos = camPos,
					color = new float4(color.r, color.g, color.b, color.a),
					vertices = _verts,
					normals = _normals,
					colors = _colors,
					samples = _sampledCurve
				};
				JobHandle handle = job.Schedule(_verts.Length, 1024);
				handle.Complete();

				_colors.Reinterpret<Color>().CopyTo(colors);

				currentSelectionMesh.colors = colors;
			}
		}


		static void DrawMeshesCheck(Event current, bool mouseType = false)
		{
			if (deformObj != null)
			{
				if (current.alt)
					DrawMeshes(-1);
				else
					DrawMeshes(1);
			}
		}

		static void OnSceneGUI(SceneView sceneview)
		{
			int ctrlID = GUIUtility.GetControlID(VertexPaintHash, FocusType.Passive);
			Event current = Event.current;
			if (deformObj && currentSelectionMesh && drawActive && current.button == 0) //if right mouse button is not pressed!
			{
				switch (current.type)
				{
					case EventType.ScrollWheel:
						if (current.shift || current.alt)
						{
							current.Use();
							if (current.shift && current.alt)
								deformObj.paintRadius -= current.delta.y * 0.001f;
							else if (current.shift)
								deformObj.paintRadius -= current.delta.y * 0.01f;
							deformObj.paintRadius = Mathf.Max(0.0001f, Mathf.Abs(deformObj.paintRadius));
						}
						if (current.control || current.alt)
						{
							current.Use();
							if (current.control && current.alt)
								deformObj.paintBlendFactor -= current.delta.y * 0.001f;
							else if (current.control)
								deformObj.paintBlendFactor -= current.delta.y * 0.01f;
							deformObj.paintBlendFactor = Mathf.Max(0.0001f, Mathf.Abs(deformObj.paintBlendFactor));
						}
						break;

					case EventType.MouseUp:
						SetColorsToDeform();
						break;

					case EventType.MouseDown:
						Undo.RegisterCompleteObjectUndo(deformObj, "Paint Control Meshes");
						//Undo.RecordObject(deformObj, "Vertex paint");
						current.Use();
						GetColorsFromDeform();
						DrawMeshesCheck(current, true);
						break;

					case EventType.MouseDrag:
						DrawMeshesCheck(current);
						DrawHandle(current);
						HandleUtility.Repaint();
						break;

					case EventType.MouseMove:
						HandleUtility.Repaint();
						break;

					case EventType.Repaint:
						DrawHandle(current);
						break;

					case EventType.Layout:
						HandleUtility.AddDefaultControl(ctrlID);
						break;
				}
			}
		}

		void OnGUI()
		{
			if (Application.isPlaying)
			{
				drawActive = false;
				SetMeshMode(drawActive);
				EditorGUILayout.LabelField("Stop Play Mode to Edit Vertex Color");
				return;
			}

			int idNum = EditorPrefs.GetInt("CharacterID");
			var obj = EditorUtility.InstanceIDToObject(idNum);
			if (obj != null)
			{
				if (obj.GetType() == typeof(GameObject))
					character = (GameObject)obj;
			}
			if (Selection.activeObject != null && Selection.activeObject.GetType() == typeof(GameObject))
			{
				GameObject goTemp = (GameObject)Selection.activeObject;
				if (!goTemp.activeInHierarchy)
				{
					EditorGUILayout.LabelField(goTemp.name + " is not active in hierarchy!");
				}
				else
				{
					if (goTemp.GetComponentInChildren<PaintObject>() != null)
					{
						character = goTemp;
						Repaint();
						GetActiveSceneView().Repaint();
					}
					else
					{
						EditorGUILayout.Space();

						if (GUILayout.Button("Add Paint Object"))
						{
							goTemp.AddComponent<PaintObject>();
							character = goTemp;
							Repaint();
							GetActiveSceneView().Repaint();
						}

						EditorGUILayout.Space();

					}
				}
			}
			EditorGUILayout.Space();
			character = (GameObject)EditorGUILayout.ObjectField("Last Selected Paint Object", character, typeof(GameObject), true);
			if (character != null)
			{
				if (lastCharacter != character)
				{
					lastCharacter = character;
					drawActive = false;
					SetMeshMode(drawActive);
				}

				EditorPrefs.SetInt("CharacterID", character.GetInstanceID());
				deformObj = character.GetComponentInChildren<PaintObject>();
			}
			EditorGUILayout.Space();
			if (deformObj != null && deformObj.gameObject.activeInHierarchy)
			{
				//EditorGUIUtility.LookLikeControls();

				var style = new GUIStyle(EditorStyles.foldout);
				style.fontSize = 14;
				style.fontStyle = FontStyle.BoldAndItalic;
				style.fixedWidth = 150;
				style.fixedHeight = 20;
				showMeshPainter = EditorGUILayout.Foldout(showMeshPainter, "Vertex Color Painter :", style);
				EditorGUILayout.Space();

				if (showMeshPainter)
				{
					EditorGUILayout.Space();
					{
						if (character.transform.localScale.x != 1 || character.transform.localScale.y != 1 || character.transform.localScale.z != 1)
						{
							GUIStyle orangeStyle = new GUIStyle(EditorStyles.miniLabel);
							orangeStyle.normal.textColor = new Color(1, 0.5f, 0.0f);
							EditorGUILayout.LabelField("Warning: Object scaling must be set to 1 to properly paint it!", orangeStyle);
						}
						//showPaintList = EditorGUILayout.Foldout(showPaintList, "Paint Settings:", EditorStyles.foldoutPreDrop);
						//if (showPaintList)
						//{
						EditorGUILayout.Space();
						if (!drawActive && GUILayout.Button("Paint Mode"))
						{
							//deformObj.isPainting = drawActive = true;
							drawActive = true;
							SetMeshMode(drawActive);
						}
						if (drawActive && GUILayout.Button("Back To Normal Mode"))
						{
							//deformObj.isPainting = drawActive = false;
							GetColorsFromDeform();
							drawActive = false;
							SetMeshMode(drawActive);
						}
						if (drawActive)
						{

							_selectedColorChannel = EditorGUILayout.Popup("Use Channel(s)", _selectedColorChannel, _colorChannelOptions);
							if (_selectedColorChannel != _lastSelectedColorChannel)
							{
								_lastSelectedColorChannel = _selectedColorChannel;
								UpdateColorChannels();
							}
							deformObj.paintColor = EditorGUILayout.ColorField("PaintColor", deformObj.paintColor);
							deformObj.paintFalloffCurve = EditorGUILayout.CurveField("Falloff Curve", deformObj.paintFalloffCurve);
							deformObj.paintBlendFactor = EditorGUILayout.Slider("Blend Factor", deformObj.paintBlendFactor, 0.0f, 1.0f);
							deformObj.paintRadius = EditorGUILayout.FloatField("Radius", deformObj.paintRadius);
							deformObj.frontFacesOnly = EditorGUILayout.Toggle("Front Faces Only", deformObj.frontFacesOnly);
							//deformObj.paintVolume = EditorGUILayout.Toggle("Volume Paint Mode", deformObj.paintVolume);
							if (GUILayout.Button("Clear with Color:"))
							{
								Undo.RegisterCompleteObjectUndo(deformObj, "Clear All Vertex Colors");
								ResetVertexColors();
							}
							deformObj.paintClearColor = EditorGUILayout.ColorField("ClearColor", deformObj.paintClearColor);

							//EditorGUILayout.Space();
							//deformObj.softnessDistance = EditorGUILayout.FloatField("AutoPaint Softness Distance", deformObj.softnessDistance);
							//if (GUILayout.Button("AutoPaint Vertex Colors"))
							//{
							//    Undo.RegisterCompleteObjectUndo(deformObj, "AutoPaint Vertex Colors");
							//    deformObj.AutoPaintVertexColors();
							//    GetColorsFromDeform();
							//}
							EditorGUILayout.Space();
							deformObj.blurRadius = EditorGUILayout.FloatField("Blur Radius", deformObj.blurRadius);
							if (GUILayout.Button("Blur All Vertex Colors"))
							{
								Undo.RegisterCompleteObjectUndo(deformObj, "Blur All Vertex Colors");
								deformObj.BlurColors(currentSelectionMesh);
								GetColorsFromDeform();
								drawActive = false;
								deformObj.StartCoroutine(DelayActivate());
							}
							EditorGUILayout.Space();
							deformObj.textureForWeighting = (Texture)EditorGUILayout.ObjectField("Texture For Weighting:", deformObj.textureForWeighting, typeof(Texture), false);
							deformObj.tilingOffset = EditorGUILayout.Vector4Field("Tiling Offset:", deformObj.tilingOffset);
							deformObj.useChannel = (PaintObject.ColorChannels)EditorGUILayout.IntSlider("Use Channel:", (int)deformObj.useChannel, 0, 3);
							if (GUILayout.Button("Copy Vertex Colors from Texture"))
							{
								Undo.RegisterCompleteObjectUndo(deformObj, "Copy Vertex Colors from Texture");
								deformObj.CopyTextureWeightsToVertexColors(_selectedColorChannel);
								GetColorsFromDeform();
							}
							EditorGUILayout.Space();
							if (GUILayout.Button("Normalize All Vertex Colors"))
							{
								Undo.RegisterCompleteObjectUndo(deformObj, "Normalize All Vertex Colors");
								NormalizeVertexColors();
							}

							//EditorGUILayout.Space();
							//EditorGUILayout.BeginHorizontal();
							//if (GUILayout.Button("Copy to Mesh"))
							//{
							//	Undo.RegisterCompleteObjectUndo(deformObj, "Read From Mesh File");
							//	//ReadFromMeshFile();
							//	GetColorsFromDeform();
							//}
							//if (GUILayout.Button("Copy to PaintObject"))
							//{
							//	Undo.RegisterCompleteObjectUndo(deformObj, "Save To Mesh File");
							//	SetColorsToDeform();
							//}
							//EditorGUILayout.EndHorizontal();
						}
						EditorGUILayout.Space();
						//}
					}
				}
			}
			if (GUI.changed)
			{
				SceneView.RepaintAll();
				if (deformObj != null) EditorUtility.SetDirty(deformObj);
			}
		}

		private static void UpdateColorChannels()
		{
			if (_selectedColorChannel > 0)
			{
				for (int i = 0; i < 4; i++)
					deformObj.paintColor[i] = 0;
				deformObj.paintColor[_selectedColorChannel - 1] = 1;
				deformObj.paintClearColor = Color.clear;
			}
			else {
				deformObj.paintColor.a = 1;
				deformObj.paintClearColor = Color.black;
			}

			if (cmGo != null)
			{
				var materials = cmGo.GetComponent<Renderer>().sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					materials[i].SetFloat("_useChannel", _selectedColorChannel);
				}
				cmGo.GetComponent<Renderer>().sharedMaterials = materials;
			}
		}

		IEnumerator DelayActivate()
		{
			yield return null;
			drawActive = true;
		}

		static void NormalizeVertexColors()
		{
			Color[] colrs = deformObj.vertexColors;
			float maxVal = 0;
			for (int i = 0; i < colrs.Length; ++i)
			{
				maxVal = Mathf.Max(maxVal, colrs[i].r);
			}
			if (maxVal > 0)
			{
				for (int i = 0; i < colrs.Length; ++i)
				{
					colrs[i] /= maxVal;
				}
			}
			deformObj.vertexColors = colrs;
			GetColorsFromDeform();
		}

		static void ResetVertexColors()
		{
			Color[] colrs = deformObj.vertexColors;
			for (int i = 0; i < colrs.Length; i++)
			{
				colrs[i] = deformObj.paintClearColor;
			}
			deformObj.vertexColors = colrs;
			//SetColorsToDeform();
			GetColorsFromDeform();
		}


		static void SetMeshMode(bool active)
		{
			if (active)
			{
				if (cmGo == null)
				{
					//string path, fileName;
					cmGo = new GameObject("TempVertexPaintObject");// Instantiate(deformObj.gameObject);// deformObj.CreateGameObjectFromResource(out path, out fileName);
					cmGo.transform.SetPositionAndRotation(deformObj.transform.position, deformObj.transform.rotation);
					//cmGo.transform.localScale = deformObj.transform.localScale;

					Mesh skinMesh = null;
					var skin = deformObj.gameObject.GetComponent<SkinnedMeshRenderer>();
					if (skin)
					{
						skinMesh = new Mesh();
						skin.BakeMesh(skinMesh);
						//skinMesh = Instantiate(skin.sharedMesh);
						var mf = cmGo.AddComponent<MeshFilter>();
						mf.sharedMesh = skinMesh;
						var rr = cmGo.AddComponent<MeshRenderer>();
						rr.sharedMaterials = skin.sharedMaterials;
					}
					else
					{
						UnityEditorInternal.ComponentUtility.CopyComponent(deformObj.gameObject.GetComponent<MeshFilter>());
						UnityEditorInternal.ComponentUtility.PasteComponentAsNew(cmGo);

						UnityEditorInternal.ComponentUtility.CopyComponent(deformObj.gameObject.GetComponent<Renderer>());
						UnityEditorInternal.ComponentUtility.PasteComponentAsNew(cmGo);
					}
					if (deformObj.gameObject.GetComponent<MeshCollider>() && !cmGo.GetComponent<MeshCollider>())
					{
						UnityEditorInternal.ComponentUtility.CopyComponent(deformObj.gameObject.GetComponent<MeshCollider>());
						UnityEditorInternal.ComponentUtility.PasteComponentAsNew(cmGo);
					}

					var collider = cmGo.GetComponent<MeshCollider>();
					if (collider == null || collider.sharedMesh == null)
					{
						if (collider != null) DestroyImmediate(collider);
						collider = cmGo.AddComponent<MeshCollider>();
					}

					collider.gameObject.layer = 31;
					currentSelectionMesh = collider.GetComponent<MeshFilter>().sharedMesh;
					var renderers = deformObj.GetComponentsInChildren<Renderer>();
					for (int g = 0; g < renderers.Length; g++)
						renderers[g].enabled = false;

					var mat = Instantiate(deformObj.GetComponent<Renderer>().sharedMaterial);
					mat.SetFloat("_useChannel", _selectedColorChannel);
					if (GraphicsSettings.currentRenderPipeline && GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
						mat.shader = Resources.Load("VertexColorGraph", typeof(Shader)) as Shader;
					else
						mat.shader = Resources.Load("VertexData", typeof(Shader)) as Shader;//TODO

					var length = deformObj.GetComponent<Renderer>().sharedMaterials.Length;
					var materials = new Material[length];
					for (int i = 0; i < length; i++)
					{
						materials[i] = mat;
					}
					cmGo.GetComponent<Renderer>().sharedMaterials = materials;

					var wireFrameGo = Instantiate(cmGo);
					wireFrameGo.transform.parent = cmGo.transform;
					var matWire = Instantiate(deformObj.GetComponent<Renderer>().sharedMaterial);
					matWire.shader = Resources.Load("Wireframe", typeof(Shader)) as Shader;
					var wireMaterials = new Material[length];
					for (int i = 0; i < length; i++)
					{
						wireMaterials[i] = matWire;
					}
					wireFrameGo.GetComponent<Renderer>().sharedMaterials = wireMaterials;

					//cmGo.transform.position = deformObj.transform.position;
					//cmGo.transform.localRotation = deformObj.transform.parent.localRotation;
					deformObj.CreateControlMeshVertexColors(skinMesh);
					GetColorsFromDeform();

					//UpdateVertexTree(currentSelectionMesh);

					collider.enabled = true;
				}
			}
			else
			{
				if (deformObj != null && currentSelectionMesh != null)
				{
					if (deformObj.GetComponent<SkinnedMeshRenderer>())
					{
						var mesh = deformObj.GetComponent<SkinnedMeshRenderer>().sharedMesh;
						mesh.colors = currentSelectionMesh.colors;
						deformObj.GetComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
					}
					else if (deformObj.GetComponent<MeshFilter>())
					{
						var mesh = deformObj.GetComponent<MeshFilter>().sharedMesh;
						mesh.colors = currentSelectionMesh.colors;
						deformObj.GetComponent<MeshFilter>().sharedMesh = mesh;
					}
				}

				if (_verts.IsCreated) _verts.Dispose();
				if (_normals.IsCreated) _normals.Dispose();
				if (_colors.IsCreated) _colors.Dispose();
				if (_sampledCurve.IsCreated) _sampledCurve.Dispose();

				if (cmGo != null)
				{
					//FillVolumeVertices();
					var renderers = deformObj.GetComponentsInChildren<Renderer>();
					for (int g = 0; g < renderers.Length; g++)
						renderers[g].enabled = true;
					currentSelectionMesh = null;
					DestroyImmediate(cmGo);
					cmGo = null;
				}
			}
		}

		void OnSelectionChange()
		{
			Repaint();
		}

		static SceneView GetActiveSceneView()
		{
			//Return the focused window, if it is a SceneView
			if (EditorWindow.focusedWindow != null
				&& EditorWindow.focusedWindow.GetType() == typeof(SceneView))
			{
				return (SceneView)EditorWindow.focusedWindow;
			}
			//Otherwise return the first available SceneView
			ArrayList temp = SceneView.sceneViews;
			return (SceneView)temp[0];
		}

	}
}