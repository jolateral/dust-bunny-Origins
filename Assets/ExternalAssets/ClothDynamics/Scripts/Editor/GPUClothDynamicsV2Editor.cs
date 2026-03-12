using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
    [CustomEditor(typeof(GPUClothDynamicsV2))]
    [CanEditMultipleObjects]
    public class GPUClothDynamicsV2Editor : Editor
    {
        SerializedProperty _globalSimParams;
        SerializedProperty _debugEvents;
        SerializedProperty _solver_trisMode;
        SerializedProperty _solver_localSpace;
        SerializedProperty _solver_worldPositionImpact;
        SerializedProperty _solver_worldRotationImpact;
        SerializedProperty _solver_manualSetup;
        SerializedProperty _solver_ignoreObjects;
        //SerializedProperty _solver_autoCollect;
        SerializedProperty _solver_colliders;
        SerializedProperty _solver_absColliders;
        SerializedProperty _solver_updateMode;
        SerializedProperty _solver_runSim;
        SerializedProperty _solver_useMouseGrabber;
        SerializedProperty _solver_debugPoints;
        SerializedProperty _collisionMeshes;
        SerializedProperty _clothList;
        SerializedProperty _extensions;

        //int _selected = 0;
        //string[] _options = new string[3] { "16", "64", "256" };
        //WaitForSeconds _waitForSeconds = new WaitForSeconds(0.1f);

        private void OnEnable()
        {
            _globalSimParams = serializedObject.FindProperty("_globalSimParams");
            _debugEvents = serializedObject.FindProperty("_debugEvents");

            _solver_trisMode = serializedObject.FindProperty("_solver._trisMode");
            _solver_localSpace = serializedObject.FindProperty("_solver._localSpace");
            _solver_worldPositionImpact = serializedObject.FindProperty("_solver._worldPositionImpact");
            _solver_worldRotationImpact = serializedObject.FindProperty("_solver._worldRotationImpact");
            _solver_manualSetup = serializedObject.FindProperty("_solver._manualSetup");
            _solver_ignoreObjects = serializedObject.FindProperty("_solver._ignoreObjects");
            //_solver_autoCollect = serializedObject.FindProperty("_solver._autoCollect");
            _solver_colliders = serializedObject.FindProperty("_solver._unityColliders");
            _solver_absColliders = serializedObject.FindProperty("_solver._absColliders");
            _solver_updateMode = serializedObject.FindProperty("_solver._updateMode");
            _solver_runSim = serializedObject.FindProperty("_solver._runSim");
            _solver_useMouseGrabber = serializedObject.FindProperty("_solver._useMouseGrabber");
            _solver_debugPoints = serializedObject.FindProperty("_solver._debugPoints");

            _collisionMeshes = serializedObject.FindProperty("_collisionMeshes");

            _clothList = serializedObject.FindProperty("_clothList");

            _extensions = serializedObject.FindProperty("_extensions");
        }

        private void OnDisable()
        {
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            var script = target as GPUClothDynamicsV2;

            if (script != null)
            {
                //Undo.RecordObject(script, "GPUClothSimulator GUI");

                GUILayout.Box(script._logo);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUIStyle GridViewCellStyle = new GUIStyle(EditorStyles.miniButton);//style for cells
                GridViewCellStyle.padding = new RectOffset(0, 0, 0, 0);
                GridViewCellStyle.alignment = TextAnchor.MiddleCenter;
                GridViewCellStyle.wordWrap = true;
                GridViewCellStyle.font = EditorStyles.miniBoldFont;
                GridViewCellStyle.fontStyle = FontStyle.Bold;
                GridViewCellStyle.fontSize = 8;
                script._settingsView = GUILayout.Toolbar(script._settingsView, new string[] { "Global", "Solver", "Colliders" }, GridViewCellStyle);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();

                GUIStyle cyanStyle = new GUIStyle(EditorStyles.miniLabel);
                cyanStyle.normal.textColor = Color.cyan;
                GUIStyle orangeStyle = new GUIStyle(EditorStyles.miniLabel);
                orangeStyle.normal.textColor = new Color(1, 0.5f, 0.0f);

                if (script._globalSimParams._sdfList != null)
                {
                    for (int n = 0; n < script._globalSimParams._sdfList.Length; n++)
                    {
                        var sdfData = script._globalSimParams._sdfList[n];
                        if (sdfData.tex == null && sdfData._sdfOffset == 0 && sdfData._sdfIntensity == 0)
                        {
                            script._globalSimParams._sdfList[n] = new GPUClothDynamicsV2.SDFTextureList();
                        }
                    }
                    if (script._globalSimParams._sdfList.Length > 8)
                        System.Array.Resize(ref script._globalSimParams._sdfList, 8);
                }

                if (script._settingsView == 0)
                {
                    if (EditorHelper.DrawHeader2("Global Settings:", false, 246))
                    {
                        GUILayout.BeginVertical("Box");
                        EditorGUILayout.PropertyField(_globalSimParams);
                        EditorGUILayout.PropertyField(_debugEvents);
                        EditorGUILayout.PropertyField(_extensions);
                        GUILayout.EndVertical();
                    }
                }
                else if (script._settingsView == 1)
                {
                    if (EditorHelper.DrawHeader2("Solver Settings:", false, 246))
                    {
                        GUILayout.BeginVertical("Box");
                        EditorGUILayout.PropertyField(_solver_updateMode);
                        EditorGUILayout.PropertyField(_solver_runSim);
                        EditorGUILayout.PropertyField(_solver_trisMode);
                        EditorGUILayout.PropertyField(_solver_useMouseGrabber);
                        EditorGUILayout.PropertyField(_solver_localSpace);
                        if (script._solver._localSpace)
                        {
                            EditorGUILayout.LabelField("Warning: Local Space is currently an experimental feature! And might not work correctly!", orangeStyle, GUILayout.MinHeight(25));

                            EditorGUILayout.PropertyField(_solver_worldPositionImpact);
                            EditorGUILayout.PropertyField(_solver_worldRotationImpact);
                        }
                        EditorGUILayout.PropertyField(_solver_debugPoints);
                        GUILayout.EndVertical();
                    }

                    if (GUILayout.Button("Reset"))
                    {
                        script._solver.UpdateBuffers();
                    }

                }
                else
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(_solver_manualSetup);
                    if (EditorHelper.DrawHeader2("Cloth Objects:", false, 246))
                    {
                        GUILayout.BeginVertical("Box");
                        EditorGUILayout.PropertyField(_clothList);
                        GUILayout.EndVertical();
                    }
                    if (EditorHelper.DrawHeader2("SDF Colliders:", false, 246))
                    {
                        GUILayout.BeginVertical("Box");
                        //EditorGUILayout.PropertyField(_solver_autoCollect);
                        EditorGUILayout.PropertyField(_solver_colliders);
                        EditorGUILayout.PropertyField(_solver_absColliders);
                        GUILayout.EndVertical();
                    }

                    if (EditorHelper.DrawHeader2("Collision Meshes:", false, 246))
                    {
                        GUILayout.BeginVertical("Box");
                        EditorGUILayout.PropertyField(_collisionMeshes);
                        GUILayout.EndVertical();
                    }
                    EditorGUILayout.PropertyField(_solver_ignoreObjects);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        //public void OnPlaymodeChanged(PlayModeStateChange state)
        //{
        //    if (state != PlayModeStateChange.EnteredPlayMode && !EditorApplication.isPlayingOrWillChangePlaymode)
        //    {
        //        var script = target as GPUClothDynamics;
        //        if (script != null && script.isActiveAndEnabled)
        //        {
        //            // Credit: https://forum.unity.com/threads/is-it-possible-to-fold-a-component-from-script-inspector-view.296333/#post-2353538
        //            //The Following is a mad hack to display the inspector correctly after exiting play mode, due to the fact that some properties mess up the inspector UI. Unity Editor Bug?
        //            ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
        //            int length = tracker.activeEditors.Length;
        //            int[] trackerSettings = new int[length];
        //            for (int i = 0; i < length; i++)
        //            {
        //                trackerSettings[i] = tracker.GetVisible(i);
        //                tracker.SetVisible(i, 0);
        //            }
        //            Repaint();
        //            script.StartCoroutine(DelayRepaint(tracker, trackerSettings));
        //        }
        //    }
        //}

        //IEnumerator DelayRepaint(ActiveEditorTracker tracker, int[] trackerSettings)
        //{
        //    yield return _waitForSeconds;
        //    for (int i = 0, length = tracker.activeEditors.Length; i < length; i++)
        //        tracker.SetVisible(i, trackerSettings[i]);
        //    Repaint();
        //}

        //[MenuItem("ClothDynamics/Reimport Shaders", priority = 11)]
        //public static void ReimportShaders()
        //{
        //	var clothPath = Directory.GetParent(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("GPUClothDynamicsUtilities")[0])));
        //	Debug.Log("<color=blue>CD: </color><color=orange>Reimport Shaders</color> from " + clothPath);
        //	var shaders = AssetDatabase.FindAssets("Graph", new string[] { clothPath.ToString() });
        //	foreach (var item in shaders)
        //	{
        //		var file = AssetDatabase.GUIDToAssetPath(item);
        //		if (Path.GetExtension(file) == ".shadergraph")
        //		{
        //			Debug.Log("<color=blue>CD: </color> Reimport " + file);
        //			AssetDatabase.ImportAsset(file);
        //		}
        //	}
        //}
    }
}