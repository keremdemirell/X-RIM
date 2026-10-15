using System;
using System.Collections.Generic;
using XRim.Rules;
using XRim.Rules.Planning;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// What a presenter can show on the planning HUD and what the player can press on it. The HUD only displays and reports
    /// presses; the rules (through the plan source) decide what a press does. It never reads game state itself.
    /// </summary>
    public interface IPlanningHudView
    {
        /// <summary>The player pressed a weapon in the selector.</summary>
        event Action<WeaponId> WeaponChosen;

        /// <summary>The player pressed the Ready button (to be Ready, or to take it back).</summary>
        event Action ReadyPressed;

        /// <summary>Shows or hides the whole HUD (it only shows during planning).</summary>
        void SetVisible(bool visible);

        /// <summary>
        /// Where the layout puts things: the own edge of the screen is the left one unless mirrored (GDD §4). The body zone strip shows
        /// <paramref name="bodyZoneFraction"/> of the screen width (it covers the player's own dummy, Session 13); the weapon selector
        /// starts at <paramref name="selectorStartFraction"/>, the configured strip's edge, so it stays clear of the centred timer
        /// however far the zone reaches (a touch on a button is a press, never a swipe).
        /// </summary>
        void ShowLayout(bool mirrored, float bodyZoneFraction, float selectorStartFraction);

        void SetLoadout(IReadOnlyList<WeaponId> loadout);

        void ShowSelectedWeapon(WeaponId weapon);

        /// <summary>The weapon selector is disabled: the weapon-switch lock-out, a status effect, or Ready freezing the plan.</summary>
        void ShowSwitchLocked(bool locked, string reason);

        /// <summary>The planning timer. <paramref name="inLockout"/> is the final stretch in which switching is locked (GDD §6).</summary>
        void ShowCountdown(double secondsLeft, bool inLockout);

        /// <summary>Ink left as a share of the weapon's budget (D7).</summary>
        void ShowInkRemaining(float fraction);

        /// <summary>The body move set so far (the swipe's result), so a swipe never feels ignored.</summary>
        void ShowBodyMove(BodyMove move);

        /// <summary>The opponent's public state: weapon and Ready (GDD §3, §6). Nothing else of theirs is ever shown.</summary>
        void ShowOpponent(PublicPlanningState state);

        void ShowReady(ReadyButtonState state);

        /// <summary>A short note, such as why a command was refused; it fades by itself.</summary>
        void ShowMessage(string text);
    }
}
