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

    [Header("カメラ位置設定（親からの高さ）")]
    [Tooltip("親オブジェクトの位置からどれだけ上に配置するか（1.0 = 1ユニット上）")]
    public float cameraHeightOffset = 1.0f;

    [Header("カメラの角度指定範囲 (useRandomCamera = true の場合)")]
    [Tooltip("水平(0度)から上下の振り幅（例: -30度 〜 +30度）")]
    public Vector2 pitchRange = new Vector2(-30f, 30f);

    [Tooltip("左右の振り幅（例: 0度 〜 360度 全方位）")]
    public Vector2 yawRange = new Vector2(0f, 360f);

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

        Vector3 origin = baseTransform.position;

        captureCamera.transform.position =
            origin + Vector3.up * cameraHeightOffset;

        if (useRandomCamera)
        {
            float randomPitch = Random.Range(
                pitchRange.x,
                pitchRange.y
            );

            float randomYaw = Random.Range(
                yawRange.x,
                yawRange.y
            );

            captureCamera.transform.rotation =
                Quaternion.Euler(
                    randomPitch,
                    randomYaw,
                    0f
                );
        }
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
