using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 名前空間の衝突（CS0104）を回避するためのエイリアス指定
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using Application = UnityEngine.Application;

public class PaddyFieldGenerator : MonoBehaviour, IProcessStep
{
    [Header("イネの3Dモデル（単体 Prefab）")]
    public GameObject ricePrefab;

    [Header("親オブジェクト（指定しない場合は自身の親を取得）")]
    public Transform paddySoilTransform;

    [Header("植え付け間隔設定（メートル単位：1.0 = 1m）")]
    public float rowSpacing = 0.3f;      // 条間（例: 30cm）
    public float plantSpacing = 0.15f;   // 株間（例: 15cm）

    [Header("1株（塊）あたりの設定")]
    public int minRicePerHill = 3;
    public int maxRicePerHill = 5;
    public float hillRadius = 0.03f;     // 株内の散らばり半径（メートル単位）

    [Header("イネの高さ範囲設定（メートル単位：1.0 = 1m）")]
    public float minPlantHeight = 0.75f; // 75cm
    public float maxPlantHeight = 0.90f; // 90cm

    [Header("めり込み調整（メートル単位）")]
    public float sinkDepth = 0.01f;      // 1cmめり込ませる

    private Transform generatedRoot;

    public IEnumerator ExecuteStep()
    {
        GenerateField();
        yield return null;
    }

    public void GenerateField()
    {
        ClearField();

        if (ricePrefab == null)
        {
            Debug.LogError("[PaddyFieldGenerator] ricePrefab が設定されていません！", this);
            return;
        }
        if (rowSpacing <= 0.001f || plantSpacing <= 0.001f)
        {
            Debug.LogError("[PaddyFieldGenerator] rowSpacing または plantSpacing が小さすぎます！", this);
            return;
        }

        if (paddySoilTransform == null && transform.parent != null)
        {
            paddySoilTransform = transform.parent;
        }
        if (paddySoilTransform == null) paddySoilTransform = transform;

        MeshRenderer soilRenderer = paddySoilTransform.GetComponentInChildren<MeshRenderer>();
        MeshFilter prefabMeshFilter = ricePrefab.GetComponentInChildren<MeshFilter>();

        if (soilRenderer == null)
        {
            Debug.LogError($"[PaddyFieldGenerator] {paddySoilTransform.name} またはその子に MeshRenderer が見つかりません！", this);
            return;
        }
        if (prefabMeshFilter == null || prefabMeshFilter.sharedMesh == null)
        {
            Debug.LogError("[PaddyFieldGenerator] ricePrefab に MeshFilter または Mesh が設定されていません！", this);
            return;
        }

        Mesh sourceMesh = prefabMeshFilter.sharedMesh;
        float localMeshHeight = sourceMesh.bounds.size.y;
        float localMinY = sourceMesh.bounds.min.y;

        if (localMeshHeight <= 0.0001f)
        {
            Debug.LogError("[PaddyFieldGenerator] ricePrefab のメッシュ高さ(bounds.size.y)が 0 です！", this);
            return;
        }

        Bounds worldSoilBounds = soilRenderer.bounds;
        float mudSurfaceY = worldSoilBounds.max.y;

        // 生成されたイネをまとめる親オブジェクトを作成
        GameObject rootObj = new GameObject("GeneratedPaddyField");
        generatedRoot = rootObj.transform;
        generatedRoot.SetParent(paddySoilTransform, false);

        int totalCount = 0;

        for (float x = worldSoilBounds.min.x + (rowSpacing / 2f); x < worldSoilBounds.max.x; x += rowSpacing)
        {
            for (float z = worldSoilBounds.min.z + (plantSpacing / 2f); z < worldSoilBounds.max.z; z += plantSpacing)
            {
                int riceCount = Random.Range(minRicePerHill, maxRicePerHill + 1);

                for (int i = 0; i < riceCount; i++)
                {
                    float targetPlantHeight = Random.Range(minPlantHeight, maxPlantHeight);
                    float scaleRatio = targetPlantHeight / localMeshHeight;
                    Vector3 desiredWorldScale = Vector3.one * scaleRatio;
                    float scaledMinY = localMinY * scaleRatio;

                    Vector2 randomCircle = Random.insideUnitCircle * hillRadius;
                    Vector3 worldPos = new Vector3(
                        x + randomCircle.x,
                        mudSurfaceY - sinkDepth - scaledMinY,
                        z + randomCircle.y
                    );

                    Quaternion worldRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                    // Prefabを複製（Instantiate）配置
                    GameObject riceInstance = Instantiate(ricePrefab, worldPos, worldRot, generatedRoot);

                    // 親オブジェクトのスケール影響を打ち消して絶対的なワールドスケールを設定
                    Vector3 parentScale = generatedRoot.lossyScale;
                    riceInstance.transform.localScale = new Vector3(
                        desiredWorldScale.x / (parentScale.x != 0 ? parentScale.x : 1f),
                        desiredWorldScale.y / (parentScale.y != 0 ? parentScale.y : 1f),
                        desiredWorldScale.z / (parentScale.z != 0 ? parentScale.z : 1f)
                    );

                    totalCount++;
                }
            }
        }

        Debug.Log($"[PaddyFieldGenerator] イネの生成が完了しました。（合計本数: {totalCount}）");
    }

    public void ClearField()
    {
        if (generatedRoot == null)
        {
            generatedRoot = paddySoilTransform != null ? paddySoilTransform.Find("GeneratedPaddyField") : transform.Find("GeneratedPaddyField");
        }

        if (generatedRoot != null)
        {
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

        System.GC.Collect();
        Resources.UnloadUnusedAssets();
    }

    private void OnDestroy()
    {
        ClearField();
    }
}