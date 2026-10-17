using XRim.Core;

namespace XRim.Presentation.UI
{
    /// <summary>What the match HUD shows through the whole match (planning and playback); it never catches a touch.</summary>
    public interface IMatchHudView
    {
        void SetVisible(bool visible);

        /// <param name="onScreenLeft">
        /// Which half of the screen the fighter's bar sits in: under its own dummy, so it follows the view flip (GDD §4).
        /// </param>
        void ShowFighter(Side side, string name, float hp, float maxHp, bool onScreenLeft);

        /// <summary>The turn counter, for example "TURN 4 / 30".</summary>
        void ShowTurn(string text);

        /// <summary>A banner under the timer (sudden death); an empty title hides it.</summary>
        void ShowBanner(string title, string detail);

        /// <summary>
        /// The name of the signature move a fighter performs, shouted over its HP bar while the turn plays (GDD §8: the absurd name is
        /// the joke); empty hides it. <paramref name="onScreenLeft"/> follows the view flip like the bar.
        /// </summary>
        void ShowSignatureShout(Side side, string text, bool onScreenLeft);
    }
}
