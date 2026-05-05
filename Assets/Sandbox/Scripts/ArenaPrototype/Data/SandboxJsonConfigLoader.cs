using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sandbox.DreamBattle
{
    public static class SandboxConfigPaths
    {
        public const string FloorGrid = "DreamBattleConfigs/floor-grid";
        public const string PlayerMovement = "DreamBattleConfigs/player-movement";

        public const string EnemyBasic = "DreamBattleConfigs/enemy-basic";
        public const string EnemySpawner = "DreamBattleConfigs/enemy-spawner";
        public const string DebugGui = "DreamBattleConfigs/debug-gui";
    }

    public static class SandboxDreamBattleConfigLoader
    {
        private static readonly Dictionary<string, object> Cache = new Dictionary<string, object>();
        private static readonly HashSet<string> MissingWarnings = new HashSet<string>();

        public static bool TryLoad<T>(string resourcePath, out T config) where T : class, new()
        {
            string cacheKey = typeof(T).FullName + ":" + resourcePath;
            if (Cache.TryGetValue(cacheKey, out object cached) && cached is T typed)
            {
                config = typed;
                return true;
            }

            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                if (!MissingWarnings.Contains(cacheKey))
                {
                    MissingWarnings.Add(cacheKey);
                    Debug.LogWarning($"Config not found: Resources/{resourcePath}. Using defaults.");
                }

                config = null;
                return false;
            }

            config = JsonUtility.FromJson<T>(StripJsonComments(asset.text)) ?? new T();
            Cache[cacheKey] = config;
            return true;
        }

        public static void ClearCache()
        {
            Cache.Clear();
            MissingWarnings.Clear();
        }

        private static string StripJsonComments(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            StringBuilder sb = new StringBuilder(raw.Length);
            bool inString = false;
            bool escaped = false;
            bool lineComment = false;
            bool blockComment = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                char n = i + 1 < raw.Length ? raw[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\r' || c == '\n') { lineComment = false; sb.Append(c); }
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && n == '/') { blockComment = false; i++; }
                    else if (c == '\r' || c == '\n') sb.Append(c);
                    continue;
                }

                if (!inString && c == '/' && n == '/') { lineComment = true; i++; continue; }
                if (!inString && c == '/' && n == '*') { blockComment = true; i++; continue; }

                sb.Append(c);

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') inString = true;
            }

            return sb.ToString();
        }
    }
}
