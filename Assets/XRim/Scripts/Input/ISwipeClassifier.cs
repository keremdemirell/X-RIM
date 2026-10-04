using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules;

namespace XRim.Input
{
    /// <summary>Turns a flick in the body zone into a body move. Directions are relative to the player's own facing.</summary>
    [GddTbd("§5", "Diagonal or combined swipes", Proposal = "Not allowed")]
    public interface ISwipeClassifier
    {
        /// <summary>
        /// The move the flick from <paramref name="startPixels"/> to <paramref name="endPixels"/> means, or null when it is
        /// not a swipe (too short, too slow, or pointing between two directions). Pixels are Unity screen pixels (Y up).
        /// </summary>
        BodyMove? Classify(Vec2 startPixels, Vec2 endPixels, float durationSeconds, ScreenLayout layout);
    }
}
