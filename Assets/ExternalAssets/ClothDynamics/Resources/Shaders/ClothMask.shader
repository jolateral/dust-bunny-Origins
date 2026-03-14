Shader "ClothDynamics/ClothMask"
{
	//Properties
	//{
	//	//_MainTex ("Texture", 2D) = "white" {}
	//	//_Amount("Extrusion Amount", Range(-1,1)) = -1
	//	//_OutlineWidth("Outline Width", Range(-1,1)) = 1
	//}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#pragma multi_compile_local _ USE_BUFFERS

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				uint vertexID : SV_VERTEXID;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 scrPos : TEXCOORD0;
			};

#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
			StructuredBuffer<float3> positionsBuffer;
			StructuredBuffer<float3> normalsBuffer;
#endif

			//float _Amount;
			//float _OutlineWidth;

			//void Unity_Projection_float4(float2 A, float2 B, out float2 Out)
			//{
			//	Out = B * dot(A, B) / dot(B, B);
			//}

			v2f vert(appdata v)
			{
				#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
				//#ifdef USE_BUFFERS
					v.vertex.xyz = positionsBuffer[v.vertexID];
					v.normal = normalsBuffer[v.vertexID];
					//#endif
				 #endif
				 v2f o;
				 o.vertex = UnityObjectToClipPos(v.vertex);
				 o.scrPos = ComputeScreenPos(o.vertex); //mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0))); //mul(unity_ObjectToWorld, v.vertex);// v.vertex;
				 //float4 scrPos = ComputeScreenPos(o.vertex);
				 //float2 screenUV = scrPos.xy / scrPos.w;
				 //float3 caculateVec = normalize(v.normal) * _Amount; //lerp(normalize(v.vertex.xyz), normalize(v.normal), _OutlineFactor);
				 //float3 norm = mul((float3x3)UNITY_MATRIX_IT_MV, caculateVec);
				 //float2 offset = TransformViewToProjection(norm.xy);
				 //o.vertex.xy += offset * o.vertex.z * _OutlineWidth;
				 //o.vertex.xyz += v.normal * 0.02;

				 return o;
			 }

			 float4 frag(v2f i) : SV_Target
			 {
				 // sample the texture
				 float4 col = float4(0,0,i.scrPos.w,0);// tex2D(_MainTex, i.uv);
				 return col;
			 }
			 ENDCG
		 }
	}
}
