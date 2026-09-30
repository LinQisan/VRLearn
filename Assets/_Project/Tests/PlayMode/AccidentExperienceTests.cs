using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRLearn.Tests.PlayMode
{
    public sealed class AccidentExperienceTests
    {
        static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp");
        static Component Find(string name) => UnityEngine.Object.FindFirstObjectByType(RuntimeType(name)) as Component;
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);

        [UnityTest]
        public IEnumerator RecordedReplayCompletesAndRetryRetainsParticipantSettings()
        {
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Gameplay_Hikone.unity");
            yield return new WaitForSeconds(0.6f);
            var director = Find("GameDirector");
            var hybrid = Find("HybridAccidentPresentation");
            var flow = Find("GameplayFlowController");
            var player = Find("PlayerActor");
            Assert.That(hybrid, Is.Not.Null);
            var vehicle = Find("CarController");
            float deadline = Time.realtimeSinceStartup + 10f;
            while (vehicle == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                vehicle = Find("CarController");
            }
            Assert.That(vehicle, Is.Not.Null, "The selected scenario must spawn a real vehicle.");
            yield return new WaitForSeconds(1f);
            Assert.That((bool)Call(flow, "TryTriggerAccident"), Is.True);
            Assert.That((bool)Call(hybrid, "BeginImpact", player, player.transform.position, vehicle.transform.forward, 8f, vehicle.transform), Is.True);
            var replay = hybrid.GetType().GetProperty("Replay").GetValue(hybrid);
            deadline = Time.realtimeSinceStartup + 5f;
            while (!(bool)replay.GetType().GetProperty("IsVisible").GetValue(replay) && Time.realtimeSinceStartup < deadline)
                yield return null;
            var recording = replay.GetType().GetProperty("Recording").GetValue(replay);
            Assert.That(recording, Is.Not.Null);
            Assert.That((bool)recording.GetType().GetProperty("IsValid").GetValue(recording), Is.True);
            Assert.That(recording.GetType().GetProperty("ImpactVehicle").GetValue(recording), Is.Not.Null,
                "The vehicle that hit the participant must be in the recording.");
            var damage = vehicle.GetComponent(RuntimeType("AccidentVehicleDamage"));
            Assert.That(damage, Is.Not.Null);
            Debug.Log("Recorded duration: " + recording.GetType().GetProperty("Duration").GetValue(recording));
            var driverCamera = (Camera)replay.GetType().GetProperty("DriverCamera").GetValue(replay);
            Assert.That(driverCamera.enabled, Is.True, "A real vehicle hit the participant: the driver's view is shown.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                SaveCamera(Camera.main, "recorded-replay.png");
            }
            deadline = Time.realtimeSinceStartup + 18f;
            while (flow.GetType().GetProperty("Phase").GetValue(flow).ToString() != "Results" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(flow.GetType().GetProperty("Phase").GetValue(flow).ToString(), Is.EqualTo("Results"));
            var results = Find("AccidentResultPresenter");
            Assert.That(Camera.main.cullingMask, Is.EqualTo(1 << LayerMask.NameToLayer("UI")),
                "Ragdoll geometry must not occlude the result buttons.");
            var metrics = Field(results, "runtimeMetrics");
            Assert.That((string)metrics.GetType().GetProperty("text").GetValue(metrics), Does.Contain("28.8"));
            var verdict = Field(results, "runtimeVerdict");
            Assert.That((string)verdict.GetType().GetProperty("text").GetValue(verdict), Is.Not.Empty);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                SaveCamera(Camera.main, "accident-results.png");
            director.GetType().GetField("EventNumber").SetValue(director, 9);
            director.GetType().GetField("Height").SetValue(director, 173);
            director.GetType().GetField("Weather").SetValue(director, 1);
            var csv = director.GetComponent(RuntimeType("CSVPrinter"));
            Call(results, "RetryScenario");
            // the retry saved this trial; keep test output out of the experiment data folder
            var written = (string[])csv.GetType().GetProperty("LastWrittenFiles").GetValue(csv);
            Assert.That(written.Length, Is.EqualTo(3), "Try again must save the finished trial.");
            foreach (var file in written)
                System.IO.File.Delete(file);
            yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            director = Find("GameDirector");
            Assert.That(director.GetType().GetField("EventNumber").GetValue(director), Is.EqualTo(9));
            Assert.That(director.GetType().GetField("Height").GetValue(director), Is.EqualTo(173));
            Assert.That(director.GetType().GetField("Weather").GetValue(director), Is.EqualTo(1));
            Assert.That(Find("GameplayFlowController").GetType().GetProperty("Phase").GetValue(Find("GameplayFlowController")).ToString(), Is.EqualTo("Playing"));
        }

        [UnityTest]
        public IEnumerator CollisionUsesActualSpeedBeforeTrafficFreeze()
        {
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Gameplay_Hikone.unity");
            var deadline = Time.realtimeSinceStartup + 8f;
            Component vehicle = null;
            while (vehicle == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                vehicle = Find("CarController");
            }
            Assert.That(vehicle, Is.Not.Null);
            vehicle.GetType().GetField("waypointSpeed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(vehicle, 2f);
            vehicle.GetType().GetField("Speed").SetValue(vehicle, 10f);
            Call(vehicle, "NotifyMetaImpact", Find("PlayerActor").GetComponent<CharacterController>());
            var hybrid = Find("HybridAccidentPresentation");
            var impact = hybrid.GetType().GetProperty("LastImpactPhysics").GetValue(hybrid);
            Assert.That(impact.GetType().GetProperty("ImpactSpeedMetersPerSecond").GetValue(impact), Is.EqualTo(2f),
                "A car travelling at 2 m/s must not be reported at its 10 m/s target speed.");
            Assert.That(Field(vehicle, "waypointSpeed"), Is.EqualTo(0f));
            Call(hybrid, "ForceResults");
        }

        [UnityTest]
        public IEnumerator DeformationUsesPrivateMeshAndPoolResetRestoresOriginal()
        {
            var root = new GameObject("DamageTest");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "BodyMain";
            body.transform.SetParent(root.transform);
            var filter = body.GetComponent<MeshFilter>();
            var shared = filter.sharedMesh;
            var original = shared.vertices;
            var damage = root.AddComponent(RuntimeType("AccidentVehicleDamage"));
            Call(damage, "Apply", Vector3.forward * 0.5f, Vector3.forward, 10f);
            Assert.That(filter.sharedMesh, Is.Not.SameAs(shared));
            CollectionAssert.AreEqual(original, shared.vertices);
            Call(damage, "ResetDamage");
            Assert.That(filter.sharedMesh, Is.SameAs(shared));
            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        static void SaveCamera(Camera camera, string name)
        {
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var texture = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/AccidentExperience-Visuals");
                File.WriteAllBytes("Logs/AccidentExperience-Visuals/" + name, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                texture.Release();
                UnityEngine.Object.Destroy(texture);
                UnityEngine.Object.Destroy(image);
            }
        }
    }
}
