using UnityEngine;

namespace Sandbox.Scripts.ColliderTest
{
    public class ColliderMargeTest : MonoBehaviour
    {
        [Header("测试设置")]
        [Tooltip("场景里的全局物理总节点 (挂了 Composite Collider 2D 的那个)")]
        public Transform globalPhysicsRoot;

        // 注意看这里，加上这行神奇的代码
        [ContextMenu("点我执行：一键合并碰撞体")]
        void MergeAllCollidersInScene()
        {
            if (globalPhysicsRoot == null)
            {
                Debug.LogError("请先把 Global_World_Physics 拖到面板上！");
                return;
            }

            Transform[] allTransforms = FindObjectsOfType<Transform>();
            int mergeCount = 0;

            foreach (Transform t in allTransforms)
            {
                if (t.name == "Collider" && t.parent != globalPhysicsRoot)
                {
                    t.SetParent(globalPhysicsRoot, true);
                    mergeCount++;
                }
            }

            if (mergeCount > 0)
            {
                Debug.Log($"<color=green>大成功！</color> 瞬间抓取并合并了 {mergeCount} 个地图块的碰撞体！");
            }
            else
            {
                Debug.LogWarning("没有找到名为 'Collider' 的节点，请检查拼写。");
            }
        }
    }
}