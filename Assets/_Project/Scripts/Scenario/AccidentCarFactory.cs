using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Trigger at the scenario's accident area. When the participant enters it, runs the active
/// scenario's accident schedule (<see cref="ScenarioDefinitionAsset.accidentLaunches"/>):
/// stops the listed regular traffic, then launches each accident car after its delay.
/// </summary>
public class AccidentCarFactory : MonoBehaviour
{
    [SerializeField] GameObject carfactory_Left;
    [SerializeField] GameObject carfactory_Right;
    [SerializeField] GameObject carfactory_Left2;
    [SerializeField] GameObject carfactory_Right2;
    [SerializeField] GameObject gamedirector;

    // true once the schedule has started (one run per scenario)
    [FormerlySerializedAs("AcidentPrediction")] [SerializeField] bool AccidentPrediction;

    ScenarioDefinitionAsset definition;

    public ScenarioDefinitionAsset Definition => definition;

    void OnTriggerEnter(Collider collider)
    {
        if (TrafficAccidentState.IsFrozen || AccidentPrediction)
            return;

        var isTrackedPlayer = PlayerActor.TryResolve(collider, out _);
        var isLegacyHuman = collider.CompareTag("Human") && collider.name == "Human";
        if (!isTrackedPlayer && !isLegacyHuman)
            return;
        if (definition == null || definition.accidentLaunches == null || definition.accidentLaunches.Length == 0)
            return;

        AccidentPrediction = true;
        if (definition.stopTraffic != null)
            foreach (var key in definition.stopTraffic)
            {
                var factory = Resolve(key);
                if (factory != null)
                    factory.AccidentCarChange();
            }
        foreach (var launch in definition.accidentLaunches)
            StartCoroutine(Launch(launch));
    }

    IEnumerator Launch(AccidentLaunch launch)
    {
        if (launch.delaySeconds > 0f)
            yield return new WaitForSeconds(launch.delaySeconds);
        if (TrafficAccidentState.IsFrozen)
            yield break;
        var factory = Resolve(launch.factory);
        if (factory != null)
            factory.SpawnAccidentCar(launch);
        else
            Debug.LogError($"{name}: accident factory {launch.factory} not found for scenario {definition.id}.", this);
    }

    public CarFactory Resolve(ScenarioFactory key)
    {
        GameObject target = null;
        switch (key)
        {
            case ScenarioFactory.Left: target = carfactory_Left; break;
            case ScenarioFactory.Right: target = carfactory_Right; break;
            case ScenarioFactory.Left2: target = carfactory_Left2; break;
            case ScenarioFactory.Right2: target = carfactory_Right2; break;
            default:
                var area = transform.parent;
                var child = area != null ? area.Find(AreaFactoryName(key)) : null;
                target = child != null ? child.gameObject : null;
                break;
        }
        return target != null ? target.GetComponent<CarFactory>() : null;
    }

    static string AreaFactoryName(ScenarioFactory key) => key switch
    {
        ScenarioFactory.AreaAccidentLeft => "CarFactory_Acident_Left",
        ScenarioFactory.AreaAccidentRight => "CarFactory_Acident_Right",
        _ => "CarFactory_Acident"
    };

    void Start()
    {
        var context = GameplaySceneContext.Instance;
        if (context != null)
        {
            carfactory_Left = context.LeftFactory != null ? context.LeftFactory.gameObject : null;
            carfactory_Right = context.RightFactory != null ? context.RightFactory.gameObject : null;
            carfactory_Left2 = context.LeftFactory2 != null ? context.LeftFactory2.gameObject : null;
            carfactory_Right2 = context.RightFactory2 != null ? context.RightFactory2.gameObject : null;
            gamedirector = context.Director != null ? context.Director.gameObject : null;
        }
        if (gamedirector == null)
            gamedirector = FindFirstObjectByType<GameDirector>()?.gameObject;
        if (gamedirector == null)
        {
            Debug.LogError("AccidentCarFactory requires a GameDirector.", this);
            enabled = false;
            return;
        }

        var eventNumber = gamedirector.GetComponent<GameDirector>().EventNumber;
        var runtime = gamedirector.GetComponent<ScenarioRuntime>();
        if (runtime == null)
            runtime = FindFirstObjectByType<ScenarioRuntime>();
        definition = runtime != null ? runtime.GetById(eventNumber)?.asset : null;
        AccidentPrediction = false;
    }
}
