

#ifdef USE_BLEND_SHAPES

Texture2D _rtArray;
#ifdef USE_NORMALS
Texture2D _rtArrayN;
#endif
#ifdef USE_TANGENTS
Texture2D _rtArrayT;
#endif

uint _rtArrayWidth;

void VertexAnim_float(uint id, in float3 vIn, in float3 normalIn, in float3 tangentIn, out float3 vertex, out float3 normal, out float3 tangent)
{
	uint width = _rtArrayWidth;
	float3 result = 0;
	float3 resultN = 0;
	float3 resultT = 0;
	float3 pos = vIn.xyz;
	float3 norm = normalIn;
	float3 tan = tangentIn.xyz;

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

#else
void VertexAnim_float(uint id, in float3 vIn, in float3 normalIn, in float3 tangentIn, out float3 vertex, out float3 normal, out float3 tangent)
{
	vertex = vIn;
	normal = normalIn;
	tangent = tangentIn;
}
#endif