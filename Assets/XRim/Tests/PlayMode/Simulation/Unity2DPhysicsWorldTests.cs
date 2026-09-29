using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.Config;
using XRim.Simulation.Unity2D;

namespace XRim.Tests.PlayMode.Simulation
{
    public sealed class Unity2DPhysicsWorldTests
    {
        private const int StepCount = 30;
        private const float StepSeconds = 1f / 240f;

        private SimulationMode2D _previousMode;

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
        }

        [TearDown]
        public void TearDown() => Physics2D.simulationMode = _previousMode;

        [UnityTest]
        public IEnumerator IsolatedPhysicsScene_MovesOnlyWhenStepped()
        {
            var world = new Unity2DPhysicsWorld(new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit));
            var probe = new GameObject("SimulationProbe", typeof(Rigidbody2D));
            SceneManager.MoveGameObjectToScene(probe, world.Scene);
            var probeBody = probe.GetComponent<Rigidbody2D>();
            var control = new GameObject("VisualSceneControl", typeof(Rigidbody2D));
            var controlBody = control.GetComponent<Rigidbody2D>();
            float probeStartY = probeBody.position.y;
            float controlStartY = controlBody.position.y;

            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(probeBody.position.y, Is.EqualTo(probeStartY), "Nothing may step the simulation scene automatically");

            for (int i = 0; i < StepCount; i++)
            {
                world.Step(StepSeconds);
            }

            Assert.That(probeBody.position.y, Is.LessThan(probeStartY), "Gravity acts when the world is stepped");
            Assert.That(controlBody.position.y, Is.EqualTo(controlStartY), "Stepping the simulation never moves the visual scene");

            Object.Destroy(control);
            world.Dispose();
            yield return new WaitUntil(() => !world.Scene.isLoaded);
        }
    }
}
