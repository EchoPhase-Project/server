// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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
            get;
            set
            {
                field = value;
                Version++;
            }
        }

        /// <summary>
        /// Bumped on every state change, so a save can tell whether the entity
        /// was touched again while it was being written.
        /// </summary>
        internal int Version { get; private set; }

        /// <summary>USING TTL / TIMESTAMP for this entity's write.</summary>
        public WriteOptions? Options { get; set; }

        /// <summary>An IF clause for this entity's write (its concurrency token adds one by itself).</summary>
        public WriteCondition? Condition { get; set; }

        /// <summary>The token value this save writes (set by the save, kept on success).</summary>
        internal object? NewToken { get; set; }

        /// <summary>The save's conditional write did not apply.</summary>
        internal bool Conflicted { get; set; }

        /// <summary>The values it was read (or last saved) with; null for an entity never loaded or saved.</summary>
        internal Dictionary<string, object?>? Original { get; set; }

        /// <summary>
        /// The properties found changed since <see cref="Original"/>: only these
        /// are written, and null among them clears the stored value. Null when
        /// the whole entity is written (Add, Update).
        /// </summary>
        internal HashSet<string>? Changed { get; set; }

        /// <summary>
        /// Properties whose stored values should be deleted. Separate from null
        /// values, which mean "leave unchanged" on both insert and update.
        /// </summary>
        public HashSet<string> ClearedColumns { get; } = new(StringComparer.Ordinal);

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

        public TrackedEntity Clone()
        {
            var clone = new TrackedEntity(EntityType, Entity, State) { Options = Options, Condition = Condition };
            clone.ClearedColumns.UnionWith(ClearedColumns);
            return clone;
        }
    }
}
