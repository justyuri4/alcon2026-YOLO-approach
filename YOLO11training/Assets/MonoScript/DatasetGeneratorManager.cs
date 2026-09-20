using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

public class DatasetGeneratorManager : MonoBehaviour
{
    [Header("自動取得設定")]
    [Tooltip("チェックを入れると、このオブジェクトの子要素からステップを自動検索します。チェックを外すとScene全体から検索します。")]
    public bool searchOnlyInChildren = false;

    [Header("ループ設定")]
    [Tooltip("データセット撮影を繰り返す回数")]
    public int captureCount = 10;

    void Start()
    {
        StartCoroutine(RunDebugSequence());
    }

    IEnumerator RunDebugSequence()
    {
        for (int i = 0; i < captureCount; i++)
        {
            Debug.Log($"[DebugManager] --- シーケンス実行開始 ({i + 1} / {captureCount}) ---");

            // =========================================================
            // 1. IProcessStep を自動検索
            // =========================================================
            List<IProcessStep> steps = new List<IProcessStep>();

            if (searchOnlyInChildren)
            {
                var components = GetComponentsInChildren<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp is IProcessStep step)
                    {
                        steps.Add(step);
                    }
                }
            }
            else
            {
                var components = FindObjectsOfType<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp is IProcessStep step)
                    {
                        steps.Add(step);
                    }
                }
            }

            if (steps.Count == 0)
            {
                Debug.LogError("[DebugManager] IProcessStep を実装した有効なコンポーネントが見つかりませんでした！");
                yield break;
            }

            // =========================================================
            // 2. RandomCameraController を検索
            // =========================================================
            RandomCameraController cameraController = null;
            foreach (var step in steps)
            {
                if (step is RandomCameraController camera)
                {
                    cameraController = camera;
                    break;
                }
            }

            if (cameraController == null)
            {
                Debug.LogWarning("[DebugManager] RandomCameraController が見つかりませんでした。");
            }

            // =========================================================
            // 3. RandomCameraController以外の IProcessStep を実行
            // =========================================================
            foreach (var step in steps)
            {
                if (step is RandomCameraController) continue;

                MonoBehaviour comp = step as MonoBehaviour;
                string stepName = comp != null ? comp.name : "Unknown";

                yield return StartCoroutine(step.ExecuteStep());
            }

            // レンダリング反映待ち
            yield return null;

            // =========================================================
            // 4. 最後にカメラを実行（撮影）
            // =========================================================
            if (cameraController != null)
            {
                yield return StartCoroutine(cameraController.ExecuteStep());
            }

            Debug.Log($"[DebugManager] --- シーケンス完了 ({i + 1} / {captureCount}) ---");
        }

        // =========================================================
        // 5. 指定回数の処理がすべて完了したため再生停止
        // =========================================================
        Debug.Log("[DebugManager] 指定された回数の撮影がすべて完了しました。再生を終了します。");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}