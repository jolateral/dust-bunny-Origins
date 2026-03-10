using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace ClothDynamics
{
	/// <summary>
	/// Replaces Unity's default linear skinning with DQ skinning
	/// 
	/// Add this component to a <a class="bold" href="https://docs.unity3d.com/ScriptReference/GameObject.html">GameObject</a> that has <a class="bold" href="https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.html">SkinnedMeshRenderer</a> attached.<br>
	/// Do not remove <a class="bold" href="https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.html">SkinnedMeshRenderer</a> component!<br>
	/// Make sure that all materials of the animated object are using shader \"<b>MadCake/Material/Standard hacked for DQ skinning</b>\"
	/// </summary>
	//[RequireComponent(typeof(MeshFilter))]
	[DefaultExecutionOrder(15100)] //When using Final IK
	public class DualQuaternionSkinner : GPUSkinnerBase
	{
		/// <summary>
		/// Bone orientation is required for bulge-compensation.<br>
		/// Do not set directly, use custom editor instead.
		/// </summary>
		public Vector3 boneOrientationVector = Vector3.up;

		/// <summary>
		/// Do not edit directly, use SetViewFrustrumCulling() instead.
		/// </summary>
		public bool viewFrustrumCulling = true;

		public bool _recalculateNormals = true;

		[Range(2, 8)]
		public int _vertexIdsToUvId = 2;

		struct VertexInfo
		{
			// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
			// https://developer.nvidia.com/content/understanding-structured-buffer-performance

			public Vector4 position;
			public Vector4 normal;
			public Vector4 tangent;

			public int boneIndex0;
			public int boneIndex1;
			public int boneIndex2;
			public int boneIndex3;

			public float weight0;
			public float weight1;
			public float weight2;
			public float weight3;

			public float compensation_coef;
		}

		struct MorphDelta
		{
			// could use float3 instead of float4 but NVidia says structures not aligned to 128 bits are slow
			// https://developer.nvidia.com/content/understanding-structured-buffer-performance

			public Vector4 position;
			public Vector4 normal;
			public Vector4 tangent;
		}

		struct DualQuaternion
		{
			public Quaternion rotationQuaternion;
			public Vector4 position;
		}

		const int numthreads = 1024;    // must be same in compute shader code
		internal int _textureWidth = 1024;  // no need to adjust compute shaders
		internal int _textureHeight;

		/// <summary>
		/// Adjusts the amount of bulge-compensation.
		/// </summary>
		[Range(0, 1)]
		public float bulgeCompensation = 0;

		public ComputeShader shaderComputeBoneDQ;
		public ComputeShader shaderDQBlend;
		public ComputeShader shaderApplyMorph;
		//public Shader _shader;

		/// <summary>
		/// Indicates whether DualQuaternionSkinner is currently active.
		/// </summary>
		public bool started { get; private set; } = false;

		Matrix4x4[] poseMatrices;

		ComputeBuffer bufPoseMatrices;
		ComputeBuffer bufSkinnedDq;
		ComputeBuffer bufBindDq;

		ComputeBuffer bufVertInfo;
		ComputeBuffer bufMorphTemp_1;
		ComputeBuffer bufMorphTemp_2;

		ComputeBuffer bufBoneDirections;

		ComputeBuffer[] arrBufMorphDeltas;

		float[] morphWeights;

		NativeArray<Matrix4x4> _nativeMatrices;

		int _vertexCount;

		WaitForSeconds waitForSeconds = new WaitForSeconds(1);

		MeshFilter mf
		{
			get
			{
				if (this._mf == null)
				{
					this._mf = this.GetComponent<MeshFilter>();
					if (this._mf == null)
					{
						this._mf = this.gameObject.AddComponent<MeshFilter>();
					}
				}

				return this._mf;
			}
		}
		MeshFilter _mf;

		MeshRenderer mr
		{
			get
			{
				if (this._mr == null)
				{
					this._mr = this.GetComponent<MeshRenderer>();
					if (this._mr == null)
					{
						this._mr = this.gameObject.AddComponent<MeshRenderer>();
					}
				}

				return this._mr;
			}
		}
		MeshRenderer _mr;

		SkinnedMeshRenderer smr
		{
			get
			{
				if (this._smr == null)
				{
					this._smr = this.GetComponent<SkinnedMeshRenderer>();
				}

				return this._smr;
			}
		}
		SkinnedMeshRenderer _smr;

		MaterialPropertyBlock materialPropertyBlock;

		Transform[] bones;
		Matrix4x4[] bindPoses;

		/*
            Vulkan and OpenGL only support ComputeBuffer in compute shaders
            passing data to the vertex and fragment shaders is done through RenderTextures

            using ComputeBuffers would improve the efficiency slightly but it would only work with Dx11

            layout is as such:
                rtSkinnedData_1			float4			vertex.xyz,	normal.x
                rtSkinnedData_2			float4			normal.yz,	tangent.xy
                rtSkinnedData_3			float2			tangent.zw
        */
		internal RenderTexture _rtSkinnedData_1;
		internal RenderTexture _rtSkinnedData_2;
		internal RenderTexture _rtSkinnedData_3;

		int kernelHandleComputeBoneDQ;
		int kernelHandleDQBlend;
		int kernelHandleApplyMorph;
		TransformAccessArray _transBones;

		public void SetViewFrustrumCulling(bool viewFrustrumculling)
		{
			if (this.viewFrustrumCulling == viewFrustrumculling)
				return;

			this.viewFrustrumCulling = viewFrustrumculling;

			if (this.started == true)
				UpdateViewFrustrumCulling();
		}

		void UpdateViewFrustrumCulling()
		{
			if (this.viewFrustrumCulling)
				this.mf.sharedMesh.bounds = this.smr != null ? this.smr.localBounds : this.mr.bounds;
			else
				this.mf.sharedMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000000);
		}

		/// <summary>
		/// Returns an array of currently applied blend shape weights.<br>
		/// Default range is 0-100.<br>
		/// It is possible to apply negative weights or exceeding 100.
		/// </summary>
		/// <returns>Array of currently applied blend shape weights</returns>
		public float[] GetBlendShapeWeights()
		{
			float[] weights = new float[this.morphWeights.Length];
			for (int i = 0; i < weights.Length; i++)
			{
				weights[i] = this.morphWeights[i];
			}

			return weights;
		}

		/// <summary>
		/// Applies blend shape weights from the given array.<br>
		/// Default range is 0-100.<br>
		/// It is possible to apply negative weights or exceeding 100.
		/// </summary>
		/// <param name="weights">An array of weights to be applied</param>
		public void SetBlendShapeWeights(float[] weights)
		{
			if (weights.Length != this.morphWeights.Length)
			{
				throw new System.ArgumentException(
					"An array of weights must contain the number of elements " +
					$"equal to the number of available blendshapes. Currently " +
					$"{this.morphWeights.Length} blendshapes ara available but {weights.Length} weights were passed."
				);
			}

			for (int i = 0; i < weights.Length; i++)
			{
				this.morphWeights[i] = weights[i];
			}

			this.ApplyMorphs();
		}

		/// <summary>
		/// Set weight for the blend shape with given index.<br>
		/// Default range is 0-100.<br>
		/// It is possible to apply negative weights or exceeding 100.
		/// </summary>
		/// <param name="index">Index of the blend shape</param>
		/// <param name="weight">Weight to be applied</param>
		public void SetBlendShapeWeight(int index, float weight)
		{
			if (this.started == false)
			{
				this.GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(index, weight);
				return;
			}

			if (index < 0 || index >= this.morphWeights.Length)
			{
				throw new System.IndexOutOfRangeException("Blend shape index out of range");
			}

			this.morphWeights[index] = weight;

			this.ApplyMorphs();
		}

		/// <summary>
		/// Returns currently applied weight for the blend shape with given index.<br>
		/// Default range is 0-100.<br>
		/// It is possible to apply negative weights or exceeding 100.
		/// </summary>
		/// <param name="index">Index of the blend shape</param>
		/// <returns>Currently applied weight</returns>
		public float GetBlendShapeWeight(int index)
		{
			if (this.started == false)
			{
				return this.GetComponent<SkinnedMeshRenderer>().GetBlendShapeWeight(index);
			}

			if (index < 0 || index >= this.morphWeights.Length)
			{
				throw new System.IndexOutOfRangeException("Blend shape index out of range");
			}

			return this.morphWeights[index];
		}

		/// <summary>
		/// UnityEngine.<a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh.html">Mesh</a> that is currently being rendered.
		/// @see <a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh.GetBlendShapeName.html">Mesh.GetBlendShapeName(int shapeIndex)</a>
		/// @see <a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh.GetBlendShapeIndex.html">Mesh.GetBlendShapeIndex(string blendShapeName)</a>
		/// @see <a class="bold" href="https://docs.unity3d.com/ScriptReference/Mesh-blendShapeCount.html">Mesh.blendShapeCount</a>
		/// </summary>
		public Mesh mesh
		{
			get
			{
				if (this.started == false)
				{
					return this.smr.sharedMesh;
				}

				return this.mf.sharedMesh;
			}
		}

		/// <summary>
		/// If the value of boneOrientationVector was changed while DualQuaternionSkinner is active (started == true), UpdatePerVertexCompensationCoef() must be called in order for the change to take effect.
		/// </summary>
		public void UpdatePerVertexCompensationCoef()
		{
			if (this.started == false)
			{
				return;
			}

			var vertInfos = new VertexInfo[this.mf.sharedMesh.vertexCount];
			this.bufVertInfo.GetData(vertInfos);

			for (int i = 0; i < vertInfos.Length; i++)
			{
				Matrix4x4 bindPose = this.bindPoses[vertInfos[i].boneIndex0].inverse;
				Quaternion boneBindRotation = bindPose.ExtractRotation();
				Vector3 boneDirection = boneBindRotation * this.boneOrientationVector;  // ToDo figure out bone orientation
				Vector3 bonePosition = bindPose.ExtractPosition();
				Vector3 toBone = bonePosition - (Vector3)vertInfos[i].position;

				vertInfos[i].compensation_coef = Vector3.Cross(toBone, boneDirection).magnitude;
			}

			this.bufVertInfo.SetData(vertInfos);
			this.ApplyMorphs();
		}

		void GrabMeshFromSkinnedMeshRenderer()
		{
			this.ReleaseBuffers();

			if (this.smr != null) this.mf.sharedMesh = this.smr.sharedMesh;
			this.bindPoses = this.mf.sharedMesh.bindposes;

			var bsCount = this.mf.sharedMesh.blendShapeCount;
			this.arrBufMorphDeltas = new ComputeBuffer[bsCount];

			this.morphWeights = new float[bsCount];

			var deltaVertices = new Vector3[this.mf.sharedMesh.vertexCount];
			var deltaNormals = new Vector3[this.mf.sharedMesh.vertexCount];
			var deltaTangents = new Vector3[this.mf.sharedMesh.vertexCount];

			var deltaVertInfos = new MorphDelta[this.mf.sharedMesh.vertexCount];

			for (int i = 0; i < bsCount; i++)
			{
				this.mf.sharedMesh.GetBlendShapeFrameVertices(i, 0, deltaVertices, deltaNormals, deltaTangents);

				this.arrBufMorphDeltas[i] = new ComputeBuffer(this.mf.sharedMesh.vertexCount, sizeof(float) * 12);

				for (int k = 0; k < this.mf.sharedMesh.vertexCount; k++)
				{
					deltaVertInfos[k].position = deltaVertices != null ? deltaVertices[k] : Vector3.zero;
					deltaVertInfos[k].normal = deltaNormals != null ? deltaNormals[k] : Vector3.zero;
					deltaVertInfos[k].tangent = deltaTangents != null ? deltaTangents[k] : Vector3.zero;
				}

				this.arrBufMorphDeltas[i].SetData(deltaVertInfos);
			}

			if (this._render)
			{
				Material[] materials = this.smr != null ? this.smr.materials : this.mr.materials;
				for (int i = 0; i < materials.Length; i++)
				{
					if (!materials[i].shader.name.ToLower().Contains("skinning") && this.GetComponent<GPUClothDynamics>() == null)
						materials[i].shader = this._shader;
					//materials[i].SetInt("_DoSkinning", 1);
					materials[i].SetShaderPassEnabled("MotionVectors", true);
					materials[i].EnableKeyword("USE_BUFFERS");
				}
				this.mr.materials = materials;
			}
			this.shaderDQBlend.SetInt("textureWidth", _textureWidth);

			this.poseMatrices = new Matrix4x4[this.mf.sharedMesh.bindposes.Length];
			_nativeMatrices = new NativeArray<Matrix4x4>(this.poseMatrices, Allocator.Persistent);

			// initiate textures and buffers

			int textureHeight = this.mf.sharedMesh.vertexCount / _textureWidth;
			if (this.mf.sharedMesh.vertexCount % _textureWidth != 0)
			{
				textureHeight++;
			}

			this._rtSkinnedData_1 = new RenderTexture(_textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat)
			{
				filterMode = FilterMode.Point,
				enableRandomWrite = true
			};
			this._rtSkinnedData_1.Create();
			this.shaderDQBlend.SetTexture(this.kernelHandleDQBlend, "skinned_data_1", this._rtSkinnedData_1);

			this._rtSkinnedData_2 = new RenderTexture(_textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat)
			{
				filterMode = FilterMode.Point,
				enableRandomWrite = true
			};
			this._rtSkinnedData_2.Create();
			this.shaderDQBlend.SetTexture(this.kernelHandleDQBlend, "skinned_data_2", this._rtSkinnedData_2);

			this._rtSkinnedData_3 = new RenderTexture(_textureWidth, textureHeight, 0, RenderTextureFormat.RGFloat)
			{
				filterMode = FilterMode.Point,
				enableRandomWrite = true
			};
			this._rtSkinnedData_3.Create();
			this.shaderDQBlend.SetTexture(this.kernelHandleDQBlend, "skinned_data_3", this._rtSkinnedData_3);

			this.bufPoseMatrices = new ComputeBuffer(this.mf.sharedMesh.bindposes.Length, sizeof(float) * 16);
			this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "pose_matrices", this.bufPoseMatrices);

			this.bufSkinnedDq = new ComputeBuffer(this.mf.sharedMesh.bindposes.Length, sizeof(float) * 8);
			this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "skinned_dual_quaternions", this.bufSkinnedDq);
			this.shaderDQBlend.SetBuffer(this.kernelHandleDQBlend, "skinned_dual_quaternions", this.bufSkinnedDq);

			this.bufBoneDirections = new ComputeBuffer(this.mf.sharedMesh.bindposes.Length, sizeof(float) * 4);
			this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "bone_directions", this.bufBoneDirections);
			this.shaderDQBlend.SetBuffer(this.kernelHandleDQBlend, "bone_directions", this.bufBoneDirections);

			this.bufVertInfo = new ComputeBuffer(this.mf.sharedMesh.vertexCount, sizeof(float) * 16 + sizeof(int) * 4 + sizeof(float));
			var vertInfos = new VertexInfo[this.mf.sharedMesh.vertexCount];

			if (_recalculateNormals) this.mf.sharedMesh.RecalculateNormals();

			Vector3[] vertices = this.mf.sharedMesh.vertices;
			Vector3[] normals = this.mf.sharedMesh.normals;
			Vector4[] tangents = this.mf.sharedMesh.tangents;
			BoneWeight[] boneWeights = this.mf.sharedMesh.boneWeights;
			for (int i = 0; i < vertInfos.Length; i++)
			{
				vertInfos[i].position = vertices[i];

				vertInfos[i].boneIndex0 = boneWeights[i].boneIndex0;
				vertInfos[i].boneIndex1 = boneWeights[i].boneIndex1;
				vertInfos[i].boneIndex2 = boneWeights[i].boneIndex2;
				vertInfos[i].boneIndex3 = boneWeights[i].boneIndex3;

				vertInfos[i].weight0 = boneWeights[i].weight0;
				vertInfos[i].weight1 = boneWeights[i].weight1;
				vertInfos[i].weight2 = boneWeights[i].weight2;
				vertInfos[i].weight3 = boneWeights[i].weight3;

				// determine per-vertex compensation coef

				Matrix4x4 bindPose = this.bindPoses[vertInfos[i].boneIndex0].inverse;
				Quaternion boneBindRotation = bindPose.ExtractRotation();
				Vector3 boneDirection = boneBindRotation * this.boneOrientationVector;  // ToDo figure out bone orientation
				Vector3 bonePosition = bindPose.ExtractPosition();
				Vector3 toBone = bonePosition - (Vector3)vertInfos[i].position;

				vertInfos[i].compensation_coef = Vector3.Cross(toBone, boneDirection).magnitude;
			}

			if (normals.Length > 0)
			{
				for (int i = 0; i < vertInfos.Length; i++)
				{
					if (i < normals.Length)
						vertInfos[i].normal = normals[i];
				}
			}

			if (tangents.Length > 0)
			{
				for (int i = 0; i < vertInfos.Length; i++)
				{
					vertInfos[i].tangent = tangents[i];
				}
			}

			this.bufVertInfo.SetData(vertInfos);
			this.shaderDQBlend.SetBuffer(this.kernelHandleDQBlend, "vertex_infos", this.bufVertInfo);

			this.bufMorphTemp_1 = new ComputeBuffer(this.mf.sharedMesh.vertexCount, sizeof(float) * 16 + sizeof(int) * 4 + sizeof(float));
			this.bufMorphTemp_2 = new ComputeBuffer(this.mf.sharedMesh.vertexCount, sizeof(float) * 16 + sizeof(int) * 4 + sizeof(float));

			// bind DQ buffer

			Matrix4x4[] bindPoses = this.mf.sharedMesh.bindposes;
			var bindDqs = new DualQuaternion[bindPoses.Length];
			for (int i = 0; i < bindPoses.Length; i++)
			{
				bindDqs[i].rotationQuaternion = bindPoses[i].ExtractRotation();
				bindDqs[i].position = bindPoses[i].ExtractPosition();
			}

			this.bufBindDq = new ComputeBuffer(bindDqs.Length, sizeof(float) * 8);
			this.bufBindDq.SetData(bindDqs);
			this.shaderComputeBoneDQ.SetBuffer(this.kernelHandleComputeBoneDQ, "bind_dual_quaternions", this.bufBindDq);

			this.UpdateViewFrustrumCulling();
			this.ApplyMorphs();
		}

		void ApplyMorphs()
		{
			ComputeBuffer bufMorphedVertexInfos = this.GetMorphedVertexInfos(
				this.bufVertInfo,
				ref this.bufMorphTemp_1,
				ref this.bufMorphTemp_2,
				this.arrBufMorphDeltas,
				this.morphWeights
			);

			this.shaderDQBlend.SetBuffer(this.kernelHandleDQBlend, "vertex_infos", bufMorphedVertexInfos);
		}

		ComputeBuffer GetMorphedVertexInfos(ComputeBuffer bufOriginal, ref ComputeBuffer bufTemp_1, ref ComputeBuffer bufTemp_2, ComputeBuffer[] arrBufDelta, float[] weights)
		{
			ComputeBuffer bufSource = bufOriginal;

			for (int i = 0; i < weights.Length; i++)
			{
				if (weights[i] == 0)
				{
					continue;
				}

				if (arrBufDelta[i] == null)
				{
					throw new System.NullReferenceException();
				}

				this.shaderApplyMorph.SetBuffer(this.kernelHandleApplyMorph, "source", bufSource);
				this.shaderApplyMorph.SetBuffer(this.kernelHandleApplyMorph, "target", bufTemp_1);
				this.shaderApplyMorph.SetBuffer(this.kernelHandleApplyMorph, "delta", arrBufDelta[i]);
				this.shaderApplyMorph.SetFloat("weight", weights[i] / 100f);

				int numThreadGroups = bufSource.count / numthreads;
				if (bufSource.count % numthreads != 0)
				{
					numThreadGroups++;
				}

				this.shaderApplyMorph.Dispatch(this.kernelHandleApplyMorph, numThreadGroups, 1, 1);

				bufSource = bufTemp_1;
				bufTemp_1 = bufTemp_2;
				bufTemp_2 = bufSource;
			}

			return bufSource;
		}

		void ReleaseBuffers()
		{
			this.bufBindDq?.Release();
			this.bufPoseMatrices?.Release();
			this.bufSkinnedDq?.Release();

			this.bufVertInfo?.Release();
			this.bufMorphTemp_1?.Release();
			this.bufMorphTemp_2?.Release();

			this.bufBoneDirections?.Release();

			if (this.arrBufMorphDeltas != null)
			{
				for (int i = 0; i < this.arrBufMorphDeltas.Length; i++)
				{
					this.arrBufMorphDeltas[i]?.Release();
				}
			}
		}
		void OnDisable()
		{
			this.ReleaseBuffers();
			if (_nativeMatrices.IsCreated) _nativeMatrices.Dispose();
			if (_transBones.isCreated) _transBones.Dispose();

		}

		void OnEnable()
		{
			this.materialPropertyBlock = new MaterialPropertyBlock();

			SetShader();

			if (this.shaderComputeBoneDQ == null) this.shaderComputeBoneDQ = Resources.Load("Compute/ComputeBoneDQ") as ComputeShader;
			if (this.shaderDQBlend == null) this.shaderDQBlend = Resources.Load("Compute/DQBlend") as ComputeShader;
			if (this.shaderApplyMorph == null) this.shaderApplyMorph = Resources.Load("Compute/ApplyMorph") as ComputeShader;

			this.shaderComputeBoneDQ = (ComputeShader)Instantiate(this.shaderComputeBoneDQ);    // bug workaround
			this.shaderDQBlend = (ComputeShader)Instantiate(this.shaderDQBlend);                // bug workaround
			this.shaderApplyMorph = (ComputeShader)Instantiate(this.shaderApplyMorph);          // bug workaround

			this.kernelHandleComputeBoneDQ = this.shaderComputeBoneDQ.FindKernel("CSMain");
			this.kernelHandleDQBlend = this.shaderDQBlend.FindKernel("CSMain");
			this.kernelHandleApplyMorph = this.shaderApplyMorph.FindKernel("CSMain");

			if (this.smr != null)
				this.bones = this.smr.bones;
			this._transBones = new TransformAccessArray(this.bones);

			this.started = true;
			this.GrabMeshFromSkinnedMeshRenderer();

			if (this.smr != null)
			{
				for (int i = 0; i < this.morphWeights.Length; i++)
				{
					this.morphWeights[i] = this.smr.GetBlendShapeWeight(i);
				}
			}

			if (this._render && this.smr != null)
				this.smr.enabled = false;

			this.mf.sharedMesh.MarkDynamic();    // once or every frame? idk.
												 // at least it does not affect performance

			this.mf.sharedMesh.RecalculateBounds();

			this._textureHeight = this.mf.sharedMesh.vertexCount / this._textureWidth;

			_vertexCount = this.mf.sharedMesh.vertexCount;

			//if (!this._render)
			//    DestroyImmediate(this.mf);

			if (!this._render && this.GetComponent<MeshRenderer>())
				this.mr.enabled = false;

			//yield return waitForSeconds; //fixed by Static TODO needs testing

			if (this._render && this.GetComponent<SkinnedMeshRenderer>())
				Destroy(this.GetComponent<SkinnedMeshRenderer>());

			//if (_mf)
			//{
			//	if (GraphicsSettings.currentRenderPipeline)
			//	{
			//		var uv2 = new Vector2[_mf.mesh.vertexCount];
			//		for (int i = 0; i < uv2.Length; i++)
			//		{
			//			uv2[i] = new Vector2(i, 0);
			//		}
			//		switch (_vertexIdsToUvId)
			//		{
			//			case 2:
			//				_mf.mesh.uv2 = uv2;
			//				break;
			//			case 3:
			//				_mf.mesh.uv3 = uv2;
			//				break;
			//			case 4:
			//				_mf.mesh.uv4 = uv2;
			//				break;
			//			case 5:
			//				_mf.mesh.uv5 = uv2;
			//				break;
			//			case 6:
			//				_mf.mesh.uv6 = uv2;
			//				break;
			//			case 7:
			//				_mf.mesh.uv7 = uv2;
			//				break;
			//			default:
			//				_mf.mesh.uv8 = uv2;
			//				break;
			//		}
			//	}
			//}
		}

		internal void SetShader(string addon = "", bool force = false)
		{
			if (!force && this.GetComponent<GPUClothDynamics>() != null) return; //Don't set the shader if this a cloth object
            var cacheShader = this._shader;
            if (GraphicsSettings.currentRenderPipeline)
			{
				if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
				{
					//Debug.Log("HDRP active");
					if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/Lit_Graph_DQSkinning" + addon, typeof(Shader)) as Shader; //Shader.Find("Shader Graphs/Lit_Graph_DQSkinning") as Shader;
				}
				else // assuming here we only have HDRP or URP options here
				{
					//Debug.Log("URP active");
#if UNITY_2020_1_OR_NEWER
					if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/URP_Lit_Graph_DQSkinning" + addon, typeof(Shader)) as Shader;
#else
					if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/URP_DQSkinning_2019" + addon, typeof(Shader)) as Shader;
#endif
				}
			}
			else
			{
				//Debug.Log("Built-in RP active");
				if (this._shader == null || !string.IsNullOrEmpty(addon)) this._shader = Resources.Load("Shaders/DQSkinning" + addon) as Shader;
			}

            if (!string.IsNullOrEmpty(addon) && this._render)
			{
				Material[] materials = this.mr.materials;
				for (int i = 0; i < materials.Length; i++)
				{
					materials[i].shader = this._shader;
					materials[i].SetShaderPassEnabled("MotionVectors", true);
					materials[i].EnableKeyword("USE_BUFFERS");
                    if (cacheShader != _shader) materials[i].SetFloat("_EmissiveIntensity", 0);
                }
                this.mr.materials = materials;
			}
		}

		[BurstCompile]
		public struct CopyMatrixJob : IJobParallelForTransform
		{
			public NativeArray<Matrix4x4> targets;
			public void Execute(int index, TransformAccess transform)
			{
				this.targets[index] = transform.localToWorldMatrix;
			}
		}

		internal override void UpdateSync()
		{
			if (_updateSync) SkinUpdate();
		}
		private void LateUpdate()
		{
			if (!_updateSync) SkinUpdate();
		}

		private void SkinUpdate()
		{
			if (this._render && this.mr.isVisible == false)
			{
				return;
			}

			var job = new CopyMatrixJob
			{
				targets = this._nativeMatrices //.Reinterpret<float4x4>(UnsafeUtility.SizeOf<float4x4>())
			};
			JobHandle handle = job.Schedule(this._transBones);
			handle.Complete();

			this.bufPoseMatrices.SetData(this._nativeMatrices);

			// Calculate blended quaternions

			int numThreadGroups = this.bones.Length / numthreads;
			numThreadGroups += this.bones.Length % numthreads == 0 ? 0 : 1;

			this.shaderComputeBoneDQ.SetVector("boneOrientation", this.boneOrientationVector);
			this.shaderComputeBoneDQ.SetMatrix(
				"self_matrix",
				this.transform.worldToLocalMatrix
			);
			this.shaderComputeBoneDQ.Dispatch(this.kernelHandleComputeBoneDQ, numThreadGroups, 1, 1);

			numThreadGroups = _vertexCount / numthreads;
			numThreadGroups += _vertexCount % numthreads == 0 ? 0 : 1;

			this.shaderDQBlend.SetFloat("compensation_coef", this.bulgeCompensation);
			this.shaderDQBlend.Dispatch(this.kernelHandleDQBlend, numThreadGroups, 1, 1);

			if (this._render)
			{
				UpdateMaterialPropertyBlock();
			}
		}

		public void UpdateMaterialPropertyBlock()
		{
			this.materialPropertyBlock.SetTexture("skinned_data_1", this._rtSkinnedData_1);
			this.materialPropertyBlock.SetTexture("skinned_data_2", this._rtSkinnedData_2);
			this.materialPropertyBlock.SetTexture("skinned_data_3", this._rtSkinnedData_3);
			this.materialPropertyBlock.SetInt("skinned_tex_height", this._textureHeight);
			this.materialPropertyBlock.SetInt("skinned_tex_width", _textureWidth);
			this.mr.SetPropertyBlock(this.materialPropertyBlock);
		}
	}
}