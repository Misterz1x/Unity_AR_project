using ARGolf.Putting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Desktop test game: "New ball location" puts the ball on a random spot on the green,
/// "Play" starts a shot (aim with A / D, Space; power gauge, Space). Shows the result.
/// </summary>
public class PuttingGameController : MonoBehaviour
{
    public VirtualBall ball;
    public AimPowerLauncher launcher;
    public PuttCamera puttCamera;
    public Transform hole;

    [Tooltip("Collider of the green surface, used to find random spots on it.")]
    public Collider green;

    public Button newBallButton;
    public Button playButton;
    public Text statusText;

    [Header("Random ball placement (m)")]
    public float minDistanceFromHole = 1f;
    public float maxDistanceFromHole = 10f;
    [Tooltip("Keep at least this far from the edge of the green.")]
    public float edgeMargin = 0.4f;
    public int maxAttempts = 500;

    bool holed;

    bool Busy => launcher.CurrentPhase != AimPowerLauncher.Phase.Idle || ball.IsRolling;

    void OnEnable()
    {
        launcher.Launched += OnLaunched;
        launcher.PhaseChanged += OnPhaseChanged;
        ball.Finished += OnFinished;
    }

    void OnDisable()
    {
        launcher.Launched -= OnLaunched;
        launcher.PhaseChanged -= OnPhaseChanged;
        ball.Finished -= OnFinished;
    }

    void Start()
    {
        launcher.AimAtHole();
        ShowReady();
        RefreshButtons();
    }

    /// <summary>Button: put the ball on a random spot on the green.</summary>
    public void NewBallLocation()
    {
        ClearUiSelection();
        if (Busy)
            return;
        if (TryRandomGreenPoint(out Vector3 surface))
        {
            ball.PlaceAt(surface + Vector3.up * ball.settings.BallRadius);
            holed = false;
            launcher.AimAtHole();
            launcher.HideGauge();
            ShowReady();
        }
        else
        {
            statusText.text = "Could not find a free spot on the green.";
        }
        RefreshButtons();
    }

    /// <summary>Button: start a shot.</summary>
    public void Play()
    {
        ClearUiSelection();
        if (Busy || holed)
            return;
        launcher.BeginAim();
    }

    void OnPhaseChanged(AimPowerLauncher.Phase phase)
    {
        switch (phase)
        {
            case AimPowerLauncher.Phase.Aiming:
                statusText.text = "A / D: aim (hold Shift for fine aim)    Space: confirm direction    Esc: cancel";
                break;
            case AimPowerLauncher.Phase.Power:
                statusText.text = "Space: hit    Esc: cancel";
                break;
            default:
                if (!ball.IsRolling) ShowReady();
                break;
        }
        RefreshButtons();
    }

    void OnLaunched(LaunchResult launch)
    {
        ball.Launch(launch);
        statusText.text = $"Rolling... launch speed {launch.Speed:F2} m/s";
        RefreshButtons();
    }

    void OnFinished(RollResult result)
    {
        holed = result.Holed;
        statusText.text = result.Holed
            ? $"Holed! ({result.SpeedAtHole:F2} m/s at the hole)    Press New ball location."
            : $"{result}    Now {DistanceToHole():F1} m from the hole. Press Play.";
        RefreshButtons();
    }

    void ShowReady() =>
        statusText.text = $"Ball {DistanceToHole():F1} m from the hole. Press Play to putt.";

    void RefreshButtons()
    {
        newBallButton.interactable = !Busy;
        playButton.interactable = !Busy && !holed;
    }

    float DistanceToHole()
    {
        Vector3 d = hole.position - ball.transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    bool TryRandomGreenPoint(out Vector3 point)
    {
        Bounds b = green.bounds;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(b.min.x, b.max.x), z = Random.Range(b.min.z, b.max.z);
            if (!SurfaceAt(x, z, out point))
                continue;

            Vector3 toHole = hole.position - point;
            toHole.y = 0f;
            float dist = toHole.magnitude;
            if (dist < minDistanceFromHole || dist > maxDistanceFromHole)
                continue;

            bool awayFromEdge = true;
            for (int k = 0; k < 8 && awayFromEdge; k++)
            {
                float a = k * Mathf.PI / 4f;
                awayFromEdge = SurfaceAt(x + Mathf.Cos(a) * edgeMargin, z + Mathf.Sin(a) * edgeMargin, out _);
            }
            if (awayFromEdge)
                return true;
        }
        point = default;
        return false;
    }

    // Point on the green's top surface at (x, z); false if off the green or inside the cup.
    bool SurfaceAt(float x, float z, out Vector3 point)
    {
        Bounds b = green.bounds;
        var ray = new Ray(new Vector3(x, b.max.y + 1f, z), Vector3.down);
        if (green.Raycast(ray, out RaycastHit hit, b.size.y + 2f) && hit.point.y > b.max.y - 0.01f)
        {
            point = hit.point;
            return true;
        }
        point = default;
        return false;
    }

    // Stops Space from "clicking" the last pressed button again.
    static void ClearUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
