using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace ClothDynamics
{
	//[DefaultExecutionOrder(-100)]
	public class AutomaticSkinning : MonoBehaviour
	{
		[Tooltip("This will be added automatically if you applied a AutomaticBoneSpheres script to the parent Animator object.")]
		[SerializeField] private AutomaticBoneSpheres _abs;
		[Tooltip("This is the start search radius for the skinning algorithm, it will grow by a multiple of 2 over time until the required bones were found.")]
		[SerializeField] private float _skinRadiusDetail = 0.1f;
		[Range(0, 1)]
		[Tooltip("This is the percentual length of a bone to start blending to the next bone.")]
		[SerializeField] private float _blendLength = 0.2f;
		[Tooltip("This pushes the mesh vertices in normal direction.")]
		[SerializeField] private float _pushVertsByNormals = 0.0f;
		[Tooltip("This welds the mesh vertices.")]
		[SerializeField] private bool _weldVertices = false;
		[Tooltip("This will write the vertex changes to the shared mesh, if you reimport the mesh the changes are gone.(Needed for editor script)")]
		[SerializeField] public bool _saveNewVertsToMesh = false;
		[Tooltip("This is for children bones that are connected with the parent bone. They can be used if more than 2 bones are connected, however weighting should be low or zero.")]
		[SerializeField] private float _childrenWeight = 0.1f;
		[Tooltip("This is for parents bones that are connected with the child bone. They can be used if more than 2 bones are connected, however weighting should be low or zero.")]
		[SerializeField] private float _parentsWeight = 0.001f;
		[Tooltip("This is the global radius for the skinning range and should be set to 1.")]
		[SerializeField] private float _skinRadius = 1.0f;
		private KDTree treeBones;

		[SerializeField] private BoneWeight[] _boneWeightsSaved;
		[SerializeField] private Matrix4x4[] _bindposesSaved;
		[SerializeField] private Transform[] _bonesSaved;


		public void OnEnable()
		{
			if (this.GetComponent<SkinnedMeshRenderer>() == null)
			{
				this.gameObject.AddComponent<SkinnedMeshRenderer>();
			}
			else
			{
				Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " -> SkinnedMeshRenderer already exists, please only use AutomaticSkinning if it's not possible for you to create your own skinning.</color>");
				ConvertSkinnedToMeshRenderer(false);
			}
			SkinnedMeshRenderer rend = GetComponent<SkinnedMeshRenderer>();

			_abs = GetComponentInParent<AutomaticBoneSpheres>();

			if (_abs == null)
			{
				Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " -> AutomaticSkinning works only with the AutomaticBoneSpheres script, please add one to your root character GameObject.</color>");
				return;
			}
			var boneTransforms = _abs.transforms;
			var bones = _abs._bones.ToArray();//.Select(x => x.first).ToArray();
			var spheres = _abs._spheresList.ToArray();//.Select(x => x.first).ToArray();

			Mesh mesh = _saveNewVertsToMesh ? this.GetComponent<MeshFilter>().sharedMesh : this.GetComponent<MeshFilter>().mesh;
			if (_pushVertsByNormals != 0) mesh.RecalculateNormals();
			var vertices = mesh.vertices;
			var normals = mesh.normals;
			if (_weldVertices) { GPUClothDynamics.WeldVerticesOld(mesh, out _); vertices = mesh.vertices; normals = mesh.normals; }
			if (_pushVertsByNormals != 0)
			{
				for (int i = 0; i < vertices.Length; i++)
				{
					if (i < normals.Length)
						vertices[i] += normals[i] * _pushVertsByNormals;
				}
				mesh.vertices = vertices;
			}

			BoneWeight[] weights = new BoneWeight[vertices.Length];

			treeBones = new KDTree(3);
			HashSet<Vector3> tempVerts = new HashSet<Vector3>();
			for (int n = 0; n < boneTransforms.Length; n++)
			{
				var pos = this.transform.InverseTransformPoint(boneTransforms[n].position);
				if (tempVerts.Add(pos))
					treeBones.insert(new double[] { pos.x, pos.y, pos.z }, n);
			}
			tempVerts.Clear();

			HashSet<int> neighbours = new HashSet<int>();
			for (int i = 0; i < weights.Length; i++)
			{
				var pos = vertices[i];
				neighbours.Clear();
				treeBones.nearestAddNbrs(new double[] { pos.x, pos.y, pos.z }, 4, ref neighbours);

				int count = neighbours.Count;

				for (int n = 0; n < count; n++)
				{
					var neighbourId = neighbours.ElementAt(n);
					float dist = math.distance(vertices[i], this.transform.InverseTransformPoint(boneTransforms[neighbourId].position));
					//if (dist < _skinRadius)
					{
						if (n == 0)
						{
							weights[i].boneIndex0 = neighbourId;
							weights[i].weight0 = math.saturate(dist / _skinRadius);
						}
						else if (n == 1)
						{
							weights[i].boneIndex1 = neighbourId;
							weights[i].weight1 = math.saturate(dist / _skinRadius);
						}
						else if (n == 2)
						{
							weights[i].boneIndex2 = neighbourId;
							weights[i].weight2 = math.saturate(dist / _skinRadius);
						}
						else if (n == 3)
						{
							weights[i].boneIndex3 = neighbourId;
							weights[i].weight3 = math.saturate(dist / _skinRadius);
						}
					}
				}

				var allWeightsPerBone = math.max(1, weights[i].weight0 + weights[i].weight1 + weights[i].weight2 + weights[i].weight3);
				weights[i].weight0 /= allWeightsPerBone;
				weights[i].weight1 /= allWeightsPerBone;
				weights[i].weight2 /= allWeightsPerBone;
				weights[i].weight3 /= allWeightsPerBone;


				//float dist = math.distance(vertices[i], this.transform.InverseTransformPoint(bones[n].first.position));
				//if (dist < _skinRadius)
				//{
				//    weights[i].boneIndex0 = System.Array.IndexOf(boneTransforms, bones[n].first);
				//    weights[i].weight0 = math.saturate((dist / _skinRadius) - weights[i].weight1);
				//}

				//dist = math.distance(vertices[i], this.transform.InverseTransformPoint(bones[n].second.position));
				//if (dist < _skinRadius)
				//{
				//    weights[i].boneIndex1 = System.Array.IndexOf(boneTransforms, bones[n].second);
				//    weights[i].weight1 = math.saturate((dist / _skinRadius) - weights[i].weight0);
				//}
			}

			treeBones = new KDTree(3);
			for (int n = 0; n < spheres.Length; n++)
			{
				var data = spheres[n];
				if (data.boneId < bones.Length)
				{
					var bone = bones[data.boneId].first;
					var spherePos = bone.position + (Vector3)Rotate(QuatToVec(bone.rotation), data.offset.xyz);
					//Gizmos.DrawWireSphere(spherePos, data.offset.w);
					var pos = this.transform.InverseTransformPoint(spherePos);

					var key = new double[] { pos.x, pos.y, pos.z };

					bool doubleFound = false;
					if (treeBones.m_count > 0)
					{
						var id = treeBones.nearest(key);
						if (id != null)
						{
							int idNum = (int)id;
							data = spheres[idNum];
							bone = bones[data.boneId].first;
							spherePos = bone.position + (Vector3)Rotate(QuatToVec(bone.rotation), data.offset.xyz);
							var pos2 = this.transform.InverseTransformPoint(spherePos);
							if (math.distancesq(pos, pos2) < float.Epsilon)
								doubleFound = true;
						}
					}
					if (!doubleFound) treeBones.insert(key, n);
				}
			}

			for (int i = 0; i < weights.Length; i++)
			{
				var pos = vertices[i];
				neighbours.Clear();
				treeBones.nearestAddNbrs(new double[] { pos.x, pos.y, pos.z }, 4, ref neighbours);

				int foundNeighbours = 0;

				var radius = _skinRadiusDetail;
				foundNeighbours = SetWeights(radius, boneTransforms, bones, spheres, vertices, weights, neighbours, i, foundNeighbours);
				int saveCount = 0;
				while (foundNeighbours < 4)
				{
					radius *= 2;
					foundNeighbours = SetWeights(radius, boneTransforms, bones, spheres, vertices, weights, neighbours, i, foundNeighbours);
					saveCount++;
					if (saveCount > 4) break;
				}
			}

			mesh.boneWeights = weights;

			Matrix4x4[] bindPoses = new Matrix4x4[boneTransforms.Length];
			for (int i = 0; i < boneTransforms.Length; i++)
			{
				bindPoses[i] = boneTransforms[i].worldToLocalMatrix * transform.localToWorldMatrix;

			}
			mesh.bindposes = bindPoses;

			rend.bones = boneTransforms;
			rend.sharedMesh = mesh;

			if (Mf != null)
				DestroyImmediate(Mf);
		}

		private int SetWeights(float radius, Transform[] boneTransforms, AutomaticBoneSpheres.TransformPair[] bones, AutomaticBoneSpheres.SphereStruct[] spheres, Vector3[] vertices, BoneWeight[] weights, HashSet<int> neighbours, int i, int foundNeighbours)
		{
			int count = neighbours.Count;
			float lastDist = float.MaxValue;
			for (int n = 0; n < count; n++)
			{
				var neighbourId = neighbours.ElementAt(n);

				var data = spheres[neighbourId];
				var boneFirst = bones[data.boneId].first;
				var spherePos = boneFirst.position + (Vector3)Rotate(QuatToVec(boneFirst.rotation), data.offset.xyz);
				float dist = math.distance(vertices[i], this.transform.InverseTransformPoint(spherePos));

				//float dist = math.distance(vertices[i], this.transform.InverseTransformPoint(boneTransforms[neighbourId].position));
				if (dist < radius && dist < lastDist)
				{
					lastDist = dist;
					//int nId = 0;
					//if (math.distancesq(boneFirst.position, spherePos) < math.distancesq(bones[data.boneId].second.position, spherePos))
					//{
					//    nId = System.Array.IndexOf(boneTransforms, boneFirst);
					//}
					//else
					//{
					//    nId = System.Array.IndexOf(boneTransforms, bones[data.boneId].second);
					//}

					////nId = System.Array.IndexOf(boneTransforms, boneTransforms.OrderBy(x => math.distancesq(x.position, spherePos)).First());
					////nId = System.Array.IndexOf(boneTransforms, boneFirst);
					//if (n == 0)
					//{
					//    weights[i].boneIndex0 = nId;
					//    weights[i].weight0 = math.saturate(dist / radius);
					//}
					//else if (n == 1)
					//{
					//    weights[i].boneIndex1 = nId;
					//    weights[i].weight1 = math.saturate(dist / radius);
					//}
					//else if (n == 2)
					//{
					//    weights[i].boneIndex2 = nId;
					//    weights[i].weight2 = math.saturate(dist / radius);
					//}
					//else if (n == 3)
					//{
					//    weights[i].boneIndex3 = nId;
					//    weights[i].weight3 = math.saturate(dist / radius);
					//}

					var bDist = math.distance(boneFirst.position, bones[data.boneId].second.position);

					dist = math.distance(this.transform.InverseTransformPoint(boneFirst.position), vertices[i]);

					float normDist = math.saturate(dist / bDist);

					if (normDist < _blendLength)
					{
						if (boneFirst.parent != null)
							weights[i].boneIndex0 = System.Array.IndexOf(boneTransforms, boneFirst.parent);
						else weights[i].boneIndex0 = System.Array.IndexOf(boneTransforms, boneFirst);
						weights[i].weight0 = 0.5f - normDist / (_blendLength * 2);

						weights[i].boneIndex1 = System.Array.IndexOf(boneTransforms, boneFirst);
						weights[i].weight1 = 0.5f + normDist / (_blendLength * 2);


					}
					else if (normDist > (1 - _blendLength))
					{
						weights[i].boneIndex0 = System.Array.IndexOf(boneTransforms, boneFirst);
						weights[i].weight0 = 0.5f + (1 - normDist) / (_blendLength * 2);

						weights[i].boneIndex1 = System.Array.IndexOf(boneTransforms, bones[data.boneId].second);
						weights[i].weight1 = 0.5f - (1 - normDist) / (_blendLength * 2);
					}
					else
					{
						weights[i].boneIndex0 = System.Array.IndexOf(boneTransforms, boneFirst);
						weights[i].weight0 = 1;

						weights[i].boneIndex1 = System.Array.IndexOf(boneTransforms, bones[data.boneId].second);
						weights[i].weight1 = 0;

					}

					weights[i].weight2 = 0;
					weights[i].weight3 = 0;
					bool w1 = false, w2 = false;
					if (boneFirst.childCount > 1)
					{
						var child1 = boneFirst.GetChild(0);
						for (int c = 0; c < boneFirst.childCount; c++)
						{
							child1 = boneFirst.GetChild(c);
							if (child1 != bones[data.boneId].second) break;
						}
						if (child1 != bones[data.boneId].second && boneTransforms.Contains(child1))
						{
							weights[i].boneIndex2 = System.Array.IndexOf(boneTransforms, child1);
							weights[i].weight2 = _childrenWeight * (_skinRadius - math.clamp(math.distance(this.transform.InverseTransformPoint(child1.position), vertices[i]), 0, _skinRadius)) / _skinRadius;
							w1 = true;
						}

						var child2 = boneFirst.GetChild(0);
						for (int c = 0; c < boneFirst.childCount; c++)
						{
							child2 = boneFirst.GetChild(c);
							if (child2 != bones[data.boneId].second && child2 != child1) break;
						}
						if (child2 != bones[data.boneId].second && child2 != child1 && boneTransforms.Contains(child2))
						{
							weights[i].boneIndex3 = System.Array.IndexOf(boneTransforms, child2);
							weights[i].weight3 = _childrenWeight * (_skinRadius - math.clamp(math.distance(this.transform.InverseTransformPoint(child2.position), vertices[i]), 0, _skinRadius)) / _skinRadius;
							w2 = true;
						}
					}

					var parent = boneFirst.parent;
					if (parent != null && parent.childCount > 1)
					{
						var child1 = parent.GetChild(0);
						for (int c = 0; c < parent.childCount; c++)
						{
							child1 = parent.GetChild(c);
							if (child1 != boneFirst) break;
						}
						if (!w1 && child1 != boneFirst && boneTransforms.Contains(child1))
						{
							weights[i].boneIndex2 = System.Array.IndexOf(boneTransforms, child1);
							weights[i].weight2 = _parentsWeight * (_skinRadius - math.clamp(math.distance(this.transform.InverseTransformPoint(child1.position), vertices[i]), 0, _skinRadius)) / _skinRadius;
						}

						var child2 = parent.GetChild(0);
						for (int c = 0; c < parent.childCount; c++)
						{
							child2 = parent.GetChild(c);
							if (child2 != boneFirst && child2 != child1) break;
						}
						if (!w2 && child2 != boneFirst && child2 != child1 && boneTransforms.Contains(child2))
						{
							weights[i].boneIndex3 = System.Array.IndexOf(boneTransforms, child2);
							weights[i].weight3 = _parentsWeight * (_skinRadius - math.clamp(math.distance(this.transform.InverseTransformPoint(child2.position), vertices[i]), 0, _skinRadius)) / _skinRadius;
						}
					}
					//foundNeighbours++;
					foundNeighbours += 4;
				}
			}
			var allWeightsPerVertex = math.max(1, weights[i].weight0 + weights[i].weight1 + weights[i].weight2 + weights[i].weight3);
			weights[i].weight0 /= allWeightsPerVertex;
			weights[i].weight1 /= allWeightsPerVertex;
			weights[i].weight2 /= allWeightsPerVertex;
			weights[i].weight3 /= allWeightsPerVertex;

			return foundNeighbours;
		}

		float4 QuatToVec(Quaternion rot)
		{
			float4 rotVec;
			rotVec.x = rot.x;
			rotVec.y = rot.y;
			rotVec.z = rot.z;
			rotVec.w = rot.w;
			return rotVec;
		}

		float3 Rotate(float4 q, float3 v)
		{
			float3 t = 2.0f * math.cross(q.xyz, v);
			return v + q.w * t + math.cross(q.xyz, t); //changed q.w to -q.w;
		}

		public void ConvertSkinnedToMeshRenderer(bool smrDelete = true)
		{
			if (Smr)
				Mf.sharedMesh = Smr.sharedMesh;
			Material[] materials = Smr ? Smr.sharedMaterials : Mr.sharedMaterials;

			if (Smr)
			{
				_boneWeightsSaved = Smr.sharedMesh.boneWeights;
				_bindposesSaved = Smr.sharedMesh.bindposes;
				_bonesSaved = Smr.bones;
				Smr.enabled = false;
			}
			if (smrDelete && GetComponent<SkinnedMeshRenderer>())
				DestroyImmediate(GetComponent<SkinnedMeshRenderer>());

			Mr.sharedMaterials = materials;

			this.enabled = true;
		}

		public void RestoreSkinnedToMeshRenderer()
		{
			Material[] savedMats = Mr != null ? Mr.sharedMaterials : null;
			if (!Smr) this.gameObject.AddComponent<SkinnedMeshRenderer>();

			if (Smr)
			{
				if (savedMats != null && savedMats.Length > 0) Smr.sharedMaterials = savedMats;
				else Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " -> shared materials do not exist.</color>");
				if (_boneWeightsSaved != null && _boneWeightsSaved.Length > 0) Smr.sharedMesh.boneWeights = _boneWeightsSaved;
				else Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " -> saved bone weights do not exist.</color>");
				if (_bindposesSaved != null && _bindposesSaved.Length > 0) Smr.sharedMesh.bindposes = _bindposesSaved;
				else Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " -> saved bindposes do not exist.</color>");
				if (_bonesSaved != null && _bonesSaved.Length > 0) Smr.bones = _bonesSaved;
				else Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " -> saved bones do not exist.</color>");
				Smr.enabled = true;
			}
			this.enabled = false;
		}

		MeshFilter Mf
		{
			get
			{
				if (_mf == null)
				{
					_mf = GetComponent<MeshFilter>();
					if (_mf == null)
					{
						_mf = gameObject.AddComponent<MeshFilter>();
						if (Smr) _mf.sharedMesh = Smr.sharedMesh;
					}

				}

				return _mf;
			}
		}
		MeshFilter _mf;

		MeshRenderer Mr
		{
			get
			{
				if (_mr == null)
				{
					_mr = GetComponent<MeshRenderer>();
					if (_mr == null)
					{
						_mr = gameObject.AddComponent<MeshRenderer>();
					}
				}

				return _mr;
			}
		}
		MeshRenderer _mr;

		SkinnedMeshRenderer Smr
		{
			get
			{
				if (_smr == null)
				{
					_smr = GetComponent<SkinnedMeshRenderer>();
				}

				return _smr;
			}
		}
		SkinnedMeshRenderer _smr;

		//private bool CheckIfParentOrChildOf(Transform main, Transform toCheck, int levels = 2)
		//{
		//    var m = main;
		//    for (int i = 0; i < levels; i++)
		//    {
		//        if (m.parent == toCheck)
		//            return true;
		//        m = m.parent;
		//        if (m == null) break;
		//    }
		//    m = main;
		//    CheckChildrenDepth(m, toCheck, levels, 0);

		//    return false;
		//}

		//private bool CheckChildrenDepth(Transform root, Transform toCheck, int levels, int depth)
		//{
		//    var children = root.GetComponentsInChildren<Transform>();
		//    foreach (var c in children)
		//    {
		//        if (c.parent == root)
		//        {
		//            if (toCheck.IsChildOf(root))
		//                return true;
		//            if (depth < levels) return CheckChildrenDepth(c, toCheck, levels, depth + 1);
		//        }
		//    }
		//    return false;
		//}

	}
}