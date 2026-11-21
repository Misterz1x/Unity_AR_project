using UnityEngine;

/// <summary>
/// A flag cloth that waves in the wind. Builds its own mesh (both sides) hanging from the object's origin:
/// the edge at x = 0 is fixed to the pole, the cloth extends towards -x and down (-y).
/// The wave grows towards the free end, with a little flutter and sag.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlagWave : MonoBehaviour
{
    [Tooltip("Width (along the wind) and height of the cloth (m).")]
    public Vector2 size = new Vector2(0.5f, 0.35f);
    [Range(4, 48)] public int columns = 24;
    [Range(2, 24)] public int rows = 12;

    [Header("Wind")]
    [Tooltip("Sideways wave at the free end (m).")]
    public float amplitude = 0.045f;
    [Tooltip("Length of one wave along the cloth (m).")]
    public float waveLength = 0.4f;
    [Tooltip("How fast the waves run along the cloth (1/s).")]
    public float speed = 4f;
    [Tooltip("Small fast ripple (m).")]
    public float flutter = 0.01f;
    [Tooltip("How much the free end droops (m).")]
    public float sag = 0.03f;

    Mesh mesh;
    Vector3[] rest, vertices;
    float phase;

    void OnEnable()
    {
        phase = Random.value * 10f;
        Build();
    }

    void OnDisable()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        mesh = null;
    }

    void OnValidate()
    {
        if (isActiveAndEnabled) Build();
    }

    void Build()
    {
        int cols = columns + 1, perSide = cols * (rows + 1);
        rest = new Vector3[perSide * 2];
        var uvs = new Vector2[perSide * 2];
        for (int side = 0; side < 2; side++)
            for (int j = 0; j <= rows; j++)
                for (int i = 0; i <= columns; i++)
                {
                    int k = side * perSide + i + j * cols;
                    rest[k] = new Vector3(-size.x * i / columns, -size.y * j / rows, 0f);
                    uvs[k] = new Vector2((float)i / columns, 1f - (float)j / rows);
                }

        var tris = new int[columns * rows * 12];
        int t = 0;
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < columns; i++)
            {
                int a = i + j * cols, b = a + 1, c = a + cols, d = c + 1;
                // front
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
                // back (own vertices, reversed)
                a += perSide; b += perSide; c += perSide; d += perSide;
                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = b; tris[t++] = d; tris[t++] = c;
            }

        if (mesh == null) mesh = new Mesh { name = "Flag cloth", hideFlags = HideFlags.DontSave };
        mesh.Clear();
        vertices = (Vector3[])rest.Clone();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        Animate(0f);
    }

    void Update()
    {
        if (mesh == null || rest == null) Build();
        if (Application.isPlaying) Animate(Time.time);
    }

    void Animate(float time)
    {
        float k = 2f * Mathf.PI / Mathf.Max(0.05f, waveLength);
        for (int n = 0; n < rest.Length; n++)
        {
            Vector3 p = rest[n];
            float u = -p.x / size.x;            // 0 at the pole, 1 at the free end
            float v = -p.y / size.y;            // 0 at the top, 1 at the bottom
            float grow = Mathf.Pow(u, 1.3f);
            float wave = amplitude * grow * Mathf.Sin(k * -p.x - speed * time + phase + 0.6f * v);
            float ripple = flutter * u * Mathf.Sin(3.1f * k * -p.x - 2.3f * speed * time + 1.7f * v + phase);
            vertices[n] = new Vector3(p.x * (1f - 0.04f * grow), p.y - sag * u * u, wave + ripple);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
