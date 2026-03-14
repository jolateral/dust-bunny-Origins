Shader "ClothDynamics/TempUnlitShaderV2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _lightDir("LightDir", Vector) = (0,1,0,0)
        _Color("Color", Color) = (1, 1, 1, 1) //The color of our object
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            //#pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                uint vertexID : SV_VERTEXID;
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float mask : TEXCOORD2;
                float3 normal : NORMAL;
                float4 posWorld : TEXCOORD1;
                float3 color : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct sData
            {
                float4 pr;
                float4 nId;
                float4 temp;
                float mask;
                float3 color;
            };


#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)

            StructuredBuffer<sData> _meshPosBuffer;
            StructuredBuffer<float3> _vertexBuffer;
            StructuredBuffer<float3> _normalBuffer;


            StructuredBuffer<float3> positionsBuffer;
            StructuredBuffer<float3> normalsBuffer;
#endif

            float4 g_RootRot;
            float4 g_RootPos;

            float3 Rotate(float3 v, float4 q)
            {
                float3 qVec = q.xyz;
                float3 t = 2.0f * cross(qVec, v);
                return v + q.w * t + cross(qVec, t);
            }

            float4 quat_inv(in float4 q)
            {
                return float4(-q.xyz, q.w);
            }

            float4x4 worldToLocalMatrix;
            uint prevNumParticles;
            float4 rotation;

            int _vertexCount;
            float _scale;
  
            #include "UnityCG.cginc" //Provides us with light data, camera information, etc
            
            uniform float4 _LightColor0; //From UnityCG
            uniform float4 _Color; //Use the above variables in here
            //float4 _lightDir;
            uniform float _normalScale;
            uniform bool _trisMode;
            uniform bool _showDebugColors;
            
            v2f vert (appdata v)
            {
                v2f o;
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)

                uint vertexId = 0;// v.vertexID;
                //int objId = vertexId /_vertexCount;
                //sData data = _meshPosBuffer[objId];
                //int meshIndex = _meshStartIndexBuffer[objId];
                //float distScale = data.mask;
                //o.vertex.xyz = _vertexBuffer[vertexId - (objId * _vertexCount)].xyz * distScale * _scale + data.pr.xyz;
                
                float3 clothVertex = mul(worldToLocalMatrix, float4(positionsBuffer[prevNumParticles + vertexId], 1)).xyz;

                o.vertex.xyz = v.vertex.xyz + clothVertex.xyz;// mul(unity_ObjectToWorld, float4(clothVertex, 1)).xyz;

                o.vertex.w = 1;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.mask = 1;// distScale;
                o.posWorld = mul(unity_ObjectToWorld, o.vertex); //Calculate the world position for our point
                o.normal = v.normal.xyz;/// normalize(mul(float4(_normalBuffer[vertexId - (objId * _vertexCount)].xyz, 0.0), unity_WorldToObject).xyz); //Calculate the normal
                o.vertex = UnityObjectToClipPos(o.vertex);
                o.color = 1;

                //UNITY_TRANSFER_FOG(o,o.vertex);
#endif
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normalDirection = normalize(i.normal);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - i.posWorld.xyz);

                float3 vert2LightSource = _WorldSpaceLightPos0.xyz - i.posWorld.xyz;
                float oneOverDistance = 1.0 / length(vert2LightSource);
                float attenuation = lerp(1.0, oneOverDistance, _WorldSpaceLightPos0.w); //Optimization for spot lights. This isn't needed if you're just getting started.
                float3 lightDirection = _WorldSpaceLightPos0.xyz - i.posWorld.xyz * _WorldSpaceLightPos0.w;

                float3 ambientLighting = UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb; //Ambient component
                float3 diffuseReflection = attenuation * _LightColor0.rgb * _Color.rgb * max(0.0, dot(normalDirection, lightDirection)); //Diffuse component


                fixed4 col = 1;
                col.rgb = saturate(diffuseReflection+0.1f) * i.color;
                col.r *= i.mask;
                // apply fog
               // UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
