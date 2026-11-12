using System;
using ARGolf.Putting;
using UnityEngine;

/// <summary>
/// Moves the ball model along the path computed by RollSimulator (no Rigidbody).
/// Listens to a SwipeLauncher (later: any tracking source that produces a LaunchResult).
/// Put this on the ball; the ball object should be scaled to 0.0427 (real size in metres).
/// </summary>
public class VirtualBall : MonoBehaviour
{
    [Tooltip("Source of launches. Swipe for now; club/ball tracking later.")]
    public SwipeLauncher launcher;

    [Tooltip("The hole. Only its X/Z position is used.")]
    public Transform hole;

    public RollSettings settings = new RollSettings();

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

    void OnLaunched(LaunchResult launch)
    {
        if (IsRolling || hole == null)
            return;

        rollStart = transform.position;
        LastResult = RollSimulator.Simulate(launch, ToFloor(rollStart), ToFloor(hole.position), settings);
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
        Vector3 next = new Vector3(p.x, rollStart.y, p.y);

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

    void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
    }

    static Vector2 ToFloor(Vector3 v) => new Vector2(v.x, v.z);
}
