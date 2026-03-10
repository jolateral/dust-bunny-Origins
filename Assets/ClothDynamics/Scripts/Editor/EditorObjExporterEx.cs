/*
Based on ObjExporter.cs, this "wrapper" lets you export to .OBJ directly from the editor menu.
 
This should be put in your "Editor"-folder. Use by selecting the objects you want to export, and select
the appropriate menu item from "Custom->Export". Exported models are put in a folder called
"ExportedObj" in the root of your Unity-project. Textures should also be copied and placed in the
same folder.
N.B. there may be a bug so if the custom option doesn't come up refer to this thread http://answers.unity3d.com/questions/317951/how-to-use-editorobjexporter-obj-saving-script-fro.html */

//Credit: Author: KeliHlodversson
//License: https://creativecommons.org/licenses/by-sa/3.0/
//http://wiki.unity3d.com/index.php?title=ObjExporter

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;

namespace ClothDynamics
{

	struct ObjMaterial
	{
		public string name;
		public string textureName;
	}

	public class EditorObjExporterEx : ScriptableObject
	{
		private static int vertexOffset = 0;
		private static int normalOffset = 0;
		private static int uvOffset = 0;


		//User should probably be able to change this. It is currently left as an excercise for
		//the reader.
		private static string targetFolder = "ExportedObj";


		private static string MeshToString(Component mf, Dictionary<string, ObjMaterial> materialList, bool reduceMesh = false, bool useTransform = false)
		{
			Mesh m;
			Material[] mats;

			if (mf is MeshFilter)
			{
				m = (mf as MeshFilter).sharedMesh;
				mats = mf.GetComponent<Renderer>().sharedMaterials;
			}
			else if (mf is SkinnedMeshRenderer)
			{
				m = new Mesh();
				(mf as SkinnedMeshRenderer).BakeMesh(m);
				//m = (mf as SkinnedMeshRenderer).sharedMesh;
				mats = (mf as SkinnedMeshRenderer).sharedMaterials;
			}
			else
			{
				return "";
			}

			if (reduceMesh)
				m = ReduceMesh(m);

			StringBuilder sb = new StringBuilder();

			sb.Append("g ").Append(mf.name).Append("\n");
			foreach (Vector3 lv in m.vertices)
			{
				Vector3 wv = useTransform ? mf.transform.TransformPoint(lv) : lv;// mf.transform.TransformPoint(lv);

				//This is sort of ugly - inverting x-component since we're in
				//a different coordinate system than "everyone" is "used to".
				sb.Append(string.Format("v {0} {1} {2}\n", -wv.x, wv.y, wv.z));
			}
			sb.Append("\n");

			foreach (Vector3 lv in m.normals)
			{
				Vector3 wv = useTransform ? mf.transform.TransformDirection(lv) : lv;// mf.transform.TransformDirection(lv);

				sb.Append(string.Format("vn {0} {1} {2}\n", -wv.x, wv.y, wv.z));
			}
			sb.Append("\n");

			foreach (Vector3 v in m.uv)
			{
				sb.Append(string.Format("vt {0} {1}\n", v.x, v.y));
			}

			for (int material = 0; material < m.subMeshCount; material++)
			{
				sb.Append("\n");
				sb.Append("usemtl ").Append(mats[material].name).Append("\n");
				sb.Append("usemap ").Append(mats[material].name).Append("\n");

				//See if this material is already in the materiallist.
				try
				{
					ObjMaterial objMaterial = new ObjMaterial();

					objMaterial.name = mats[material].name;

					if (mats[material].mainTexture)
						objMaterial.textureName = AssetDatabase.GetAssetPath(mats[material].mainTexture);
					else
						objMaterial.textureName = null;

					materialList.Add(objMaterial.name, objMaterial);
				}
				catch (ArgumentException)
				{
					//Already in the dictionary
				}


				int[] triangles = m.GetTriangles(material);
				for (int i = 0; i < triangles.Length; i += 3)
				{
					//Because we inverted the x-component, we also needed to alter the triangle winding.
					sb.Append(string.Format("f {1}/{1}/{1} {0}/{0}/{0} {2}/{2}/{2}\n",
						triangles[i] + 1 + vertexOffset, triangles[i + 1] + 1 + normalOffset, triangles[i + 2] + 1 + uvOffset));
				}
			}

			vertexOffset += m.vertices.Length;
			normalOffset += m.normals.Length;
			uvOffset += m.uv.Length;

			return sb.ToString();
		}

		private static void Clear()
		{
			vertexOffset = 0;
			normalOffset = 0;
			uvOffset = 0;
		}

		private static Dictionary<string, ObjMaterial> PrepareFileWrite()
		{
			Clear();

			return new Dictionary<string, ObjMaterial>();
		}

		private static void MaterialsToFile(Dictionary<string, ObjMaterial> materialList, string folder, string filename)
		{
			using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".mtl"))
			{
				foreach (KeyValuePair<string, ObjMaterial> kvp in materialList)
				{
					sw.Write("\n");
					sw.Write("newmtl {0}\n", kvp.Key);
					sw.Write("Ka  0.6 0.6 0.6\n");
					sw.Write("Kd  0.6 0.6 0.6\n");
					sw.Write("Ks  0.9 0.9 0.9\n");
					sw.Write("d  1.0\n");
					sw.Write("Ns  0.0\n");
					sw.Write("illum 2\n");

					if (kvp.Value.textureName != null)
					{
						string destinationFile = kvp.Value.textureName;


						int stripIndex = destinationFile.LastIndexOf('/');//FIXME: Should be Path.PathSeparator;

						if (stripIndex >= 0)
							destinationFile = destinationFile.Substring(stripIndex + 1).Trim();


						string relativeFile = destinationFile;

						destinationFile = folder + "/" + destinationFile;

						Debug.Log("Copying texture from " + kvp.Value.textureName + " to " + destinationFile);

						try
						{
							//Copy the source file
							File.Copy(kvp.Value.textureName, destinationFile);
						}
						catch
						{

						}


						sw.Write("map_Kd {0}", relativeFile);
					}

					sw.Write("\n\n\n");
				}
			}
		}

		private static void MeshToFile(Component mf, string folder, string filename)
		{
			Dictionary<string, ObjMaterial> materialList = PrepareFileWrite();

			using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".obj"))
			{
				sw.Write("mtllib ./" + filename + ".mtl\n");

				sw.Write(MeshToString(mf, materialList));
			}

			MaterialsToFile(materialList, folder, filename);
		}

		private static void MeshesToFile(Component[] mf, string folder, string filename, bool useTransform = false)
		{
			Dictionary<string, ObjMaterial> materialList = PrepareFileWrite();

			using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".obj"))
			{
				sw.Write("mtllib ./" + filename + ".mtl\n");

				for (int i = 0; i < mf.Length; i++)
				{
					sw.Write(MeshToString(mf[i], materialList, useTransform: useTransform));
				}
			}

			MaterialsToFile(materialList, folder, filename);
		}

		private static bool CreateTargetFolder()
		{
			try
			{
				System.IO.Directory.CreateDirectory(targetFolder);
			}
			catch
			{
				EditorUtility.DisplayDialog("Error!", "Failed to create target folder!", "");
				return false;
			}

			return true;
		}
		private static bool CreateFolder(string folder)
		{
			try
			{
				System.IO.Directory.CreateDirectory(folder);
			}
			catch
			{
				EditorUtility.DisplayDialog("Error!", "Failed to create folder!", "");
				return false;
			}

			return true;
		}

		[MenuItem("ClothDynamics/OBJ Export/Export all MeshFilters in selection to separate OBJs")]
		static void ExportSelectionToSeparate()
		{
			if (!CreateTargetFolder())
				return;

			Transform[] selection = Selection.GetTransforms(SelectionMode.Editable | SelectionMode.ExcludePrefab);

			if (selection.Length == 0)
			{
				EditorUtility.DisplayDialog("No source object selected!", "Please select one or more target objects", "");
				return;
			}

			int exportedObjects = 0;

			for (int i = 0; i < selection.Length; i++)
			{
				Component[] meshfilter = selection[i].GetComponentsInChildren(typeof(MeshFilter)).Concat(selection[i].GetComponentsInChildren(typeof(SkinnedMeshRenderer))).ToArray();

				for (int m = 0; m < meshfilter.Length; m++)
				{
					exportedObjects++;
					MeshToFile(meshfilter[m], targetFolder, selection[i].name + "_" + i + "_" + m);
				}
			}

			if (exportedObjects > 0)
				EditorUtility.DisplayDialog("Objects exported", "Exported " + exportedObjects + " objects", "");
			else
				EditorUtility.DisplayDialog("Objects not exported", "Make sure at least some of your selected objects have mesh filters!", "");
		}

		[MenuItem("ClothDynamics/OBJ Export/Export whole selection to single OBJ")]
		static void ExportWholeSelectionToSingle()
		{
			if (!CreateTargetFolder())
				return;


			Transform[] selection = Selection.GetTransforms(SelectionMode.Editable | SelectionMode.ExcludePrefab);

			if (selection.Length == 0)
			{
				EditorUtility.DisplayDialog("No source object selected!", "Please select one or more target objects", "");
				return;
			}

			int exportedObjects = 0;

			ArrayList mfList = new ArrayList();

			for (int i = 0; i < selection.Length; i++)
			{
				Component[] meshfilter = selection[i].GetComponentsInChildren(typeof(MeshFilter)).Concat(selection[i].GetComponentsInChildren(typeof(SkinnedMeshRenderer))).ToArray();

				for (int m = 0; m < meshfilter.Length; m++)
				{
					exportedObjects++;
					mfList.Add(meshfilter[m]);
				}
			}

			if (exportedObjects > 0)
			{
				Component[] mf = new Component[mfList.Count];

				for (int i = 0; i < mfList.Count; i++)
				{
					mf[i] = (Component)mfList[i];
				}

				var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
				string filename = activeScene != null ? activeScene.name : "TempScene" + "_" + exportedObjects;

				int stripIndex = filename.LastIndexOf('/');//FIXME: Should be Path.PathSeparator

				if (stripIndex >= 0)
					filename = filename.Substring(stripIndex + 1).Trim();

				MeshesToFile(mf, targetFolder, filename, useTransform: true);


				EditorUtility.DisplayDialog("Objects exported", "Exported " + exportedObjects + " objects to " + filename, "");
			}
			else
				EditorUtility.DisplayDialog("Objects not exported", "Make sure at least some of your selected objects have mesh filters!", "");
		}



		//[MenuItem("ClothDynamics/OBJ Export/Export each selected to single OBJ")]
		static void ExportEachSelectionToSingle()
		{
			if (!CreateTargetFolder())
				return;

			Transform[] selection = Selection.GetTransforms(SelectionMode.Editable | SelectionMode.ExcludePrefab);

			if (selection.Length == 0)
			{
				EditorUtility.DisplayDialog("No source object selected!", "Please select one or more target objects", "");
				return;
			}

			int exportedObjects = 0;


			for (int i = 0; i < selection.Length; i++)
			{
				Component[] meshfilter = selection[i].GetComponentsInChildren(typeof(MeshFilter)).Concat(selection[i].GetComponentsInChildren(typeof(SkinnedMeshRenderer))).ToArray();
				for (int m = 0; m < meshfilter.Length; m++)
				{
					exportedObjects++;
				}
				MeshesToFile(meshfilter, targetFolder, selection[i].name + "_" + i);
			}

			if (exportedObjects > 0)
			{
				EditorUtility.DisplayDialog("Objects exported", "Exported " + exportedObjects + " objects", "");
			}
			else
				EditorUtility.DisplayDialog("Objects not exported", "Make sure at least some of your selected objects have mesh filters!", "");
		}


		private static void BlendShapeToFile(Component mf, string folder, string filename, bool reduceMesh = false)
		{
			Dictionary<string, ObjMaterial> materialList = PrepareFileWrite();

			using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".obj"))
			{
				//sw.Write("mtllib ./" + filename + ".mtl\n");
				sw.Write(MeshToString(mf, materialList, reduceMesh));
			}

			//MaterialsToFile(materialList, folder, filename);
		}

		static Vector3 Rotate(Vector3 v, Vector4 q)
		{
			Vector3 t = 2.0f * Vector3.Cross(q, v);
			return v + q.w * t + Vector3.Cross(q, t);
		}
		static Vector4 QuatToVec(Quaternion rot)
		{
			Vector4 rotVec;
			rotVec.x = rot.x;
			rotVec.y = rot.y;
			rotVec.z = rot.z;
			rotVec.w = rot.w;
			return rotVec;
		}
		//[MenuItem("Custom/Export/ReduceMesh Test")]
		static Mesh ReduceMesh(Mesh pMesh)
		{
			//Transform[] selection = Selection.GetTransforms(SelectionMode.Editable | SelectionMode.ExcludePrefab);
			//Component[] meshfilter = selection[0].GetComponentsInChildren(typeof(MeshFilter)).Concat(selection[0].GetComponentsInChildren(typeof(SkinnedMeshRenderer))).ToArray();

			//var mf = meshfilter[0];

			//Mesh m = null;
			//Material[] mats;
			//if (mf is MeshFilter)
			//{
			//    m = (mf as MeshFilter).mesh;
			//    mats = mf.GetComponent<Renderer>().sharedMaterials;
			//}
			//else if (mf is SkinnedMeshRenderer)
			//{
			//    m = new Mesh();
			//    (mf as SkinnedMeshRenderer).BakeMesh(m);
			//    mats = (mf as SkinnedMeshRenderer).sharedMaterials;
			//}
			//var pMesh = m;
			//if (pMesh == null) return;

			List<List<int>> mapVertsBack = new List<List<int>>();
			List<List<Vector2>> uvsTemp = new List<List<Vector2>>();

			mapVertsBack.Add(new List<int>());
			uvsTemp.Add(new List<Vector2>());


			List<Vector3> normals = new List<Vector3>();

			List<Vector3> verts = new List<Vector3>();
			List<Vector3> uniqueVerts = new List<Vector3>();
			Dictionary<Vector3, int> dictVertsIndex = new Dictionary<Vector3, int>();
			Dictionary<Vector3, List<int>> dictTris = new Dictionary<Vector3, List<int>>();
			int index = 0;
			int globali = 0;
			int offset = 0;

			Vector3[] vertices = pMesh.vertices;
			Vector3[] norm = pMesh.normals;
			Vector2[] uv = pMesh.uv;

			int lastCount = verts.Count;
			int l = 0;

			//var quadVec = QuatToVec(Quaternion.Euler(90, 0, 0));

			for (int i = 0; i < pMesh.vertexCount; i++)
			{
				//vertices[i] *= 100.0f;
				//vertices[i] = Rotate(vertices[i], quadVec);

				verts.Add(vertices[i]);

				if (dictVertsIndex.TryGetValue(vertices[i], out index))
				{
					mapVertsBack[l].Add(index);
					offset++;
				}
				else
				{
					dictVertsIndex.Add(vertices[i], uniqueVerts.Count);
					uniqueVerts.Add(vertices[i]);
					normals.Add(norm[i]);
					uvsTemp[l].Add(uv[i]);
					mapVertsBack[l].Add(globali - offset);
				}
				globali++;
			}

			Debug.Log("verts.Count" + verts.Count);
			Debug.Log("uniqueVerts.Count" + uniqueVerts.Count);

			//var go = new GameObject("TestMesh");
			//var mfilter = go.AddComponent<MeshFilter>();
			//go.AddComponent<MeshRenderer>();

			Mesh newMesh = new Mesh();

			newMesh.SetVertices(uniqueVerts);
			int length = newMesh.subMeshCount = pMesh.subMeshCount;
			for (int i = 0; i < length; i++)
			{
				int[] faces = pMesh.GetTriangles(i);
				List<int> newTris = new List<int>();
				for (int f = 0; f < faces.Length; f += 3)
				{
					newTris.Add(mapVertsBack[l][faces[f + 0]]);
					newTris.Add(mapVertsBack[l][faces[f + 1]]);
					newTris.Add(mapVertsBack[l][faces[f + 2]]);
				}
				newMesh.SetTriangles(newTris.ToArray(), i);
			}
			newMesh.SetNormals(normals);
			newMesh.SetUVs(0, uvsTemp[l]);
			newMesh.RecalculateBounds();
			//mfilter.sharedMesh = newMesh;
			return newMesh;
		}


		//[MenuItem("ClothDynamics/OBJ Export/Export each selected to single OBJ with all blend shapes")]
		static void ExportEachSelectionToSingleWithBlendShapes()
		{
			ExportEachSelectionToSingleWithBlendShapes(false, false);
		}

		//[MenuItem("ClothDynamics/OBJ Export/Export each selected to single OBJ with current blend shape")]
		static void ExportEachSelectionToSingleWithCurrentBlendShape()
		{
			ExportEachSelectionToSingleWithBlendShapes(false, true);
		}

		//[MenuItem("ClothDynamics/OBJ Export/Export each selected to single OBJ with all blend shapes (Remove double vertices)")]
		//static void ExportEachSelectionToSingleWithBlendShapesReduceMesh()
		//{
		//    ExportEachSelectionToSingleWithBlendShapes(true, false);
		//}

		static void ExportEachSelectionToSingleWithBlendShapes(bool reduceMesh, bool useCurrent)
		{
			if (!CreateTargetFolder())
				return;

			Transform[] selection = Selection.GetTransforms(SelectionMode.Editable | SelectionMode.ExcludePrefab);

			if (selection.Length == 0)
			{
				EditorUtility.DisplayDialog("No source object selected!", "Please select one or more target objects", "");
				return;
			}

			int exportedObjects = 0;


			for (int i = 0; i < selection.Length; i++)
			{
				Component[] meshfilter = selection[i].GetComponentsInChildren(typeof(MeshFilter)).Concat(selection[i].GetComponentsInChildren(typeof(SkinnedMeshRenderer))).ToArray();
				for (int m = 0; m < meshfilter.Length; m++)
				{
					exportedObjects++;
					var mf = meshfilter[m];
					if (mf.GetType() == typeof(SkinnedMeshRenderer))
					{
						var skin = (SkinnedMeshRenderer)mf as SkinnedMeshRenderer;

						//var cm = mf.transform.parent.parent.GetComponent<M3DCharacterManager>();
						//var morphs = cm.coreMorphs.morphs;

						//int length = morphs.Count;
						//for (int b = 0; b < length; b++)
						//{
						//    //morphs[b].value = 0;
						//    morphs[b].attached = false;
						//}
						//cm.coreMorphs.AttachMorphs(morphs.ToArray(), true);

						//cm.SyncAllBlendShapes();


						//Debug.Log("morphs.Count " + length);
						var mesh = skin.sharedMesh;
						var count = mesh.blendShapeCount;

						//Debug.Log("blendShapeCount " + count);
						var objectName = selection[i].name;//.Split (new char[]{ '.' }) [0];

						if (useCurrent)
						{
							BlendShapeToFile(mf, targetFolder, objectName + "_current", reduceMesh);
						}
						else
						{
							for (int b = 0; b < count; b++)
							{
								skin.SetBlendShapeWeight(b, 0);
							}
							var folder = targetFolder + "/" + objectName + "_blendShapes";
							if (!CreateFolder(folder))
								return;
							for (int b = 0; b < count; b++)
							{
								skin.SetBlendShapeWeight(b, 100);

								BlendShapeToFile(mf, folder, mesh.GetBlendShapeName(b), reduceMesh);
								skin.SetBlendShapeWeight(b, 0);
							}
						}

					}
				}
			}

			if (exportedObjects > 0)
			{
				EditorUtility.DisplayDialog("Objects exported", "Exported " + exportedObjects + " objects", "");
			}
			else
				EditorUtility.DisplayDialog("Objects not exported", "Make sure at least some of your selected objects have mesh filters!", "");
		}
	}
}