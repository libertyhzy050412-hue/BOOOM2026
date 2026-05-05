using UnityEngine;

namespace Sandbox.DreamBattle
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SandboxArenaBounds : MonoBehaviour
    {
        private const string TopWallName = "Top Wall";
        private const string BottomWallName = "Bottom Wall";
        private const string LeftWallName = "Left Wall";
        private const string RightWallName = "Right Wall";

        [SerializeField] private Vector2 arenaSize = new Vector2(30f, 18f);
        [SerializeField] private float wallThickness = 1f;

        public Vector2 ArenaSize => arenaSize;

        public Rect WorldRect
        {
            get
            {
                Vector2 center = transform.position;
                Vector2 halfSize = arenaSize * 0.5f;
                return Rect.MinMaxRect(
                    center.x - halfSize.x,
                    center.y - halfSize.y,
                    center.x + halfSize.x,
                    center.y + halfSize.y);
            }
        }

        private void Reset()
        {
            ClampValues();
            RebuildWalls();
        }

        private void OnValidate()
        {
            ClampValues();
            RebuildWalls();
        }

        [ContextMenu("Rebuild Arena Walls")]
        public void RebuildWalls()
        {
            ClampValues();

            Vector2 halfSize = arenaSize * 0.5f;
            float halfThickness = wallThickness * 0.5f;

            ConfigureWall(
                GetOrCreateWall(TopWallName),
                new Vector2(0f, halfSize.y + halfThickness),
                new Vector2(arenaSize.x + wallThickness * 2f, wallThickness));

            ConfigureWall(
                GetOrCreateWall(BottomWallName),
                new Vector2(0f, -(halfSize.y + halfThickness)),
                new Vector2(arenaSize.x + wallThickness * 2f, wallThickness));

            ConfigureWall(
                GetOrCreateWall(LeftWallName),
                new Vector2(-(halfSize.x + halfThickness), 0f),
                new Vector2(wallThickness, arenaSize.y));

            ConfigureWall(
                GetOrCreateWall(RightWallName),
                new Vector2(halfSize.x + halfThickness, 0f),
                new Vector2(wallThickness, arenaSize.y));
        }

        private void ClampValues()
        {
            arenaSize.x = Mathf.Max(4f, arenaSize.x);
            arenaSize.y = Mathf.Max(4f, arenaSize.y);
            wallThickness = Mathf.Max(0.1f, wallThickness);
        }

        public Vector2 GetRandomSpawnPointNearInnerEdge(float inset)
        {
            Rect rect = WorldRect;
            float clampedInset = Mathf.Max(0.05f, inset);
            float minX = rect.xMin + clampedInset;
            float maxX = rect.xMax - clampedInset;
            float minY = rect.yMin + clampedInset;
            float maxY = rect.yMax - clampedInset;

            switch (Random.Range(0, 4))
            {
                case 0:
                    return new Vector2(Random.Range(minX, maxX), maxY);
                case 1:
                    return new Vector2(Random.Range(minX, maxX), minY);
                case 2:
                    return new Vector2(minX, Random.Range(minY, maxY));
                default:
                    return new Vector2(maxX, Random.Range(minY, maxY));
            }
        }

        private Transform GetOrCreateWall(string wallName)
        {
            Transform wall = transform.Find(wallName);
            if (wall != null)
            {
                return wall;
            }

            GameObject wallObject = new GameObject(wallName);
            wallObject.transform.SetParent(transform, false);
            wallObject.layer = gameObject.layer;
            return wallObject.transform;
        }

        private void ConfigureWall(Transform wallTransform, Vector2 localPosition, Vector2 size)
        {
            wallTransform.localPosition = localPosition;
            wallTransform.localRotation = Quaternion.identity;
            wallTransform.localScale = Vector3.one;
            wallTransform.gameObject.layer = gameObject.layer;

            BoxCollider2D collider = wallTransform.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = wallTransform.gameObject.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = false;
            collider.size = size;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.7f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(arenaSize.x, arenaSize.y, 0f));
        }
    }
}