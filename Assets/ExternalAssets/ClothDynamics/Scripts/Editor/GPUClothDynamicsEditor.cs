using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
	[CustomEditor(typeof(GPUClothDynamics))]
	[CanEditMultipleObjects]
	public class GPUClothDynamicsEditor : Editor
	{
		SerializedProperty _runSim;
		SerializedProperty _timeMultiplier;
		SerializedProperty _timestep;
		SerializedProperty _iterationNum;
		SerializedProperty _subSteps;
		SerializedProperty _gravity;
		SerializedProperty _wind;
		SerializedProperty _windIntensity;
		SerializedProperty _staticFriction;
		SerializedProperty _dynamicFriction;
		SerializedProperty _worldPositionImpact;
		SerializedProperty _worldRotationImpact;
		SerializedProperty _bufferScale;
		SerializedProperty _updateBufferSize;
		SerializedProperty _delayInit;

		SerializedProperty _distanceCompressionStiffness;
		SerializedProperty _distanceStretchStiffness;
		SerializedProperty _bendingStiffness;

		SerializedProperty _pointConstraintType;
		SerializedProperty _enableMouseInteraction;
		SerializedProperty _deltaConstraintMul;

		SerializedProperty _dampingMethod;
		SerializedProperty _dampingVel;
		SerializedProperty _clampVel;
		SerializedProperty _dampingStiffness;

		SerializedProperty _usePreCache;
		SerializedProperty _preCacheFile;

		SerializedProperty _useCollisionFinder;
		SerializedProperty _voxelCubeScale;
		//SerializedProperty _gridCount;
		SerializedProperty _useCustomVoxelCenter;
		SerializedProperty _customVoxelCenter;
		SerializedProperty _predictiveContact;
		SerializedProperty _meshObjects;
		SerializedProperty _vertexScale;
		SerializedProperty _vertexNormalScale;
		SerializedProperty _useTriangleMesh;
		SerializedProperty _triangleScale;
		SerializedProperty _triangleNormalScale;
		SerializedProperty _autoSphereScale;
		SerializedProperty _secondClothScale;
		SerializedProperty _staticCollisionRadius;
		SerializedProperty _unifiedSphereSize;

		SerializedProperty _useCollidableObjectsList;
		SerializedProperty _collidableObjects;
		SerializedProperty _collidableObjectsBias;
		SerializedProperty _useSelfCollision;
		SerializedProperty _selfCollisionScale;
		SerializedProperty _selfCollisionTriangles;
		SerializedProperty _useNeighbourCheck;

		SerializedProperty _useClothSkinning;
		//SerializedProperty _skinComponent;
		SerializedProperty _blendSkinning;
		SerializedProperty _minBlend;
		SerializedProperty _useSurfacePush;
		SerializedProperty _surfacePush;
		SerializedProperty _surfaceOffset;
		SerializedProperty _skinningForSurfacePush;
		SerializedProperty _forceSurfacePushColliders;

		SerializedProperty _useLOD;
		SerializedProperty _distLod;
		SerializedProperty _lodCurve;
		SerializedProperty _useFrustumClipping;
		SerializedProperty _useShadowCulling;
		SerializedProperty _cullingLights;

		SerializedProperty _meshProxy;
		SerializedProperty _useMeshProxy;
		SerializedProperty _weightsCurve;
		SerializedProperty _weightsToleranceDistance;
		SerializedProperty _scaleWeighting;
		SerializedProperty _skinPrefab;

		SerializedProperty _updateTransformCenter;
		SerializedProperty _scaleBoundingBox;

		SerializedProperty _weldVertices;
		SerializedProperty _weldThreshold;
		SerializedProperty _sewEdges;
		SerializedProperty _fixDoubles;

		SerializedProperty _useReadbackVertices;
		SerializedProperty _readbackVertexEveryX;
		SerializedProperty _readbackVertexScale;

		SerializedProperty _useGarmentMesh;
		SerializedProperty _updateGarmentMesh;
		SerializedProperty _onlyAtStart;
		SerializedProperty _garmentSeamLength;
		SerializedProperty _blendGarment;
		SerializedProperty _pushVertsByNormals;
		SerializedProperty _showBoundingBox;
		SerializedProperty _debugBuffers;
		SerializedProperty _debugMode;
		SerializedProperty _renderDebugPoints;
		SerializedProperty _debugObject;

		int _selected = 0;
		string[] _options = new string[3] { "16", "64", "256" };
		WaitForSeconds _waitForSeconds = new WaitForSeconds(0.1f);

		private void OnEnable()
		{
			_runSim = serializedObject.FindProperty("_runSim");
			_timeMultiplier = serializedObject.FindProperty("_timeMultiplier");
			_timestep = serializedObject.FindProperty("_timestep");
			_iterationNum = serializedObject.FindProperty("_iterationNum");
			_subSteps = serializedObject.FindProperty("_subSteps");
			_gravity = serializedObject.FindProperty("_gravity");
			_wind = serializedObject.FindProperty("_wind");
			_windIntensity = serializedObject.FindProperty("_windIntensity");
			_staticFriction = serializedObject.FindProperty("_staticFriction");
			_dynamicFriction = serializedObject.FindProperty("_dynamicFriction");
			_worldPositionImpact = serializedObject.FindProperty("_worldPositionImpact");
			_worldRotationImpact = serializedObject.FindProperty("_worldRotationImpact");
			_bufferScale = serializedObject.FindProperty("_bufferScale");
			_updateBufferSize = serializedObject.FindProperty("_updateBufferSize");
			_delayInit = serializedObject.FindProperty("_delayInit");
			
			_distanceCompressionStiffness = serializedObject.FindProperty("_distanceCompressionStiffness");
			_distanceStretchStiffness = serializedObject.FindProperty("_distanceStretchStiffness");
			_bendingStiffness = serializedObject.FindProperty("_bendingStiffness");

			_pointConstraintType = serializedObject.FindProperty("_pointConstraintType");
			_enableMouseInteraction = serializedObject.FindProperty("_enableMouseInteraction");
			_deltaConstraintMul = serializedObject.FindProperty("_deltaConstraintMul");

			_dampingMethod = serializedObject.FindProperty("_dampingMethod");
			_dampingVel = serializedObject.FindProperty("_dampingVel");
			_clampVel = serializedObject.FindProperty("_clampVel");
			_dampingStiffness = serializedObject.FindProperty("_dampingStiffness");

			_usePreCache = serializedObject.FindProperty("_usePreCache");
			_preCacheFile = serializedObject.FindProperty("_preCacheFile");

			_useCollisionFinder = serializedObject.FindProperty("_useCollisionFinder");
			_voxelCubeScale = serializedObject.FindProperty("_voxelCubeScale");
			//_gridCount = serializedObject.FindProperty("_collisionFinder._gridCount");
			_useCustomVoxelCenter = serializedObject.FindProperty("_useCustomVoxelCenter");
			_customVoxelCenter = serializedObject.FindProperty("_customVoxelCenter");
			_predictiveContact = serializedObject.FindProperty("_predictiveContact");

			_meshObjects = serializedObject.FindProperty("_meshObjects");
			_vertexScale = serializedObject.FindProperty("_vertexScale");
			_vertexNormalScale = serializedObject.FindProperty("_vertexNormalScale");
			_useTriangleMesh = serializedObject.FindProperty("_useTriangleMesh");
			_triangleScale = serializedObject.FindProperty("_triangleScale");
			_triangleNormalScale = serializedObject.FindProperty("_triangleNormalScale");
			_autoSphereScale = serializedObject.FindProperty("_autoSphereScale");
			_secondClothScale = serializedObject.FindProperty("_secondClothScale");
			_staticCollisionRadius = serializedObject.FindProperty("_staticCollisionRadius");
			_unifiedSphereSize = serializedObject.FindProperty("_collisionFinder._unifiedSphereSize");

			_useCollidableObjectsList = serializedObject.FindProperty("_useCollidableObjectsList");
			_collidableObjects = serializedObject.FindProperty("_collidableObjects");
			_collidableObjectsBias = serializedObject.FindProperty("_collidableObjectsBias");

			_useSelfCollision = serializedObject.FindProperty("_useSelfCollision");
			_selfCollisionScale = serializedObject.FindProperty("_selfCollisionScale");
			_selfCollisionTriangles = serializedObject.FindProperty("_selfCollisionTriangles");
			_useNeighbourCheck = serializedObject.FindProperty("_useNeighbourCheck");

			_useClothSkinning = serializedObject.FindProperty("_useClothSkinning");
			//_skinComponent = serializedObject.FindProperty("_skinComponent");
			_blendSkinning = serializedObject.FindProperty("_blendSkinning");
			_minBlend = serializedObject.FindProperty("_minBlend");
			_useSurfacePush = serializedObject.FindProperty("_useSurfacePush");
			_surfacePush = serializedObject.FindProperty("_surfacePush");
			_surfaceOffset = serializedObject.FindProperty("_surfaceOffset");
			_skinningForSurfacePush = serializedObject.FindProperty("_skinningForSurfacePush");
			_forceSurfacePushColliders = serializedObject.FindProperty("_forceSurfacePushColliders");

			_useLOD = serializedObject.FindProperty("_useLOD");
			_distLod = serializedObject.FindProperty("_distLod");
			_lodCurve = serializedObject.FindProperty("_lodCurve");
			_useFrustumClipping = serializedObject.FindProperty("_useFrustumClipping");
			_useShadowCulling = serializedObject.FindProperty("_useShadowCulling");
			_cullingLights = serializedObject.FindProperty("_cullingLights");

			_meshProxy = serializedObject.FindProperty("_meshProxy");
			_useMeshProxy = serializedObject.FindProperty("_useMeshProxy");
			_weightsCurve = serializedObject.FindProperty("_weightsCurve");
			_weightsToleranceDistance = serializedObject.FindProperty("_weightsToleranceDistance");
			_scaleWeighting = serializedObject.FindProperty("_scaleWeighting");
			_skinPrefab = serializedObject.FindProperty("_skinPrefab");

			_updateTransformCenter = serializedObject.FindProperty("_updateTransformCenter");
			_scaleBoundingBox = serializedObject.FindProperty("_scaleBoundingBox");

			_weldVertices = serializedObject.FindProperty("_weldVertices");
			_weldThreshold = serializedObject.FindProperty("_weldThreshold");
			_sewEdges = serializedObject.FindProperty("_sewEdges");
			_fixDoubles = serializedObject.FindProperty("_fixDoubles");

			_useReadbackVertices = serializedObject.FindProperty("_useReadbackVertices");
			_readbackVertexEveryX = serializedObject.FindProperty("_readbackVertexEveryX");
			_readbackVertexScale = serializedObject.FindProperty("_readbackVertexScale");

			_useGarmentMesh = serializedObject.FindProperty("_useGarmentMesh");
			_updateGarmentMesh = serializedObject.FindProperty("_updateGarmentMesh");
			_onlyAtStart = serializedObject.FindProperty("_onlyAtStart");
			_garmentSeamLength = serializedObject.FindProperty("_garmentSeamLength");
			_blendGarment = serializedObject.FindProperty("_blendGarment");
			_pushVertsByNormals = serializedObject.FindProperty("_pushVertsByNormals");

			_showBoundingBox = serializedObject.FindProperty("_showBoundingBox");
			_debugBuffers = serializedObject.FindProperty("_debugBuffers");
			_debugMode = serializedObject.FindProperty("_debugMode");
			_renderDebugPoints = serializedObject.FindProperty("_collisionFinder._renderDebugPoints");
			_debugObject = serializedObject.FindProperty("_collisionFinder._debugObject");

			var script = target as GPUClothDynamics;

			if (script._collisionFinder != null)
			{
				_selected = script._collisionFinder._gridCount == 16 ? 0 : script._collisionFinder._gridCount == 64 ? 1 : 2;

				if (script._useCollisionFinder)
				{
#if UNITY_ANDROID || UNITY_IPHONE
                    Debug.Log("<color=blue>CD: </color>Info: CollisionFinder doesn't work on mobile devices!");
//#if !UNITY_EDITOR
//                    script._useCollisionFinder = false;
//#endif
#endif
				}
			}

			//EditorApplication.playModeStateChanged += OnPlaymodeChanged;
		}

		private void OnDisable()
		{
			//EditorApplication.playModeStateChanged -= OnPlaymodeChanged;
		}

		public void OnPlaymodeChanged(PlayModeStateChange state)
		{
			if (state != PlayModeStateChange.EnteredPlayMode && !EditorApplication.isPlayingOrWillChangePlaymode)
			{
				var script = target as GPUClothDynamics;
				if (script != null && script.isActiveAndEnabled)
				{
					// Credit: https://forum.unity.com/threads/is-it-possible-to-fold-a-component-from-script-inspector-view.296333/#post-2353538
					//The Following is a mad hack to display the inspector correctly after exiting play mode, due to the fact that some properties mess up the inspector UI. Unity Editor Bug?
					ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
					int length = tracker.activeEditors.Length;
					int[] trackerSettings = new int[length];
					for (int i = 0; i < length; i++)
					{
						trackerSettings[i] = tracker.GetVisible(i);
						tracker.SetVisible(i, 0);
					}
					Repaint();
					script.StartCoroutine(DelayRepaint(tracker, trackerSettings));
				}
			}
		}

		IEnumerator DelayRepaint(ActiveEditorTracker tracker, int[] trackerSettings)
		{
			yield return _waitForSeconds;
			for (int i = 0, length = tracker.activeEditors.Length; i < length; i++)
				tracker.SetVisible(i, trackerSettings[i]);
			Repaint();
		}

		//private void OnValidate()
		//{
		//    Repaint();
		//}

		public override void OnInspectorGUI()
		{
			serializedObject.UpdateIfRequiredOrScript();

			var script = target as GPUClothDynamics;

			if (script != null)
			{
				//Undo.RecordObject(script, "GPUClothSimulator GUI");

				GUILayout.Box(script._logo);

				if (GUILayout.Button(script._advancedMode ? "Normal Mode" : "Advanced Mode"))
				{
					script._advancedMode = script._advancedMode == false;
				}
				if (script._advancedMode)
				{
					DrawDefaultInspector();
				}
				else
				{
					//if (serializedObject != null) serializedObject.UpdateIfRequiredOrScript();
					//serializedObject.Update();

					GUIStyle cyanStyle = new GUIStyle(EditorStyles.miniLabel);
					cyanStyle.normal.textColor = Color.cyan;
					GUIStyle orangeStyle = new GUIStyle(EditorStyles.miniLabel);
					orangeStyle.normal.textColor = new Color(1, 0.5f, 0.0f);

					if (EditorHelper.DrawHeader("Main Settings:", false, 246))
					{
						GUILayout.BeginVertical("Box");

						if (script.transform.localScale.x != 1 || script.transform.localScale.y != 1 || script.transform.localScale.z != 1)
						{
							EditorGUILayout.LabelField("Warning: Local object scale must be set to 1 for proper results!\n (Correct it in the unity import settings or your 3D Modeling App)", orangeStyle, GUILayout.MinHeight(25));
						}
						EditorGUILayout.PropertyField(_runSim);
						EditorGUILayout.PropertyField(_timestep);
						EditorGUILayout.PropertyField(_timeMultiplier);
						EditorGUILayout.PropertyField(_iterationNum);
						EditorGUILayout.PropertyField(_subSteps);
						if (Application.isPlaying) GUI.enabled = false;
						EditorGUILayout.PropertyField(_bufferScale);
						if (script._bufferScale > 256) EditorGUILayout.LabelField("High BufferScale value detected! Please check whether you need it.\nStart with a low number and use UpdateBufferSize during runtime.", orangeStyle, GUILayout.MinHeight(32));
						GUI.enabled = true;
						EditorGUILayout.PropertyField(_updateBufferSize);
						EditorGUILayout.PropertyField(_delayInit);

						EditorGUILayout.PropertyField(_gravity);
						EditorGUILayout.PropertyField(_wind);
						if (script._wind != null) EditorGUILayout.PropertyField(_windIntensity);
						EditorGUILayout.PropertyField(_staticFriction);
						EditorGUILayout.PropertyField(_dynamicFriction);
						EditorGUILayout.PropertyField(_worldPositionImpact);
						EditorGUILayout.PropertyField(_worldRotationImpact);

						EditorGUILayout.PropertyField(_distanceCompressionStiffness);
						EditorGUILayout.PropertyField(_distanceStretchStiffness);
						EditorGUILayout.PropertyField(_bendingStiffness);

						EditorGUILayout.PropertyField(_pointConstraintType);
						EditorGUILayout.PropertyField(_enableMouseInteraction);
						if (script._enableMouseInteraction)
						{
#if UNITY_ANDROID || UNITY_IPHONE
                                EditorGUILayout.LabelField("This feature might not work on all mobile devices!", orangeStyle);
#endif
							EditorGUILayout.PropertyField(_deltaConstraintMul);
						}
						EditorGUILayout.PropertyField(_dampingMethod);
						if (script._dampingMethod == DampingMethod.simpleDamping || script._dampingMethod == DampingMethod.smartAndSimpleDamping)
						{
							EditorGUILayout.PropertyField(_dampingVel);
							EditorGUILayout.PropertyField(_clampVel);
						}
						if (script._dampingMethod == DampingMethod.smartDamping || script._dampingMethod == DampingMethod.smartAndSimpleDamping)
						{
							EditorGUILayout.PropertyField(_dampingStiffness);
							EditorGUILayout.LabelField("Smart Damping is an experimental feature!", cyanStyle);
						}

						using (new EditorGUI.DisabledScope(Application.isPlaying))
							EditorGUILayout.PropertyField(_usePreCache);
						if (script._usePreCache)
						{
							EditorGUILayout.PropertyField(_preCacheFile);
							if (GUILayout.Button("Remove Pre-Cache File"))
							{
								script._preCacheFile = null;
							}
						}

						GUILayout.EndVertical();
					}

					if (EditorHelper.DrawHeader("Collision Settings:", false, 246))
					{
						GUILayout.BeginVertical("Box");

						if (!script._useCollisionFinder && !script._useCollidableObjectsList) EditorGUILayout.LabelField("At least one collision system needs to be used (CollisionFinder or/and CollidableObjects)!", orangeStyle);

						if (script._collisionFinder != null)
						{
							using (new EditorGUI.DisabledScope(Application.isPlaying))
								EditorGUILayout.PropertyField(_useCollisionFinder);
							if (script._useCollisionFinder)
							{
								EditorGUILayout.PropertyField(_useCustomVoxelCenter);
								if (script._useCustomVoxelCenter) EditorGUILayout.PropertyField(_customVoxelCenter);

								EditorGUILayout.PropertyField(_voxelCubeScale);

#if UNITY_ANDROID || UNITY_IPHONE
                                EditorGUILayout.LabelField("This feature doesn't work on mobile devices!", orangeStyle);
#endif
								using (new EditorGUI.DisabledScope(Application.isPlaying))
									_selected = EditorGUILayout.Popup("Grid Count", _selected, _options);
								script._collisionFinder._gridCount = _selected == 0 ? 16 : _selected == 1 ? 64 : 256;

								//using (new EditorGUI.DisabledScope(Application.isPlaying))
								EditorGUILayout.PropertyField(_predictiveContact);

								using (new EditorGUI.DisabledScope(Application.isPlaying))
									EditorGUILayout.PropertyField(_meshObjects, true);

								if (script._meshObjects != null && script._meshObjects.Any(x => x != null && x != script.transform.parent && x.parent != script.transform.parent))
									EditorGUILayout.LabelField("Mesh objects have not the same parent as cloth object!", orangeStyle);

								if (script.gameObject.scene.name == null || script._meshObjects != null && script._meshObjects.Any(x => x != null && (x.GetComponent<GPUSkinnerBase>() || x.GetComponent<GPUMesh>() || x.GetComponent<GPUClothDynamics>())))
								{
									using (new EditorGUI.DisabledScope(Application.isPlaying))
										EditorGUILayout.PropertyField(_useTriangleMesh);

									if (!script._useTriangleMesh)
									{
										EditorGUILayout.PropertyField(_vertexScale);
										EditorGUILayout.PropertyField(_vertexNormalScale);
									}
									else
									{
										EditorGUILayout.PropertyField(_triangleScale);
										EditorGUILayout.PropertyField(_triangleNormalScale);
									}
								}
								else
								{
									if (script._meshObjects != null && script._meshObjects.Length > 0 && GUILayout.Button("Update Mesh Objects?"))
									{
										if (EditorUtility.DisplayDialog("Update Mesh Objects?", "Do you want to add necessary components to the mesh objects automatically?", "Yes", "No"))
										{
											foreach (var item in script._meshObjects)
											{
												if (item == null) continue;
												if (item.GetComponent<SkinnedMeshRenderer>() || (item.GetComponent<AutomaticSkinning>() && item.GetComponent<AutomaticSkinning>().enabled))
												{
													if (!item.GetComponent<GPUSkinning>()) item.gameObject.AddComponent<GPUSkinning>();
												}
												else if (item.GetComponent<MeshRenderer>() && !item.GetComponent<AutomaticBoneSpheres>())
												{
													if (!item.GetComponent<GPUMesh>()) item.gameObject.AddComponent<GPUMesh>();
												}
												else if (item.GetComponent<Animator>())
												{
													if (!item.GetComponent<AutomaticBoneSpheres>()) item.gameObject.AddComponent<AutomaticBoneSpheres>();
												}
											}
										}
									}
								}

								if (script._meshObjects != null && script._meshObjects.Any(x => x != null && x.GetComponent<AutomaticBoneSpheres>()))
								{
									EditorGUILayout.PropertyField(_autoSphereScale);
								}
								if (script._meshObjects != null && script._meshObjects.Length > 1 && script._meshObjects.Any(x => x != null && x.GetComponent<GPUClothDynamics>() && x != script.transform))
								{
									EditorGUILayout.PropertyField(_secondClothScale);
								}
							}
						}

						if ((script._useCollisionFinder && script._predictiveContact) || script._usePredictiveContactColliders)
							EditorGUILayout.PropertyField(_staticCollisionRadius);

						if (script._useCollisionFinder)
							EditorGUILayout.PropertyField(_unifiedSphereSize);

						if (script._useCollisionFinder)
						{
							EditorGUILayout.PropertyField(_useSelfCollision);
							if (script._useSelfCollision)
							{
								EditorGUILayout.PropertyField(_selfCollisionScale);
								using (new EditorGUI.DisabledScope(Application.isPlaying))
									EditorGUILayout.PropertyField(_selfCollisionTriangles);

								using (new EditorGUI.DisabledScope(Application.isPlaying))
									EditorGUILayout.PropertyField(_useNeighbourCheck);
#if !UNITY_2020_1_OR_NEWER
								if (script._useNeighbourCheck && !script.CheckShaderKeyword(isOn: true))
									EditorGUILayout.LabelField("Using Unity 2019: You need to restart the playmode to update the neighbour check!", orangeStyle, GUILayout.MinHeight(25));
#endif

								if (script._useNeighbourCheck && !script._predictiveContact)
								{
									EditorGUILayout.LabelField("Warning: NeighbourCheck works better with PredictiveContact, this might affect performance!", orangeStyle, GUILayout.MinHeight(25));
								}
							}

						}

						EditorGUILayout.PropertyField(_useCollidableObjectsList);
						if (script._useCollidableObjectsList)
						{
							EditorGUILayout.LabelField("Supported Colliders: Cube, Sphere, Ellipsoid, Capsule, Cylinder, RoundCone and Plane", cyanStyle);
							EditorGUILayout.PropertyField(_collidableObjects);
							if (GUILayout.Button("Cleanup Collidable Objects List"))
							{
								Extensions.CleanupList(ref script._collidableObjects);
							}
							EditorGUILayout.PropertyField(_collidableObjectsBias);
						}

						GUILayout.EndVertical();
					}

					if (EditorHelper.DrawHeader("Skinning Settings:", false, 246))
					{
						GUILayout.BeginVertical("Box");

						using (new EditorGUI.DisabledScope(Application.isPlaying))
							EditorGUILayout.PropertyField(_useClothSkinning);
						if (script._useClothSkinning)
						{
							//EditorGUILayout.PropertyField(_skinComponent);
							EditorGUILayout.PropertyField(_blendSkinning);
							EditorGUILayout.PropertyField(_minBlend);
						}
						//if (script._useCollisionFinder)
						//{
						EditorGUILayout.PropertyField(_useSurfacePush);
						if (script._useSurfacePush)
						{
							EditorGUILayout.PropertyField(_surfacePush);
							EditorGUILayout.PropertyField(_surfaceOffset);
							EditorGUILayout.PropertyField(_skinningForSurfacePush);
							EditorGUILayout.PropertyField(_forceSurfacePushColliders);
						}
						//}
						EditorGUILayout.PropertyField(_useLOD);
						if (script._useLOD)
						{
							EditorGUILayout.PropertyField(_distLod);
							EditorGUILayout.PropertyField(_lodCurve);
						}

						using (new EditorGUI.DisabledScope(Application.isPlaying))
							EditorGUILayout.PropertyField(_meshProxy);

						if (script._meshProxy != null)
						{
							using (new EditorGUI.DisabledScope(Application.isPlaying))
								EditorGUILayout.PropertyField(_useMeshProxy);
							if (script._useMeshProxy)
							{
								EditorGUILayout.PropertyField(_weightsCurve);
								EditorGUILayout.PropertyField(_weightsToleranceDistance);
								EditorGUILayout.PropertyField(_scaleWeighting);
								EditorGUILayout.PropertyField(_skinPrefab);

								if (GUILayout.Button("Remove Skin Prefab"))
								{
									script._skinPrefab = null;
								}
							}
						}

						GUILayout.EndVertical();
					}

					if (EditorHelper.DrawHeader("Extra Settings:", false, 246))
					{
						GUILayout.BeginVertical("Box");
						EditorGUILayout.PropertyField(_useFrustumClipping);
						if (script._useFrustumClipping)
						{
							EditorGUILayout.PropertyField(_useShadowCulling);
							if (script._useShadowCulling) EditorGUILayout.PropertyField(_cullingLights);
						}
						EditorGUILayout.PropertyField(_updateTransformCenter);
						using (new EditorGUI.DisabledScope(Application.isPlaying))
							EditorGUILayout.PropertyField(_scaleBoundingBox);

						EditorGUILayout.PropertyField(_sewEdges);
						EditorGUILayout.PropertyField(_fixDoubles);
						//EditorGUILayout.PropertyField(_weldVertices);
						if (script._weldVertices) EditorGUILayout.PropertyField(_weldThreshold);

						EditorGUILayout.PropertyField(_useReadbackVertices);
						if (script._useReadbackVertices)
						{
							EditorGUILayout.PropertyField(_readbackVertexEveryX);
							EditorGUILayout.PropertyField(_readbackVertexScale);
						}
						EditorGUILayout.PropertyField(_useGarmentMesh);
						if (script._useGarmentMesh)
						{
							EditorGUILayout.PropertyField(_updateGarmentMesh);
							//GUILayout.BeginHorizontal();
							EditorGUILayout.PropertyField(_onlyAtStart);
							EditorGUILayout.PropertyField(_garmentSeamLength);
							//GUILayout.EndHorizontal();
							EditorGUILayout.PropertyField(_blendGarment);
							EditorGUILayout.PropertyField(_pushVertsByNormals);
						}

						EditorGUILayout.PropertyField(_showBoundingBox);
						EditorGUILayout.PropertyField(_renderDebugPoints);
						if (script._collisionFinder != null && script._collisionFinder._renderDebugPoints) EditorGUILayout.PropertyField(_debugObject);
						EditorGUILayout.PropertyField(_debugMode);
						EditorGUILayout.PropertyField(_debugBuffers);

						GUILayout.EndVertical();
					}

					//if (serializedObject != null && serializedObject.hasModifiedProperties) 
				}

				EditorGUILayout.Space();

				if (GUILayout.Button("Export Mesh"))
				{
					script.ExportMesh();
				}
				if (script._meshProxy != null)
				{
					if (GUILayout.Button("Export Proxy Mesh"))
					{
						script.ExportMesh(useProxy: true);
					}
				}
				script._applyTransformAtExport = EditorGUILayout.Toggle("Apply Transform At Export", script._applyTransformAtExport);

				//GUILayout.BeginHorizontal();
				//if (GUILayout.Button("Copy Data"))
				//{
				//    script.TransferData();
				//}
				//if (GUILayout.Button("Paste Data"))
				//{
				//    Undo.RecordObject(script, "Paste Data to " + script.name);
				//    script.TransferData(copy:false);
				//}
				//GUILayout.EndHorizontal();

				serializedObject.ApplyModifiedProperties();
			}
		}

		[MenuItem("ClothDynamics/Reimport Shaders", priority = 11)]
		public static void ReimportShaders()
		{

            //var clothPath = Directory.GetParent(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(this))));
            var clothPath = Directory.GetParent(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("GPUClothDynamicsUtilities")[0]))).ToString().Replace("\\","/");
            if (clothPath.StartsWith(Application.dataPath))
            {
                clothPath = "Assets" + clothPath.Substring(Application.dataPath.Length);
            }
            Debug.Log("<color=blue>CD: </color><color=orange>Reimport Shaders</color> from " + clothPath);
			var shaders = AssetDatabase.FindAssets("Graph", new string[] { clothPath });
			foreach (var item in shaders)
			{
				var file = AssetDatabase.GUIDToAssetPath(item);
				if (Path.GetExtension(file) == ".shadergraph")
				{
					Debug.Log("<color=blue>CD: </color> Reimport " + file);
					AssetDatabase.ImportAsset(file);
				}
			}
		}
	}
}