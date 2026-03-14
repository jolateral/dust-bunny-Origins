Shader "GPU Skinning/VertOut GPU Skinning" {
	Properties {
		//[HideInInspector] [Toggle(USE_BUFFERS)] _UseBuffers("Use Buffers", Float) = 0
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		//_rtArray("RT Array", 2DArray) = "" {}
		//_rtArrayN("RT Array N", 2DArray) = "" {}
		//_rtArrayT("RT Array T", 2DArray) = "" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard addshadow vertex:vert

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		//#pragma shader_feature USE_BUFFERS
		#pragma multi_compile_local _ USE_BUFFERS
		//#define USE_BUFFERS 1
		#pragma multi_compile_local _ USE_BLEND_SHAPES
		#pragma multi_compile_local _ USE_NORMALS
		#pragma multi_compile_local _ USE_TANGENTS

		#include "BlendShapes.cginc"

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			//fixed4 color : COLOR;
		};
		struct SVertOut
		{
			float3 pos;
			float3 norm;
			float4 tang;
		};

#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
		StructuredBuffer<SVertOut> _VertIn;
#endif
		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		struct appdata {
			float4 vertex : POSITION;
			float4 tangent : TANGENT;
			float3 normal : NORMAL;
			float4 texcoord : TEXCOORD0;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
			float4 texcoord3 : TEXCOORD3;
			fixed4 color : COLOR;
			UNITY_VERTEX_INPUT_INSTANCE_ID

			uint vId : SV_VertexID;
		};


		void vert(inout appdata v) {
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
#ifdef USE_BUFFERS
			SVertOut vin = _VertIn[v.vId];
			v.vertex.xyz = vin.pos;
			v.normal = vin.norm;
			v.tangent = vin.tang;
#ifdef USE_BLEND_SHAPES
			VertexAnim(v.vId, v.vertex, v.normal, v.tangent);
#endif
#endif
#endif
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
			//clip(IN.color.a - 0.1);
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
