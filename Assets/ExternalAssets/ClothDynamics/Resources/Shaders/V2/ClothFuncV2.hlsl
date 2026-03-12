#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED

#if defined USE_BUFFERS && !defined SHADER_TARGET_GLSL
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

void MyClothFunction_float(uint vertexId, out float3 vertex, out float3 normal)
{
#if defined USE_BUFFERS && !defined SHADER_TARGET_GLSL
    vertex = mul(worldToLocalMatrix, float4(positionsBuffer[prevNumParticles + vertexId],1)).xyz;
    normal = normalsBuffer[prevNumParticles + vertexId];
#else
    vertex = float3(0,0,0);
    normal = float3(0,1,0);
#endif
    normal = Rotate(normal, quat_inv(rotation));
#ifdef USE_TRANSFER_DATA
    vertex -= g_RootPos;
    vertex = Rotate(vertex, quat_inv(g_RootRot));
    normal = Rotate(normal, quat_inv(g_RootRot));
#endif
}

#endif //end MYHLSLINCLUDE_INCLUDED