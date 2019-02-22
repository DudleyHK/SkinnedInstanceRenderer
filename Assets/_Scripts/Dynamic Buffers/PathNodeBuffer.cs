using Unity.Entities;
using Unity.Mathematics;



[InternalBufferCapacity(12)]
public struct PathNodeBuffer : IBufferElementData
{
    public static implicit operator float3(PathNodeBuffer _e) { return _e.value; }
    public static implicit operator PathNodeBuffer(float3 _e) { return new PathNodeBuffer { value = _e }; }

    public float3 value;
}