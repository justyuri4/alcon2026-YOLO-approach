using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition; // HDRP用ネームスペース

public class RandomSkyboxSetter : MonoBehaviour, IProcessStep
{
    [Header("HDRP Volume 設定")]
    [Tooltip("HDRP の HDRI Sky オーバーライドが含まれている Global Volume を指定")]
    [SerializeField] private Volume globalVolume;

    [Header("読み込み設定")]
    [Tooltip("Assets/Resources/ 以下のフォルダパスを指定（例: Skyboxes）")]
    [SerializeField] private string folderPath = "Skyboxes";

    /// <summary>
    /// DatasetGeneratorManager から実行される処理ステップ
    /// </summary>
    public IEnumerator ExecuteStep()
    {
        UnityEngine.Debug.Log($"[RandomSkyboxSetter] ExecuteStep を開始します。 (対象フォルダ: Assets/Resources/{folderPath})", this);

        SetRandomSkybox();

        // 1フレーム待機してライティング反映を確実にする
        yield return null;

        UnityEngine.Debug.Log("[RandomSkyboxSetter] ExecuteStep が正常に完了しました。", this);
    }

    [ContextMenu("Randomize Skybox")]
    public void SetRandomSkybox()
    {
        // 1. Volume の参照チェック
        if (globalVolume == null)
        {
            UnityEngine.Debug.LogError("[RandomSkyboxSetter][エラー] Global Volume が Inspector で指定されていません。", this);
            return;
        }

        if (globalVolume.profile == null)
        {
            UnityEngine.Debug.LogError("[RandomSkyboxSetter][エラー] 指定された Volume に VolumeProfile が設定されていません。", this);
            return;
        }

        // 2. VolumeProfile から HDRISky コンポーネント（オーバーライド）を取得
        if (!globalVolume.profile.TryGet<HDRISky>(out var hdriSky))
        {
            UnityEngine.Debug.LogError("[RandomSkyboxSetter][エラー] Volume Profile 内に 'HDRI Sky' オーバーライドが見つかりません。Add Override > Sky > HDRI Sky を追加してください。", this);
            return;
        }

        // 3. Resources フォルダから Cubemap (.exr 含む) をすべてロード
        Cubemap[] cubemaps = Resources.LoadAll<Cubemap>(folderPath);

        if (cubemaps == null || cubemaps.Length == 0)
        {
            UnityEngine.Debug.LogError($"[RandomSkyboxSetter][エラー:画像読み込み失敗] 'Assets/Resources/{folderPath}' 内に Cubemap (EXR等) が見つかりませんでした。Import Settings で Texture Shape が 'Cube' になっているか確認してください。", this);
            return;
        }

        // 4. ランダムに 1 つ選択して HDRI Sky に設定
        int randomIndex = UnityEngine.Random.Range(0, cubemaps.Length);
        Cubemap selectedCubemap = cubemaps[randomIndex];

        hdriSky.hdriSky.value = selectedCubemap;

        // 5. 環境光（GI）を更新
        DynamicGI.UpdateEnvironment();

        UnityEngine.Debug.Log($"[RandomSkyboxSetter][成功] HDRP HDRI Sky を更新しました: '{selectedCubemap.name}'", this);
    }
}