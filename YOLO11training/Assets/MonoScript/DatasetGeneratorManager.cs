using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DatasetGeneratorManager : MonoBehaviour
{
    [Header("生成ループ設定")]
    public int numberOfImages = 100;

    [Header("実行ステップ一覧（インスペクターで順序通りに設定）")]
    public List<MonoBehaviour> processSteps = new List<MonoBehaviour>();

    void Start()
    {
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        // クラス名に依存せず、リセットインターフェースを持つものだけを一括リセット
        foreach (var stepObj in processSteps)
        {
            if (stepObj is IResettableStep resettable)
            {
                resettable.ResetIndex();
            }
        }

        for (int i = 0; i < numberOfImages; i++)
        {
            foreach (var stepObj in processSteps)
            {
                if (stepObj is IProcessStep step)
                {
                    yield return StartCoroutine(step.ExecuteStep());
                }
                else if (stepObj != null)
                {
                    Debug.LogWarning($"{stepObj.name} は IProcessStep を実装していません。", stepObj);
                }
            }
        }

        Debug.Log("すべてのデータセットの生成が完了しました！");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}