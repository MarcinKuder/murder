﻿using Bang.Entities;
using Murder.Components;
using Murder.Core.Sounds;
using Murder.Diagnostics;
using Murder.Helpers;
using System.Collections.Immutable;

namespace Murder.Services;

public static class SoundServices
{
    public static ValueTask Play(SoundEventId id, Entity? target, SoundProperties properties = SoundProperties.None)
    {
        if (id.IsGuidEmpty || Game.Instance.IsSkippingDeltaTimeOnUpdate)
        {
            return default;
        }

        SoundSpatialAttributes? attributes = GetSpatialAttributes(target);
        return Game.Sound.PlayEvent(id, properties, attributes);
    }

    public static async ValueTask Play(SoundEventId id, SoundProperties properties = SoundProperties.None, SoundSpatialAttributes? attributes = null)
    {
        if (Game.Instance.IsSkippingDeltaTimeOnUpdate)
        {
            // Do not play sounds if we are currently skipping... I think?
            return;
        }

        if (!id.IsGuidEmpty)
        {
            await Game.Sound.PlayEvent(id, properties, attributes);
        }
    }

    public static async ValueTask PlayMusic(SoundEventId id)
    {
        await Game.Sound.PlayEvent(id, SoundProperties.Persist, attributes: null);
    }

    public static float GetGlobalParameter(ParameterId id)
    {
        return Game.Sound.GetGlobalParameter(id);
    }

    public static void SetGlobalParameter<T>(ParameterId id, T value)
    {
        try
        {
            Game.Sound.SetGlobalParameter(id, Convert.ToSingle(value));
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            GameLogger.Warning($"{value} is not a valid float.");
        }
    }

    public static void Stop(SoundEventId? id, bool fadeOut)
    {
        Game.Sound.Stop(id, fadeOut);
    }

    public static SoundEventId[] StopAll(bool fadeOut, HashSet<SoundEventId> exceptFor)
    {
        Game.Sound.Stop(fadeOut, exceptFor, out SoundEventId[] stoppedEvents);

        return stoppedEvents;
    }

    /// <summary>
    /// Stop all the ongoing events.
    /// </summary>
    /// <param name="fadeOut">Whether it should fade out in fmod.</param>
    /// <returns>List of all the events which were stopped.</returns>
    public static SoundEventId[] StopAll(bool fadeOut)
    {
        Game.Sound.Stop(fadeOut, out SoundEventId[] stoppedEvents);

        return stoppedEvents;
    }

    public static void TrackEventSourcePosition(SoundEventId eventId, Entity e)
    {
        SoundSpatialAttributes? attributes = GetSpatialAttributes(e);
        if (attributes is null)
        {
            GameLogger.Error("How is the entity attribute null?");
            return;
        }

        Game.Sound.UpdateEvent(eventId, attributes.Value);
    }

    /// <summary>
    /// Return the spatial attributes for playing a sound from <paramref name="target"/>.
    /// </summary>
    public static SoundSpatialAttributes? GetSpatialAttributes(Entity? target)
    {
        SoundSpatialAttributes attributes = new();
        if (target is null)
        {
            return null;
        }

        Entity root = EntityServices.FindRootEntity(target);
        if (root.TryGetTransform() is IMurderTransformComponent transform)
        {
            attributes.Position = transform.Vector2;
        }

        if (target.TryGetFacing()?.Direction is Direction direction)
        {
            attributes.Direction = direction;
        }

        return attributes;
    }

    /// <summary>
    /// Try to get a sound id associated with an <paramref name="animationEventId"/>
    /// on an entity with an <see cref="EventListenerComponent"/>.
    /// </summary>
    /// <param name="e">Target entity.</param>
    /// <param name="animationEventId">Animation string identifier.</param>
    /// <returns>If found, the sound event id for the animation event.</returns>
    public static SoundEventId? TryGetSoundForEvent(Entity e, string animationEventId)
    {
        EventListenerComponent? eventListener = e.TryGetEventListener() ?? e.TryFetchChild("interaction")?.TryGetEventListener();
        if (eventListener is null)
        {
            return null;
        }

        if (eventListener.Value.Events.TryGetValue(animationEventId, out SpriteEventInfo info))
        {
            return info.Sound;
        }

        return null;
    }

    public static ImmutableDictionary<string, SpriteEventInfo> ReplaceIdentifiers(
        ImmutableDictionary<string, SpriteEventInfo> source,
        Func<string, string> converter)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, SpriteEventInfo>();

        foreach ((string id, SpriteEventInfo info) in source)
        {
            string newIdentifier = converter(id);
            builder[newIdentifier] = new(newIdentifier, info.Sound, info.Persist);
        }

        return builder.ToImmutable();
    }
}