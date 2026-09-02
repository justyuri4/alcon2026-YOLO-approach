using System.Collections;
using System.IO;
using UnityEngine;

public class DatasetGenerator : MonoBehaviour
{
    [Header("学習データ保存設定")]
    public string saveFolder = "Dataset";
    public int numberOfImages = 3;
    public int imageWidth = 1024;
    public int imageHeight = 1024;
    public Camera captureCamera;

    [Header("背景のランダム設定")]
    [Tooltip("切り替えるSkyboxマテリアルを複数登録してください")]
    public Material[] backgroundMaterials;

    [Header("カメラ設定")]
    public bool useRandomCamera = true;

    [Header("カメラのランダム範囲設定 (useRandomCamera = true)")]
    public Vector2 cameraHeightRange = new Vector2(0.8f, 1.2f);
    public Vector2 cameraOffsetXRange = new Vector2(-0.5f, 0.5f);
    public Vector2 cameraOffsetZRange = new Vector2(-0.5f, 0.5f);
    public Vector2 cameraPitchRange = new Vector2(15f, 18f);
    public Vector2 cameraYawRange = new Vector2(-20f, 20f);

    [Header("高密度・田んぼの生成範囲")]
    public int fieldWidth = 45;
    public int fieldLength = 45;
    public float spacing = 0.2f;

    [Header("イネの3Dモデル（単体）")]
    public GameObject ricePrefab;

    [Header("1株（塊）あたりの高密度設定")]
    public int minRicePerHill = 5;
    public int maxRicePerHill = 10;
    public float hillRadius = 0.15f;
    public Vector3 baseScale = Vector3.one;
    public float scaleVariation = 0.02f;

    [Header("葉の広がり（地面を隠すための傾き）")]
    public float maxTiltAngle = 20f;

    private Transform fieldParent;

    // Unityの再生ボタン（Play）を押した時に自動実行されるように追加
    private void Start()
    {
        StartGeneration();
    }

    [ContextMenu("Generate Dataset")]
    public void StartGeneration()
    {
        StartCoroutine(GenerateDatasetRoutine());
    }

    private IEnumerator GenerateDatasetRoutine()
    {
        // 保存フォルダの作成
        string path = Path.Combine(Application.dataPath, saveFolder);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        // 田んぼの生成
        GenerateField();

        if (captureCamera == null)
        {
            captureCamera = Camera.main;
        }

        Vector3 originalCamPos = captureCamera.transform.position;

        // 連続撮影ループ
        for (int i = 0; i < numberOfImages; i++)
        {
            // 1. 背景（Skybox）のランダム変更
            if (backgroundMaterials != null && backgroundMaterials.Length > 0)
            {
                int randomIndex = Random.Range(0, backgroundMaterials.Length);
                RenderSettings.skybox = backgroundMaterials[randomIndex];
                DynamicGI.UpdateEnvironment();
            }

            // 2. カメラ位置・角度のランダム変更
            if (useRandomCamera)
            {
                float height = Random.Range(cameraHeightRange.x, cameraHeightRange.y);
                float offsetX = Random.Range(cameraOffsetXRange.x, cameraOffsetXRange.y);
                float offsetZ = Random.Range(cameraOffsetZRange.x, cameraOffsetZRange.y);
                captureCamera.transform.position = originalCamPos + new Vector3(offsetX, height, offsetZ);

                float pitch = Random.Range(cameraPitchRange.x, cameraPitchRange.y);
                float yaw = Random.Range(cameraYawRange.x, cameraYawRange.y);
                captureCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            yield return new WaitForEndOfFrame();

            // 3. キャプチャと保存
            CaptureAndSave(path, i);
        }

        Debug.Log($"完了: {numberOfImages}枚の画像を保存しました（保存先: {path}）");
    }

    private void GenerateField()
    {
        if (fieldParent != null)
        {
            DestroyImmediate(fieldParent.gameObject);
        }

        GameObject parentObj = new GameObject("GeneratedField");
        fieldParent = parentObj.transform;

        if (ricePrefab == null)
        {
            Debug.LogError("Rice Prefab が設定されていません！");
            return;
        }

        for (int x = 0; x < fieldWidth; x++)
        {
            for (int z = 0; z < fieldLength; z++)
            {
                Vector3 hillCenter = new Vector3(x * spacing, 0, z * spacing);
                int riceCount = Random.Range(minRicePerHill, maxRicePerHill + 1);

                for (int i = 0; i < riceCount; i++)
                {
                    Vector2 randomCircle = Random.insideUnitCircle * hillRadius;
                    Vector3 spawnPos = hillCenter + new Vector3(randomCircle.x, 0, randomCircle.y);

                    GameObject rice = Instantiate(ricePrefab, spawnPos, Quaternion.identity, fieldParent);

                    float randomYRot = Random.Range(0f, 360f);
                    float randomTiltX = Random.Range(-maxTiltAngle, maxTiltAngle);
                    float randomTiltZ = Random.Range(-maxTiltAngle, maxTiltAngle);
                    rice.transform.rotation = Quaternion.Euler(randomTiltX, randomYRot, randomTiltZ);

                    float scaleOffset = Random.Range(-scaleVariation, scaleVariation);
                    rice.transform.localScale = baseScale + new Vector3(scaleOffset, scaleOffset, scaleOffset);
                }
            }
        }
    }

    private void CaptureAndSave(string folderPath, int index)
    {
        RenderTexture rt = new RenderTexture(imageWidth, imageHeight, 24);
        captureCamera.targetTexture = rt;
        Texture2D screenShot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        captureCamera.Render();
        RenderTexture.active = rt;

        screenShot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenShot.Apply();

        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);

        byte[] bytes = screenShot.EncodeToPNG();
        string filename = Path.Combine(folderPath, $"rice_dataset_{index:D4}.png");
        File.WriteAllBytes(filename, bytes);
        DestroyImmediate(screenShot);
    }
}