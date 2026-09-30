using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a <see cref="CustomScenario"/> in the running gameplay scene: spawn point, goal (cloned
/// from a built-in goal so it behaves identically), trigger area, one waypoint route per vehicle,
/// and the vehicle schedule. Everything else — player placement, accident detection, replay,
/// feedback, CSV — is the shared built-in pipeline.
/// </summary>
[DisallowMultipleComponent]
public sealed class CustomScenarioRunner : MonoBehaviour
{
    const float TriggerHeight = 3f;

    GameDirector director;
    readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
    readonly List<Transform> routes = new List<Transform>();

    public CustomScenario Scenario { get; private set; }
    public Transform Root { get; private set; }
    public bool Triggered { get; private set; }
    public float StartedAt { get; private set; }
    public float TriggeredAt { get; private set; } = -1f;
    /// <summary>Vehicles spawned so far, in order (for tests and tooling).</summary>
    public int SpawnCount { get; private set; }

    /// <summary>Builds the scenario and activates it in <paramref name="runtime"/>. Errors are logged and returned.</summary>
    public static CustomScenarioRunner Setup(GameDirector director, ScenarioRuntime runtime, CustomScenario scenario,
        List<string> errors)
    {
        var runner = director.GetComponent<CustomScenarioRunner>();
        if (runner == null)
            runner = director.gameObject.AddComponent<CustomScenarioRunner>();
        if (!runner.Build(director, runtime, scenario, errors))
        {
            foreach (var error in errors)
                Debug.LogError($"[CustomScenario] {scenario?.id}: {error}", runner);
            return null;
        }
        return runner;
    }

    bool Build(GameDirector configuredDirector, ScenarioRuntime runtime, CustomScenario scenario, List<string> errors)
    {
        director = configuredDirector;
        Scenario = scenario;
        CollectModels();
        var template = runtime != null ? runtime.GoalTemplate : null;
        if (template == null)
            errors.Add("ゴールのひな形（シナリオ 01 のゴール）が見つかりません。");
        if (!TryGround(scenario.spawn.x, scenario.spawn.z, out var spawnY))
            errors.Add($"spawn ({scenario.spawn.x:0.#}, {scenario.spawn.z:0.#}) の下に地面がありません。");
        if (!TryGround(scenario.goal.x, scenario.goal.z, out var goalY))
            errors.Add($"goal ({scenario.goal.x:0.#}, {scenario.goal.z:0.#}) の下に地面がありません。");
        foreach (var vehicle in scenario.vehicles)
            if (!models.ContainsKey(vehicle.model))
                errors.Add($"車種 {vehicle.model} はありません（{string.Join(", ", models.Keys)}）。");
        if (errors.Count > 0)
            return false;

        Root = new GameObject("CustomScenario_" + scenario.id).transform;

        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(Root, false);
        spawn.position = new Vector3(scenario.spawn.x, spawnY + 1f, scenario.spawn.z);
        var toGoal = new Vector3(scenario.goal.x - scenario.spawn.x, 0f, scenario.goal.z - scenario.spawn.z);
        spawn.rotation = Quaternion.LookRotation(toGoal.normalized, Vector3.up);

        var goal = BuildGoal(template, scenario.goal, goalY);

        GameObject triggerObject = null;
        if (scenario.HasTrigger)
        {
            TryGround(scenario.trigger.x, scenario.trigger.z, out var triggerY);
            triggerObject = new GameObject("Trigger");
            triggerObject.transform.SetParent(Root, false);
            triggerObject.transform.SetPositionAndRotation(
                new Vector3(scenario.trigger.x, triggerY + TriggerHeight * 0.5f, scenario.trigger.z),
                Quaternion.Euler(0f, scenario.trigger.yaw, 0f));
            var box = triggerObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(scenario.trigger.width, TriggerHeight, scenario.trigger.depth);
            triggerObject.AddComponent<CustomScenarioTrigger>().Configure(this);
        }

        for (var i = 0; i < scenario.vehicles.Length; i++)
            routes.Add(BuildRoute(i, scenario.vehicles[i]));

        runtime.ActivateCustom(new ScenarioDefinition
        {
            id = CustomScenarioSession.EventNumber,
            displayName = scenario.name,
            playerMode = scenario.PlayerMode,
            root = Root.gameObject,
            playerSpawn = spawn,
            goal = goal,
            accidentArea = triggerObject,
            trafficFlow = ScenarioTrafficFlow.None,
            custom = scenario
        });
        return true;
    }

    /// <summary>Starts the vehicle schedule; call once the scenario is active.</summary>
    public void Begin()
    {
        StartedAt = Time.time;
        for (var i = 0; i < Scenario.vehicles.Length; i++)
            if (Scenario.vehicles[i].start == ScenarioVehicle.StartBegin)
                StartCoroutine(RunVehicle(i));
    }

    public void NotifyTriggerEntered()
    {
        if (Triggered || TrafficAccidentState.IsFrozen)
            return;
        Triggered = true;
        TriggeredAt = Time.time;
        for (var i = 0; i < Scenario.vehicles.Length; i++)
            if (Scenario.vehicles[i].start == ScenarioVehicle.StartTrigger)
                StartCoroutine(RunVehicle(i));
    }

    IEnumerator RunVehicle(int index)
    {
        var vehicle = Scenario.vehicles[index];
        if (vehicle.delaySeconds > 0f)
            yield return new WaitForSeconds(vehicle.delaySeconds);
        while (true)
        {
            if (TrafficAccidentState.IsFrozen || (vehicle.stopOnTrigger && Triggered))
                yield break;
            Spawn(index);
            if (!vehicle.IsRepeating)
                yield break;
            yield return new WaitForSeconds(UnityEngine.Random.Range(vehicle.repeatMinSeconds, vehicle.repeatMaxSeconds));
        }
    }

    void Spawn(int index)
    {
        var vehicle = Scenario.vehicles[index];
        var route = routes[index];
        var start = route.GetChild(0).position;
        var next = route.GetChild(1).position;
        var yaw = vehicle.overrideYaw
            ? vehicle.yaw
            : Quaternion.LookRotation(Vector3.ProjectOnPlane(next - start, Vector3.up).normalized, Vector3.up).eulerAngles.y;
        var car = VehiclePool.Instance.Get(models[vehicle.model], start, Quaternion.Euler(0f, yaw, 0f));
        var controller = car.GetComponent<CarController>();
        controller.CarID = director.CarID;
        director.CarID += 1;
        controller.pointsParent = route;
        controller.AccidentCar = vehicle.accident;
        controller.InitializeForSpawn();
        controller.SetCruiseSpeed(vehicle.SpeedMetersPerSecond);
        SpawnCount++;
    }

    GameObject BuildGoal(GameObject template, GroundArea area, float groundY)
    {
        var goal = Instantiate(template, Root);
        goal.name = "Goal";
        goal.SetActive(true);
        var height = template.transform.lossyScale.y;
        goal.transform.SetPositionAndRotation(
            new Vector3(area.x, groundY + 0.76f, area.z),
            Quaternion.Euler(0f, area.yaw, 0f));
        goal.transform.localScale = new Vector3(area.width, height, area.depth);
        // children (the floating "Goal" label) keep their own size
        foreach (Transform child in goal.transform)
        {
            if (child.GetComponent<ParticleSystem>() != null)
            {
                Destroy(child.gameObject);    // the old goal smoke; the success page replaces it
                continue;
            }
            child.localScale = new Vector3(1f / area.width, 1f / height, 1f / area.depth);
            child.localPosition = new Vector3(0f, child.localPosition.y, 0f);
        }
        return goal;
    }

    Transform BuildRoute(int index, ScenarioVehicle vehicle)
    {
        var route = new GameObject($"Route_{index}_{vehicle.name}").transform;
        route.SetParent(Root, false);
        for (var p = 0; p < vehicle.route.Length; p++)
        {
            var point = vehicle.route[p];
            var y = TryGround(point.x, point.z, out var ground) ? ground + 1f : 1.34f;
            var waypoint = new GameObject("P" + p).transform;
            waypoint.SetParent(route, false);
            waypoint.position = new Vector3(point.x, y, point.z);
        }
        route.gameObject.AddComponent<WaypointPath>().RebuildFromChildren();
        return route;
    }

    /// <summary>Vehicle models are the prefabs the scene's car factories already use.</summary>
    void CollectModels()
    {
        models.Clear();
        foreach (var factory in FindObjectsByType<CarFactory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (factory.CarPrefab != null && !models.ContainsKey(factory.CarPrefab.name))
                models.Add(factory.CarPrefab.name, factory.CarPrefab);
    }

    public IReadOnlyCollection<string> ModelNames => models.Keys;

    /// <summary>Walkable/drivable surface under (x, z): the first collider marked <see cref="GroundSurface"/>.</summary>
    public static bool TryGround(float x, float z, out float y)
    {
        var found = Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(found, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in found)
        {
            if (hit.collider.GetComponentInParent<GroundSurface>() == null)
                continue;
            y = hit.point.y;
            return true;
        }
        y = 0f;
        return false;
    }
}

/// <summary>The trigger area of a custom scenario: fires once when the participant enters it.</summary>
public sealed class CustomScenarioTrigger : MonoBehaviour
{
    CustomScenarioRunner runner;

    public void Configure(CustomScenarioRunner configuredRunner) => runner = configuredRunner;

    void OnTriggerEnter(Collider other)
    {
        if (runner == null)
            return;
        var isPlayer = PlayerActor.TryResolve(other, out _) || other.name == "Bicycle";
        if (isPlayer)
            runner.NotifyTriggerEntered();
    }
}
