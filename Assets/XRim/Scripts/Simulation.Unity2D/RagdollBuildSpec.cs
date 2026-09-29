using System.Collections.Generic;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Unity2D
{
    /// <summary>Everything the placeholder ragdoll builder needs: body sizes, reach, segmentation (D2), scale, weapons and look.</summary>
    public sealed class RagdollBuildSpec
    {
        public RagdollSettings Body { get; }

        /// <summary>Shoulder position and arm length, so the ragdoll's reach matches the §6 reach limit.</summary>
        public PathSettings Paths { get; }

        public RagdollSegmentation Segmentation { get; }
        public ArenaSpace Space { get; }

        /// <summary>One held-item body is built per weapon, sized from its stats (length, ink thickness as hit width, mass).</summary>
        public IReadOnlyList<WeaponStats> Weapons { get; }

        /// <summary>Null = no renderers (physics-only dummies for tests).</summary>
        public RagdollSprites Sprites { get; }

        public RagdollBuildSpec(RagdollSettings body, PathSettings paths, RagdollSegmentation segmentation, ArenaSpace space,
            IReadOnlyList<WeaponStats> weapons, RagdollSprites sprites)
        {
            Body = Guard.NotNull(body, nameof(body));
            Paths = Guard.NotNull(paths, nameof(paths));
            Segmentation = segmentation;
            Space = space;
            Weapons = Guard.NotNull(weapons, nameof(weapons));
            Sprites = sprites;
        }
    }
}
