using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DatasetDebugManager : MonoBehaviour
{
    [Header("自動取得設定")]
    [Tooltip("チェックを入れると、このオブジェクトの子要素からステップを自動検索します。チェックを外すとScene全体から検索します。")]
    public bool searchOnlyInChildren = false;

    void Start()
    {
        StartCoroutine(RunDebugSequence());
    }

    IEnumerator RunDebugSequence()
    {
        Debug.Log("[DebugManager] IProcessStep の自動検索を開始します...");

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
            Debug.LogError(
                "[DebugManager] IProcessStep を実装した有効なコンポーネントが見つかりませんでした！"
            );

            yield break;
        }

        Debug.Log(
            $"[DebugManager] 検出されたIProcessStep数: {steps.Count}"
        );

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

        // カメラが見つからなくても、
        // 他のIProcessStepは実行できるようにする
        if (cameraController == null)
        {
            Debug.LogWarning(
                "[DebugManager] RandomCameraController が見つかりませんでした。"
            );
        }

        // =========================================================
        // 3. RandomCameraController以外の
        //    IProcessStepをすべて実行
        //
        // ここではカメラを実行しない
        // =========================================================

        Debug.Log(
            "[DebugManager] カメラ以外のIProcessStepを実行します..."
        );

        foreach (var step in steps)
        {
            // RandomCameraControllerは最後に実行するためスキップ
            if (step is RandomCameraController)
            {
                continue;
            }

            MonoBehaviour comp = step as MonoBehaviour;

            string stepName =
                comp != null ? comp.name : "Unknown";

            Debug.Log(
                $"[DebugManager] IProcessStep実行中: {stepName}"
            );

            // この処理が完全に終わるまで次へ進まない
            yield return StartCoroutine(step.ExecuteStep());

            Debug.Log(
                $"[DebugManager] IProcessStep完了: {stepName}"
            );
        }

        // =========================================================
        // 4. すべてのIProcessStepが完了
        // =========================================================

        Debug.Log(
            "[DebugManager] すべてのIProcessStepが完了しました。"
        );

        // ここで1フレーム待つ
        // Skyboxやその他の変更をレンダリング側へ反映させるため
        yield return null;

        // =========================================================
        // 5. 最後にカメラを実行
        //
        // Skybox / オブジェクト / その他の処理が
        // すべて完了した後にここへ来る
        // =========================================================

        if (cameraController != null)
        {
            Debug.Log(
                "[DebugManager] 最後にRandomCameraControllerを実行します。"
            );

            yield return StartCoroutine(
                cameraController.ExecuteStep()
            );

            Debug.Log(
                "[DebugManager] RandomCameraControllerが完了しました。"
            );
        }

        // =========================================================
        // 6. 全処理完了
        // =========================================================

        Debug.Log(
            "[DebugManager] すべてのデータセット処理が完了しました！"
        );
    }
}