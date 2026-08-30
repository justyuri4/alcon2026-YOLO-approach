using System.Collections;
using System.IO;
using UnityEngine;

public class DatasetGenerator : MonoBehaviour
{
    [Header("学習データ保存設定")]
    public string saveFolder = "Dataset";
    public int numberOfImages = 100;
    public int imageWidth = 1024;
    public int imageHeight = 1024;
    public Camera captureCamera;

    [Header("カメラ設定")]
    [Tooltip("チェックを外すと、インスペクター(シーン)で配置したカメラの座標・角度をそのまま使います")]
    public bool useRandomCamera = true;
    
    [Header("カメラのランダム範囲設定 (useRandomCamera = true の場合)")]
    public Vector2 cameraHeightRange = new Vector2(0.7f, 1.3f); // Y軸の高さ
    public Vector2 cameraOffsetXRange = new Vector2(-0.5f, 0.5f); // X軸のズレ
    public Vector2 cameraOffsetZRange = new Vector2(-0.5f, 0.5f); // Z軸のズレ
    public Vector2 cameraPitchRange = new Vector2(8f, 20f);       // X軸回転 (見下ろす角度)
    public Vector2 cameraYawRange = new Vector2(-20f, 20f);       // Y軸回転 (左右の首振り)

    [Header("高密度・田んぼの生成範囲")]
    public int fieldWidth = 35;           // 横方向の株数
    public int fieldLength = 35;          // 奥行き方向の株数
    public float spacing = 0.08f;         // 株間の距離

    [Header("イネの3Dモデル（単体）")]
    public GameObject ricePrefab;

    [Header("1株（塊）あたりの高密度設定")]
    public int minRicePerHill = 10;       // 1株あたりの最低本数
    public int maxRicePerHill = 18;       // 1株あたりの最高本数
    public float hillRadius = 0.05f;      // 株の広がり半径
    public Vector3 baseScale = new Vector3(0.1f, 0.1f, 0.1f); 
    public float scaleVariation = 0.02f; 

    [Header("葉の広がり（地面を隠すための傾き）")]
    public float maxTiltAngle = 20f;      // 外側・放射状に倒して隙間を埋める角度

    void Start()
    {
        if (captureCamera == null) captureCamera = Camera.main;
        StartCoroutine(GenerateAndCapture());
    }

    IEnumerator GenerateAndCapture()
    {
        string folderPath = Path.Combine(Application.dataPath, saveFolder);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        for (int i = 0; i < numberOfImages; i++)
        {
            ClearField();
            GenerateField();
            RandomizeCamera();

            yield return new WaitForEndOfFrame();

            string filename = Path.Combine(folderPath, $"rice_dataset_{i:D4}.png");
            CaptureScreenshot(filename);

            Debug.Log($"[{i+1}/{numberOfImages}] 保存完了: {filename}");
        }

        Debug.Log("すべての学習データの生成が完了しました！");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void GenerateField()
    {
        for (int x = 0; x < fieldWidth; x++)
        {
            for (int z = 0; z < fieldLength; z++)
            {
                // 株の位置を少しランダムにずらす
                float offsetX = Random.Range(-spacing * 0.25f, spacing * 0.25f);
                float offsetZ = Random.Range(-spacing * 0.25f, spacing * 0.25f);
                
                Vector3 hillPos = transform.position + new Vector3(x * spacing + offsetX, 0, z * spacing + offsetZ);
                GenerateSingleHill(hillPos);
            }
        }
    }

    void GenerateSingleHill(Vector3 centerPosition)
    {
        int riceCount = Random.Range(minRicePerHill, maxRicePerHill + 1);
        
        for (int i = 0; i < riceCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * hillRadius;
            Vector3 finalRicePos = centerPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

            GameObject riceInstance = Instantiate(ricePrefab, finalRicePos, Quaternion.identity);
            
            // 向き（Y軸）をランダムに
            float randomYaw = Random.Range(0f, 360f);

            // 株の中心から外側に向けて葉を傾かせる
            Vector3 directionFromCenter = (finalRicePos - centerPosition).normalized;
            float tiltAmount = Random.Range(5f, maxTiltAngle);
            
            Quaternion yawRotation = Quaternion.Euler(0f, randomYaw, 0f);
            Quaternion tiltRotation = Quaternion.Euler(tiltAmount, 0f, 0f);

            // 回転を合成
            riceInstance.transform.rotation = yawRotation * tiltRotation;

            // スケールランダム
            float randomScaleY = Random.Range(-scaleVariation, scaleVariation);
            riceInstance.transform.localScale = new Vector3(
                baseScale.x + (randomScaleY * 0.5f),
                baseScale.y + randomScaleY,
                baseScale.z + (randomScaleY * 0.5f)
            );

            riceInstance.transform.parent = this.transform;
        }
    }

    void ClearField()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    void RandomizeCamera()
    {
        // ランダム化オフの場合は、インスペクター(シーン)のカメラ状態をそのまま保持して処理を抜ける
        if (!useRandomCamera) return;

        // 畑の中央付近を見下ろす視点
        float centerX = (fieldWidth * spacing) * 0.5f;
        float centerZ = (fieldLength * spacing) * 0.3f;

        // インスペクターで設定した範囲を使ってランダム化
        float randomX = centerX + Random.Range(cameraOffsetXRange.x, cameraOffsetXRange.y);
        float randomZ = centerZ + Random.Range(cameraOffsetZRange.x, cameraOffsetZRange.y);
        float randomHeight = Random.Range(cameraHeightRange.x, cameraHeightRange.y);

        captureCamera.transform.position = transform.position + new Vector3(randomX, randomHeight, randomZ);

        float pitch = Random.Range(cameraPitchRange.x, cameraPitchRange.y); 
        float yaw = Random.Range(cameraYawRange.x, cameraYawRange.y); 
        
        captureCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void CaptureScreenshot(string savePath)
    {
        RenderTexture rt = new RenderTexture(imageWidth, imageHeight, 24);
        captureCamera.targetTexture = rt;
        
        Texture2D screenShot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        captureCamera.Render();
        
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(savePath, bytes);
        Destroy(screenShot);
    }
}