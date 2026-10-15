using System;
using XRim.Core;
using XRim.Rules.Arena;

namespace XRim.Presentation.Cameras
{
    /// <summary>
    /// The duel framing (GDD §2 camera behaviour TBD; designer 2026-10-11, F3): a closer fixed side view centred between the two fighters,
    /// a set height so the dummies are big and distances read the same from turn to turn, widened only when the fighters stand too far
    /// apart to fit with room behind each of them, kept from showing far past the arena's edges, and with the floor near the bottom of the
    /// screen. Pure arithmetic over the pelvis positions, in arena units.
    /// </summary>
    public static class DuelFraming
    {
        public static ViewFraming Compute(float leftPelvisXUnits, float rightPelvisXUnits, float aspect, CameraFeelSettings settings,
            ArenaEdges? edges)
        {
            Guard.NotNull(settings, nameof(settings));
            Guard.Positive(aspect, nameof(aspect));
            float near = Math.Min(leftPelvisXUnits, rightPelvisXUnits);
            float far = Math.Max(leftPelvisXUnits, rightPelvisXUnits);

            float width = Math.Max(settings.DuelViewHeightUnits * aspect, far - near + 2f * settings.FramingMarginBehindUnits);
            float height = width / aspect;
            if (settings.MaxViewHeightUnits > 0f && height > settings.MaxViewHeightUnits) height = settings.MaxViewHeightUnits;
            width = height * aspect;

            float centreX = (near + far) * 0.5f;
            if (edges.HasValue)
            {
                ArenaEdges arena = edges.Value;
                float low = arena.LeftXUnits - settings.FramingEdgeMarginUnits + width * 0.5f;
                float high = arena.RightXUnits + settings.FramingEdgeMarginUnits - width * 0.5f;
                centreX = low <= high ? XMath.Clamp(centreX, low, high) : arena.CentreXUnits;
            }

            return new ViewFraming(new Vec2(centreX, height * 0.5f - settings.FramingFloorMarginUnits), height);
        }
    }
}
