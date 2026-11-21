using ARGolf.Putting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Desktop practice on the built-in greens (FamousGreens). Keys:
///   Enter = putt (then A / D aim, Space, Space)   N = next putt   R = random ball spot
///   G / B = next / previous green   H = contours / slope colours   L = ideal line   I = watch the ideal putt
/// Shows an on-screen panel (IMGUI), so it needs no UI setup in the scene.
/// </summary>
public class GreenPracticeController : MonoBehaviour
{
    public GreenView green;
    public VirtualBall ball;
    public AimPowerLauncher launcher;
    public PuttCamera puttCamera;
    public Transform hole;

    [Tooltip("Ground under the green (rough). Moved to just below the lowest point of each green.")]
    public Transform ground;
    [Tooltip("Water plane, shown only for greens with water around them.")]
    public Transform water;

    [Header("Random ball spots (m)")]
    public float minRandomDistance = 2f;
    public float maxRandomDistance = 12f;

    int greenIndex, puttIndex = -1;
    string status = "";
    string result = "";
    int strokes;
    PuttSolution ideal;
    bool idealValid;

    bool Busy => launcher.CurrentPhase != AimPowerLauncher.Phase.Idle || ball.IsRolling;

    GreenDefinition Def => green.Definition;

    void OnEnable()
    {
        launcher.Launched += OnLaunched;
        ball.Finished += OnFinished;
    }

    void OnDisable()
    {
        launcher.Launched -= OnLaunched;
        ball.Finished -= OnFinished;
    }

    void Start()
    {
        int found = IndexOf(green.greenId);
        LoadGreen(found >= 0 ? found : 0);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || Busy)
            return;

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) StartPutt();
        else if (kb.nKey.wasPressedThisFrame) NextPutt();
        else if (kb.rKey.wasPressedThisFrame) RandomSpot();
        else if (kb.gKey.wasPressedThisFrame) LoadGreen(greenIndex + 1);
        else if (kb.bKey.wasPressedThisFrame) LoadGreen(greenIndex - 1);
        else if (kb.hKey.wasPressedThisFrame) ToggleSlopeColours();
        else if (kb.lKey.wasPressedThisFrame) ToggleIdealLine();
        else if (kb.iKey.wasPressedThisFrame) PlayIdealPutt();
    }

    // ------------------------------------------------------------------ actions

    public void LoadGreen(int index)
    {
        int count = FamousGreens.All.Count;
        greenIndex = ((index % count) + count) % count;
        green.SetGreen(FamousGreens.All[greenIndex].id);

        float low = green.transform.position.y + green.LowestVisualHeight - 0.01f;
        if (ground != null) ground.position = new Vector3(ground.position.x, low, ground.position.z);
        if (water != null)
        {
            water.gameObject.SetActive(Def.waterAround);
            water.position = new Vector3(water.position.x, low + 0.005f, water.position.z);
        }

        puttIndex = -1;
        NextPutt();
    }

    public void NextPutt()
    {
        puttIndex = (puttIndex + 1) % Def.putts.Count;
        GreenPutt putt = Def.putts[puttIndex];
        SetUpPutt(putt.ball, putt.pin, putt.name);
    }

    public void RandomSpot()
    {
        Vector2 pinLocal = green.Model.PinPosition(green.pin);
        float holding = green.Model.MakeRollSettings().MaxHoldingSlope;
        for (int attempt = 0; attempt < 500; attempt++)
        {
            float a = Random.Range(0f, 2f * Mathf.PI);
            float d = Random.Range(minRandomDistance, maxRandomDistance);
            Vector2 p = pinLocal + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            if (Def.outline.DistanceOutside(p) > -0.5f) continue;
            if (green.Model.SlopeAt(p).magnitude > 0.8f * holding) continue;
            SetUpPutt(p, green.pin, $"Random spot, {d:F1} m");
            return;
        }
        status = "No free spot found; try another pin (N).";
    }

    void SetUpPutt(Vector2 ballLocal, int pin, string name)
    {
        green.SetPin(pin);
        hole.position = green.PinWorldPosition(pin);
        ball.PlaceAt(green.SurfacePoint(ballLocal) + Vector3.up * ball.settings.BallRadius);
        launcher.AimAtHole();
        launcher.HideGauge();
        if (puttCamera != null) puttCamera.Snap();
        green.ClearPaths();
        idealValid = false;
        strokes = 0;
        result = "";
        status = name;
    }

    public void StartPutt()
    {
        if (ball.LastResult != null && ball.LastResult.Holed && !ball.IsRolling && strokes > 0)
            return; // holed: press N for the next putt
        launcher.BeginAim();
    }

    public void ToggleSlopeColours()
    {
        // Clean -> contour lines -> contour lines + slope colours -> clean
        if (!green.showContours) green.showContours = true;
        else if (!green.showSlopeColours) green.showSlopeColours = true;
        else green.showContours = green.showSlopeColours = false;
        int pin = green.pin;
        green.Rebuild();
        green.SetPin(pin);
        hole.position = green.PinWorldPosition(pin);
        if (idealValid) green.ShowPath(ideal.Result.Path, true);
    }

    public void ToggleIdealLine()
    {
        if (green.IdealLineVisible)
        {
            green.ShowPath(null, true);
            return;
        }
        if (!SolveIdeal()) return;
        green.ShowPath(ideal.Result.Path, true);
    }

    public void PlayIdealPutt()
    {
        if (!SolveIdeal()) return;
        green.ShowPath(ideal.Result.Path, true);
        strokes++;
        ball.Launch(new LaunchResult(ideal.Speed, ideal.Direction, Time.timeAsDouble));
        result = "";
    }

    bool SolveIdeal()
    {
        if (idealValid) return true;
        Vector2 b = Floor(ball.transform.position), h = Floor(hole.position);
        ideal = PuttSolver.Solve(b, h, ball.ActiveSettings, green.Surface);
        idealValid = ideal.Found;
        if (!idealValid) status = "No holed putt found from here.";
        return idealValid;
    }

    // ------------------------------------------------------------------ events

    void OnLaunched(LaunchResult launch)
    {
        strokes++;
        ball.Launch(launch);
        result = "";
    }

    void OnFinished(RollResult r)
    {
        green.ShowPath(r.Path, false);
        idealValid = false; // ball moved

        if (!r.Holed && Def.waterAround && green.GrassAt(r.End) == GrassKind.Rough)
        {
            strokes++; // penalty
            result = $"Splash! In the water (+1). Back to where you putted from. Strokes: {strokes}";
            Vector2 back = r.Start;
            ball.PlaceAt(new Vector3(back.x, green.VisualHeightAt(back) + ball.settings.BallRadius, back.y));
            launcher.AimAtHole();
            return;
        }

        if (r.Holed)
        {
            result = $"Holed in {strokes}! ({r.SpeedAtHole:F2} m/s at the hole)   N = next putt";
            return;
        }

        string where = green.GrassAt(r.End) == GrassKind.Green ? "" : " (off the green)";
        result = $"{r}{where}.   Strokes: {strokes}";
        ball.PlaceAt(ball.transform.position); // next putt from where it stopped
        launcher.AimAtHole();
        if (puttCamera != null) puttCamera.Snap();
    }

    // ------------------------------------------------------------------ on-screen panel

    void OnGUI()
    {
        if (green == null || green.Model == null) return;

        // Size from the screen height, so the panel fits small Game views and big screens.
        float scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 2.5f);
        int font = Mathf.RoundToInt(13f * scale);
        float margin = 8f * scale;
        float w = Mathf.Min(440f * scale, Screen.width - 2f * margin);

        var box = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true, fontSize = font,
            padding = new RectOffset((int)(8 * scale), (int)(8 * scale), (int)(6 * scale), (int)(6 * scale)),
        };

        Vector2 b = Floor(ball.transform.position), h = Floor(hole.position);
        float distance = (h - b).magnitude;
        float rise = (green.Surface.HeightAt(h) - green.Surface.HeightAt(b)) * 100f;
        float slopeHere = green.Surface.SlopeAt(b).magnitude * 100f;

        string head = $"<b>{Def.name}</b>   stimp {Def.stimpFeet:F1}   ({greenIndex + 1}/{FamousGreens.All.Count})\n";
        string body =
            $"<b>{status}</b>\n" +
            $"{Def.pins[green.pin].name} hole: {distance:F1} m, {(rise >= 0f ? "uphill" : "downhill")} {Mathf.Abs(rise):F0} cm. " +
            $"Slope at ball {slopeHere:F1} %";
        if (idealValid)
        {
            float aim = ideal.AimOffsetDegrees(b, h);
            body += $"\nIdeal: aim {Mathf.Abs(aim):F1}° {(aim >= 0f ? "right" : "left")}, launch {ideal.Speed:F2} m/s";
        }
        if (!string.IsNullOrEmpty(result)) body += $"\n<b>{result}</b>";

        // Leave out the description if the panel would cover too much of the view.
        string text = head + $"<i>{Def.description}</i>\n" + body;
        float height = box.CalcHeight(new GUIContent(text), w);
        if (height > Screen.height * 0.45f)
        {
            text = head + body;
            height = box.CalcHeight(new GUIContent(text), w);
        }
        GUI.Box(new Rect(margin, margin, w, height), text, box);

        float y = margin * 2f + height;
        if (launcher.CurrentPhase == AimPowerLauncher.Phase.Power)
        {
            float speed = launcher.GaugeValue * launcher.maxLaunchSpeed;
            float barH = 26f * scale;
            GUI.Box(new Rect(margin, y, w, barH), "");
            GUI.Box(new Rect(margin + 2f, y + 2f, (w - 4f) * launcher.GaugeValue, barH - 4f), "");
            var label = new GUIStyle(GUI.skin.label) { richText = true, fontSize = font, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(margin + 8f, y, w, barH),
                $"<b>Power {launcher.GaugeValue * 100f:F0}   {speed:F2} m/s, rolls ~{RollSimulator.StopDistance(speed, ball.ActiveSettings):F1} m on flat</b>",
                label);
        }

        string help = launcher.CurrentPhase switch
        {
            AimPowerLauncher.Phase.Aiming => "A / D: aim (Shift = fine)   Space: confirm   Esc: cancel",
            AimPowerLauncher.Phase.Power => "Space: hit   Esc: cancel",
            _ => "Enter: putt   N: next putt   R: random spot   G / B: next / previous green\n" +
                 "H: contours / slope colours   L: ideal line   I: watch the ideal putt",
        };
        float helpW = Mathf.Min(560f * scale, Screen.width - 2f * margin);
        float helpH = box.CalcHeight(new GUIContent(help), helpW);
        GUI.Box(new Rect(margin, Screen.height - margin - helpH, helpW, helpH), help, box);
    }

    static Vector2 Floor(Vector3 v) => new Vector2(v.x, v.z);

    static int IndexOf(string id)
    {
        for (int i = 0; i < FamousGreens.All.Count; i++)
            if (FamousGreens.All[i].id == id) return i;
        return -1;
    }
}
