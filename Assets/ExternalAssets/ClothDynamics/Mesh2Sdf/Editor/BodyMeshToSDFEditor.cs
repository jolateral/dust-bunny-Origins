using UnityEngine;
using System.Linq;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace ClothDynamics
{
#if UNITY_EDITOR
    [CustomEditor(typeof(BodyMeshToSDF))]
    public class BodyMeshToSDFEditor : Editor
    {
        SerializedProperty _SDFTexture;
        SerializedProperty _FloodMode;
        SerializedProperty _FloodFillQuality;
        SerializedProperty _FloodFillIterations;
        SerializedProperty _DistanceMode;
        SerializedProperty _UpdateMode;
        SerializedProperty _Offset;

        void OnEnable()
        {
            _SDFTexture = serializedObject.FindProperty("_SDFTexture");
            _FloodMode = serializedObject.FindProperty("_FloodMode");
            _FloodFillQuality = serializedObject.FindProperty("_FloodFillQuality");
            _FloodFillIterations = serializedObject.FindProperty("_FloodFillIterations");
            _DistanceMode = serializedObject.FindProperty("_DistanceMode");
            _UpdateMode = serializedObject.FindProperty("_UpdateMode");
            _Offset = serializedObject.FindProperty("_Offset");
        }

        public override void OnInspectorGUI()
        {
            ValidateMesh();

            EditorGUILayout.PropertyField(_UpdateMode);

            if ((BodyMeshToSDF.UpdateMode)_UpdateMode.enumValueIndex == BodyMeshToSDF.UpdateMode.Explicit)
            {
                EditorGUILayout.HelpBox("Explicit update mode - SDF updates driven by a script", MessageType.Info);
                EditorGUILayout.Space();
            }

            EditorGUILayout.PropertyField(_SDFTexture);

            BodySDFTexture sdftexture = _SDFTexture.objectReferenceValue as BodySDFTexture;
            if (sdftexture == null)
                EditorGUILayout.HelpBox("Assign an object with an SDFTexture component - that's where this script will write the SDF to.", MessageType.Warning);
            //else if (sdftexture.mode != SDFTexture.Mode.Dynamic)
            //    EditorGUILayout.HelpBox("SDFTexture needs to reference a RenderTexture to be writeable.", MessageType.Error);

            EditorGUILayout.PropertyField(_FloodMode);
            EditorGUILayout.PropertyField(_FloodFillQuality);

            if ((BodyMeshToSDF.FloodMode)_FloodMode.enumValueIndex == BodyMeshToSDF.FloodMode.Linear)
            {
                EditorGUILayout.PropertyField(_FloodFillIterations);
                EditorGUILayout.PropertyField(_DistanceMode);
            }
            else
            {
                GUI.enabled = false;

                int oldDistanceMode = _DistanceMode.enumValueIndex;
                _DistanceMode.enumValueIndex = (int)BodyMeshToSDF.DistanceMode.Unsigned;
                EditorGUILayout.PropertyField(_DistanceMode);
                _DistanceMode.enumValueIndex = oldDistanceMode;

                GUI.enabled = true;
            }

            if ((BodyMeshToSDF.FloodMode)_FloodMode.enumValueIndex == BodyMeshToSDF.FloodMode.Linear && (BodyMeshToSDF.DistanceMode)_DistanceMode.enumValueIndex == BodyMeshToSDF.DistanceMode.Signed)
            {
                EditorGUILayout.PropertyField(_Offset);
            }
            else
            {
                GUI.enabled = false;

                float oldOffset = _Offset.floatValue;
                _Offset.floatValue = 0;
                EditorGUILayout.PropertyField(_Offset);
                _Offset.floatValue = oldOffset;

                GUI.enabled = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

        void ValidateMesh()
        {
            BodyMeshToSDF meshToSDF = target as BodyMeshToSDF;
            Mesh mesh = null;
            SkinnedMeshRenderer smr = meshToSDF.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
                mesh = smr.sharedMesh;
            if (mesh == null)
            {
                MeshFilter mf = meshToSDF.GetComponent<MeshFilter>();
                if (mf != null)
                    mesh = mf.sharedMesh;
            }
            if (mesh == null)
            {
                EditorGUILayout.HelpBox("BodyMeshToSDF needs a Mesh from either a SkinnedMeshRenderer or a MeshFilter component on this GameObject.", MessageType.Error);
                return;
            }

            if (!meshToSDF.GetComponent<GPUSkinning>() && mesh.subMeshCount > 1)
                EditorGUILayout.HelpBox("Multiple submeshes detected, will only use the first one.", MessageType.Warning);

            if (mesh.GetTopology(0) != MeshTopology.Triangles)
                EditorGUILayout.HelpBox("Only triangular topology meshes supported (MeshTopology.Triangles).", MessageType.Error);

            if (mesh.GetIndexCount(0) > 3 * 10000)
                EditorGUILayout.HelpBox("This looks like a large mesh. For best performance and a smoother SDF, use a proxy mesh of under 10k triangles.", MessageType.Warning);

            if (mesh.GetVertexAttributeStream(UnityEngine.Rendering.VertexAttribute.Position) < 0)
                EditorGUILayout.HelpBox("No vertex positions in the mesh.", MessageType.Error);

            if (meshToSDF.sdfTexture == null && meshToSDF.transform.parent != null)
            {
                BodySDFTexture sdfTexture = null;
                if ((sdfTexture = meshToSDF.transform.parent.GetComponentInChildren<BodySDFTexture>()) == null)
                {
                    var go = Instantiate(Resources.Load("SDFTexture") as GameObject);
                    if (go != null)
                    {
                        sdfTexture = go.GetComponent<BodySDFTexture>();
                    }
                }
                if (sdfTexture != null)
                {
                    sdfTexture.name = "SDFTexture";
                    sdfTexture.transform.parent = meshToSDF.transform.parent;
                    meshToSDF.sdfTexture = sdfTexture;

                    var dynamics = FindObjectOfType<GPUClothDynamicsV2>();
                    if (dynamics != null)
                    {
                        var list = dynamics._globalSimParams._sdfList?.ToList();
                        if (list == null) list = new List<GPUClothDynamicsV2.SDFTextureList>();
                        var foundDouble = false;
                        foreach (var item in list) if (item.tex == sdfTexture) { foundDouble = true; break; }
                        if (!foundDouble) list.Add(new GPUClothDynamicsV2.SDFTextureList() { tex = sdfTexture });
                        Extensions.CleanupList(ref list);
                        dynamics._globalSimParams._sdfList = list.ToArray();
                    }
                }
            }
        }
    }
#endif
}