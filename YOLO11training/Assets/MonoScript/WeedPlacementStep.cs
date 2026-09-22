using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 名前空間の競合（CS0104）を回避するためのエイリアス指定
using Debug = UnityEngine.Debug;
using Application = UnityEngine.Application;
using Random = UnityEngine.Random;

public class WeedPlacementStep : MonoBehaviour, IProcessStep
{
    [Header("Weed Prefabs")]
    public GameObject[] weedPrefabs;

    [Header("Placement Area")]
    [Tooltip("配置対象の領域オブジェクト（MeshRendererが付いている土壌など）。未設定の場合は自身または親を参照します。")]
    public Transform placementArea;
    public bool useGeneratedPaddyField = true;

    [Header("Random Placement Settings")]
    [Tooltip("ランダムに選択する植え付け数の最小値")]
    public int minSpawnCount = 0;

    [Tooltip("ランダムに選択する植え付け数の最大値")]
    public int maxSpawnCount = 1000;

    [Header("Ground Placement Settings")]
    public bool placeOnGroundSurface = true;

    [Header("Size & Height Offset（メートル単位：1.0 = 1m）")]
    [Tooltip("生成後のオブジェクトの高さ範囲（メートル）")]
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

    [Tooltip("接地面からの高さオフセット（メートル）")]
    public float surfaceOffset = 0f;

    [Header("Execution")]
    public bool clearBeforePlacement = true;
    public bool useFixedSeed = false;
    public int seed = 0;

    private Transform generatedRoot;

    public IEnumerator ExecuteStep()
    {
        Debug.Log("[WeedPlacementStep] Starting ExecuteStep...");
        PlaceWeeds();
        yield return null;
    }

    [ContextMenu("Place Weeds")]
    public void PlaceWeeds()
    {
        Debug.Log("[WeedPlacementStep] Starting PlaceWeeds process.");
        ClearPlacedWeeds();

        if (weedPrefabs == null || weedPrefabs.Length == 0)
        {
            Debug.LogError("[WeedPlacementStep] Failed to place weeds: 'weedPrefabs' array is null or empty.");
            return;
        }

        int minCount = Mathf.Clamp(minSpawnCount, 0, 1000);
        int maxCount = Mathf.Clamp(maxSpawnCount, 0, 1000);

        if (minCount > maxCount)
        {
            int temporary = minCount;
            minCount = maxCount;
            maxCount = temporary;
        }

        // Random.Range(int, int) の最大値は含まれないため、+1する
        int targetSpawnCount = Random.Range(minCount, maxCount + 1);

        if (targetSpawnCount <= 0)
        {
            Debug.LogWarning("[WeedPlacementStep] 'spawnCount' is less than or equal to 0.");
            return;
        }

        if (useFixedSeed)
        {
            Random.InitState(seed);
        }

        Transform areaTransform = ResolvePlacementArea();
        MeshRenderer areaRenderer = areaTransform.GetComponentInChildren<MeshRenderer>();

        Bounds areaBounds;
        if (areaRenderer != null)
        {
            areaBounds = areaRenderer.bounds;
        }
        else
        {
            areaBounds = new Bounds(areaTransform.position, new Vector3(10f, 10f, 10f));
        }

        generatedRoot = new GameObject("GeneratedWeeds").transform;
        generatedRoot.SetParent(transform, false);

        int successfullyPlaced = 0;
        int outOfBoundsCount = 0;

        float mudSurfaceY = areaBounds.max.y;

        for (int i = 0; i < targetSpawnCount; i++)
        {
            GameObject weedPrefab = weedPrefabs[Random.Range(0, weedPrefabs.Length)];
            if (weedPrefab == null) continue;

            // X, Z 領域内で完全ランダムな座標を生成
            float randomX = Random.Range(areaBounds.min.x, areaBounds.max.x);
            float randomZ = Random.Range(areaBounds.min.z, areaBounds.max.z);
            Vector3 candidatePos = new Vector3(randomX, mudSurfaceY, randomZ);

            Vector3 groundPosition = candidatePos;

            if (placeOnGroundSurface)
            {
                // placementArea（またはその配下）のオブジェクトと衝突したか判定
                bool groundHit = TryGetGroundPositionOnArea(
    candidatePos,
    areaTransform,
    areaBounds,
    out groundPosition);
                if (!groundHit)
                {
                    outOfBoundsCount++;
                    continue;
                }
            }

            GameObject weedInstance = Instantiate(weedPrefab, groundPosition, Quaternion.identity, generatedRoot);

            float yaw = Random.Range(0f, 360f);
            weedInstance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 親オブジェクトのスケール影響を打ち消して絶対的なワールドスケールを設定
            float scale = Random.Range(scaleRange.x, scaleRange.y);
            Vector3 desiredWorldScale = Vector3.one * scale;
            Vector3 parentScale = generatedRoot.lossyScale;

            weedInstance.transform.localScale = new Vector3(
                desiredWorldScale.x / (parentScale.x != 0 ? parentScale.x : 1f),
                desiredWorldScale.y / (parentScale.y != 0 ? parentScale.y : 1f),
                desiredWorldScale.z / (parentScale.z != 0 ? parentScale.z : 1f)
            );

            // 元モデルの高さに関係なく、指定した高さになるようにスケール
            float targetHeight = Random.Range(scaleRange.x, scaleRange.y);

            if (TryGetObjectBounds(weedInstance, out Bounds originalBounds) &&
                originalBounds.size.y > Mathf.Epsilon)
            {
                float heightScale = targetHeight / originalBounds.size.y;

                // 現在のモデル倍率を維持したまま、均一倍率を適用
                weedInstance.transform.localScale *= heightScale;
            }

            if (placeOnGroundSurface)
            {
                AlignBottomToGround(weedInstance, groundPosition);
            }

            successfullyPlaced++;
        }

        Debug.Log(
    $"[WeedPlacementStep] Completed: " +
    $"{successfullyPlaced}/{targetSpawnCount} weeds placed. " +
    $"(Out of bounds/hits skipped: {outOfBoundsCount})");
    }

    private void AlignBottomToGround(GameObject instance, Vector3 targetGroundPosition)
    {
        Bounds combinedBounds = default;
        bool hasBounds = false;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (!hasBounds)
            {
                combinedBounds = rend.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(rend.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] colliders = instance.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                if (!hasBounds)
                {
                    combinedBounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(col.bounds);
                }
            }
        }

        if (hasBounds)
        {
            float bottomY = combinedBounds.min.y;
            float currentPivotY = instance.transform.position.y;
            float offsetFromPivotToBottom = currentPivotY - bottomY;
            instance.transform.position = targetGroundPosition + Vector3.up * (offsetFromPivotToBottom + surfaceOffset);
        }
        else
        {
            instance.transform.position = targetGroundPosition + Vector3.up * surfaceOffset;
        }
    }

    private Transform ResolvePlacementArea()
    {
        if (placementArea != null)
        {
            return placementArea;
        }

        if (useGeneratedPaddyField)
        {
            Transform generatedField = transform.Find("GeneratedPaddyField");
            if (generatedField != null)
            {
                return generatedField;
            }
        }

        return transform;
    }

    [ContextMenu("Clear Weeds")]
    public void ClearWeeds()
    {
        ClearPlacedWeeds(true);
    }

    /// <summary>
    /// レイキャストのヒット対象が placementArea 内（またはその子）である場合のみ位置を取得する
    /// </summary>
    private bool TryGetGroundPositionOnArea(
    Vector3 candidatePosition,
    Transform areaTransform,
    Bounds areaBounds,
    out Vector3 groundPosition)
    {
        // 配置領域の大きさから余白を自動計算
        float raycastPadding = Mathf.Max(1f, areaBounds.size.y * 0.05f);

        Vector3 rayOrigin = new Vector3(
            candidatePosition.x,
            areaBounds.max.y + raycastPadding,
            candidatePosition.z);

        float raycastDistance =
            areaBounds.size.y + raycastPadding * 2f;

        // RaycastAll で直線上のヒット対象をすべて取得
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            raycastDistance);

        // placementArea 自身または配下の Transform に最も近いヒットを採用
        float closestDistance = float.MaxValue;
        bool foundValidHit = false;
        Vector3 bestHitPoint = candidatePosition;

        foreach (RaycastHit hit in hits)
        {
            bool isChildOrSelf =
                hit.transform == areaTransform ||
                hit.transform.IsChildOf(areaTransform);

            if (isChildOrSelf && hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHitPoint = hit.point;
                foundValidHit = true;
            }
        }

        groundPosition = bestHitPoint;
        return foundValidHit;
    }

    private void ClearPlacedWeeds(bool force = false)
    {
        if (generatedRoot == null)
        {
            generatedRoot = transform.Find("GeneratedWeeds");
        }

        if (generatedRoot == null || (!force && !clearBeforePlacement))
        {
            return;
        }

        Debug.Log($"[WeedPlacementStep] Clearing existing weeds under '{generatedRoot.name}'.");

        if (Application.isPlaying)
        {
            Destroy(generatedRoot.gameObject);
        }
        else
        {
            DestroyImmediate(generatedRoot.gameObject);
        }

        generatedRoot = null;
    }

    private bool TryGetObjectBounds(GameObject instance, out Bounds bounds)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.size.y > Mathf.Epsilon;
        }

        Collider[] colliders = instance.GetComponentsInChildren<Collider>();

        if (colliders.Length > 0)
        {
            bounds = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds.size.y > Mathf.Epsilon;
        }

        bounds = default;
        return false;
    }
}