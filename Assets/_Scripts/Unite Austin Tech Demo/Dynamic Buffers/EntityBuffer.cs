using Unity.Entities;



[InternalBufferCapacity(16)] // 16 bytes reserved for the two integers in an entity component TODO: should the static Entity be considered?
public struct EntityBuffer : IBufferElementData
{
    public static implicit operator Entity(EntityBuffer _e) { return _e.value; }
    public static implicit operator EntityBuffer(Entity _e) { return new EntityBuffer { value = _e }; }

    public Entity value;
}
