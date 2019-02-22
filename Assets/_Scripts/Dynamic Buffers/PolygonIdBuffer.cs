using Unity.Entities;
using UnityEngine.Experimental.AI;


[InternalBufferCapacity(64)]
public struct PolygonIdBuffer : IBufferElementData
{
    public static implicit operator PolygonId(PolygonIdBuffer _e) { return _e.value; }
    public static implicit operator PolygonIdBuffer(PolygonId _e) { return new PolygonIdBuffer { value = _e }; }

    public PolygonId value;
}
