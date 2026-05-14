using System.Collections.Generic;
using UnityEngine;

public static class GuiAudioButton
{
    private static readonly HashSet<string> HoveredKeys = new HashSet<string>();
    private static readonly Dictionary<string, int> LastSeenFrameByKey = new Dictionary<string, int>();
    private static int lastCleanupFrame = -1;

    public static bool LayoutButton(string key, string text, GUIStyle style, params GUILayoutOption[] options)
    {
        GUIContent content = new GUIContent(text);
        Rect rect = GUILayoutUtility.GetRect(content, style, options);
        HandleAudio(key, rect, GUI.enabled);
        return GUI.Button(rect, text, style);
    }

    public static bool Button(string key, Rect rect, string text, GUIStyle style, bool interactive = true)
    {
        bool canInteract = interactive && GUI.enabled;
        HandleAudio(key, rect, canInteract);
        return GUI.Button(rect, text, style);
    }

    private static void HandleAudio(string key, Rect rect, bool interactive)
    {
        //跳过Layout阶段可以解决音效的问题
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.Layout)
        {
            return;
        }
        
        CleanupStaleHoverKeys();
        LastSeenFrameByKey[key] = Time.frameCount;

        if (!interactive)
        {
            HoveredKeys.Remove(key);
            return;
        }

        bool isHovering = rect.Contains(currentEvent.mousePosition);

        if (!isHovering)
        {
            HoveredKeys.Remove(key);
            return;
        }

        if (!HoveredKeys.Contains(key))
        {
            HoveredKeys.Add(key);
            AudioManager.PlayUiHover();
        }

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            AudioManager.PlayUiClick();
        }
    }

    private static void CleanupStaleHoverKeys()
    {
        int currentFrame = Time.frameCount;
        if (lastCleanupFrame == currentFrame)
        {
            return;
        }

        lastCleanupFrame = currentFrame;
        if (HoveredKeys.Count == 0)
        {
            return;
        }

        List<string> staleKeys = null;
        foreach (string hoveredKey in HoveredKeys)
        {
            if (!LastSeenFrameByKey.TryGetValue(hoveredKey, out int lastSeenFrame) || lastSeenFrame < currentFrame - 1)
            {
                staleKeys ??= new List<string>();
                staleKeys.Add(hoveredKey);
            }
        }

        if (staleKeys == null)
        {
            return;
        }

        for (int index = 0; index < staleKeys.Count; index++)
        {
            HoveredKeys.Remove(staleKeys[index]);
        }
    }
}