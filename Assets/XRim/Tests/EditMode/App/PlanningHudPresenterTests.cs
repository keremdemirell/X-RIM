using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Input;
using XRim.Networking;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Input;
using XRim.Tests.EditMode.Networking;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// The planning HUD shows what the rules allow right now (GDD §3, §6): the countdown and the weapon-switch lock-out, Ready and
    /// whether it can be taken back (D9), the ink left (D7), the opponent's public state; presses become commands and refusals show.
    /// </summary>
    public sealed class PlanningHudPresenterTests
    {
        private const double Deadline = 12.0;
        private const double LockoutStarts = 10.5;

        private sealed class FakeHud : IPlanningHudView
        {
            public event Action<WeaponId> WeaponChosen;
            public event Action ReadyPressed;

            public bool Visible { get; private set; }
            public bool Mirrored { get; private set; }
            public float BodyZoneFraction { get; private set; }
            public IReadOnlyList<WeaponId> Loadout { get; private set; }
            public int LoadoutCalls { get; private set; }
            public WeaponId Selected { get; private set; }
            public bool SwitchLocked { get; private set; }
            public string LockReason { get; private set; } = string.Empty;
            public double SecondsLeft { get; private set; }
            public bool InLockout { get; private set; }
            public float Ink { get; private set; } = -1f;
            public BodyMove Move { get; private set; }
            public PublicPlanningState Opponent { get; private set; }
            public ReadyButtonState Ready { get; private set; }
            public List<string> Messages { get; } = new List<string>();

            public void SetVisible(bool visible) => Visible = visible;

            public void ShowLayout(bool mirrored, float bodyZoneWidthFraction)
            {
                Mirrored = mirrored;
                BodyZoneFraction = bodyZoneWidthFraction;
            }

            public void SetLoadout(IReadOnlyList<WeaponId> loadout)
            {
                Loadout = loadout;
                LoadoutCalls++;
            }

            public void ShowSelectedWeapon(WeaponId weapon) => Selected = weapon;

            public void ShowSwitchLocked(bool locked, string reason)
            {
                SwitchLocked = locked;
                LockReason = reason;
            }

            public void ShowCountdown(double secondsLeft, bool inLockout)
            {
                SecondsLeft = secondsLeft;
                InLockout = inLockout;
            }

            public void ShowInkRemaining(float fraction) => Ink = fraction;

            public void ShowBodyMove(BodyMove move) => Move = move;

            public void ShowOpponent(PublicPlanningState state) => Opponent = state;

            public void ShowReady(ReadyButtonState state) => Ready = state;

            public void ShowMessage(string text) => Messages.Add(text);

            public void PressWeapon(WeaponId weapon) => WeaponChosen?.Invoke(weapon);

            public void PressReady() => ReadyPressed?.Invoke();
        }

        private FakeHud _hud;
        private FakeTurnAuthority _authority;
        private SchemeRig _rig;
        private MatchSettings _match;
        private TouchPlanSource _left;
        private PlanningHudPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _hud = new FakeHud();
            _authority = new FakeTurnAuthority();
            _rig = new SchemeRig();
            _match = GddStartingValues.CreateRulesSettings().Match;
            _left = new TouchPlanSource(Side.Left, new StubInputScheme(), _rig.Layout);
            _left.OnPlanningStarted(_rig.Window, _rig.Sink);
            _presenter = new PlanningHudPresenter(_hud, _authority, TestData.CreateMatchSetup(), _match) { Source = _left };
        }

        [TearDown]
        public void TearDown() => _presenter.Dispose();

        [Test]
        public void TheHudShowsOnlyWhileAHumanIsPlanning()
        {
            _presenter.Source = null;
            _presenter.Tick(0.0, null);
            Assert.That(_hud.Visible, Is.False, "no human");

            _presenter.Source = _left;
            _presenter.Tick(0.0, null);
            Assert.That(_hud.Visible, Is.True);

            _left.OnPlanningEnded();
            _presenter.Tick(1.0, null);
            Assert.That(_hud.Visible, Is.False, "plans are locked");
        }

        [Test]
        public void ANewPlanningPhase_ShowsTheLoadoutTheWeaponInHandAndAFreshBoard()
        {
            _presenter.Tick(0.0, null);

            Assert.That(_hud.Loadout, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }));
            Assert.That(_hud.Selected, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(_hud.Move, Is.EqualTo(BodyMove.None));
            Assert.That(_hud.Ready, Is.EqualTo(ReadyButtonState.Ready));
            Assert.That(_hud.SwitchLocked, Is.False);
            Assert.That(_hud.Ink, Is.EqualTo(1f));
            Assert.That(_hud.Mirrored, Is.False);
            Assert.That(_hud.BodyZoneFraction, Is.EqualTo(SchemeRig.BodyZoneFraction));
            Assert.That(_hud.Opponent.Weapon, Is.EqualTo(WeaponIds.Mace), "what everyone sees until the opponent changes something");
            Assert.That(_hud.Opponent.IsReady, Is.False);
        }

        [Test]
        public void TheCountdownRunsToTheDeadline_AndTheLockoutStartsAtTheTunableBoundary()
        {
            _presenter.Tick(2.0, null);
            Assert.That(_hud.SecondsLeft, Is.EqualTo(10.0).Within(1e-6));
            Assert.That(_hud.InLockout, Is.False);
            Assert.That(_hud.SwitchLocked, Is.False);

            _presenter.Tick(LockoutStarts - 0.001, null);
            Assert.That(_hud.InLockout, Is.False);

            _presenter.Tick(LockoutStarts, null);
            Assert.That(_hud.InLockout, Is.True, "§6: switching is disabled in the final 1.5 s");
            Assert.That(_hud.SwitchLocked, Is.True);
            Assert.That(_hud.LockReason, Does.Contain("LOCKED"));
            Assert.That(_hud.SecondsLeft, Is.EqualTo(1.5).Within(1e-6));
        }

        [Test]
        public void ALongerLockoutSettingMovesTheBoundary()
        {
            _match.WeaponSwitchLockoutSeconds = 3f;

            _presenter.Tick(Deadline - 3.0, null);

            Assert.That(_hud.InLockout, Is.True);
        }

        [Test]
        public void AStatusEffectThatForbidsSwitching_DisablesTheSelector()
        {
            _rig.Window.Constraints[Side.Left].CanSwitchWeapon = false;

            _presenter.Tick(1.0, null);

            Assert.That(_hud.SwitchLocked, Is.True);
            Assert.That(_hud.LockReason, Does.Contain("DISABLED"));
        }

        [Test]
        public void PressingAWeapon_SendsThePickToTheRules_AndTheHudFollowsWhatWasAccepted()
        {
            _presenter.Tick(1.0, null);

            _hud.PressWeapon(WeaponIds.Mace);
            _presenter.Tick(1.1, null);

            Assert.That(_rig.Sink.Only<SelectWeaponCommand>().Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(_hud.Selected, Is.EqualTo(WeaponIds.Mace));
        }

        [Test]
        public void Ready_IsPressedAndTakenBack_AndFreezesTheSelectorWhileItStands()
        {
            _presenter.Tick(1.0, null);

            _hud.PressReady();
            _presenter.Tick(1.1, null);
            Assert.That(_rig.Sink.Commands[0], Is.InstanceOf<SetReadyCommand>().With.Property("IsReady").True);
            Assert.That(_hud.Ready, Is.EqualTo(ReadyButtonState.Cancel), "D9: it can be taken back before the lock-out");
            Assert.That(_hud.SwitchLocked, Is.True);
            Assert.That(_hud.LockReason, Does.Contain("READY"));

            _hud.PressReady();
            _presenter.Tick(1.2, null);
            Assert.That(_rig.Sink.Commands[1], Is.InstanceOf<SetReadyCommand>().With.Property("IsReady").False);
            Assert.That(_hud.Ready, Is.EqualTo(ReadyButtonState.Ready));
            Assert.That(_hud.SwitchLocked, Is.False);
        }

        [Test]
        public void ReadyCannotBeTakenBack_InTheLockout_OrWhenTheRuleIsOff()
        {
            _hud.PressReady();
            _presenter.Tick(LockoutStarts, null);
            Assert.That(_hud.Ready, Is.EqualTo(ReadyButtonState.LockedIn), "D9 default: no cancel in the lock-out");

            _match.AllowReadyCancelDuringLockout = true;
            _presenter.Tick(LockoutStarts + 0.1, null);
            Assert.That(_hud.Ready, Is.EqualTo(ReadyButtonState.Cancel));

            _match.AllowReadyCancel = false;
            _presenter.Tick(LockoutStarts + 0.2, null);
            Assert.That(_hud.Ready, Is.EqualTo(ReadyButtonState.LockedIn));
        }

        [Test]
        public void TheBodyMoveSetByASwipe_IsShown()
        {
            _left.Send(new SetBodyMoveCommand(BodyMove.Crouch));

            _presenter.Tick(1.0, null);

            Assert.That(_hud.Move, Is.EqualTo(BodyMove.Crouch));
        }

        [Test]
        public void D7_TheInkLeftComesFromThePreview()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            PathPreview preview = new PathPreviewBuilder(new RulePolicies()).Build(new[] { new Vec2(100f, 100f), new Vec2(250f, 100f) },
                new Vec2(100f, 100f), rapier, new PathSettings());

            _presenter.Tick(1.0, preview);
            Assert.That(_hud.Ink, Is.EqualTo(preview.InkRemainingFraction));
            Assert.That(_hud.Ink, Is.LessThan(1f));

            _presenter.Tick(1.1, null);
            Assert.That(_hud.Ink, Is.EqualTo(1f), "no stroke, all the ink");
        }

        [Test]
        public void TheOpponentsPublicState_IsShown_AndForgottenWhenPlanningLocks()
        {
            _presenter.Tick(1.0, null);

            _authority.RaisePublicStateChanged(Side.Right, new PublicPlanningState(WeaponIds.Spear, true, false));
            _presenter.Tick(1.1, null);
            Assert.That(_hud.Opponent.Weapon, Is.EqualTo(WeaponIds.Spear));
            Assert.That(_hud.Opponent.IsReady, Is.True);

            _authority.RaisePublicStateChanged(Side.Left, new PublicPlanningState(WeaponIds.Shield, true, false));
            _presenter.Tick(1.2, null);
            Assert.That(_hud.Opponent.Weapon, Is.EqualTo(WeaponIds.Spear), "only the other side is ever shown");

            _authority.RaisePlanningLocked(0);
            _presenter.Tick(1.3, null);
            Assert.That(_hud.Opponent.Weapon, Is.EqualTo(WeaponIds.Mace), "back to what the board says");
            Assert.That(_hud.Opponent.IsReady, Is.False);
        }

        [Test]
        public void EveryRefusal_ShowsItsReason_OnceEach()
        {
            _presenter.Tick(1.0, null);
            int before = _hud.Messages.Count;
            _rig.Sink.Result = CommandResult.Reject(CommandRejection.WeaponSwitchLockedOut);

            _hud.PressWeapon(WeaponIds.Mace);
            _presenter.Tick(1.1, null);
            _presenter.Tick(1.2, null);
            Assert.That(_hud.Messages.Count, Is.EqualTo(before + 1), "one refusal, one note");
            Assert.That(_hud.Messages[_hud.Messages.Count - 1], Is.EqualTo(RejectionMessages.Text(CommandRejection.WeaponSwitchLockedOut, 1.5f)));

            _hud.PressWeapon(WeaponIds.Shield);
            _presenter.Tick(1.3, null);
            Assert.That(_hud.Messages.Count, Is.EqualTo(before + 2), "the same reason again still shows");
        }

        [Test]
        public void ALayoutChange_IsShownAtOnce()
        {
            _presenter.Tick(1.0, null);

            _left.Layout = new ScreenLayout(SchemeRig.Width, SchemeRig.Height, 0.2f, true, Side.Left);
            _presenter.Tick(1.1, null);

            Assert.That(_hud.Mirrored, Is.True);
            Assert.That(_hud.BodyZoneFraction, Is.EqualTo(0.2f));
        }

        [Test]
        public void AnotherHuman_GetsTheirOwnLoadoutAndOpponent()
        {
            var right = new TouchPlanSource(Side.Right, new StubInputScheme(), new ScreenLayout(SchemeRig.Width, SchemeRig.Height, 0.15f, false, Side.Right));
            right.OnPlanningStarted(_rig.Window, _rig.Sink);
            _presenter.Tick(1.0, null);
            int calls = _hud.LoadoutCalls;

            _presenter.Source = right;
            _presenter.Tick(1.1, null);

            Assert.That(_hud.LoadoutCalls, Is.EqualTo(calls + 1));
            Assert.That(_hud.Loadout, Is.EqualTo(new[] { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield }));
            Assert.That(_hud.Selected, Is.EqualTo(WeaponIds.Mace));
            Assert.That(_hud.Opponent.Weapon, Is.EqualTo(WeaponIds.Rapier));
        }

        [Test]
        public void AfterDispose_NothingReactsAnyMore()
        {
            _presenter.Dispose();

            _hud.PressReady();
            _hud.PressWeapon(WeaponIds.Mace);
            _authority.RaisePublicStateChanged(Side.Right, new PublicPlanningState(WeaponIds.Spear, true, false));

            Assert.That(_rig.Sink.Commands, Is.Empty);
        }
    }
}
