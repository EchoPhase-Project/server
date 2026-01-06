using EchoPhase.DAL.Scylla.Enums;

namespace EchoPhase.DAL.Scylla.Models
{
    public class TrackedEntity
    {
        public Guid TrackingId { get; } = Guid.NewGuid();
        public object Entity
        {
            get;
        }
        public Type EntityType
        {
            get;
        }
        public EntityState State
        {
            get; set;
        }

        public TrackedEntity(object entity, EntityState state)
        {
            Entity = entity;
            EntityType = entity.GetType();
            State = state;
        }

        public TrackedEntity(Type type, object entity, EntityState state)
        {
            Entity = entity;
            EntityType = type;
            State = state;
        }

        public TrackedEntity Clone() => new TrackedEntity(Entity, State);
    }
}
