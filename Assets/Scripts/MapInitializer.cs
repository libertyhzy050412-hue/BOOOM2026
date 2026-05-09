using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MapInitializer : MonoBehaviour
{
    private const string GeneratedChunkPrefix = "GeneratedMapChunk_";

    [SerializeField] private string resourcesFolder = "Map";
    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private bool loadOnStart = true;

    private void Start()
    {
        if (loadOnStart)
        {
            LoadMap();
        }
    }

    [ContextMenu("Load Map")]
    public void LoadMap()
    {
        Transform root = EnsureMapRoot();
        ClearLoadedMap();

        GameObject[] prefabs = Resources.LoadAll<GameObject>(resourcesFolder);
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"[MapInitializer] Resources/{resourcesFolder} 下没有找到地图块 prefab。", this);
            return;
        }

        Array.Sort(prefabs, ComparePrefabNames);

        for (int index = 0; index < prefabs.Length; index++)
        {
            GameObject prefab = prefabs[index];
            if (prefab == null)
            {
                continue;
            }

            GameObject instance = Instantiate(prefab, root, false);
            instance.name = GeneratedChunkPrefix + prefab.name;
        }
    }

    [ContextMenu("Clear Loaded Map")]
    public void ClearLoadedMap()
    {
        Transform root = EnsureMapRoot();
        for (int childIndex = root.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = root.GetChild(childIndex);
            if (child == null || !child.name.StartsWith(GeneratedChunkPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private Transform EnsureMapRoot()
    {
        if (mapRoot != null)
        {
            return mapRoot;
        }

        string rootName = string.IsNullOrWhiteSpace(mapRootName) ? "MapRoot" : mapRootName;
        GameObject existingRootObject = GameObject.Find(rootName);
        if (existingRootObject != null)
        {
            mapRoot = existingRootObject.transform;
            return mapRoot;
        }

        GameObject rootObject = new GameObject(rootName);
        mapRoot = rootObject.transform;
        return mapRoot;
    }

    private static int ComparePrefabNames(GameObject left, GameObject right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        return string.CompareOrdinal(leftName, rightName);
    }
}