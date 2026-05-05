using UnityEngine;

namespace Sandbox.DreamBattle
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SandboxFloorGrid : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private Vector2 worldSize = new Vector2(30f, 18f);
        [SerializeField] [Range(8f, 80f)] private float pixelsPerUnit = 24f;

        [Header("Colors")]
        [SerializeField] private Color nightmareColor = new Color(0.04f, 0.02f, 0.08f);
        [SerializeField] private Color dreamColor = new Color(0.85f, 0.65f, 0.95f);

        [Header("Effects")]
        [SerializeField] private float nightmareSlowFactor = 0.5f;
        [SerializeField] private float dreamDamagePerSecond = 5f;
        [SerializeField] [Range(0.05f, 0.95f)] private float dreamThreshold = 0.55f;

        private Texture2D floorTexture;
        private Color32[] pixelBuffer;
        private float[] dreamBuffer;
        private int texWidth;
        private int texHeight;
        private Vector2 gridOrigin;
        private bool textureDirty;
        private float dreamCount;
        private float lastStatsUpdateTime;
        private Material floorMaterial;
        private Mesh floorMesh;

        private Color32 nightmareColor32;
        private Color32 dreamColor32;

        // ── Public API ──────────────────────────────

        public float DreamPercentage { get; private set; }

        public float NightmareSlowFactor => nightmareSlowFactor;

        public float DreamDamagePerSecond => dreamDamagePerSecond;

        public Vector2 WorldSize => worldSize;

        public Vector2 GridOrigin => gridOrigin;

        // ── Lifecycle ────────────────────────────────

        private void Awake()
        {
            InitColors();
            BuildFloor();
        }

        private void OnEnable()
        {
            if (floorTexture == null) { InitColors(); BuildFloor(); }
        }

        private void OnValidate()
        {
            ClampValues();
            InitColors();
            if (floorTexture != null && Application.isPlaying)
                BuildFloor();
        }

        private void LateUpdate()
        {
            if (textureDirty)
            {
                floorTexture.SetPixels32(pixelBuffer);
                floorTexture.Apply(false);
                textureDirty = false;
            }
        }

        private void Update()
        {
            if (Time.time - lastStatsUpdateTime > 0.5f)
                UpdateStats();
        }

        // ── Build ────────────────────────────────────

        [ContextMenu("Rebuild Floor")]
        public void BuildFloor()
        {
            ClampValues();

            MeshFilter filter = GetComponent<MeshFilter>();
            MeshRenderer renderer = GetComponent<MeshRenderer>();

            DestroyMesh();
            DestroyTexture();
            DestroyMaterial();

            // Quad mesh
            floorMesh = new Mesh { name = "Floor Quad" };
            floorMesh.hideFlags = HideFlags.HideAndDontSave;

            float hw = worldSize.x * 0.5f;
            float hh = worldSize.y * 0.5f;

            floorMesh.vertices = new[]
            {
                new Vector3(-hw, -hh, 0f),
                new Vector3( hw, -hh, 0f),
                new Vector3(-hw,  hh, 0f),
                new Vector3( hw,  hh, 0f),
            };
            floorMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            floorMesh.triangles = new[] { 0, 2, 3, 0, 3, 1 };
            floorMesh.RecalculateBounds();
            filter.sharedMesh = floorMesh;

            // Texture
            texWidth = Mathf.Max(32, Mathf.RoundToInt(worldSize.x * pixelsPerUnit));
            texHeight = Mathf.Max(32, Mathf.RoundToInt(worldSize.y * pixelsPerUnit));

            floorTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false)
            {
                name = "Floor Texture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            int total = texWidth * texHeight;
            pixelBuffer = new Color32[total];
            dreamBuffer = new float[total];
            InitAllPixels();

            floorTexture.SetPixels32(pixelBuffer);
            floorTexture.Apply();

            floorMaterial = CreateMaterial();
            floorMaterial.mainTexture = floorTexture;
            renderer.sharedMaterial = floorMaterial;

            gridOrigin = new Vector2(transform.position.x - hw, transform.position.y - hh);
            textureDirty = false;
            UpdateStats();
        }

        // ── Paint API (direct-set, no lerp) ──────────

        /// <summary>Set all pixels within a circle to Dream.</summary>
        public void SetDreamCircle(Vector2 center, float radius)
        {
            PaintDreamStamp(center, radius, 1f, 1f);
        }

        /// <summary>Set all pixels along a line segment to Dream.</summary>
        public void SetDreamLine(Vector2 from, Vector2 to, float width)
        {
            float radius = Mathf.Max(0.01f, width * 0.5f);
            PaintDreamStroke(from, to, radius, 1f, 0.78f, radius * 0.32f);
        }

        /// <summary>Set all pixels along a line segment to Nightmare.</summary>
        public void SetNightmareLine(Vector2 from, Vector2 to, float width)
        {
            float radius = Mathf.Max(0.01f, width * 0.5f);
            PaintNightmareStroke(from, to, radius, 1f, 0.82f, radius * 0.4f);
        }

        public void PaintDreamStamp(Vector2 center, float radius, float opacity, float hardness)
        {
            PaintStamp(center, radius, opacity, hardness, true);
        }

        public void PaintNightmareStamp(Vector2 center, float radius, float opacity, float hardness)
        {
            PaintStamp(center, radius, opacity, hardness, false);
        }

        public void PaintDreamStroke(Vector2 from, Vector2 to, float radius, float opacity, float hardness, float spacing)
        {
            PaintStroke(from, to, radius, opacity, hardness, spacing, true);
        }

        public void PaintNightmareStroke(Vector2 from, Vector2 to, float radius, float opacity, float hardness, float spacing)
        {
            PaintStroke(from, to, radius, opacity, hardness, spacing, false);
        }

        /// <summary>Query color at world position.</summary>
        public Color GetColorAt(Vector2 worldPos)
        {
            if (pixelBuffer == null) return nightmareColor;
            int idx = PosToIndex(worldPos);
            if (idx < 0 || idx >= pixelBuffer.Length) return nightmareColor;
            return pixelBuffer[idx];
        }

        /// <summary>True if the pixel at worldPos is closer to dream than nightmare.</summary>
        public bool IsDreamAt(Vector2 worldPos)
        {
            if (dreamBuffer == null) return false;
            int idx = PosToIndex(worldPos);
            if (idx < 0 || idx >= dreamBuffer.Length) return false;
            return dreamBuffer[idx] >= dreamThreshold;
        }

        // ── Internal painting ────────────────────────

        private void PaintStamp(Vector2 center, float radius, float opacity, float hardness, bool towardsDream)
        {
            if (pixelBuffer == null || dreamBuffer == null) return;

            radius = Mathf.Max(0.01f, radius);
            opacity = Mathf.Clamp01(opacity);
            hardness = Mathf.Clamp01(hardness);
            if (opacity <= 0.0001f) return;

            float cx = WorldToPixelFloatX(center.x);
            float cy = WorldToPixelFloatY(center.y);
            float radiusPx = radius * pixelsPerUnit;
            int rPx = Mathf.CeilToInt(radiusPx);

            int pxMin = Mathf.Max(0, Mathf.FloorToInt(cx - rPx));
            int pxMax = Mathf.Min(texWidth - 1, Mathf.CeilToInt(cx + rPx));
            int pyMin = Mathf.Max(0, Mathf.FloorToInt(cy - rPx));
            int pyMax = Mathf.Min(texHeight - 1, Mathf.CeilToInt(cy + rPx));

            bool changed = false;

            for (int py = pyMin; py <= pyMax; py++)
            {
                float dy = (py + 0.5f) - cy;
                for (int px = pxMin; px <= pxMax; px++)
                {
                    float dx = (px + 0.5f) - cx;
                    float distance01 = Mathf.Sqrt(dx * dx + dy * dy) / radiusPx;
                    if (distance01 <= 1f)
                    {
                        int idx = py * texWidth + px;
                        float falloff = EvaluateBrushFalloff(distance01, hardness);
                        float current = dreamBuffer[idx];
                        float next = towardsDream
                            ? current + (1f - current) * opacity * falloff
                            : current - current * opacity * falloff;
                        next = Mathf.Clamp01(next);

                        if (Mathf.Abs(next - current) > 0.0001f)
                        {
                            dreamBuffer[idx] = next;
                            pixelBuffer[idx] = SamplePaintColor(next);
                            changed = true;
                        }
                    }
                }
            }

            if (changed) textureDirty = true;
        }

        private void PaintStroke(Vector2 from, Vector2 to, float radius, float opacity, float hardness, float spacing, bool towardsDream)
        {
            if (pixelBuffer == null || dreamBuffer == null) return;

            radius = Mathf.Max(0.01f, radius);
            spacing = Mathf.Max(radius * 0.15f, spacing);
            Vector2 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.0001f)
            {
                PaintStamp(from, radius, opacity, hardness, towardsDream);
                return;
            }

            int stampCount = Mathf.Max(1, Mathf.CeilToInt(len / spacing));
            for (int i = 0; i <= stampCount; i++)
            {
                float t = i / (float)stampCount;
                PaintStamp(Vector2.Lerp(from, to, t), radius, opacity, hardness, towardsDream);
            }
        }

        // ── Coordinate mapping ───────────────────────

        private int WorldToPixelX(float worldX)
        {
            return Mathf.RoundToInt((worldX - gridOrigin.x) / worldSize.x * texWidth);
        }

        private int WorldToPixelY(float worldY)
        {
            return Mathf.RoundToInt((worldY - gridOrigin.y) / worldSize.y * texHeight);
        }

        private float WorldToPixelFloatX(float worldX)
        {
            return (worldX - gridOrigin.x) / worldSize.x * texWidth;
        }

        private float WorldToPixelFloatY(float worldY)
        {
            return (worldY - gridOrigin.y) / worldSize.y * texHeight;
        }

        private float PixelToWorldX(int px)
        {
            return gridOrigin.x + (px + 0.5f) / texWidth * worldSize.x;
        }

        private float PixelToWorldY(int py)
        {
            return gridOrigin.y + (py + 0.5f) / texHeight * worldSize.y;
        }

        private int PosToIndex(Vector2 worldPos)
        {
            int px = Mathf.Clamp(WorldToPixelX(worldPos.x), 0, texWidth - 1);
            int py = Mathf.Clamp(WorldToPixelY(worldPos.y), 0, texHeight - 1);
            return py * texWidth + px;
        }

        // ── Stats ────────────────────────────────────

        private void UpdateStats()
        {
            if (pixelBuffer == null) return;
            lastStatsUpdateTime = Time.time;

            dreamCount = 0f;
            int step = 4;
            int sampled = 0;

            for (int i = 0; i < pixelBuffer.Length; i += step)
            {
                sampled++;
                dreamCount += dreamBuffer[i];
            }

            DreamPercentage = sampled > 0 ? dreamCount / sampled : 0f;
        }

        private bool IsDreamIndex(int idx)
        {
            return dreamBuffer[idx] >= dreamThreshold;
        }

        // ── Helpers ──────────────────────────────────

        private void InitColors()
        {
            nightmareColor32 = nightmareColor;
            dreamColor32 = dreamColor;
        }

        private void InitAllPixels()
        {
            for (int i = 0; i < pixelBuffer.Length; i++)
            {
                dreamBuffer[i] = 0f;
                pixelBuffer[i] = nightmareColor32;
            }
        }

        private float EvaluateBrushFalloff(float distance01, float hardness)
        {
            if (distance01 >= 1f) return 0f;
            if (distance01 <= hardness) return 1f;

            float edgeT = Mathf.InverseLerp(1f, hardness, distance01);
            return edgeT * edgeT * (3f - 2f * edgeT);
        }

        private Color32 SamplePaintColor(float dreamAmount)
        {
            byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(nightmareColor32.r, dreamColor32.r, dreamAmount));
            byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(nightmareColor32.g, dreamColor32.g, dreamAmount));
            byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(nightmareColor32.b, dreamColor32.b, dreamAmount));
            byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(nightmareColor32.a, dreamColor32.a, dreamAmount));
            return new Color32(r, g, b, a);
        }

        private Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            return new Material(shader)
            {
                name = "Floor Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private void ClampValues()
        {
            worldSize.x = Mathf.Max(4f, worldSize.x);
            worldSize.y = Mathf.Max(4f, worldSize.y);
            pixelsPerUnit = Mathf.Clamp(pixelsPerUnit, 8f, 80f);
            dreamThreshold = Mathf.Clamp(dreamThreshold, 0.05f, 0.95f);
        }

        private void OnDestroy()
        {
            DestroyMesh();
            DestroyTexture();
            DestroyMaterial();
        }

        private void DestroyMesh()
        {
            if (floorMesh == null) return;
            if (Application.isPlaying) Destroy(floorMesh);
            else DestroyImmediate(floorMesh);
            floorMesh = null;
        }

        private void DestroyTexture()
        {
            if (floorTexture == null) return;
            if (Application.isPlaying) Destroy(floorTexture);
            else DestroyImmediate(floorTexture);
            floorTexture = null;
        }

        private void DestroyMaterial()
        {
            if (floorMaterial == null) return;
            if (Application.isPlaying) Destroy(floorMaterial);
            else DestroyImmediate(floorMaterial);
            floorMaterial = null;
        }
    }
}
