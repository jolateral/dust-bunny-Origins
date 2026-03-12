



UNITY_DECLARE_TEX2D_NOSAMPLER(_rtArray);
#ifdef USE_NORMALS
UNITY_DECLARE_TEX2D_NOSAMPLER(_rtArrayN);
#endif
#ifdef USE_TANGENTS
UNITY_DECLARE_TEX2D_NOSAMPLER(_rtArrayT);
#endif

uint _rtArrayWidth;

void VertexAnim(uint id, inout float4 vertex, inout float3 normal, inout float4 tangent)
{
	uint width = _rtArrayWidth;
	float3 result = 0;
	float3 resultN = 0;
	float3 resultT = 0;
	float3 pos = vertex.xyz;
	float3 norm = normal;
	float3 tan = tangent.xyz;

	result = _rtArray[uint2(id % width, id / width)].xyz;
#ifdef USE_NORMALS
	resultN = _rtArrayN[uint2(id % width, id / width)].xyz;
#endif
#ifdef USE_TANGENTS
	resultT = _rtArrayT[uint2(id % width, id / width)].xyz;
#endif
	
	vertex.xyz = pos + result;
	normal = norm + resultN;//TODO normalize needed?
	tangent.xyz = tan + resultT.xyz;
}
