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
    float shownGauge = -1f; // power of the last hit, shown until the next putt (-1 = none)

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
        launcher.green = green; // arrow follows the green's slope
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

        // The rough around the green ends at LowestVisualHeight: ground just below, water a bit above
        // (so the island's bank runs into the water).
        float low = green.transform.position.y + green.LowestVisualHeight;
        if (ground != null) ground.position = new Vector3(ground.position.x, low - 0.005f, ground.position.z);
        if (water != null)
        {
            water.gameObject.SetActive(Def.waterAround);
            water.position = new Vector3(water.position.x, low + 0.08f, water.position.z);
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
        shownGauge = -1f;
    }

    public void StartPutt()
    {
        if (ball.LastResult != null && ball.LastResult.Holed && !ball.IsRolling && strokes > 0)
            return; // holed: press N for the next putt
        shownGauge = -1f;
        launcher.BeginAim();
    }

    public void ToggleSlopeColours()
    {
        // Clean -> contour lines -> contour lines + slope colours -> clean
        if (!green.showContours) green.showContours = true;
        else if (!green.showSlopeColours) green.showSlopeColours = true;
        else green.showContours = green.showSlopeColours = false;
        green.RefreshTexture();
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
        shownGauge = launcher.GaugeValue;
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

        GrassKind lie = green.GrassAt(r.End);
        string where = lie == GrassKind.Fringe ? " (in the fringe)" : lie == GrassKind.Rough ? " (in the rough)" : "";
        result = $"{r}{where}.   Strokes: {strokes}";
        ball.PlaceAt(ball.transform.position); // next putt from where it stopped (sitting down in longer grass)
        launcher.AimAtHole();
        if (puttCamera != null) puttCamera.Snap();
    }

    // ------------------------------------------------------------------ on-screen panel

    void OnGUI()
    {
        if (green == null || green.Model == null) return;

        // Size from the screen height, so it fits small Game views and big screens.
        float scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 2.5f);
        int font = Mathf.RoundToInt(13f * scale);
        float margin = 10f * scale;
        bool landscape = Screen.width >= Screen.height;

        var box = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true, fontSize = font,
            padding = new RectOffset((int)(8 * scale), (int)(8 * scale), (int)(6 * scale), (int)(6 * scale)),
        };

        // ---- key help, bottom left
        string help = launcher.CurrentPhase switch
        {
            AimPowerLauncher.Phase.Aiming => "A / D: aim (Shift = fine)   Space: confirm   Esc: cancel",
            AimPowerLauncher.Phase.Power => "Space: hit   Esc: cancel",
            _ => "Enter: putt   N: next putt   R: random spot   G / B: next / previous green\n" +
                 "H: contours / slope colours   L: ideal line   I: watch the ideal putt",
        };
        float helpW = Mathf.Min(560f * scale, Screen.width - 2f * margin);
        float helpH = box.CalcHeight(new GUIContent(help), helpW);
        Rect helpRect = new Rect(margin, Screen.height - margin - helpH, helpW, helpH);
        GUI.Box(helpRect, help, box);

        // ---- info panel: top right in landscape (the power bar is on the left), top in portrait
        Vector2 b = Floor(ball.transform.position), h = Floor(hole.position);
        float distance = (h - b).magnitude;
        float rise = green.Surface.HeightAt(h) - green.Surface.HeightAt(b);
        float slopeHere = green.Surface.SlopeAt(b).magnitude * 100f;

        string head = $"<b>{Def.name}</b>   stimp {Def.stimpFeet:F1}   ({greenIndex + 1}/{FamousGreens.All.Count})\n";
        string body =
            $"<b>{status}</b>\n" +
            $"{Def.pins[green.pin].name} hole: {distance:F1} m, {(rise >= 0f ? "uphill" : "downhill")} {Mathf.Abs(rise) * 100f:F0} cm. " +
            $"Slope at ball {slopeHere:F1} %";
        if (idealValid)
        {
            float aim = ideal.AimOffsetDegrees(b, h);
            body += $"\nIdeal: aim {Mathf.Abs(aim):F1}° {(aim >= 0f ? "right" : "left")}, launch {ideal.Speed:F2} m/s";
        }
        if (!string.IsNullOrEmpty(result)) body += $"\n<b>{result}</b>";

        float panelW = landscape ? Mathf.Min(420f * scale, Screen.width * 0.45f) : Screen.width - 2f * margin;
        string text = head + $"<i>{Def.description}</i>\n" + body;
        float panelH = box.CalcHeight(new GUIContent(text), panelW);
        if (panelH > Screen.height * 0.4f)
        {
            text = head + body;
            panelH = box.CalcHeight(new GUIContent(text), panelW);
        }
        float panelX = landscape ? Screen.width - margin - panelW : margin;
        GUI.Box(new Rect(panelX, margin, panelW, panelH), text, box);

        // ---- power bar: vertical on the left in landscape, horizontal above the help in portrait
        DrawPowerBar(scale, font, margin, landscape, helpRect, distance, rise);
    }

    void DrawPowerBar(float scale, int font, float margin, bool vertical, Rect helpRect, float distance, float rise)
    {
        bool live = launcher.CurrentPhase == AimPowerLauncher.Phase.Power;
        if (!live && shownGauge < 0f) return;
        float value = live ? launcher.GaugeValue : shownGauge;

        float thick = 44f * scale;
        float label = 2.6f * font;
        Rect frame;
        if (vertical)
        {
            float top = margin + label, bottom = helpRect.y - margin - label;
            float length = Mathf.Min(560f * scale, bottom - top);
            frame = new Rect(margin + 6f * scale, top + (bottom - top - length) * 0.5f, thick, length);
        }
        else
        {
            float length = Mathf.Min(560f * scale, Screen.width - 2f * margin);
            frame = new Rect((Screen.width - length) * 0.5f, helpRect.y - margin - label - thick, length, thick);
        }

        Fill(frame, new Color(0f, 0f, 0f, 0.55f));
        float pad = 4f * scale;
        Rect inner = new Rect(frame.x + pad, frame.y + pad, frame.width - 2f * pad, frame.height - 2f * pad);

        // Fill from 0 (bottom / left) to value, coloured green -> yellow -> red.
        const int segments = 60;
        int filled = Mathf.RoundToInt(value * segments);
        for (int i = 0; i < filled; i++)
            Fill(Segment(inner, vertical, (float)i / segments, (i + 1f) / segments), PowerColour((i + 0.5f) / segments));

        // Ticks every 10 %.
        for (int t = 1; t < 10; t++)
        {
            Rect tick = Segment(inner, vertical, t / 10f - 0.002f, t / 10f + 0.002f);
            if (vertical) tick.width *= t == 5 ? 0.6f : 0.35f; else tick.height *= t == 5 ? 0.6f : 0.35f;
            Fill(tick, new Color(1f, 1f, 1f, 0.45f));
        }

        // Estimated power for the hole: flat roll-out to the hole + 40 cm, corrected for up / downhill.
        RollSettings rules = ball.ActiveSettings;
        float equivalent = distance + 0.4f + RollSettings.RollingGravityFactor * rise / Mathf.Max(1e-3f, rules.Mu);
        float holeValue = RollSimulator.LaunchSpeedForDistance(Mathf.Max(0f, equivalent), rules) / launcher.maxLaunchSpeed;
        var small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(font * 0.85f), richText = true };
        if (holeValue <= 1f)
        {
            Rect mark = Segment(frame, vertical, holeValue - 0.004f, holeValue + 0.004f);
            Fill(mark, Color.white);
            Rect tag = vertical
                ? new Rect(frame.xMax + 4f * scale, mark.center.y - font, 140f * scale, 2f * font)
                : new Rect(mark.center.x - 40f * scale, frame.y - 1.6f * font, 140f * scale, 1.6f * font);
            GUI.Label(tag, vertical ? "<b>◄ hole (est.)</b>" : "<b>▼ hole (est.)</b>", small);
        }

        // Current value: marker line and labels.
        Rect now = Segment(frame, vertical, value - 0.006f, value + 0.006f);
        Fill(now, new Color(1f, 1f, 1f, 0.95f));

        float speed = value * launcher.maxLaunchSpeed;
        var big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(font * 1.25f), richText = true, alignment = TextAnchor.MiddleCenter };
        var info = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        string speedText = $"{speed:F2} m/s\n~{RollSimulator.StopDistance(speed, rules):F1} m flat";
        if (vertical)
        {
            GUI.Label(new Rect(frame.x - 20f * scale, frame.y - label, frame.width + 40f * scale, label), $"<b>{value * 100f:F0}</b>", big);
            GUI.Label(new Rect(frame.x - 30f * scale, frame.yMax, frame.width + 60f * scale, label), speedText, info);
        }
        else
        {
            GUI.Label(new Rect(frame.x, frame.yMax, frame.width, label),
                $"<b>Power {value * 100f:F0}</b>   {speed:F2} m/s, ~{RollSimulator.StopDistance(speed, rules):F1} m flat", info);
        }
    }

    // Part of the bar from a to b (0..1), measured from the bottom (vertical) or the left (horizontal).
    static Rect Segment(Rect r, bool vertical, float a, float b)
    {
        a = Mathf.Clamp01(a);
        b = Mathf.Clamp01(b);
        return vertical
            ? new Rect(r.x, r.yMax - b * r.height, r.width, (b - a) * r.height)
            : new Rect(r.x + a * r.width, r.y, (b - a) * r.width, r.height);
    }

    static Color PowerColour(float t) =>
        t < 0.5f ? Color.Lerp(new Color(0.2f, 0.85f, 0.3f), new Color(1f, 0.85f, 0.15f), t / 0.5f)
                 : Color.Lerp(new Color(1f, 0.85f, 0.15f), new Color(0.95f, 0.2f, 0.1f), (t - 0.5f) / 0.5f);

    static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static Vector2 Floor(Vector3 v) => new Vector2(v.x, v.z);

    static int IndexOf(string id)
    {
        for (int i = 0; i < FamousGreens.All.Count; i++)
            if (FamousGreens.All[i].id == id) return i;
        return -1;
    }
}
