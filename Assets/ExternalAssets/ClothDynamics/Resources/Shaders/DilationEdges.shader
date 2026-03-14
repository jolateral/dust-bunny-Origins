Shader "Hidden/DilationEdges"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_MaxSteps("MaxSteps", Int) = 1
	}
		SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

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

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			int _MaxSteps;

			float4 frag(v2f i) : SV_Target
			{
				float2 UV = i.uv;

				float2 texelsize = _MainTex_TexelSize.xy;
				float mindist = 10000000;
				float2 offsets[8] = {float2(-1,0), float2(1,0), float2(0,1), float2(0,-1), float2(-1,1), float2(1,1), float2(1,-1), float2(-1,-1)};

				float4 oldTex = tex2Dlod(_MainTex, float4(UV,0,0));
				float4 newTex = oldTex;

				if (oldTex.g == 0)
				{
					int i = 0;
					while (i < _MaxSteps)
					{
						i++;
						int j = 0;
						while (j < 8)
						{
							float2 curUV = UV + offsets[j] * texelsize * i;
							float4 offsetsample = tex2Dlod(_MainTex, float4(curUV,0,0));

							if (offsetsample.g != 0)
							{
								float curdist = length(UV - curUV);

								if (curdist < mindist)
								{
									float2 projectUV = curUV + offsets[j] * texelsize * i * 0.25;
									float4 direction = tex2Dlod(_MainTex, float4(projectUV,0,0));
									mindist = curdist;

									if (direction.g != 0)
									{
										float4 delta = offsetsample - direction;
										newTex = offsetsample + delta * 4;
									}

								   else
									{
										newTex = offsetsample;
									}
								}
							}
							j++;
						}
					}
				}

				return float4(newTex.r, oldTex.g, oldTex.b, oldTex.a);
			}
			ENDCG
		}
	}
}
