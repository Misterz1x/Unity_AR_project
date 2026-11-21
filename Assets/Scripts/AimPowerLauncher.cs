using System;
using ARGolf.Putting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keyboard launch for testing in the Editor: a stand-in for real hit detection.
/// 1. Aim: an arrow on the ground, A / D turn it (Shift = fine), Space confirms.
/// 2. Power: a gauge line moves 0 -> 100 -> 0, Space fixes it. Gauge value = launch speed.
/// Esc cancels. Produces the same LaunchResult as every other input method.
/// </summary>
public class AimPowerLauncher : MonoBehaviour
{
    public enum Phase { Idle, Aiming, Power }

    [Tooltip("The ball (its centre).")]
    public Transform ball;

    [Tooltip("The hole. The arrow starts pointing at it.")]
    public Transform hole;

    [Tooltip("Arrow on the ground, pointing along its local +Z. Pivot at the ball centre.")]
    public Transform arrow;

    public PowerGauge gauge;

    [Tooltip("Turn speed while holding A or D (degrees per second).")]
    public float aimSpeed = 40f;

    [Tooltip("Holding Shift multiplies the turn speed by this.")]
    public float fineAimFactor = 0.2f;

    [Tooltip("Seconds for the gauge line to go 0 -> 100 -> 0.")]
    public float gaugeCycleSeconds = 1.6f;

    [Tooltip("Launch speed at 100 on the gauge (m/s). 4 m/s rolls about 14.5 m on a stimp-10 green.")]
    public float maxLaunchSpeed = 4f;

    [Tooltip("Height of the arrow above the bottom of the ball (m).")]
    public float arrowLift = 0.004f;

    [Tooltip("Optional: the green. The arrow then follows its slope and stays above the grass.")]
    public GreenView green;

    [Tooltip("Length of the arrow from the ball (m), used to tilt it along the green.")]
    public float arrowReach = 1.15f;

    public Phase CurrentPhase { get; private set; } = Phase.Idle;

    /// <summary>Aim direction on the floor (world, y = 0, unit length).</summary>
    public Vector3 AimDirection { get; private set; } = Vector3.forward;

    /// <summary>Gauge value 0..1.</summary>
    public float GaugeValue { get; private set; }

    public event Action<LaunchResult> Launched;
    public event Action<Phase> PhaseChanged;

    float gaugeTime;

    void Awake()
    {
        AimAtHole();
        if (gauge != null) gauge.SetVisible(false);
        UpdateArrow();
    }

    /// <summary>Points the aim from the ball to the hole.</summary>
    public void AimAtHole()
    {
        if (ball == null || hole == null)
            return;
        Vector3 d = hole.position - ball.position;
        d.y = 0f;
        if (d.sqrMagnitude > 1e-6f)
            AimDirection = d.normalized;
    }

    /// <summary>Starts a shot: aiming first, then power.</summary>
    public void BeginAim()
    {
        if (CurrentPhase != Phase.Idle)
            return;
        if (gauge != null) gauge.SetVisible(false);
        SetPhase(Phase.Aiming);
    }

    public void Cancel()
    {
        if (gauge != null) gauge.SetVisible(false);
        SetPhase(Phase.Idle);
    }

    public void HideGauge()
    {
        if (gauge != null) gauge.SetVisible(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (CurrentPhase == Phase.Aiming)
            {
                float turn = 0f;
                if (kb.aKey.isPressed) turn -= 1f;
                if (kb.dKey.isPressed) turn += 1f;
                if (turn != 0f)
                {
                    float speed = aimSpeed * (kb.shiftKey.isPressed ? fineAimFactor : 1f);
                    AimDirection = Quaternion.Euler(0f, turn * speed * Time.deltaTime, 0f) * AimDirection;
                }

                if (kb.spaceKey.wasPressedThisFrame)
                {
                    gaugeTime = 0f;
                    GaugeValue = 0f;
                    if (gauge != null)
                    {
                        gauge.SetValue(0f);
                        gauge.SetVisible(true);
                    }
                    SetPhase(Phase.Power);
                }
                else if (kb.escapeKey.wasPressedThisFrame)
                {
                    Cancel();
                }
            }
            else if (CurrentPhase == Phase.Power)
            {
                gaugeTime += Time.deltaTime;
                GaugeValue = Mathf.PingPong(gaugeTime * 2f / gaugeCycleSeconds, 1f);
                if (gauge != null) gauge.SetValue(GaugeValue);

                if (kb.spaceKey.wasPressedThisFrame)
                    Hit();
                else if (kb.escapeKey.wasPressedThisFrame)
                    Cancel();
            }
        }
        UpdateArrow();
    }

    void Hit()
    {
        var launch = new LaunchResult(GaugeValue * maxLaunchSpeed,
            new Vector2(AimDirection.x, AimDirection.z), Time.timeAsDouble);
        Launched?.Invoke(launch); // the gauge stays visible with the fixed value
        SetPhase(Phase.Idle);
    }

    void SetPhase(Phase phase)
    {
        CurrentPhase = phase;
        UpdateArrow();
        PhaseChanged?.Invoke(phase);
    }

    void UpdateArrow()
    {
        if (arrow == null)
            return;
        bool show = CurrentPhase != Phase.Idle && ball != null;
        if (arrow.gameObject.activeSelf != show)
            arrow.gameObject.SetActive(show);
        if (!show)
            return;
        if (green != null && green.Model != null)
        {
            FitArrowToGreen();
            return;
        }
        float ballRadius = ball.lossyScale.y * 0.5f;
        arrow.SetPositionAndRotation(
            ball.position + Vector3.up * (arrowLift - ballRadius),
            Quaternion.LookRotation(AimDirection, Vector3.up));
    }

    // On a sloped green: tilt the arrow along the green and lift it over any bump under it.
    void FitArrowToGreen()
    {
        var dir = new Vector2(AimDirection.x, AimDirection.z);
        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();

        var start = new Vector2(ball.position.x, ball.position.z);
        float h0 = green.VisualHeightAt(start);
        float h1 = green.VisualHeightAt(start + dir * arrowReach);
        float clearance = Mathf.Max(arrowLift, 0.008f); // the green mesh is a few mm off the exact surface between vertices
        float lift = clearance;
        for (int i = 1; i < 10; i++)
        {
            float t = i / 10f;
            float surface = green.VisualHeightAt(start + dir * (arrowReach * t));
            lift = Mathf.Max(lift, surface - Mathf.Lerp(h0, h1, t) + clearance);
        }

        var forward = new Vector3(dir.x * arrowReach, h1 - h0, dir.y * arrowReach);
        arrow.SetPositionAndRotation(new Vector3(start.x, h0 + lift, start.y), Quaternion.LookRotation(forward, Vector3.up));
    }
}
