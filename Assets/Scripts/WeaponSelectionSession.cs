public static class WeaponSelectionSession
{
    private static WeaponBase selectedWeaponPrefab;
    private static string selectedWeaponId = string.Empty;
    private static string selectedWeaponDisplayName = string.Empty;

    public static bool HasSelection => selectedWeaponPrefab != null;
    public static WeaponBase SelectedWeaponPrefab => selectedWeaponPrefab;
    public static string SelectedWeaponId => selectedWeaponId;
    public static string SelectedWeaponDisplayName => selectedWeaponDisplayName;

    public static void SelectWeapon(string weaponId, string displayName, WeaponBase weaponPrefab)
    {
        selectedWeaponId = string.IsNullOrWhiteSpace(weaponId) ? weaponPrefab != null ? weaponPrefab.name : string.Empty : weaponId;
        selectedWeaponDisplayName = string.IsNullOrWhiteSpace(displayName) ? selectedWeaponId : displayName;
        selectedWeaponPrefab = weaponPrefab;
    }

    public static void ClearSelection()
    {
        selectedWeaponPrefab = null;
        selectedWeaponId = string.Empty;
        selectedWeaponDisplayName = string.Empty;
    }
}