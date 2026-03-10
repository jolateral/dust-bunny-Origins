using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace ClothDynamics
{
	/// <summary>
	/// Provides custom inspector for {@link GPUBlendShapes}
	/// </summary>
	[CustomEditor(typeof(GPUBlendShapes))]
	public class GPUBlendShapesEditor : Editor
	{
		//SerializedProperty _blendShapeNames;
		//SerializedProperty _blendWeightArray;
		SerializedProperty _useStreamingPath;
		SerializedProperty _uniqueID;
		SerializedProperty _useTangentFiles;
		SerializedProperty _useNormalFiles;
        SerializedProperty _updateType;
        SerializedProperty _externalController;

        private void OnEnable()
		{
			//this._blendShapeNames = this.serializedObject.FindProperty("_blendShapeNames");
			//this._blendWeightArray = this.serializedObject.FindProperty("_blendWeightArray");
			this._useStreamingPath = this.serializedObject.FindProperty("_useStreamingPath");
			this._uniqueID = this.serializedObject.FindProperty("_uniqueID");
			this._useNormalFiles = this.serializedObject.FindProperty("_useNormalFiles");
            this._useTangentFiles = this.serializedObject.FindProperty("_useTangentFiles");
            this._updateType = this.serializedObject.FindProperty("_updateType");
            this._externalController = this.serializedObject.FindProperty("_externalController");
		}

		public override void OnInspectorGUI()
		{
			this.serializedObject.Update();

			var gpubs = (GPUBlendShapes)this.target;

			EditorGUILayout.PropertyField(_useStreamingPath);
			EditorGUILayout.PropertyField(_uniqueID);
			EditorGUILayout.PropertyField(_useNormalFiles);
			EditorGUILayout.PropertyField(_useTangentFiles);
            EditorGUILayout.PropertyField(_updateType);
            EditorGUILayout.PropertyField(_externalController);

            EditorGUILayout.LabelField("Blend Shapes", EditorStyles.boldLabel);

			if (!Application.isPlaying || gpubs._externalController != null) GUI.enabled = false;
			var count = gpubs._blendWeightArray.Count;
			for (int i = 0; i < count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(gpubs._blendShapeNames[i]);
				gpubs._blendWeightArray[i] = EditorGUILayout.Slider(gpubs._blendWeightArray[i], 0, 100);
				EditorGUILayout.EndHorizontal();
			}
			GUI.enabled = true;
			this.serializedObject.ApplyModifiedProperties();
		}

	}

}