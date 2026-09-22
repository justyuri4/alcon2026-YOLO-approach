using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class RandomSkyboxSetter : MonoBehaviour, IProcessStep
{
    [Header("HDRP Volume 設定")]
    [Tooltip("HDRP の HDRI Sky オーバーライドが含まれている Global Volume を指定")]
    [SerializeField] private Volume globalVolume;

    [Header("読み込み設定")]
    [Tooltip("Assets/Resources/ 以下のフォルダパスを指定（例: Skyboxes）")]
    [SerializeField] private string folderPath = "Skyboxes";

    private Cubemap[] skyboxes;

    private void Awake()
    {
        LoadSkyboxes();
    }

    /// <summary>
    /// Resources内のCubemapを最初に一度だけ読み込む。
    /// </summary>
    private void LoadSkyboxes()
    {
        skyboxes = Resources.LoadAll<Cubemap>(folderPath);

        if (skyboxes == null || skyboxes.Length == 0)
        {
            Debug.LogError(
                $"[RandomSkyboxSetter][エラー] " +
                $"Assets/Resources/{folderPath} 内にCubemapが見つかりません。",
                this
            );

            skyboxes = new Cubemap[0];
            return;
        }

        Debug.Log(
            $"[RandomSkyboxSetter] Skyboxを{skyboxes.Length}個読み込みました。",
            this
        );
    }

    /// <summary>
    /// DatasetGeneratorManagerから実行される処理ステップ。
    /// </summary>
    public IEnumerator ExecuteStep()
    {
        SetRandomSkybox();

        // HDRPのライティング反映を待つ
        yield return null;
    }

    [ContextMenu("Randomize Skybox")]
    public void SetRandomSkybox()
    {
        if (globalVolume == null)
        {
            Debug.LogError(
                "[RandomSkyboxSetter][エラー] " +
                "Global VolumeがInspectorで指定されていません。",
                this
            );

            return;
        }

        if (globalVolume.profile == null)
        {
            Debug.LogError(
                "[RandomSkyboxSetter][エラー] " +
                "Global VolumeにVolume Profileが設定されていません。",
                this
            );

            return;
        }

        if (!globalVolume.profile.TryGet<HDRISky>(out HDRISky hdriSky))
        {
            Debug.LogError(
                "[RandomSkyboxSetter][エラー] " +
                "Volume Profile内にHDRI Skyオーバーライドがありません。",
                this
            );

            return;
        }

        if (skyboxes == null || skyboxes.Length == 0)
        {
            Debug.LogError(
                "[RandomSkyboxSetter][エラー] " +
                "使用可能なSkyboxがありません。",
                this
            );

            return;
        }

        int randomIndex = Random.Range(0, skyboxes.Length);
        Cubemap selectedCubemap = skyboxes[randomIndex];

        hdriSky.hdriSky.value = selectedCubemap;

        // 環境光を更新
        DynamicGI.UpdateEnvironment();

        Debug.Log(
            $"[RandomSkyboxSetter] Skyboxを変更しました: {selectedCubemap.name}",
            this
        );
    }
}