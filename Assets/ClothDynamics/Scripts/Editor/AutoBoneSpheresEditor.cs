using UnityEngine;
using UnityEditor;
using System.Linq;

namespace ClothDynamics
{
	[CustomEditor(typeof(AutomaticBoneSpheres))]
	[CanEditMultipleObjects]
	public class AutoBoneSpheresEditor : Editor
	{
		static readonly GUIContent k_BoneSizeLabel = new GUIContent("Bone Size");
		static readonly GUIContent k_BoneColorLabel = new GUIContent("Color");
		static readonly GUIContent k_BoneShapeLabel = new GUIContent("Shape");
		static readonly GUIContent k_TripodSizeLabel = new GUIContent("Tripod Size");

		SerializedProperty m_DrawSpheres;
		SerializedProperty m_DrawBones;
		SerializedProperty m_BoneShape;
		SerializedProperty m_BoneSize;
		SerializedProperty m_BoneColor;

		SerializedProperty m_DrawTripods;
		SerializedProperty m_TripodSize;

		SerializedProperty m_Transforms;
		SerializedProperty m_mc;
		SerializedProperty m_useNearestVertex;
			SerializedProperty m_traceDist;
		SerializedProperty m_minRadius;
		SerializedProperty m_boneCount;
		SerializedProperty m_outwardsPush;
		SerializedProperty m_outwardsSpine;
		SerializedProperty m_spineSplitCount;
		SerializedProperty m_spineForward;
		SerializedProperty m_hipOffset;
		SerializedProperty m_boneNamesList;
		SerializedProperty m_collisionLayer;
		SerializedProperty m_sphereScale;
		SerializedProperty m_ignoreBonesList;
		SerializedProperty m_drawRoundCones;
		SerializedProperty m_coneTolerance;
		SerializedProperty m_coneDist;
		SerializedProperty m_createCollidersAndRenderers;
		public void OnEnable()
		{
			m_DrawSpheres = serializedObject.FindProperty("_drawSpheres");
			m_DrawBones = serializedObject.FindProperty("_drawBones");
			m_BoneSize = serializedObject.FindProperty("_boneSize");
			m_BoneShape = serializedObject.FindProperty("_boneShape");
			m_BoneColor = serializedObject.FindProperty("_boneColor");

			m_DrawTripods = serializedObject.FindProperty("_drawTripods");
			m_TripodSize = serializedObject.FindProperty("_tripodSize");

			m_Transforms = serializedObject.FindProperty("_iTransforms");

			m_mc = serializedObject.FindProperty("_meshColliderSource");
			m_useNearestVertex = serializedObject.FindProperty("_useNearestVertex");
			m_traceDist = serializedObject.FindProperty("_traceDist");
			m_minRadius = serializedObject.FindProperty("_minRadius");
			m_boneCount = serializedObject.FindProperty("_boneCount");
			m_outwardsPush = serializedObject.FindProperty("_outwardsPush");
			m_outwardsSpine = serializedObject.FindProperty("_outwardsSpine");
			m_spineSplitCount = serializedObject.FindProperty("_spineSplitCount");
			m_spineForward = serializedObject.FindProperty("_spineForward");
			m_hipOffset = serializedObject.FindProperty("_hipOffset");
			m_boneNamesList = serializedObject.FindProperty("_boneNamesList");

			m_collisionLayer = serializedObject.FindProperty("_collisionLayer");
			m_sphereScale = serializedObject.FindProperty("_sphereScale");
			m_ignoreBonesList = serializedObject.FindProperty("_ignoreBonesList");

			m_drawRoundCones = serializedObject.FindProperty("_drawRoundCones");
			m_coneTolerance = serializedObject.FindProperty("_roundConeTolerance");
			m_coneDist = serializedObject.FindProperty("_roundConeDist");

			m_createCollidersAndRenderers = serializedObject.FindProperty("_createCollidersAndRenderers");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var boneScript = target as AutomaticBoneSpheres;
			Undo.RecordObject(boneScript, "AutoBoneSpheres Data");

			GUIStyle orangeStyle = new GUIStyle(EditorStyles.miniLabel);
			orangeStyle.normal.textColor = new Color(1, 0.5f, 0.0f);

			if (boneScript.transforms == null || boneScript.transforms.Length == 0)
				EditorGUILayout.LabelField("No bones were found, did you optimize the GameObjects in the import rig settings of your model?", orangeStyle);

			if (boneScript._meshColliderSource != null && boneScript._meshColliderSource.Count > 0)
			{
				var mcs = boneScript._meshColliderSource[0];
				if (mcs != null)
				{
					var mesh = mcs.GetComponent<SkinnedMeshRenderer>() != null ?
							   mcs.GetComponent<SkinnedMeshRenderer>().sharedMesh
							 : mcs.GetComponent<MeshFilter>() != null ?
							   mcs.GetComponent<MeshFilter>().sharedMesh : null;

					if (mesh != null)
					{
						Object parentObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(mesh);
						if (parentObject != null)
						{
							var path = AssetDatabase.GetAssetPath(parentObject);
							var importer = (ModelImporter)ModelImporter.GetAtPath(path);

							if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
								EditorGUILayout.LabelField("The Rig Animation Type of this model is not set to Humanoid, some settings might not work!", orangeStyle);
						}
					}
				}
			}

			EditorGUI.BeginChangeCheck();
			if (EditorHelper.DrawHeader("Global Settings:", false, 246))
			{
				GUILayout.BeginVertical("Box");
				bool mcUsed = EditorGUILayout.PropertyField(m_mc);
				GUI.enabled = boneScript._meshColliderSource.Count < 1;
				EditorGUILayout.PropertyField(m_collisionLayer);
				GUI.enabled = true;
				EditorGUILayout.PropertyField(m_useNearestVertex);
				EditorGUILayout.PropertyField(m_traceDist);
				EditorGUILayout.PropertyField(m_minRadius);
				EditorGUILayout.PropertyField(m_boneCount);
				EditorGUILayout.PropertyField(m_outwardsPush);
				EditorGUILayout.PropertyField(m_outwardsSpine);
				EditorGUILayout.PropertyField(m_spineSplitCount);
				EditorGUILayout.PropertyField(m_spineForward);
				EditorGUILayout.PropertyField(m_hipOffset);
				EditorGUILayout.PropertyField(m_sphereScale);
				EditorGUILayout.PropertyField(m_coneTolerance);
				EditorGUILayout.PropertyField(m_coneDist);
				EditorGUILayout.PropertyField(m_createCollidersAndRenderers);			
				GUILayout.EndVertical();
			}
			if (boneScript._selectedBones.Count == 0) boneScript._selectedBones.Add(0);

			if (EditorHelper.DrawHeader("Per Bone Settings:", false, 246))
			{
				GUILayout.BeginVertical("Box");
				if (boneScript._bones != null)
				{
					string[] options = boneScript._bones.Select(p => p.first?.name + " -> " + p.second?.name).ToArray();

					boneScript._selectedBones[0] = EditorGUILayout.Popup("Set Per Bone", Mathf.Clamp(boneScript._selectedBones[0], 0, options.Length), options);
				}

				if (boneScript._perBoneScale.TryGetValue(boneScript._selectedBones[0], out AutomaticBoneSpheres.PerBoneData data))
				{
					if (data != null)
					{
						data.scale = EditorGUILayout.FloatField("Per Bone Scale", data.scale);
						data.offset = EditorGUILayout.Vector3Field("Per Bone Offset", data.offset);
						data.addBones = EditorGUILayout.IntField("Per Bone Add", data.addBones);

						if (GUILayout.Button("Mirror Bone Data"))
						{
							int id = boneScript._selectedBones[0];
							if (id < boneScript._bones.Length)
							{
								var pos = (boneScript._bones[id].first.position + boneScript._bones[id].second.position) * 0.5f;
								var mirrorPos = boneScript.transform.TransformPoint(Vector3.Reflect(boneScript.transform.InverseTransformPoint(pos), Vector3.right));
								float lastDist = float.MaxValue;
								int nearest = 0;
								for (int i = 0; i < boneScript._bones.Length; i++)
								{
									var bone = boneScript._bones[i];
									var center = (bone.first.position + bone.second.position) * 0.5f;
									var dist = Vector3.Distance(center, mirrorPos);
									if (dist < lastDist)
									{
										lastDist = dist;
										nearest = i;
									}
								}
								if (boneScript._perBoneScale.TryGetValue(nearest, out AutomaticBoneSpheres.PerBoneData dataMirror))
								{
									if (dataMirror != null)
									{
										dataMirror.scale = data.scale;
										dataMirror.offset = data.offset;
										dataMirror.offset.x = -data.offset.x;
										dataMirror.addBones = data.addBones;
									}
								}
							}
						}
					}

					int count = boneScript._selectedBones.Count;
					for (int i = 0; i < count; i++)
					{
						boneScript._perBoneScale[boneScript._selectedBones[i]] = data;
					}
				}
				GUILayout.EndVertical();
			}

			bool boneRendererDirty = EditorGUI.EndChangeCheck();

			EditorGUILayout.PropertyField(m_boneNamesList, true);
			GUI.enabled = false;
			EditorGUILayout.PropertyField(m_Transforms, true);
			GUI.enabled = true;

			EditorGUILayout.PropertyField(m_ignoreBonesList, true);

			if (GUILayout.Button("Update Bones Structure"))
			{
				for (int i = 0; i < targets.Length; i++)
				{
					var boneRenderer = targets[i] as AutomaticBoneSpheres;
					boneRenderer.BoneRendererSetupChanged();
				}
			}

			using (new EditorGUI.DisabledScope(true))
				EditorGUILayout.LabelField("Display:");
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(m_DrawSpheres);
			using (new EditorGUI.DisabledScope(true))
				EditorGUILayout.LabelField("Count: " + boneScript._spheresList.Count);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(m_drawRoundCones);
			using (new EditorGUI.DisabledScope(true))
				EditorGUILayout.LabelField("Count: " + boneScript._roundConeList.Count);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(m_DrawBones, k_BoneSizeLabel);
			using (new EditorGUI.DisabledScope(!m_DrawBones.boolValue))
				EditorGUILayout.PropertyField(m_BoneSize, GUIContent.none);
			EditorGUILayout.EndHorizontal();

			using (new EditorGUI.DisabledScope(!m_DrawBones.boolValue))
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(m_BoneShape, k_BoneShapeLabel);
				EditorGUILayout.PropertyField(m_BoneColor, k_BoneColorLabel);
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(m_DrawTripods, k_TripodSizeLabel);
			using (new EditorGUI.DisabledScope(!m_DrawTripods.boolValue))
				EditorGUILayout.PropertyField(m_TripodSize, GUIContent.none);
			EditorGUILayout.EndHorizontal();

			//GUIStyle s = new GUIStyle(EditorStyles.toolbarButton);
			//s.normal.textColor = InspectorLocker.unlocked ? Color.white : Color.cyan;

			EditorGUILayout.Space();
			InspectorLocker.UpdateLockValue();
			var bgColor = GUI.backgroundColor;
			GUI.backgroundColor = InspectorLocker._unlocked ? Color.red : Color.green;
			if (GUILayout.Button(InspectorLocker._unlocked ? "Unlock Inspector" : "Lock Inspector" /*, s*/))
			{
				InspectorLocker.Toggle();
			}
			GUI.backgroundColor = bgColor;
			EditorGUILayout.Space();

			if (GUILayout.Button("Convert Spheres To Colliders"))
			{
				boneScript.ConvertToColliders();
			}
			if (GUILayout.Button("Convert Round Cones To Colliders"))
			{
				boneScript.ConvertToColliders(useCones: true);
			}

			EditorGUILayout.PropertyField(m_createCollidersAndRenderers);

			EditorGUILayout.Space();
			if (GUILayout.Button("Clear ABS Colliders In Bones Hierarchy"))
			{
				if (EditorUtility.DisplayDialog("Clear ABS Colliders In Bones Hierarchy", "Do you want to delete the sphere colliders in the bones hierarchy ?", "Yes", "No"))
				{
					boneScript.ClearABSCollidersInBonesHierarchy();
				}
			}

			if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "UndoRedoPerformed")
				boneRendererDirty = true;

			serializedObject.ApplyModifiedProperties();

			if (boneRendererDirty)
			{
				for (int i = 0; i < targets.Length; i++)
				{
					var boneRenderer = targets[i] as AutomaticBoneSpheres;
					boneRenderer.ExtractBones();
				}
			}
		}


	}
}
