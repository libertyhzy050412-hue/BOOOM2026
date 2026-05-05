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

        private Texture2D floorTexture;
        private Color32[] pixelBuffer;
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
            SetCirclePixels(center, radius, dreamColor32);
        }

        /// <summary>Set all pixels along a line segment to Dream.</summary>
        public void SetDreamLine(Vector2 from, Vector2 to, float width)
        {
            SetLinePixels(from, to, width, dreamColor32);
        }

        /// <summary>Set all pixels along a line segment to Nightmare.</summary>
        public void SetNightmareLine(Vector2 from, Vector2 to, float width)
        {
            SetLinePixels(from, to, width, nightmareColor32);
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
            if (pixelBuffer == null) return false;
            int idx = PosToIndex(worldPos);
            if (idx < 0 || idx >= pixelBuffer.Length) return false;
            return IsDreamIndex(idx);
        }

        // ── Internal painting ────────────────────────

        private void SetCirclePixels(Vector2 center, float radius, Color32 target)
        {
            if (pixelBuffer == null) return;

            int cx = WorldToPixelX(center.x);
            int cy = WorldToPixelY(center.y);
            int rPx = Mathf.RoundToInt(radius * pixelsPerUnit);

            int pxMin = Mathf.Max(0, cx - rPx);
            int pxMax = Mathf.Min(texWidth - 1, cx + rPx);
            int pyMin = Mathf.Max(0, cy - rPx);
            int pyMax = Mathf.Min(texHeight - 1, cy + rPx);

            float rPxSq = (float)rPx * rPx;
            bool changed = false;

            for (int py = pyMin; py <= pyMax; py++)
            {
                float dy = py - cy;
                float dySq = dy * dy;
                for (int px = pxMin; px <= pxMax; px++)
                {
                    float dx = px - cx;
                    if (dx * dx + dySq <= rPxSq)
                    {
                        int idx = py * texWidth + px;
                        if (!ValueEquals(pixelBuffer[idx], target))
                        {
                            pixelBuffer[idx] = target;
                            changed = true;
                        }
                    }
                }
            }

            if (changed) textureDirty = true;
        }

        private void SetLinePixels(Vector2 from, Vector2 to, float width, Color32 target)
        {
            if (pixelBuffer == null) return;

            float halfW = width * 0.5f;
            Vector2 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.0001f)
            {
                SetCirclePixels(from, halfW, target);
                return;
            }

            Vector2 dirNorm = dir / len;

            float minX = Mathf.Min(from.x, to.x) - halfW;
            float maxX = Mathf.Max(from.x, to.x) + halfW;
            float minY = Mathf.Min(from.y, to.y) - halfW;
            float maxY = Mathf.Max(from.y, to.y) + halfW;

            int pxMin = Mathf.Max(0, WorldToPixelX(minX));
            int pxMax = Mathf.Min(texWidth - 1, WorldToPixelX(maxX));
            int pyMin = Mathf.Max(0, WorldToPixelY(minY));
            int pyMax = Mathf.Min(texHeight - 1, WorldToPixelY(maxY));

            float halfWSq = halfW * halfW;
            bool changed = false;

            for (int py = pyMin; py <= pyMax; py++)
            {
                float worldY = PixelToWorldY(py);
                for (int px = pxMin; px <= pxMax; px++)
                {
                    float worldX = PixelToWorldX(px);
                    Vector2 point = new Vector2(worldX, worldY);

                    Vector2 ap = point - from;
                    float t = Mathf.Clamp01(Vector2.Dot(ap, dirNorm) / len);
                    Vector2 closest = from + dirNorm * (t * len);
                    float distSq = (point - closest).sqrMagnitude;

                    if (distSq <= halfWSq)
                    {
                        int idx = py * texWidth + px;
                        if (!ValueEquals(pixelBuffer[idx], target))
                        {
                            pixelBuffer[idx] = target;
                            changed = true;
                        }
                    }
                }
            }

            if (changed) textureDirty = true;
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
                if (IsDreamIndex(i)) dreamCount += 1f;
            }

            DreamPercentage = sampled > 0 ? dreamCount / sampled : 0f;
        }

        private bool IsDreamIndex(int idx)
        {
            Color32 c = pixelBuffer[idx];
            return ColorDistSq(c, dreamColor32) < ColorDistSq(c, nightmareColor32);
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
                pixelBuffer[i] = nightmareColor32;
        }

        private static bool ValueEquals(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private static float ColorDistSq(Color32 a, Color32 b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
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
