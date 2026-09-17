using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DatasetGeneratorManager : MonoBehaviour
{
    [Header("生成ループ設定")]
    public int numberOfImages = 100;

    [Header("自動取得設定")]
    [Tooltip("チェックを入れると、このオブジェクトの子要素からステップを自動検索します。チェックを外すとScene全体から検索します。")]
    public bool searchOnlyInChildren = false;

    void Start()
    {
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        // 1. IProcessStep を自動検索
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
            Debug.LogError("[GeneratorManager] IProcessStep を実装した有効なコンポーネントが見つかりませんでした！");
            yield break;
        }

        // 2. RandomCameraController を検索
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
            Debug.LogWarning("[GeneratorManager] RandomCameraController が見つかりませんでした。");
        }

        // 3. リセット処理（一括）
        foreach (var step in steps)
        {
            if (step is IResettableStep resettable)
            {
                resettable.ResetIndex();
            }
        }

        // 4. 指定回数分ループ生成
        for (int i = 0; i < numberOfImages; i++)
        {
            // カメラ以外の IProcessStep を実行
            foreach (var step in steps)
            {
                if (step is RandomCameraController)
                {
                    continue;
                }

                yield return StartCoroutine(step.ExecuteStep());
            }

            // Skyboxやオブジェクトの変更を反映させるため1フレーム待機
            yield return null;

            // 最後にカメラ（RandomCameraController）を実行
            if (cameraController != null)
            {
                yield return StartCoroutine(cameraController.ExecuteStep());
            }
        }

        Debug.Log("すべてのデータセットの生成が完了しました！");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}