using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DatasetGeneratorManager : MonoBehaviour
{
    [Header("自動取得設定")]
    [Tooltip("チェックを入れると、このオブジェクトの子要素からステップを自動検索します。チェックを外すとScene全体から検索します。")]
    public bool searchOnlyInChildren = false;

    [Header("ループ設定")]
    [Tooltip("データセット撮影を繰り返す回数")]
    public int captureCount = 10;

    [Header("撮影安定化設定")]
    [Tooltip("シーン生成後、撮影前に待機するフレーム数")]
    [Min(0)]
    public int framesBeforeCapture = 2;

    [Tooltip("撮影要求後、次のシーン生成を始める前に待機するフレーム数")]
    [Min(0)]
    public int framesAfterCapture = 2;

    private readonly List<IProcessStep> processSteps = new List<IProcessStep>();
    private RandomCameraController cameraController;

    private void Awake()
    {
        CacheProcessSteps();
    }

    private void Start()
    {
        if (processSteps.Count == 0)
        {
            Debug.LogError(
                "[DatasetGeneratorManager] IProcessStepを実装した有効なコンポーネントが見つかりませんでした。",
                this
            );

            return;
        }

        StartCoroutine(RunSequence());
    }

    private void CacheProcessSteps()
    {
        processSteps.Clear();
        cameraController = null;

        MonoBehaviour[] components;

        if (searchOnlyInChildren)
        {
            components = GetComponentsInChildren<MonoBehaviour>(true);
        }
        else
        {
            components = FindObjectsOfType<MonoBehaviour>(true);
        }

        foreach (MonoBehaviour component in components)
        {
            if (component is not IProcessStep step)
            {
                continue;
            }

            if (!component.isActiveAndEnabled)
            {
                continue;
            }

            if (step is RandomCameraController camera)
            {
                cameraController = camera;
            }
            else
            {
                processSteps.Add(step);
            }
        }

        Debug.Log(
            $"[DatasetGeneratorManager] ステップをキャッシュしました。準備ステップ: {processSteps.Count}個",
            this
        );

        if (cameraController == null)
        {
            Debug.LogWarning(
                "[DatasetGeneratorManager] RandomCameraControllerが見つかりませんでした。",
                this
            );
        }
    }

    private IEnumerator RunSequence()
    {
        for (int i = 0; i < captureCount; i++)
        {
            Debug.Log(
                $"[DatasetGeneratorManager] --- シーケンス実行開始 ({i + 1} / {captureCount}) ---"
            );

            // 田んぼ生成、雑草配置、空などを実行
            foreach (IProcessStep step in processSteps)
            {
                yield return step.ExecuteStep();
            }

            // Destroy、Instantiate、Label登録、ライティングの反映を待つ
            yield return WaitForStableFrame(framesBeforeCapture);

            // カメラ位置を変更して撮影を要求
            if (cameraController != null)
            {
                yield return cameraController.ExecuteStep();
            }

            // RequestCapture後の画像・ラベル保存完了を待つ
            yield return WaitForStableFrame(framesAfterCapture);

            Debug.Log(
                $"[DatasetGeneratorManager] --- シーケンス完了 ({i + 1} / {captureCount}) ---"
            );
        }

        Debug.Log(
            "[DatasetGeneratorManager] 指定された回数の撮影がすべて完了しました。"
        );

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator WaitForStableFrame(int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
        {
            // 画面描画終了まで待機する
            yield return new WaitForEndOfFrame();
        }
    }
}