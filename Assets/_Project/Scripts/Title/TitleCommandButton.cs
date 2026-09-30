using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TitleCommand
{
    StartGame,
    EventUp,
    EventDown,
    HeightUp,
    HeightDown,
    WeightUp,
    WeightDown,
    AgeUp,
    AgeDown,
    ResetDefaults,
    PreviewSound
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class TitleCommandButton : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] GameDirector_Title director;
    [SerializeField] TitleCommand command;

    const float ClickCooldown = 0.12f;
    float nextAllowedClickTime;
    Button button;

    public TitleCommand Command => command;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(TryExecute);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(TryExecute);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            TryExecute();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        TryExecute();
    }

    void TryExecute()
    {
        if (director == null || Time.unscaledTime < nextAllowedClickTime)
            return;

        if (!director.TryAcceptCommand())
            return;

        nextAllowedClickTime = Time.unscaledTime + ClickCooldown;

        switch (command)
        {
            case TitleCommand.StartGame: director.StartGame(); break;
            case TitleCommand.EventUp: director.ChangeEventNumber(1); break;
            case TitleCommand.EventDown: director.ChangeEventNumber(-1); break;
            case TitleCommand.HeightUp: director.ChangeHeight(5); break;
            case TitleCommand.HeightDown: director.ChangeHeight(-5); break;
            case TitleCommand.WeightUp: director.ChangeWeight(5); break;
            case TitleCommand.WeightDown: director.ChangeWeight(-5); break;
            case TitleCommand.AgeUp: director.ChangeAge(5); break;
            case TitleCommand.AgeDown: director.ChangeAge(-5); break;
            case TitleCommand.PreviewSound: director.PreviewSound(); break;
            case TitleCommand.ResetDefaults: director.ResetDefaults(); break;
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(GameDirector_Title configuredDirector, TitleCommand configuredCommand)
    {
        director = configuredDirector;
        command = configuredCommand;
    }
#endif
}
