Shader "ClothDynamics/ClothSurfaceShaderTransparent"
{
    Properties
    {
        [HideInInspector] [Toggle(USE_BUFFERS)] _UseBuffers("Use Buffers", Float) = 0
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 200
        Cull Off

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:myvert addshadow alpha:fade keepalpha nolightmap
        //#pragma shader_feature USE_BUFFERS
        #pragma multi_compile_local _ USE_BUFFERS
        #pragma multi_compile_local _ USE_TRANSFER_DATA

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        #if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_PS5) || defined(SHADER_API_XBOXONE)
        StructuredBuffer<float3> positionsBuffer;
        StructuredBuffer<float3> normalsBuffer;
        #endif

        struct appdata_base2 {
            uint vertexID : SV_VERTEXID;
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 texcoord : TEXCOORD0;
            float4 texcoord1 : TEXCOORD1;
            float4 texcoord2 : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

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

        void myvert (inout appdata_base2 v)
        {
            #if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_PS5) || defined(SHADER_API_XBOXONE)
                #ifdef USE_BUFFERS
                v.vertex.xyz = positionsBuffer[v.vertexID];
                v.normal = normalsBuffer[v.vertexID];
                    #ifdef USE_TRANSFER_DATA
                    v.vertex.xyz -= g_RootPos;
                    v.vertex.xyz = Rotate(v.vertex.xyz, quat_inv(g_RootRot));
                    v.normal = Rotate(v.normal, quat_inv(g_RootRot));
                    #endif
                #endif
            #endif
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
