Shader "GPU Skinning/Standard shader for DQ Skinning Mask" {
	Properties {
		[HideInInspector] [Toggle(USE_BUFFERS)] _UseBuffers("Use Buffers", Float) = 0

		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_MaskTex("MaskTex", 2D) = "white" {}
		_MaskThreshold("MaskThreshold", Range(-1,1)) = 0.01
		_Cutout("Cutout", Range(0,1)) = 0.5
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard addshadow vertex:vert alphatest:_Cutout nolightmap
		
		//#pragma shader_feature USE_BUFFERS
		#pragma multi_compile_local _ USE_BUFFERS

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;
		sampler2D _MaskTex;
		float _MaskThreshold;

		struct Input {
			float2 uv_MainTex;
			float4 screenPos;
			//float3 customColor;
		};
//		struct SVertOut
//		{
//			float3 pos;
//			float3 norm;
//			float4 tang;
//		};
//
//#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
//		uniform StructuredBuffer<SVertOut> _VertIn;
//#endif
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
			uint id : SV_VertexID;
		};

		// variables used for skining, always the same for every pass
		sampler2D skinned_data_1;
		sampler2D skinned_data_2;
		sampler2D skinned_data_3;
		uint skinned_tex_height;
		uint skinned_tex_width;
		//bool _DoSkinning;

		// the actual skinning function
		// don't change the code but change the argument type to the name of vertex input structure used in current pass
		// for this pass it is VertexInputSkinningForward
		void vert(inout appdata v/*, out Input o*/) {
			//if (_DoSkinning) {
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
#ifdef USE_BUFFERS
			float2 skinned_tex_uv;

			skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
			skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

			float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
			float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

#ifdef _TANGENT_TO_WORLD
			float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
#endif

			v.vertex.xyz = data_1.xyz;
			v.vertex.w = 1;

			v.normal.x = data_1.w;
			v.normal.yz = data_2.xy;

#ifdef _TANGENT_TO_WORLD
			v.tangent.xy = data_2.zw;
			v.tangent.zw = data_3.xy;
#endif
#endif
#endif
			//UNITY_INITIALIZE_OUTPUT(Input, o);
			//o.customColor = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0))).xyz; //mul(unity_ObjectToWorld, v.vertex).xyz; //v.vertex.xyz;
			//}
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {

			float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
			float4 d = tex2D(_MaskTex, screenUV);
			if (IN.screenPos.w < d.z + _MaskThreshold) d.r = 1;

			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = d.r * c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
