using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
    [CustomEditor(typeof(PaintObject))]
    public class PaintObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Open Vertex Painter"))
            {
                VertexPaintEditor.Init();
            }
        }
    }
}