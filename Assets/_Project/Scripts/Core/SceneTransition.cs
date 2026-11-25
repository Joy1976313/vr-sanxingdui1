using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 通用场景切换触发器：玩家碰撞体进入区域即加载目标场景。
/// 序章→大厅、大厅→各关卡、关卡→大厅 全部复用此组件。
/// 用法：挂在一个 BoxCollider(IsTrigger) 物体上，设置目标场景名即可。
/// </summary>
[AddComponentMenu("三星堆/Scene Transition")]
public class SceneTransition : MonoBehaviour
{
    [SerializeField, Tooltip("目标场景名（必须已加入 Build Settings）")]
    private string targetScene = "Hub";

    [SerializeField, Tooltip("触发后的延迟（秒），给转场演出留时间")]
    private float delay = 0.3f;

    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        // 只响应玩家的 CharacterController（XR Origin 身上的胶囊体）
        if (!(other is CharacterController)) return;
        _triggered = true;
        StartCoroutine(LoadSceneAfterDelay());
    }

    private IEnumerator LoadSceneAfterDelay()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(targetScene);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.35f);
        var box = GetComponent<BoxCollider>();
        if (box != null) Gizmos.DrawCube(transform.position, box.size);
    }
}
