using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// <see cref="IPhysicsWorld"/> on Unity 2D physics (Rigidbody2D ragdolls joined by HingeJoint2D, GDD §18).
    /// Runs in its own hidden scene with a local PhysicsScene2D that only moves when <see cref="Step"/> is called,
    /// so a whole turn can be simulated faster than real time and nothing in the visual scene is affected.
    /// Requires Play mode (SceneManager.CreateScene is a runtime API).
    /// </summary>
    public sealed class Unity2DPhysicsWorld : IPhysicsWorld
    {
        private const string SceneNamePrefix = "XRim.SimulationPhysics.";

        private readonly PhysicsScene2D _physicsScene;
        private readonly List<ContactFacts> _pendingContacts = new List<ContactFacts>();

        public Scene Scene { get; }
        public ArenaSpace Space { get; }

        public Unity2DPhysicsWorld(ArenaSpace space)
        {
            Space = space;
            Scene = SceneManager.CreateScene(SceneNamePrefix + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _physicsScene = Scene.GetPhysicsScene2D();
        }

        public void Step(float deltaSeconds) => _physicsScene.Simulate(deltaSeconds);

        public void DrainContacts(List<ContactFacts> into)
        {
            into.AddRange(_pendingContacts);
            _pendingContacts.Clear();
        }

        public void Load(PoseSnapshot pose, MatchState state, RulesSettings settings)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.Load is not implemented yet.");
        }

        public void SetHeldItemTarget(Side side, BodyPose target)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.SetHeldItemTarget is not implemented yet.");
        }

        public void SetRootTarget(Side side, BodyPose target)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.SetRootTarget is not implemented yet.");
        }

        public void BreakJoint(Side side, BodyPart part)
        {
            // Placeholder: will call Ragdoll.BreakJoint on the side's ragdoll.
            throw new NotImplementedException("Unity2DPhysicsWorld.BreakJoint is not implemented yet.");
        }

        public void DropHeldItem(Side side)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.DropHeldItem is not implemented yet.");
        }

        public void ApplyImpulse(Side side, BodyPart part, Vec2 impulse)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.ApplyImpulse is not implemented yet.");
        }

        public BodyPose GetPose(Side side, BodyPart part)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.GetPose is not implemented yet.");
        }

        public PoseSnapshot CapturePose()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.CapturePose is not implemented yet.");
        }

        public bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("Unity2DPhysicsWorld.IsSettled is not implemented yet.");
        }

        public void Dispose()
        {
            if (Scene.IsValid() && Scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(Scene);
            }
        }
    }
}
