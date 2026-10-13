using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRLearn.Tests.EditMode
{
    public sealed class TrafficSignalTests
    {
        static Type Clock => Type.GetType("TrafficSignalCycle, Assembly-CSharp");
        static (string aspect, bool visible) At(float time, bool first, bool vertical, bool pedestrian = false)
        {
            object[] args = { time, first, vertical, pedestrian, false };
            var result = Clock.GetMethod("Evaluate").Invoke(null, args);
            return (result.ToString(), (bool)args[4]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OpposingVehicleGreensNeverOverlapAndPedestriansHaveNoYellow(bool first)
        {
            for (var time = 0f; time < 96f; time += 0.125f)
            {
                var vertical = At(time, first, true);
                var beside = At(time, first, false);
                Assert.That(vertical.aspect == "Green" && beside.aspect == "Green", Is.False, time.ToString());
                foreach (var direction in new[] { true, false })
                {
                    var walk = At(time, first, direction, true);
                    Assert.That(walk.aspect, Is.Not.EqualTo("Yellow"));
                    if (walk.aspect == "Green") Assert.That(At(time, first, direction).aspect, Is.EqualTo("Red"));
                }
            }
        }

        [Test]
        public void YellowClearanceAndPedestrianBlinkPrecedeTheDirectionChange()
        {
            Assert.That(At(0f, true, true).aspect, Is.EqualTo("Green"));
            Assert.That(At(20f, true, true).aspect, Is.EqualTo("Yellow"));
            Assert.That(At(23.5f, true, true).aspect, Is.EqualTo("Red"));
            Assert.That(At(23.5f, true, false).aspect, Is.EqualTo("Red"));
            Assert.That(At(24f, true, false).aspect, Is.EqualTo("Green"));
            Assert.That(At(48f, true, true).aspect, Is.EqualTo("Green"));
            Assert.That(At(15.5f, true, false, true).visible, Is.True);
            Assert.That(At(16.5f, true, false, true).visible, Is.False);
            Assert.That(At(17f, true, false, true).visible, Is.True);
            Assert.That(At(20f, true, false, true).aspect, Is.EqualTo("Red"));
        }

        [Test]
        public void SignalVisualsBindEveryLensWithoutAddingPhysics()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Gameplay_Hikone.unity", OpenSceneMode.Single);
            var heads = UnityEngine.Object.FindObjectsByType(Type.GetType("TrafficSignalPreset, Assembly-CSharp"), FindObjectsSortMode.None);
            Assert.That(heads.Length, Is.EqualTo(6));
            Assert.That(UnityEngine.Object.FindObjectsByType(Clock, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            foreach (Component head in heads)
            {
                var visual = head.transform.Find("HK_SignalVisual");
                Assert.That(visual, Is.Not.Null, head.name);
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(head.GetComponent<Renderer>().enabled, Is.False);
                foreach (var name in new[] { "RedLight", "YellowLight", "GreenLight" })
                {
                    var channel = head.transform.Find(name);
                    if (channel == null) continue;
                    var lamp = channel.GetComponent(Type.GetType("TrafficLightController, Assembly-CSharp"));
                    var renderers = (Renderer[])lamp.GetType().GetField("visualRenderers",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(lamp);
                    Assert.That(renderers.Length, Is.EqualTo(1), head.name + "/" + name);
                    Assert.That(renderers[0].transform.IsChildOf(visual), Is.True);
                    Assert.That(renderers[0].name, Is.EqualTo(name));
                }
            }
        }

        [Test]
        public void LensChangesKeepSharedMaterialsAndIndependentSubmeshEmission()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var red = new Material(shader);
            var glyph = new Material(shader);
            try
            {
                red.SetColor("_BaseColor", Color.red);
                glyph.SetColor("_BaseColor", Color.white);
                red.SetColor("_EmissionColor", Color.red * 0.38f);
                glyph.SetColor("_EmissionColor", Color.white * 0.18f);
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterials = new[] { red, glyph };
                var lamp = go.AddComponent(Type.GetType("TrafficLightController, Assembly-CSharp"));
                var set = lamp.GetType().GetMethod("SetLit");
                set.Invoke(lamp, new object[] { true });
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 1);
                Assert.That(block.GetColor("_EmissionColor").r, Is.EqualTo(0.18f).Within(0.001f));
                set.Invoke(lamp, new object[] { false });
                renderer.GetPropertyBlock(block, 0);
                Assert.That(block.GetColor("_EmissionColor").maxColorComponent, Is.Zero);
                Assert.That(red.GetColor("_BaseColor"), Is.EqualTo(Color.red));
                Assert.That(red.GetColor("_EmissionColor").r, Is.EqualTo(0.38f).Within(0.001f));
                Assert.That(renderer.sharedMaterials[0], Is.SameAs(red));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(red);
                UnityEngine.Object.DestroyImmediate(glyph);
            }
        }
    }
}
