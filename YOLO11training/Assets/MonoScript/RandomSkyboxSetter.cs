using System.Collections;
using UnityEngine;

public class RandomSkyboxSetter : MonoBehaviour, IProcessStep
{
    [Header("読み込み設定")]
    [Tooltip("Assets/ 以下のフォルダパスを指定（例: Skyboxes/FolderA）")]
    [SerializeField] private string folderPath = "Skyboxes";

    [Header("Skyboxシェーダー設定")]
    [Tooltip("画像1枚（全方位パノラマ）の場合は 'Skybox/Panoramic' を選択")]
    [SerializeField] private string shaderName = "Skybox/Panoramic";

    /// <summary>
    /// DatasetGeneratorManager から実行される処理ステップ
    /// </summary>
    public IEnumerator ExecuteStep()
    {
        // 1. Skyboxをランダムに設定
        SetRandomSkybox();

        // 2. 設定されたマテリアルやライティングの反映を保証するため1フレーム待機
        yield return null;
    }

    [ContextMenu("Randomize Skybox")]
    public void SetRandomSkybox()
    {
        // 1. Resourcesフォルダから指定フォルダ内のすべてのTexture2Dを取得
        Texture2D[] textures = Resources.LoadAll<Texture2D>(folderPath);

        if (textures == null || textures.Length == 0)
        {
            Debug.LogWarning($"[RandomSkyboxSetter] '{folderPath}' フォルダ内に画像が見つかりませんでした。Assets/Resources/{folderPath}/ に画像があるか確認してください。");
            return;
        }

        // 2. ランダムに1枚の画像を選択
        int randomIndex = Random.Range(0, textures.Length);
        Texture2D selectedTexture = textures[randomIndex];

        // 3. Skybox用マテリアルを動的に生成
        Shader skyboxShader = Shader.Find(shaderName);
        if (skyboxShader == null)
        {
            Debug.LogError($"[RandomSkyboxSetter] シェーダー '{shaderName}' が見つかりません。");
            return;
        }

        Material skyboxMaterial = new Material(skyboxShader);

        // シェーダーのプロパティ（_MainTex）に選択したテクスチャをセット
        if (skyboxMaterial.HasProperty("_MainTex"))
        {
            skyboxMaterial.SetTexture("_MainTex", selectedTexture);
        }

        // 4. ライティング/環境のSkyboxに適用
        RenderSettings.skybox = skyboxMaterial;

        // 5. 環境光（ライティング）を更新
        DynamicGI.UpdateEnvironment();

        Debug.Log($"Skyboxを設定しました: {selectedTexture.name} (フォルダ: {folderPath})");
    }
}