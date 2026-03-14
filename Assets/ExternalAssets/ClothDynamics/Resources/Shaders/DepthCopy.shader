// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/DepthCopy"
{
	//Properties
	//{
	//	_MainTex ("Texture", 2D) = "white" {}
	//	//_MyDepthTex("Texture", 2D) = "white" {}
	//}
	SubShader
	{
		Cull Off ZWrite On ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment CopyDepthBufferFragmentShader

			//#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			//sampler2D _MainTex;
			//sampler2D_float _MyDepthTex;
			Texture2DArray<float> _MyDepthTex;
			SamplerState my_linear_clamp_sampler;
			float4 _screenSize;

			// important part: outputs depth from _MyDepthTex to depth buffer
			half4 CopyDepthBufferFragmentShader(v2f i, out float outDepth : SV_Depth) : SV_Target
			{
				//float depth = _MyDepthTex.SampleLevel(my_linear_clamp_sampler, float3(i.uv.xy,0), 0).r;//SAMPLE_DEPTH_TEXTURE(_MyDepthTex, i.uv);
				float depth = _MyDepthTex.Load(uint4(uint2(i.uv.xy * _screenSize.xy), 0, 0)).r;

				outDepth = depth;
				return 0;
			}

			ENDCG
		}
	}
}
