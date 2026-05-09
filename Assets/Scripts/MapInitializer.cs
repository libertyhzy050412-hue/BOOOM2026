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
    
    //碰撞体合并
    [Header("Physics Merge")] [Tooltip("是否在生成的时候自动合并碰撞体")] [SerializeField]
    private bool mergeColliders = true;

    [Tooltip("预制体内装在碰撞体的节点名称")] [SerializeField]
    private string colliderRootName = "CollidersRoot";
    
    [Tooltip("自动生成的全局节点名称")]
    [SerializeField] private string globalPhysicsRootName = "GlobalWorldPhysicsRoot";

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

        // 开启了自动合并后，进行合并
        if (mergeColliders)
        {
            MergeAllMapColliders();
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

        if (mergeColliders)
        {
            ClearGlobalPhysicsRoot();
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

    /// <summary>
    /// 方法一：创建并配置全局物理总节点
    /// </summary>
    [ContextMenu("1. Ensure Global Physics Root")]
    public Transform EnsureGlobalPhysicsRoot()
    {
        GameObject physicsObj = GameObject.Find(globalPhysicsRootName);
        if (physicsObj == null)
        {
            physicsObj = new GameObject(globalPhysicsRootName);
            physicsObj.transform.position = Vector3.zero;
        }

        Rigidbody2D rb = physicsObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = physicsObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        CompositeCollider2D comp = physicsObj.GetComponent<CompositeCollider2D>();
        if (comp == null) comp = physicsObj.AddComponent<CompositeCollider2D>();
        return physicsObj.transform;
    }

    /// <summary>
    /// 方法二：遍历已加载的地图块，提取它们的碰撞体并吸附到全局节点下
    /// </summary>
    [ContextMenu("2. Merge All Map Colliders")]
    public void MergeAllMapColliders()
    {
        Transform root = EnsureMapRoot();
        Transform physicsRoot = EnsureGlobalPhysicsRoot();

        // 遍历目前挂在 MapRoot 下的所有地图块
        for (int i = 0; i < root.childCount; i++)
        {
            Transform chunk = root.GetChild(i);
            if (!chunk.name.StartsWith(GeneratedChunkPrefix, StringComparison.Ordinal)) continue;

            // 寻找名为 Colliders_Root 的子节点并进行转移
            Transform colliders = chunk.Find(colliderRootName);
            if (colliders != null && colliders.parent != physicsRoot)
            {
                colliders.SetParent(physicsRoot, true);
            }
        }
    }

    /// <summary>
    /// 方法三：彻底删除全局物理总节点及其内部的所有碰撞体
    /// </summary>
    [ContextMenu("3. Clear Global Physics Root")]
    public void ClearGlobalPhysicsRoot()
    {
        GameObject oldPhysicsRoot = GameObject.Find(globalPhysicsRootName);
        if (oldPhysicsRoot != null)
        {
            if (Application.isPlaying) Destroy(oldPhysicsRoot);
            else DestroyImmediate(oldPhysicsRoot);
        }
    }

  
    
}