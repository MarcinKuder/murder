﻿using Bang;
using Bang.Entities;
using Bang.Systems;
using Murder.Components;
using Murder.Editor.Components;
using Murder.Editor.Utilities;
using System.Collections.Immutable;

namespace Murder.Editor.Systems
{
    [Watch(typeof(ColliderComponent))]
    public class UpdateColliderSystem : IReactiveSystem
    {
        public void OnAdded(World world, ImmutableArray<Entity> entities)
        { }

        public void OnModified(World world, ImmutableArray<Entity> entities)
        {
            if (world.TryGetUnique<EditorComponent>() is not EditorComponent editor)
            {
                return;
            }

            EditorHook hook = editor.EditorHook;
            foreach (Entity e in entities)
            {
                hook.OnComponentModified?.Invoke(e.EntityId, e.GetCollider());
            }
        }

        public void OnRemoved(World world, ImmutableArray<Entity> entities)
        { }
    }
}