using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameInitializer : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string playerResourcePath = "Player";
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private bool initializeOnStart = true;
    [Header("Weapon Setup")]
    [SerializeField] private WeaponBase fallbackWeaponPrefab;
    [SerializeField] private string playerWeaponMountName = "WeaponMount";
    [SerializeField] private bool resetWeaponLocalTransform = true;

    private void Start()
    {
        if (initializeOnStart)
        {
            InitializeGame();
        }
    }

    [ContextMenu("Initialize Game")]
    public void InitializeGame()
    {
        InitializePlayer();
    }

    [ContextMenu("Initialize Player")]
    public Player InitializePlayer()
    {
        GameObject existingPlayer = FindExistingPlayerObject();
        if (existingPlayer != null)
        {
            Player existingPlayerComponent = existingPlayer.GetComponent<Player>();
            EnsurePlayerWeaponLoadout(existingPlayerComponent);
            return existingPlayerComponent;
        }

        GameObject prefab = ResolvePlayerPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[GameInitializer] 没有找到 Player 预制体。请在 Inspector 指定 playerPrefab，或把预制体放到 Resources/Player。", this);
            return null;
        }

        Vector3 spawnPosition = ResolvePlayerSpawnPosition();
        spawnPosition.z = prefab.transform.position.z;

        GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
        if (!string.IsNullOrWhiteSpace(playerObjectName))
        {
            instance.name = playerObjectName;
        }

        Player player = instance.GetComponent<Player>();
        if (player == null)
        {
            Debug.LogWarning("[GameInitializer] 创建出来的 Player 预制体上没有 Player 组件。", instance);
        }

        EnsurePlayerWeaponLoadout(player);

        return player;
    }

    private GameObject FindExistingPlayerObject()
    {
        Player existingPlayer = FindFirstObjectByType<Player>();
        if (existingPlayer != null)
        {
            return existingPlayer.gameObject;
        }

        if (string.IsNullOrWhiteSpace(playerObjectName))
        {
            return null;
        }

        return GameObject.Find(playerObjectName);
    }

    private GameObject ResolvePlayerPrefab()
    {
        if (playerPrefab != null)
        {
            return playerPrefab;
        }

        if (string.IsNullOrWhiteSpace(playerResourcePath))
        {
            return null;
        }

        playerPrefab = Resources.Load<GameObject>(playerResourcePath);
        return playerPrefab;
    }

    private Vector3 ResolvePlayerSpawnPosition()
    {
        if (TryGetMapBounds(out Bounds mapBounds))
        {
            return mapBounds.center;
        }

        MapInitializer mapInitializer = FindFirstObjectByType<MapInitializer>();
        if (mapInitializer != null)
        {
            mapInitializer.LoadMap();
            if (TryGetMapBounds(out mapBounds))
            {
                return mapBounds.center;
            }
        }

        return Vector3.zero;
    }

    private bool TryGetMapBounds(out Bounds bounds)
    {
        ResolveMapRoot();
        bounds = default;

        if (mapRoot == null)
        {
            return false;
        }

        Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer rendererReference = renderers[index];
            if (rendererReference == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = rendererReference.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererReference.bounds);
            }
        }

        return hasBounds;
    }

    private void ResolveMapRoot()
    {
        if (mapRoot != null)
        {
            return;
        }

        string rootName = string.IsNullOrWhiteSpace(mapRootName) ? "MapRoot" : mapRootName;
        GameObject rootObject = GameObject.Find(rootName);
        if (rootObject != null)
        {
            mapRoot = rootObject.transform;
        }
    }

    private void EnsurePlayerWeaponLoadout(Player player)
    {
        if (player == null)
        {
            return;
        }

        WeaponBase weaponPrefab = WeaponSelectionSession.SelectedWeaponPrefab != null
            ? WeaponSelectionSession.SelectedWeaponPrefab
            : fallbackWeaponPrefab;

        if (weaponPrefab == null)
        {
            return;
        }

        ClearExistingWeapons(player);

        Transform weaponParent = ResolveWeaponMount(player.transform);
        WeaponBase weaponInstance = Instantiate(weaponPrefab, weaponParent, false);
        weaponInstance.name = weaponPrefab.name;

        if (resetWeaponLocalTransform)
        {
            Transform weaponTransform = weaponInstance.transform;
            weaponTransform.localPosition = Vector3.zero;
            weaponTransform.localRotation = Quaternion.identity;
            weaponTransform.localScale = Vector3.one;
        }

        weaponInstance.SetOwner(player);
        weaponInstance.SetWeaponEnabled(true);
    }

    private void ClearExistingWeapons(Player player)
    {
        WeaponBase[] existingWeapons = player.GetComponentsInChildren<WeaponBase>(true);
        for (int index = 0; index < existingWeapons.Length; index++)
        {
            WeaponBase existingWeapon = existingWeapons[index];
            if (existingWeapon == null)
            {
                continue;
            }

            if (existingWeapon.gameObject == player.gameObject)
            {
                continue;
            }

            Destroy(existingWeapon.gameObject);
        }
    }

    private Transform ResolveWeaponMount(Transform playerTransform)
    {
        if (playerTransform == null || string.IsNullOrWhiteSpace(playerWeaponMountName))
        {
            return playerTransform;
        }

        Transform[] childTransforms = playerTransform.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < childTransforms.Length; index++)
        {
            Transform childTransform = childTransforms[index];
            if (childTransform != null && childTransform != playerTransform && childTransform.name == playerWeaponMountName)
            {
                return childTransform;
            }
        }

        return playerTransform;
    }
}