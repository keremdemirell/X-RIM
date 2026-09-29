using System;
using XRim.Config;
using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Input
{
    /// <summary>
    /// Converts screen pixels into arena units in the fighter's torso frame (+X toward the opponent), undoing the
    /// view flip. Screen size, resolution and touch rate must not change any budget or outcome (GDD §4, Decided).
    /// </summary>
    [GddTbd("§18", "Arena unit scale and reference resolution")]
    public sealed class ScreenToArenaMapper
    {
        private InputConfig Config { get; }
        private ArenaSpace Space { get; }

        public ScreenToArenaMapper(InputConfig config, ArenaSpace space)
        {
            Config = Guard.NotNull(config, nameof(config));
            Space = space;
        }

        public Vec2 ToFighterFrame(Vec2 screenPixels, ScreenLayout layout, Vec2 torsoArenaPosition)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("ScreenToArenaMapper.ToFighterFrame is not implemented yet.");
        }
    }
}
