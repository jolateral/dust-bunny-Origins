#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED

struct SVertOut
{
	float3 pos;
	float3 norm;
	float4 tang;
};

//#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
StructuredBuffer<SVertOut> _VertIn;
//#endif

void MyGPUSkinningFunction_float(uint vertexId, out float3 vertex, out float3 normal, out float3 tangent)
{
//#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PS4) || defined(SHADER_API_XBOXONE)
//#ifdef USE_BUFFERS
	SVertOut vin = _VertIn[vertexId];
	vertex = vin.pos;
    normal = vin.norm;
	tangent = vin.tang.xyz;
	//#endif
//#endif

}

#endif //end MYHLSLINCLUDE_INCLUDED