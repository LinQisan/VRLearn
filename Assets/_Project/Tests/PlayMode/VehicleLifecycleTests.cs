using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRLearn.Tests.PlayMode
{
    /// <summary>
    /// Minimal lifecycle contract tests for the vehicle pool. Follows the
    /// existing suite's reflection-only style: the PlayMode test assembly
    /// does not reference game types at compile time.
    /// Contract: Acquire -&gt; Configure -&gt; InitializeForSpawn -&gt; Run
    /// -&gt; Release (explicit PrepareForPoolReuse) -&gt; Pool.
    /// New and reused instances must observe identical spawn configuration.
    /// </summary>
    public sealed class VehicleLifecycleTests
    {
        const string GameplayScene = "Assets/_Project/Scenes/TraficAcident_Meta.unity";
        const string GameAssembly = "Assembly-CSharp";

        static Type GameType(string name) => Type.GetType(name + ", " + GameAssembly);

        static object PoolInstance()
        {
            var poolType = GameType("VehiclePool");
            Assert.That(poolType, Is.Not.Null);
            return poolType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        static GameObject PoolGet(object pool, GameObject prefab)
        {
            return pool.GetType().GetMethod("Get")?.Invoke(
                pool, new object[] { prefab, Vector3.zero, Quaternion.identity }) as GameObject;
        }

        static bool PoolRelease(object pool, GameObject vehicle)
        {
            return (bool)pool.GetType().GetMethod("Release")?.Invoke(pool, new object[] { vehicle });
        }

        static bool PoolIsPooled(object pool, GameObject vehicle)
        {
            return (bool)pool.GetType().GetMethod("IsPooled")?.Invoke(pool, new object[] { vehicle });
        }

        static Component ControllerOf(GameObject vehicle)
        {
            return vehicle.GetComponent(GameType("CarController"));
        }

        IEnumerator LoadGameplay()
        {
            var load = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            // Let Awake/Start of scene systems (registry, factories, player) run.
            yield return null;
            yield return null;
        }

        static GameObject FindCarPrefab()
        {
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType(
                typeof(MonoBehaviour), FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.GetType().Name != "CarFactory")
                    continue;
                var prefab = behaviour.GetType().GetField("CarPrefab")?.GetValue(behaviour) as GameObject;
                if (prefab != null)
                    return prefab;
            }
            return null;
        }

        static List<Component> FindRoutes(int minimumPoints)
        {
            var result = new List<Component>();
            // Most scenario roots are inactive; routes live under all of them.
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType(
                typeof(MonoBehaviour), FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.GetType().Name != "WaypointPath")
                    continue;
                var points = behaviour.GetType().GetProperty("Points")?.GetValue(behaviour) as Transform[];
                if (points != null && points.Length >= minimumPoints)
                    result.Add(behaviour as Component);
            }
            return result;
        }

        static Transform[] GetAuthoredPoints(Component route)
        {
            return route.GetType().GetProperty("Points")?.GetValue(route) as Transform[];
        }

        static Transform[] GetRuntimePoints(GameObject vehicle)
        {
            var field = ControllerOf(vehicle).GetType().GetField(
                "points", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(ControllerOf(vehicle)) as Transform[];
        }

        static int GetIgnorePairCount(GameObject vehicle)
        {
            var field = ControllerOf(vehicle).GetType().GetField(
                "ignoredCollisionPairs", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = field?.GetValue(ControllerOf(vehicle)) as System.Collections.IList;
            return list?.Count ?? -1;
        }

        static void ConfigureLikeFactory(GameObject vehicle, Component route, bool accidentCar, int carId)
        {
            var controller = ControllerOf(vehicle);
            var type = controller.GetType();
            type.GetField("CarID")?.SetValue(controller, carId);
            type.GetField("pointsParent")?.SetValue(controller, route.transform);
            type.GetField("AcidentCar")?.SetValue(controller, accidentCar);
            type.GetMethod("InitializeForSpawn")?.Invoke(controller, null);
        }

        static void SnapshotInitState(
            GameObject vehicle,
            out float speed, out bool accidentCar, out int accidentNumber,
            out int destPoint, out int eventNumber, out Transform[] points)
        {
            var controller = ControllerOf(vehicle);
            var type = controller.GetType();
            speed = (float)(type.GetField("Speed")?.GetValue(controller) ?? float.NaN);
            accidentCar = (bool)(type.GetField("AcidentCar")?.GetValue(controller) ?? true);
            accidentNumber = (int)(type.GetField("AcidentCarNumber",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(controller) ?? -1);
            destPoint = (int)(type.GetField("destPoint",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(controller) ?? -1);
            eventNumber = (int)(type.GetField("EventNumber",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(controller) ?? int.MinValue);
            points = GetRuntimePoints(vehicle);
        }

        [UnityTest]
        public IEnumerator FirstUseAndReuseObserveIdenticalSpawnConfig()
        {
            yield return LoadGameplay();
            var pool = PoolInstance();
            Assert.That(pool, Is.Not.Null);
            var prefab = FindCarPrefab();
            Assert.That(prefab, Is.Not.Null, "Gameplay scene must provide a CarFactory prefab.");
            var routes = FindRoutes(3);
            Assert.That(routes.Count, Is.GreaterThanOrEqualTo(1));
            var route = routes[0];

            var first = PoolGet(pool, prefab);
            Assert.That(first, Is.Not.Null);
            ConfigureLikeFactory(first, route, true, 101);
            SnapshotInitState(first, out _, out _, out _,
                out var destInit1, out _, out _);
            Assert.That(destInit1, Is.EqualTo(0), "Fresh init must start at waypoint 0.");
            // Cover the deferred Start-after-Initialize order for new instances.
            // (The car may legitimately drive during these frames, so motion
            // state is not compared afterwards; init-owned state is.)
            yield return null;
            yield return null;
            SnapshotInitState(first, out var speed1, out var ac1, out var num1,
                out _, out var ev1, out var points1);
            Assert.That(speed1, Is.EqualTo(10f).Within(0.01f),
                "Speed baseline must be the established 10 m/s.");
            Assert.That(GetIgnorePairCount(first), Is.GreaterThan(0),
                "Configure must establish IgnoreCollision pairs for this life.");
            Assert.That(PoolRelease(pool, first), Is.True);

            var second = PoolGet(pool, prefab);
            Assert.That(second, Is.SameAs(first), "Single pooled instance must be reused.");
            ConfigureLikeFactory(second, route, true, 102);
            SnapshotInitState(second, out _, out _, out _,
                out var destInit2, out _, out _);
            Assert.That(destInit2, Is.EqualTo(0), "Re-init must restart at waypoint 0.");
            yield return null;
            yield return null;
            SnapshotInitState(second, out var speed2, out var ac2, out var num2,
                out _, out var ev2, out var points2);

            Assert.That(speed2, Is.EqualTo(speed1), "Speed baseline must match first use.");
            Assert.That(ac2, Is.EqualTo(ac1));
            Assert.That(num2, Is.EqualTo(1), "Reused accident car must report AcidentCarNumber=1.");
            Assert.That(num2, Is.EqualTo(num1), "First-use and reuse accident numbers must match.");
            Assert.That(ev2, Is.EqualTo(ev1));
            Assert.That(points2.Length, Is.EqualTo(points1.Length));
            for (var i = 0; i < points2.Length; i++)
                Assert.That(points2[i], Is.SameAs(points1[i]),
                    "Same spawn config must resolve the same runtime route.");
        }

        [UnityTest]
        public IEnumerator DifferentSpawnConfigDoesNotBleedAcrossReuse()
        {
            yield return LoadGameplay();
            var pool = PoolInstance();
            Assert.That(pool, Is.Not.Null);
            var prefab = FindCarPrefab();
            Assert.That(prefab, Is.Not.Null);
            var routes = FindRoutes(3);
            Assert.That(routes.Count, Is.GreaterThanOrEqualTo(2),
                "Need two distinct routes to prove no bleed.");
            var routeA = routes[0];
            var routeB = routes[1];
            Assert.That(routeB, Is.Not.SameAs(routeA));

            var vehicle = PoolGet(pool, prefab);
            ConfigureLikeFactory(vehicle, routeA, true, 201);
            yield return null;
            SnapshotInitState(vehicle, out _, out _, out var numA, out _, out _, out _);
            Assert.That(numA, Is.EqualTo(1));
            Assert.That(PoolRelease(pool, vehicle), Is.True);
            Assert.That(GetIgnorePairCount(vehicle), Is.EqualTo(0),
                "Release must restore IgnoreCollision pairs.");

            var reused = PoolGet(pool, prefab);
            Assert.That(reused, Is.SameAs(vehicle));
            ConfigureLikeFactory(reused, routeB, false, 202);
            yield return null;
            SnapshotInitState(reused, out _, out var acB, out var numB, out _, out _, out var pointsB);

            Assert.That(acB, Is.False, "Normal spawn must not inherit AcidentCar=true.");
            Assert.That(numB, Is.EqualTo(0));
            var authoredB = GetAuthoredPoints(routeB);
            Assert.That(pointsB.Length, Is.EqualTo(authoredB.Length));
            for (var i = 0; i < pointsB.Length; i++)
                Assert.That(pointsB[i], Is.SameAs(authoredB[i]),
                    "Route B must be fully effective, no route A remnants.");
        }

        [UnityTest]
        public IEnumerator DoubleReleasePoolsExactlyOnce()
        {
            yield return LoadGameplay();
            var pool = PoolInstance();
            Assert.That(pool, Is.Not.Null);
            var prefab = FindCarPrefab();
            Assert.That(prefab, Is.Not.Null);
            var routes = FindRoutes(1);
            Assert.That(routes.Count, Is.GreaterThanOrEqualTo(1));

            var vehicle = PoolGet(pool, prefab);
            ConfigureLikeFactory(vehicle, routes[0], false, 301);
            yield return null;

            Assert.That(PoolRelease(pool, vehicle), Is.True);
            Assert.That(PoolIsPooled(pool, vehicle), Is.True);
            Assert.That(PoolRelease(pool, vehicle), Is.True,
                "Second release must be an idempotent success, never a Destroy trigger.");

            var first = PoolGet(pool, prefab);
            ConfigureLikeFactory(first, routes[0], false, 302);
            var second = PoolGet(pool, prefab);
            Assert.That(second, Is.Not.SameAs(first),
                "Two consecutive Gets must never hand out the same instance.");
        }

        [UnityTest]
        public IEnumerator RuntimeWaypointEditIsIsolatedPerVehicle()
        {
            yield return LoadGameplay();
            var pool = PoolInstance();
            Assert.That(pool, Is.Not.Null);
            var prefab = FindCarPrefab();
            Assert.That(prefab, Is.Not.Null);
            var routes = FindRoutes(3);
            Assert.That(routes.Count, Is.GreaterThanOrEqualTo(2));
            var sharedRoute = routes[0];
            var otherRoute = routes[1];
            var authoredFirst = GetAuthoredPoints(sharedRoute)[0];

            var vehicleA = PoolGet(pool, prefab);
            ConfigureLikeFactory(vehicleA, sharedRoute, false, 401);
            var vehicleB = PoolGet(pool, prefab);
            ConfigureLikeFactory(vehicleB, sharedRoute, false, 402);
            yield return null;

            var pointsA = GetRuntimePoints(vehicleA);
            var pointsB = GetRuntimePoints(vehicleB);
            Assert.That(pointsA, Is.Not.Null);
            Assert.That(pointsB, Is.Not.Null);
            Assert.That(pointsA, Is.Not.SameAs(pointsB),
                "Each vehicle must own its runtime route array.");
            pointsA[0] = GetAuthoredPoints(otherRoute)[0];

            Assert.That(GetRuntimePoints(vehicleB)[0], Is.SameAs(authoredFirst),
                "Editing A must not affect B.");
            Assert.That(GetAuthoredPoints(sharedRoute)[0], Is.SameAs(authoredFirst),
                "Editing A must not pollute the authored route definition.");
        }
    }
}
