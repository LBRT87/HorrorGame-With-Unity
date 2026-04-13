using UnityEngine;

public class HUDTimeReceiver : MonoBehaviour
{
    [Tooltip("True = GhostHUD, False = Exorcist HUD")]
    [SerializeField]public bool isGhostHUD = false;

    private HUD _exorcistHud;
    private GhostHUD _ghostHud;

    private int _lastPhase = -1;
    private string _lastTime = "";

    private void Awake()
    {
        if (isGhostHUD)
            _ghostHud = GetComponent<GhostHUD>();
        else
            _exorcistHud = GetComponent<HUD>();
    }

    private void Update()
    {
        if (!isGhostHUD && _exorcistHud == null)
            _exorcistHud = GetComponent<HUD>();
        if (isGhostHUD && _ghostHud == null)
            _ghostHud = GetComponent<GhostHUD>();

        var gpm = GamePhaseManager.Instance;
        if (gpm == null) return;

        string t = gpm.FormatTime(gpm.GetCurrentMinute());
        int phase = gpm.currentPhase.Value;

        bool timeChanged = t != _lastTime;
        bool phaseChanged = phase != _lastPhase;

        if (!timeChanged && !phaseChanged) return;

        _lastTime = t;
        _lastPhase = phase;

        if (!isGhostHUD && _exorcistHud != null)
        {
            if (timeChanged) _exorcistHud.UpdateTime(t);
            if (phaseChanged) _exorcistHud.UpdatePhase(phase);
        }
        else if (isGhostHUD && _ghostHud != null)
        {
            if (timeChanged) _ghostHud.UpdateTimePublic(t);
            if (phaseChanged) _ghostHud.UpdatePhasePublic(phase);
        }
    }
}