using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace VRLearn.Tests.EditMode
{
    /// <summary>The JSON scenario format: templates, validation messages, round trip.</summary>
    public sealed class CustomScenarioFormatTests
    {
        static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");
        static string TemplateFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Scenarios", "templates"));

        static object Parse(string json, List<string> errors) =>
            T("CustomScenario").GetMethod("FromJson").Invoke(null, new object[] { json, errors });

        static string Template(string id) => File.ReadAllText(Path.Combine(TemplateFolder, id + ".json"));

        static string Modify(string json, Action<object> change)
        {
            var type = T("CustomScenario");
            var scenario = typeof(JsonUtility).GetMethod("FromJson", new[] { typeof(string) })
                .MakeGenericMethod(type).Invoke(null, new object[] { json });
            change(scenario);
            return (string)type.GetMethod("ToJson").Invoke(scenario, null);
        }

        static void Set(object target, string field, object value) => target.GetType().GetField(field).SetValue(target, value);
        static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);

        [Test]
        public void EveryBuiltInTemplateIsAValidScenario()
        {
            var files = Directory.GetFiles(TemplateFolder, "builtin-*.json");
            Assert.That(files.Length, Is.EqualTo(10), "Export with Tools/VRLearn/Custom Scenarios/Export Built-in Scenarios As Templates.");
            foreach (var file in files)
            {
                var errors = new List<string>();
                Assert.That(Parse(File.ReadAllText(file), errors), Is.Not.Null, file + ": " + string.Join(" / ", errors));
            }
        }

        [Test]
        public void RoundTripKeepsTheScenarioAndRoundsCoordinates()
        {
            var json = Template("builtin-01");
            var errors = new List<string>();
            var scenario = Parse(json, errors);
            var again = (string)scenario.GetType().GetMethod("ToJson").Invoke(scenario, null);
            Assert.That(again, Is.EqualTo(json.TrimEnd('\n')));
            Assert.That(again, Does.Not.Match(@"\d\.\d{3,}"), "Coordinates are written to the centimetre.");
        }

        static IEnumerable<TestCaseData> BrokenFiles()
        {
            yield return new TestCaseData((Action<object>)(s => Set(s, "name", "")), "name").SetName("MissingName");
            yield return new TestCaseData((Action<object>)(s => Set(s, "id", "Bad Id!")), "id").SetName("BadId");
            yield return new TestCaseData((Action<object>)(s => Set(s, "version", 99)), "version").SetName("NewerVersion");
            yield return new TestCaseData((Action<object>)(s => Set(s, "playerMode", "flying")), "playerMode").SetName("UnknownMode");
            yield return new TestCaseData((Action<object>)(s => Set(Get(s, "spawn"), "x", 500f)), "地図の外").SetName("SpawnOffMap");
            yield return new TestCaseData((Action<object>)(s =>
            {
                Set(Get(s, "goal"), "x", Get(Get(s, "spawn"), "x"));
                Set(Get(s, "goal"), "z", Get(Get(s, "spawn"), "z"));
            }), "3 m").SetName("GoalOnSpawn");
            yield return new TestCaseData((Action<object>)(s => Set(Get(s, "trigger"), "width", 0f)), "trigger エリア").SetName("TriggerStartWithoutTrigger");
            yield return new TestCaseData((Action<object>)(s =>
            {
                var first = ((Array)Get(s, "vehicles")).GetValue(0);
                var route = (Array)Get(first, "route");
                var shorter = Array.CreateInstance(route.GetType().GetElementType(), 1);
                shorter.SetValue(route.GetValue(0), 0);
                Set(first, "route", shorter);
            }), "route").SetName("RouteWithOnePoint");
            yield return new TestCaseData((Action<object>)(s => Set(((Array)Get(s, "vehicles")).GetValue(0), "speedKmh", 200f)), "speedKmh").SetName("TooFast");
        }

        [TestCaseSource(nameof(BrokenFiles))]
        public void BrokenFilesAreRejectedWithAReadableReason(Action<object> breakIt, string mentioned)
        {
            var json = Modify(Template("builtin-01"), breakIt);
            var errors = new List<string>();
            Assert.That(Parse(json, errors), Is.Null);
            Assert.That(string.Join("\n", errors), Does.Contain(mentioned));
        }

        [Test]
        public void GarbageIsRejectedNotThrown()
        {
            var errors = new List<string>();
            Assert.That(Parse("{ this is not json", errors), Is.Null);
            Assert.That(errors, Is.Not.Empty);
        }
    }
}
