using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
	public class TransferVertexColors : Editor
	{
		[MenuItem("ClothDynamics/Transfer Vertex Colors")]
		public static void TransferColors()
		{
			var objs = Selection.gameObjects;
			var source = Selection.activeGameObject;
			MeshFilter smf = null;
			SkinnedMeshRenderer sSkin = null;
			if (source != null && ((sSkin = source.GetComponent<SkinnedMeshRenderer>()) != null) || (smf = source.GetComponent<MeshFilter>()) != null)
			{
				var sharedMesh = sSkin != null ? sSkin.sharedMesh : smf != null ? smf.sharedMesh : null;
				//sSkin.BakeMesh(sharedMesh);

				if (sharedMesh != null && objs != null && objs.Length > 0 && sharedMesh.colors != null && sharedMesh.colors.Length > 0)
				{
					if (EditorUtility.DisplayDialog("Transfer Vertex Colors", "Do you want to transfer the vertex colors from \"" + source.name + "\" to  \"" + (source == objs[0] ? objs[1].name : objs[0].name) + "\" ?", "Yes", "No"))
					{
						KDTree tree = new KDTree(3);
						var sVerts = sharedMesh.vertices;
						var sColors = sharedMesh.colors;
						var doubles = new HashSet<Vector3>();
						var transform = sSkin != null ? sSkin.transform : smf != null ? smf.transform : null;

						for (int i = 0; i < sVerts.Length; i++)
						{
							var pos = transform.TransformPoint(sVerts[i]);
							if (doubles.Add(pos))
								tree.insert(new double[] { pos.x, pos.y, pos.z }, i);
						}
						var sPaint = transform.GetComponent<PaintObject>();

						foreach (var item in objs)
						{
							if (item == source) continue;

							var mfs = item.GetComponentsInChildren<MeshFilter>().Select(x => x.transform).ToList();
							mfs.AddRange(item.GetComponentsInChildren<SkinnedMeshRenderer>().Select(x => x.transform));
							foreach (var m in mfs)
							{
								var mesh = m.GetComponent<MeshFilter>() ? m.GetComponent<MeshFilter>().sharedMesh : m.GetComponent<SkinnedMeshRenderer>().sharedMesh;
								//m.GetComponent<SkinnedMeshRenderer>().BakeMesh(mesh);
								if (mesh != null)
								{
									var tr = m;
									var vCount = mesh.vertexCount;
									var verts = mesh.vertices;
									var vColors = mesh.colors;
									if (vColors.Length == 0) vColors = new Color[vCount];
									var paint = tr.GetComponent<PaintObject>();
									if (paint != null && paint.vertexColors.Length != vColors.Length) paint.vertexColors = new Color[vColors.Length];
									for (int i = 0; i < vColors.Length; i++)
									{
										var pos = tr.TransformPoint(verts[i]);
										var id = tree.nearest(new double[] { pos.x, pos.y, pos.z });
										if (id != null)
										{
											int index = (int)id;
											vColors[i] = sColors[index];
											if (paint != null && sPaint != null) paint.vertexColors[i] = sPaint.vertexColors[index];
										}
									}
									mesh.colors = vColors;
								}
							}
						}
					}
				}
				else
					EditorUtility.DisplayDialog("Transfer Vertex Colors", "Source SharedMesh or vertex colors not found! Did you select at least two objects and does the source has vertex colors applied?", "OK");
			}
			else
				EditorUtility.DisplayDialog("Transfer Vertex Colors", "Select a destination and source GameObject to make a vertex colors transfer!", "OK");
		}
	}
}