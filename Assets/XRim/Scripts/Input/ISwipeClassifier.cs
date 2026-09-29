using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules;

namespace XRim.Input
{
    /// <summary>Turns a flick in the body zone into a body move. Directions are relative to the player's own facing.</summary>
    [GddTbd("§5", "Diagonal or combined swipes", Proposal = "Not allowed")]
    public interface ISwipeClassifier
    {
        BodyMove Classify(Vec2 startPixels, Vec2 endPixels, float durationSeconds, ScreenLayout layout);
    }
}
