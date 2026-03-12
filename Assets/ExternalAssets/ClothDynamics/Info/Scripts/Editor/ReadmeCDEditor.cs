using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using UnityEditor.PackageManager;
using System.Linq;

namespace ClothDynamics
{

    [CustomEditor(typeof(ReadmeCD))]
    //[InitializeOnLoad]
    public class ReadmeCDEditor : Editor
    {

        static string kShowedReadmeSessionStateName = "ReadmeCDEditor.showedReadme";

        static float kSpace = 16f;

        static ReadmeCDEditor()
        {
            EditorApplication.delayCall += SelectReadmeAutomatically;
        }

        public static bool IsPackageInstalled(string packageId)
        {
            if (!File.Exists("Packages/manifest.json"))
                return true;

            string jsonText = File.ReadAllText("Packages/manifest.json");
            return jsonText.Contains(packageId);
        }

        static void SelectReadmeAutomatically()
        {
            var packInstalled = (IsPackageInstalled("com.unity.burst") || IsPackageInstalled("com.unity.collection")) && IsPackageInstalled("com.unity.shadergraph");
            //Debug.Log("packInstalled " + packInstalled);

            if (!packInstalled)
            {
                if (!SessionState.GetBool(kShowedReadmeSessionStateName, false))
                {
                    var readme = SelectReadme();
                    SessionState.SetBool(kShowedReadmeSessionStateName, true);

                    if (readme && !readme.loadedLayout)
                    {
                        LoadLayout();
                        readme.loadedLayout = true;
                    }
                }
                EditorUtility.DisplayDialog("Welcome to Cloth Dynamics!", "Please install the Burst and Shader Graph packages from the Package Manager!", "OK");
            }
        }

        static void LoadLayout()
        {
            var assembly = typeof(EditorApplication).Assembly;
            var windowLayoutType = assembly.GetType("UnityEditor.WindowLayout", true);
            var method = windowLayoutType.GetMethod("LoadWindowLayout", BindingFlags.Public | BindingFlags.Static);
            method.Invoke(null, new object[] { Path.Combine(Application.dataPath, "Info/Layout.wlt"), false });
        }

        [MenuItem("ClothDynamics/Show Instructions", priority = 10)]
        static ReadmeCD SelectReadme()
        {
            var ids = AssetDatabase.FindAssets("ReadmeCD");
            if (ids.Length > 0)
            {
                foreach (var item in ids)
                {
                    var file = AssetDatabase.GUIDToAssetPath(item);
                    if (Path.GetExtension(file) == ".asset")
                    {
                        var readmeObject = AssetDatabase.LoadMainAssetAtPath(file);
                        Selection.objects = new UnityEngine.Object[] { readmeObject };
                        return (ReadmeCD)readmeObject;
                    }
                    else return null;
                }
                return null;
            }
            else
            {
                Debug.Log("Couldn't find a readme");
                return null;
            }
        }

        protected override void OnHeaderGUI()
        {
            var readme = (ReadmeCD)target;
            Init();

            var iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / 3f - 20f, 128f);

            GUILayout.BeginHorizontal("In BigTitle");
            {
                GUILayout.Label(readme.icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
                GUILayout.Label(readme.title, TitleStyle);
            }
            GUILayout.EndHorizontal();
        }

        public override void OnInspectorGUI()
        {
            var readme = (ReadmeCD)target;
            Init();

            foreach (var section in readme.sections)
            {
                if (!string.IsNullOrEmpty(section.heading))
                {
                    GUILayout.Label(section.heading, HeadingStyle);
                }
                if (!string.IsNullOrEmpty(section.text))
                {
                    GUILayout.Label(section.text, BodyStyle);
                }
                if (!string.IsNullOrEmpty(section.linkText))
                {
                    if (LinkLabel(new GUIContent(section.linkText)))
                    {
                        Application.OpenURL(section.url);
                    }
                }
                GUILayout.Space(kSpace);
            }
        }


        bool m_Initialized;

        GUIStyle LinkStyle { get { return m_LinkStyle; } }
        [SerializeField] GUIStyle m_LinkStyle;

        GUIStyle TitleStyle { get { return m_TitleStyle; } }
        [SerializeField] GUIStyle m_TitleStyle;

        GUIStyle HeadingStyle { get { return m_HeadingStyle; } }
        [SerializeField] GUIStyle m_HeadingStyle;

        GUIStyle BodyStyle { get { return m_BodyStyle; } }
        [SerializeField] GUIStyle m_BodyStyle;

        void Init()
        {
            if (m_Initialized)
                return;
            m_BodyStyle = new GUIStyle(EditorStyles.label);
            m_BodyStyle.wordWrap = true;
            m_BodyStyle.fontSize = 14;
            m_BodyStyle.richText = true;

            m_TitleStyle = new GUIStyle(m_BodyStyle);
            m_TitleStyle.fontSize = 26;

            m_HeadingStyle = new GUIStyle(m_BodyStyle);
            m_HeadingStyle.fontStyle = FontStyle.Bold;
            m_HeadingStyle.fontSize = 18;

            m_LinkStyle = new GUIStyle(m_BodyStyle);
            m_LinkStyle.wordWrap = false;
            // Match selection color which works nicely for both light and dark skins
            m_LinkStyle.normal.textColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f);
            m_LinkStyle.stretchWidth = false;

            m_Initialized = true;
        }

        bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
        {
            var position = GUILayoutUtility.GetRect(label, LinkStyle, options);

            Handles.BeginGUI();
            Handles.color = LinkStyle.normal.textColor;
            Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
            Handles.color = Color.white;
            Handles.EndGUI();

            EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

            return GUI.Button(position, label, LinkStyle);
        }
    }

}