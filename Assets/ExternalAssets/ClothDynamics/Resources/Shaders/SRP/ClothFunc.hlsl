#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED

StructuredBuffer<float3> positionsBuffer;
StructuredBuffer<float3> normalsBuffer;

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

void MyClothFunction_float(uint vertexId, out float3 vertex, out float3 normal)
{
    vertex = positionsBuffer[vertexId];
    normal = normalsBuffer[vertexId];
#ifdef USE_TRANSFER_DATA
    vertex -= g_RootPos;
    vertex = Rotate(vertex, quat_inv(g_RootRot));
    normal = Rotate(normal, quat_inv(g_RootRot));
#endif
}

#endif //end MYHLSLINCLUDE_INCLUDED