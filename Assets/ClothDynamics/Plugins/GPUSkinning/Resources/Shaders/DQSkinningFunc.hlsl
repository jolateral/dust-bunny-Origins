#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED
#endif //end MYHLSLINCLUDE_INCLUDED
sampler2D skinned_data_1;
sampler2D skinned_data_2;
sampler2D skinned_data_3;
uint skinned_tex_height;
uint skinned_tex_width;

void MyDQSkinningFunction_float(uint vertexId, out float4 vertex, out float3 normal, out float4 tangent)
{
//#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
//#ifdef USE_BUFFERS
			float2 skinned_tex_uv;

			skinned_tex_uv.x = (float(vertexId % skinned_tex_width)) / skinned_tex_width;
			skinned_tex_uv.y = (float(vertexId / skinned_tex_width)) / skinned_tex_height;

			float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
			float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

//#ifdef _TANGENT_TO_WORLD
			float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
//#endif

			vertex.xyz = data_1.xyz;
			vertex.w = 1;

			normal.x = data_1.w;
			normal.yz = data_2.xy;

#ifdef _TANGENT_TO_WORLD
			tangent.xy = data_2.zw;
			tangent.zw = data_3.xy;
#else
    tangent = 0;
#endif
//#endif
//#endif
//#endif
//#endif

}