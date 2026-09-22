using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Perception.GroundTruth;
using Random = UnityEngine.Random;

public class RandomCameraController : MonoBehaviour, IProcessStep
{
    [Header("撮影カメラ・Perception設定")]
    [Tooltip("撮影対象のPerceptionCamera（未設定の場合は captureCamera から自動取得します）")]
    public PerceptionCamera perceptionCamera;

    [Tooltip("PerceptionCameraがアタッチされているCameraオブジェクト")]
    public Camera captureCamera;

    [Header("カメラランダム化設定")]
    public bool useRandomCamera = true;

    [Header("カメラ高さ設定（ワールド座標系）")]
    [Tooltip("親オブジェクトの端からワールド単位で何ユニット上にカメラを置くか")]
    public float cameraHeightOffset = 1.0f;


    [Header("注視角度設定")]
    [Tooltip("親オブジェクトの中心に対する上下方向の注視角度最小値（度）。0度は水平")]
    public float targetMinAngle = -10.0f;

    [Tooltip("親オブジェクトの中心に対する上下方向の注視角度最大値（度）。0度は水平")]
    public float targetMaxAngle = 10.0f;

    [Header("モーションブラー設定")]
    [Tooltip("撮影時にモーションブラーを無効化するGlobal Volume")]
    [SerializeField] private Volume captureVolume;

    [SerializeField] private bool disableMotionBlurForCapture = true;

    private MotionBlur motionBlur;

    private void Awake()
    {
        // PerceptionCameraの自動参照設定
        if (perceptionCamera == null && captureCamera != null)
        {
            perceptionCamera = captureCamera.GetComponent<PerceptionCamera>();
        }

        if (!disableMotionBlurForCapture || captureVolume == null)
        {
            return;
        }

        if (captureVolume.profile == null)
        {
            Debug.LogWarning(
                "[RandomCameraController] captureVolumeにVolume Profileが設定されていません。",
                this
            );

            return;
        }

        if (captureVolume.profile.TryGet<MotionBlur>(out MotionBlur foundMotionBlur))
        {
            motionBlur = foundMotionBlur;
            motionBlur.active = false;

            Debug.Log(
                "[RandomCameraController] 撮影用モーションブラーを無効化しました。",
                this
            );
        }
        else
        {
            Debug.LogWarning(
                "[RandomCameraController] Volume Profile内にMotion Blurが見つかりません。",
                this
            );
        }
    }

    /// <summary>
    /// カメラの位置をランダム化し、PerceptionCameraにキャプチャ（撮影）をリクエストします。
    /// </summary>
    public IEnumerator ExecuteStep()
    {
        if (captureCamera == null)
        {
            Debug.LogError("[RandomCameraController] captureCamera が設定されていません！", this);
            yield break;
        }

        if (perceptionCamera == null)
        {
            Debug.LogError("[RandomCameraController] PerceptionCamera が設定されていません！", this);
            yield break;
        }

        // カメラ位置・向きのランダム化
        captureCamera.clearFlags = CameraClearFlags.Skybox;
        RandomizeCamera();

        // Transformsの更新を反映させるために1フレーム待機
        yield return null;

        // PerceptionCamera にリクエストを送り、撮影とGround Truthアノテーション生成を委託
        perceptionCamera.RequestCapture();

        Debug.Log("[RandomCameraController] PerceptionCamera に撮影をリクエストしました。");
    }

    private void RandomizeCamera()
    {
        Transform baseTransform = transform.parent != null ? transform.parent : transform;

        // 1. 親オブジェクトのワールド空間におけるバウンディングボックスを取得
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
            Vector3 worldScale = baseTransform.lossyScale;
            bounds = new Bounds(baseTransform.position, worldScale);
        }

        if (useRandomCamera)
        {
            // 2. XZ平面の「端（周縁）」上の点をワールド座標でランダム選出
            Vector3 edgePositionOnXZ = GetRandomEdgePositionOnXZ(bounds);

            // 3. カメラの位置を設定
            Vector3 cameraPosition = new Vector3(
                edgePositionOnXZ.x,
                bounds.center.y + cameraHeightOffset,
                edgePositionOnXZ.z
            );
            captureCamera.transform.position = cameraPosition;

            // 4. 注視角度をランダムに設定
            float randomTargetAngle = Random.Range(targetMinAngle, targetMaxAngle);

            // カメラとオブジェクト中心との水平距離
            float horizontalDistance = Vector2.Distance(
                new Vector2(cameraPosition.x, cameraPosition.z),
                new Vector2(bounds.center.x, bounds.center.z)
            );

            // 注視角度から注視点のY座標だけを計算
            float targetHeightOffset =
                horizontalDistance * Mathf.Tan(randomTargetAngle * Mathf.Deg2Rad);

            // X軸・Z軸は常にオブジェクトの中心位置を使用し、
            // Y軸だけ注視角度に応じて変更
            Vector3 targetPosition = new Vector3(
                bounds.center.x,
                bounds.center.y + targetHeightOffset,
                bounds.center.z
            );

            // 5. カメラを注視点に向けさせる
            captureCamera.transform.LookAt(targetPosition);
        }
        else
        {
            // ランダム化オフの場合の標準位置
            captureCamera.transform.position = bounds.center + Vector3.up * cameraHeightOffset;
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
}