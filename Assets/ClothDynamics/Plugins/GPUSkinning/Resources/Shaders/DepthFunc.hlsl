//#ifndef MYHLSLINCLUDE_INCLUDED
//#define MYHLSLINCLUDE_INCLUDED

//#include "UnityCG.cginc"

void MyDepthFunction_float(float3 vertex, out float3 customColor)
{
	customColor = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(vertex.xyz, 1.0))).xyz;
}

//#endif //end MYHLSLINCLUDE_INCLUDED