using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
    [CustomEditor(typeof(ClothObjectGPU))]
    [CanEditMultipleObjects]
    public class ClothObjectGPUEditor : Editor
    {
        SerializedProperty _meshObjects;
        SerializedProperty _weldVertices;
        SerializedProperty _sewEdges;
        SerializedProperty _fixDoubles;
        SerializedProperty _attachedObjects;
        SerializedProperty _clothId;
        SerializedProperty _meshProxy;
        SerializedProperty _useMeshProxy;
        SerializedProperty _weightsCurve;
        SerializedProperty _weightsToleranceDistance;
        SerializedProperty _scaleWeighting;
        SerializedProperty _minRadius;
        SerializedProperty _skinPrefab;
        private SerializedProperty _followObjects;
        private SerializedProperty _useGarmentMesh;
        private SerializedProperty _garmentSeamLength;

        //int _selected = 0;
        //string[] _options = new string[3] { "16", "64", "256" };
        //WaitForSeconds _waitForSeconds = new WaitForSeconds(0.1f);

        private void OnEnable()
        {
            _meshObjects = serializedObject.FindProperty("_meshObjects");
            _weldVertices = serializedObject.FindProperty("_weldVertices");
            _sewEdges = serializedObject.FindProperty("_sewEdges");
            _fixDoubles = serializedObject.FindProperty("_fixDoubles");
            _attachedObjects = serializedObject.FindProperty("_attachedObjects");
            _clothId = serializedObject.FindProperty("_clothId");

            _meshProxy = serializedObject.FindProperty("_meshProxy");
            _useMeshProxy = serializedObject.FindProperty("_useMeshProxy");
            _weightsCurve = serializedObject.FindProperty("_weightsCurve");
            _weightsToleranceDistance = serializedObject.FindProperty("_weightsToleranceDistance");
            _scaleWeighting = serializedObject.FindProperty("_scaleWeighting");
            _minRadius = serializedObject.FindProperty("_minRadius");
            _skinPrefab = serializedObject.FindProperty("_skinPrefab");

            _followObjects = serializedObject.FindProperty("_followObjects");
            _useGarmentMesh = serializedObject.FindProperty("_useGarmentMesh");
            _garmentSeamLength = serializedObject.FindProperty("_garmentSeamLength");
        }

        private void OnDisable()
        {
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            var script = target as ClothObjectGPU;

            if (script != null)
            {
                EditorGUILayout.PropertyField(_meshObjects);
                EditorGUILayout.PropertyField(_weldVertices);
                EditorGUILayout.PropertyField(_sewEdges);
                EditorGUILayout.PropertyField(_fixDoubles);
                EditorGUILayout.PropertyField(_followObjects);
                EditorGUILayout.PropertyField(_attachedObjects);
                EditorGUILayout.PropertyField(_clothId);

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
                        EditorGUILayout.PropertyField(_minRadius);
                        EditorGUILayout.PropertyField(_skinPrefab);

                        if (GUILayout.Button("Remove Skin Prefab"))
                        {
                            script._skinPrefab = null;
                        }
                    }
                }

                EditorGUILayout.PropertyField(_useGarmentMesh);
                if (script._useGarmentMesh)
                {
                    EditorGUILayout.PropertyField(_garmentSeamLength);
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


                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}