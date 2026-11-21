using System;
using ARGolf.Putting;
using UnityEngine;

/// <summary>
/// Moves the ball model along the (possibly curved) path computed by RollSimulator (no Rigidbody).
/// Listens to a SwipeLauncher (later: any tracking source that produces a LaunchResult).
/// Put this on the ball; the ball object should be scaled to 0.0427 (real size in metres).
/// </summary>
public class VirtualBall : MonoBehaviour
{
    [Tooltip("Source of launches. Swipe for now; club/ball tracking later.")]
    public SwipeLauncher launcher;

    [Tooltip("The hole. Only its X/Z position is used.")]
    public Transform hole;

    [Tooltip("Optional: the green the ball rolls on (shape, speed, and the ball follows its height).")]
    public GreenView green;

    public RollSettings settings = new RollSettings();

    [Header("Test slope (until greens have height maps)")]
    [Tooltip("Tilts the green for the physics only, in percent. X: + = rises towards +X. Y: + = rises towards +Z. " +
             "The green mesh stays flat. Try (2, 0) to see a putt break.")]
    public Vector2 testSlopePercent;

    /// <summary>
    /// Shape of the green for the physics. If null: the GreenView's surface, else a plane tilted by testSlopePercent.
    /// </summary>
    public IGreenSurface Surface { get; set; }

    IGreenSurface ActiveSurface =>
        Surface ?? (green != null && green.Surface != null
            ? green.Surface
            : (IGreenSurface)PlaneGreen.FromPercent(testSlopePercent.x, testSlopePercent.y));

    /// <summary>Roll settings in use: the green's speed if a GreenView is set.</summary>
    public RollSettings ActiveSettings => green != null ? green.RollSettingsFor(settings) : settings;

    /// <summary>Raised when the ball has stopped or dropped.</summary>
    public event Action<RollResult> Finished;

    public RollResult LastResult { get; private set; }
    public bool IsRolling { get; private set; }

    Vector3 startPosition;
    Vector3 rollStart;
    float rollStartTime;
    Renderer[] renderers;

    void Awake()
    {
        startPosition = transform.position;
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable()
    {
        if (launcher != null) launcher.Launched += OnLaunched;
    }

    void OnDisable()
    {
        if (launcher != null) launcher.Launched -= OnLaunched;
    }

    /// <summary>Launch from any input method (keyboard aim + power gauge, tracking, ...).</summary>
    public void Launch(LaunchResult launch) => OnLaunched(launch);

    void OnLaunched(LaunchResult launch)
    {
        if (IsRolling || hole == null)
            return;

        rollStart = transform.position;
        LastResult = RollSimulator.Simulate(launch, ToFloor(rollStart), ToFloor(hole.position), ActiveSettings, ActiveSurface);
        rollStartTime = Time.time;
        IsRolling = true;
        if (launcher != null) launcher.inputEnabled = false;
        Debug.Log($"{launch}  ->  hole at {(ToFloor(hole.position) - ToFloor(rollStart)).magnitude:F2} m");
    }

    void Update()
    {
        if (!IsRolling)
            return;

        float t = Time.time - rollStartTime;
        Vector2 p = LastResult.PositionAt(t);
        float y = green != null ? green.VisualHeightAt(p) + settings.BallRadius : rollStart.y;
        Vector3 next = new Vector3(p.x, y, p.y);

        // Spin the ball to match the distance rolled.
        Vector3 step = next - transform.position;
        if (step.sqrMagnitude > 0f && settings.BallRadius > 0f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, step.normalized);
            transform.Rotate(axis, step.magnitude / settings.BallRadius * Mathf.Rad2Deg, Space.World);
        }
        transform.position = next;

        if (t >= LastResult.Duration)
            Finish();
    }

    void Finish()
    {
        IsRolling = false;
        if (LastResult.Holed)
            SetVisible(false);
        Debug.Log(LastResult.ToString());
        Finished?.Invoke(LastResult);
    }

    /// <summary>Puts the ball back at its starting spot. Hook this to a Reset button.</summary>
    public void ResetBall()
    {
        IsRolling = false;
        transform.SetPositionAndRotation(startPosition, Quaternion.identity);
        SetVisible(true);
        if (launcher != null) launcher.inputEnabled = true;
    }

    /// <summary>Moves the ball's starting spot (ball centre) and puts the ball there.</summary>
    public void PlaceAt(Vector3 position)
    {
        startPosition = position;
        ResetBall();
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
    }

    static Vector2 ToFloor(Vector3 v) => new Vector2(v.x, v.z);
}
