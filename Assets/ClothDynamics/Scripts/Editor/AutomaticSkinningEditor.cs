using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{

	[CustomEditor(typeof(AutomaticSkinning))]
	[CanEditMultipleObjects]
	public class AutomaticSkinningEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			var script = target as AutomaticSkinning;

			GUILayout.Space(5);

			string targetText = script.GetComponent<SkinnedMeshRenderer>() == null ? "SkinnedMeshRenderer" : "MeshRenderer";
			if (script != null && GUILayout.Button("Convert To " + targetText))
			{
				if (EditorUtility.DisplayDialog("Convert To " + targetText, "Do you want to convert \"" + script.gameObject.name + "\" to " + targetText + " ?", "Yes", "No"))
				{
					//Undo.RegisterCompleteObjectUndo(script.gameObject, "Convert To " + targetText);
					if (script.GetComponent<SkinnedMeshRenderer>() == null)
					{
						//Undo.RegisterCompleteObjectUndo(script.gameObject, "Convert To SkinnedMeshRenderer"); //Not working, unity bug?
						var oldState = script._saveNewVertsToMesh;
						script._saveNewVertsToMesh = true;
						script.OnEnable();
						script._saveNewVertsToMesh = oldState;
						script.enabled = false;
					}
					else
					{
						//Undo.RegisterCompleteObjectUndo(script.gameObject, "Convert To MeshRenderer"); //Not working, unity bug?
						script.ConvertSkinnedToMeshRenderer();
					}
				}
			}
			if (script != null && GUILayout.Button("Restore SkinnedMeshRenderer Data"))
			{
				if (EditorUtility.DisplayDialog("RestoreSkinnedToMeshRenderer", "Do you want to restore existing skinned data of \"" + script.gameObject.name + "\" ?", "Yes", "No"))
				{
					//Undo.RegisterCompleteObjectUndo(script.gameObject, "RestoreSkinnedToMeshRenderer");
					script.RestoreSkinnedToMeshRenderer();
				}
			}
		}
	}
}