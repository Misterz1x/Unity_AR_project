using System;
using System.Collections.Generic;
using ARGolf.Putting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Finger (or mouse) swipe launch: a stand-in for real hit detection.
/// Produces the same LaunchResult that club/ball tracking will produce later,
/// so the rest of the app never waits on tracking.
/// Swipe direction on the floor = launch direction; finger speed at release × gain = launch speed.
/// </summary>
public class SwipeLauncher : MonoBehaviour
{
    [Tooltip("Camera used to project the swipe onto the floor. Defaults to Camera.main.")]
    public Camera cam;

    [Tooltip("The ball. Its height defines the floor plane the swipe is projected onto.")]
    public Transform ball;

    [Tooltip("Launch speed = finger speed on the floor (m/s) × gain.")]
    public float speedGain = 1f;

    [Tooltip("Upper limit for launch speed (m/s). 3 m/s rolls about 8 m on a stimp-10 green.")]
    public float maxLaunchSpeed = 3f;

    [Tooltip("Swipes shorter than this on the floor (m) are ignored.")]
    public float minSwipeDistance = 0.05f;

    [Tooltip("Finger speed is measured over the last part of the swipe (s).")]
    public float speedWindow = 0.1f;

    [Tooltip("When false, swipes are ignored (e.g. while the ball rolls).")]
    public bool inputEnabled = true;

    public event Action<LaunchResult> Launched;

    readonly List<(double time, Vector3 point)> samples = new List<(double, Vector3)>();
    bool dragging;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || cam == null || ball == null)
            return;

        if (!inputEnabled)
        {
            dragging = false;
            return;
        }

        Vector2 screen = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            dragging = !overUi && TryFloorPoint(screen, out _);
            samples.Clear();
        }

        if (!dragging)
            return;

        if (TryFloorPoint(screen, out Vector3 p))
            samples.Add((Time.unscaledTimeAsDouble, p));

        if (pointer.press.wasReleasedThisFrame)
        {
            dragging = false;
            TryLaunch();
        }
    }

    void TryLaunch()
    {
        if (samples.Count < 2)
            return;

        Vector3 start = samples[0].point;
        var last = samples[samples.Count - 1];
        Vector3 swipe = last.point - start;
        if (swipe.magnitude < minSwipeDistance)
            return;

        // Finger speed over the final window of the swipe.
        int i = samples.Count - 2;
        while (i > 0 && last.time - samples[i].time < speedWindow)
            i--;
        double dt = last.time - samples[i].time;
        if (dt <= 0.0)
            return;
        float fingerSpeed = (last.point - samples[i].point).magnitude / (float)dt;

        float speed = Mathf.Min(fingerSpeed * speedGain, maxLaunchSpeed);
        var dir = new Vector2(swipe.x, swipe.z);
        var launch = new LaunchResult(speed, dir, Time.timeAsDouble);
        Launched?.Invoke(launch);
    }

    bool TryFloorPoint(Vector2 screen, out Vector3 point)
    {
        var plane = new Plane(Vector3.up, ball.position);
        Ray ray = cam.ScreenPointToRay(screen);
        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        point = default;
        return false;
    }
}
