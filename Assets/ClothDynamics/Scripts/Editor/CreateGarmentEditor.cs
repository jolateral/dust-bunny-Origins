using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
    [CustomEditor(typeof(CreateGarment))]
    [CanEditMultipleObjects]
    public class CreateGarmentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Update Preview"))
            {
                var script = target as CreateGarment;
                script._updateData = true;
                foreach (var item in script.connectedEdgesList)
                {
                    if (item.p1?.Internal_Spline?.GetComponent<CreateGarment>()) item.p1.Internal_Spline.GetComponent<CreateGarment>()._updateData = true;
                    if (item.p2?.Internal_Spline?.GetComponent<CreateGarment>()) item.p2.Internal_Spline.GetComponent<CreateGarment>()._updateData = true;
                }
            }
            InspectorLocker.UpdateLockValue();
            var bgColor = GUI.backgroundColor;
            GUI.backgroundColor = InspectorLocker._unlocked ? Color.red : Color.green;
            if (GUILayout.Button(InspectorLocker._unlocked ? "Unlock Inspector" : "Lock Inspector" /*, s*/))
            {
                InspectorLocker.Toggle();
            }
            GUI.backgroundColor = bgColor;

            DrawDefaultInspector();

            var selected = Selection.objects;
            if (selected.Length != 2)
                GUI.enabled = false;

            if (GUILayout.Button("Connect Edges"))
            {
                var script = target as CreateGarment;
                script.AddConnectedEdgeList();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Create Mesh From Splines"))
            {
                var script = target as CreateGarment;
                if (script.connectedEdgesList.Count != 0 || EditorUtility.DisplayDialog("No connected edges found", "There are no connected edges in this garment's list, do you still want to create it ?", "Yes", "No"))
                {
                    script.CreateMeshFromSplines();
                }
            }
        }
    }
}