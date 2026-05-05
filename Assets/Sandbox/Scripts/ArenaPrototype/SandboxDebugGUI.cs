using UnityEngine;

namespace Sandbox.DreamBattle
{
    public class SandboxDebugGUI : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string configPath = SandboxConfigPaths.DebugGui;

        [Header("References")]
        [SerializeField] private SandboxFloorGrid floorGrid;
        [SerializeField] private SandboxEnemySpawner enemySpawner;
        [SerializeField] private SandboxPlayerController player;

        // Layout
        private float panelX = 12f;
        private float panelY = 12f;
        private float panelWidth = 320f;
        private float panelHeight = 220f;
        private float padding = 12f;

        // Colors
        private Color bgColor = new Color(0f, 0f, 0f, 0.75f);
        private Color fillColorDream = new Color(0.6f, 0.4f, 0.9f, 0.9f);
        private Color fillColorNightmare = new Color(0.12f, 0.06f, 0.2f, 0.9f);
        private Color textColor = Color.white;
        private Color dimTextColor = new Color(0.7f, 0.7f, 0.7f);

        private Texture2D pixelTex;
        private GUIStyle labelStyle;

        private void Awake()
        {
            LoadConfig();
            pixelTex = CreatePixelTexture();
        }

        private void OnValidate()
        {
            LoadConfig();
        }

        private void OnGUI()
        {
            EnsureStyles();

            Rect panel = new Rect(panelX, panelY, panelWidth, panelHeight);
            GUI.color = bgColor;
            GUI.DrawTexture(panel, pixelTex);
            GUI.color = Color.white;

            float y = panelY + padding;

            // Title
            DrawLabel(panelX + padding, y, "<b>Dream Battle — Debug</b>", textColor, 16);
            y += 24f;

            // Dream %
            float dreamPct = floorGrid != null ? floorGrid.DreamPercentage : 0f;
            DrawLabel(panelX + padding, y, $"Dream: {dreamPct * 100f:F1}%", textColor, 14);
            y += 20f;

            // Dream bar
            Rect barRect = new Rect(panelX + padding, y, panelWidth - padding * 2f, 14f);
            GUI.color = fillColorNightmare;
            GUI.DrawTexture(barRect, pixelTex);
            GUI.color = fillColorDream;
            Rect dreamRect = new Rect(barRect.x, barRect.y, barRect.width * dreamPct, barRect.height);
            GUI.DrawTexture(dreamRect, pixelTex);
            GUI.color = Color.white;
            y += 22f;

            // Enemy count
            int enemyCount = enemySpawner != null ? enemySpawner.ActiveCount : 0;
            DrawLabel(panelX + padding, y, $"Enemies: {enemyCount}", textColor, 14);
            y += 20f;

            // Pour paint info
            if (player != null)
            {
                float pourPct = player.PourMaxRadius > 0f ? player.PourRadius / player.PourMaxRadius : 0f;
                DrawLabel(panelX + padding, y,
                    $"Pour: {pourPct * 100f:F0}%  (r={player.PourRadius:F1}/{player.PourMaxRadius:F1})",
                    dimTextColor, 12);
                y += 18f;
            }

            // Floor info
            if (floorGrid != null)
            {
                DrawLabel(panelX + padding, y,
                    $"Floor: {floorGrid.WorldSize.x:F0}×{floorGrid.WorldSize.y:F0}",
                    dimTextColor, 12);
                y += 18f;
            }

            // Controls hint
            DrawLabel(panelX + padding, panelY + panelHeight - padding - 16f,
                "WASD: Move  |  Mouse: Aim  |  Hold LMB: Pour Paint",
                dimTextColor, 11);
        }

        private void DrawLabel(float x, float y, string text, Color color, int size)
        {
            labelStyle.fontSize = size;
            labelStyle.normal.textColor = color;
            GUI.Label(new Rect(x, y, panelWidth - padding * 2f, size + 4f), text, labelStyle);
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    fontSize = 14,
                    normal = { textColor = textColor }
                };
            }
        }

        private Texture2D CreatePixelTexture()
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return tex;
        }

        private void LoadConfig()
        {
            if (SandboxDreamBattleConfigLoader.TryLoad(configPath, out DebugGuiConfig cfg))
            {
                panelX = cfg.panelX;
                panelY = cfg.panelY;
                panelWidth = cfg.panelWidth;
                panelHeight = cfg.panelHeight;
                padding = cfg.padding;
            }

            panelWidth = Mathf.Max(100f, panelWidth);
            panelHeight = Mathf.Max(60f, panelHeight);
            padding = Mathf.Clamp(padding, 4f, 40f);
        }

        private void OnDestroy()
        {
            if (pixelTex != null)
            {
                if (Application.isPlaying)
                    Destroy(pixelTex);
                else
                    DestroyImmediate(pixelTex);

                pixelTex = null;
            }
        }

        [System.Serializable]
        private class DebugGuiConfig
        {
            public float panelX = 12f;
            public float panelY = 12f;
            public float panelWidth = 320f;
            public float panelHeight = 220f;
            public float padding = 12f;
        }
    }
}
