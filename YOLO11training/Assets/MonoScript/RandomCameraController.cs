using System.Collections;
using System.IO;
using UnityEngine;

public class RandomCameraController : MonoBehaviour, IProcessStep, IResettableStep
{
    [Header("撮影カメラ")]
    public Camera captureCamera;

    [Header("画像保存設定")]
    public string saveFolder = "Dataset";
    public string fileNamePrefix = "rice_dataset_";
    public int imageWidth = 1024;
    public int imageHeight = 1024;

    [Header("カメラランダム化設定")]
    public bool useRandomCamera = true;

    [Header("カメラ高さ設定（ワールド座標系）")]
    [Tooltip("親オブジェクトの端からワールド単位で何ユニット上にカメラを置くか")]
    public float cameraHeightOffset = 1.0f;

    [Header("カメラ注視点高さ設定（ワールド座標系）")]
    [Tooltip("親オブジェクトの中心からワールド単位で指定する注視点の高さ最小値")]
    public float targetMinHeight = 0.5f;

    [Tooltip("親オブジェクトの中心からワールド単位で指定する注視点の高さ最大値")]
    public float targetMaxHeight = 1.2f;

    private int currentIndex = 0;

    public void ResetIndex()
    {
        currentIndex = 0;
        Debug.Log("[RandomCameraController] インデックスをリセットしました。");
    }

    public IEnumerator ExecuteStep()
    {
        if (captureCamera == null)
        {
            Debug.LogError(
                "[RandomCameraController] captureCamera が設定されていません！",
                this
            );
            yield break;
        }

        Debug.Log("[RandomCameraController] カメラ設定・撮影開始");

        // カメラ設定
        captureCamera.clearFlags = CameraClearFlags.Skybox;
        RandomizeCamera();

        // 変更を反映
        yield return null;

        // 保存先
        string folderPath = Path.Combine(
            Application.dataPath,
            saveFolder
        );

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filename = Path.Combine(
            folderPath,
            $"{fileNamePrefix}{currentIndex:D4}.png"
        );

        // 撮影
        CaptureScreenshot(filename);

        if (File.Exists(filename))
        {
            Debug.Log($"[{currentIndex + 1}] 保存成功: {filename}");
        }
        else
        {
            Debug.LogError(
                $"[RandomCameraController] 保存失敗: {filename}"
            );
        }

        currentIndex++;

        yield return null;
    }

    private void RandomizeCamera()
    {
        Transform baseTransform =
            transform.parent != null
                ? transform.parent
                : transform;

        // 1. 親オブジェクトのワールド空間におけるバウンディングボックスを取得
        // Renderer / Collider の bounds はスケールが適用されたワールド座標系のサイズを返すため、
        // 親の Scale の影響を受けずに正確な絶対ユニットを扱えます。
        Bounds bounds;
        if (baseTransform.TryGetComponent<Renderer>(out var renderer))
        {
            bounds = renderer.bounds;
        }
        else if (baseTransform.TryGetComponent<Collider>(out var col))
        {
            bounds = col.bounds;
        }
        else
        {
            // Renderer も Collider もない場合は Transform のワールドスケールから推定
            Vector3 worldScale = baseTransform.lossyScale;
            bounds = new Bounds(baseTransform.position, worldScale);
        }

        if (useRandomCamera)
        {
            // 2. XZ平面の「端（周縁）」上の点をワールド座標でランダム選出
            Vector3 edgePositionOnXZ = GetRandomEdgePositionOnXZ(bounds);

            // 3. カメラの位置を設定 (選んだ端の座標のY軸方向に +cameraHeightOffset ユニット上)
            Vector3 cameraPosition = new Vector3(
                edgePositionOnXZ.x,
                bounds.center.y + cameraHeightOffset,
                edgePositionOnXZ.z
            );
            captureCamera.transform.position = cameraPosition;

            // 4. 注視点（Target）をワールド単位で設定 (親の中心からY軸方向に targetMinHeight ~ targetMaxHeight ユニット上)
            float randomTargetHeight = Random.Range(targetMinHeight, targetMaxHeight);
            Vector3 targetPosition = bounds.center + Vector3.up * randomTargetHeight;

            // 5. カメラを注視点に向けさせる
            captureCamera.transform.LookAt(targetPosition);
        }
        else
        {
            // ランダム化オフの場合の標準位置（親の中心上空）
            captureCamera.transform.position =
                bounds.center + Vector3.up * cameraHeightOffset;
            captureCamera.transform.LookAt(bounds.center);
        }
    }

    /// <summary>
    /// Bounds（ワールド空間）のXZ平面の4つの端（外周の辺）からランダムな1点を取得
    /// </summary>
    private Vector3 GetRandomEdgePositionOnXZ(Bounds bounds)
    {
        int edgeIndex = Random.Range(0, 4);
        float x = bounds.center.x;
        float z = bounds.center.z;

        switch (edgeIndex)
        {
            case 0: // +X 辺
                x = bounds.max.x;
                z = Random.Range(bounds.min.z, bounds.max.z);
                break;
            case 1: // -X 辺
                x = bounds.min.x;
                z = Random.Range(bounds.min.z, bounds.max.z);
                break;
            case 2: // +Z 辺
                x = Random.Range(bounds.min.x, bounds.max.x);
                z = bounds.max.z;
                break;
            case 3: // -Z 辺
                x = Random.Range(bounds.min.x, bounds.max.x);
                z = bounds.min.z;
                break;
        }

        return new Vector3(x, bounds.center.y, z);
    }

    private void CaptureScreenshot(string savePath)
    {
        RenderTexture rt = null;
        Texture2D screenShot = null;

        RenderTexture previousRT =
            captureCamera.targetTexture;

        RenderTexture previousActive =
            RenderTexture.active;

        try
        {
            // RenderTexture作成
            rt = new RenderTexture(
                imageWidth,
                imageHeight,
                24,
                RenderTextureFormat.Default
            );

            rt.Create();

            captureCamera.targetTexture = rt;
            captureCamera.clearFlags = CameraClearFlags.Skybox;

            // 撮影
            screenShot = new Texture2D(
                imageWidth,
                imageHeight,
                TextureFormat.RGB24,
                false
            );

            captureCamera.Render();

            RenderTexture.active = rt;

            screenShot.ReadPixels(
                new Rect(
                    0,
                    0,
                    imageWidth,
                    imageHeight
                ),
                0,
                0
            );

            screenShot.Apply();

            // 保存
            byte[] bytes = screenShot.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);

            Debug.Log(
                $"[RandomCameraController] 保存しました: {savePath}"
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[RandomCameraController] 撮影エラー: {ex}"
            );
        }
        finally
        {
            captureCamera.targetTexture = previousRT;
            RenderTexture.active = previousActive;

            if (rt != null)
            {
                if (Application.isPlaying)
                    Destroy(rt);
                else
                    DestroyImmediate(rt);
            }

            if (screenShot != null)
            {
                if (Application.isPlaying)
                    Destroy(screenShot);
                else
                    DestroyImmediate(screenShot);
            }
        }
    }
}