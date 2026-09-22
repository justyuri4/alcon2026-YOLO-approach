using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 名前空間の衝突（CS0104）を回避するためのエイリアス指定
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using Application = UnityEngine.Application;

[Serializable]
public class RicePrefabSetting
{
    [Tooltip("生成に使用するイネのPrefab")]
    public GameObject prefab;

    [Tooltip("このPrefabの生成時の高さ（メートル単位）")]
    public float height = 0.8f;
}

public class PaddyFieldGenerator : MonoBehaviour, IProcessStep
{
    [Header("イネのPrefab設定")]
    [Tooltip("リストからランダムに1つ選択して生成します")]
    public List<RicePrefabSetting> ricePrefabs = new List<RicePrefabSetting>();

    [Header("親オブジェクト（指定しない場合は自身の親を取得）")]
    public Transform paddySoilTransform;

    [Header("植え付け間隔設定（メートル単位：1.0 = 1m）")]
    public float rowSpacing = 0.3f;      // 条間（例: 30cm）
    public float plantSpacing = 0.15f;   // 株間（例: 15cm）

    [Header("1株（塊）あたりの設定")]
    public int minRicePerHill = 3;
    public int maxRicePerHill = 5;
    public float hillRadius = 0.03f;     // 株内の散らばり半径（メートル単位）

    [Header("めり込み調整（メートル単位）")]
    public float sinkDepth = 0.01f;      // 1cmめり込ませる

    private Transform generatedRoot;

    private class ValidPrefabData
    {
        public RicePrefabSetting setting;
        public float localMeshHeight;
        public float localMinY;
    }

    private readonly List<ValidPrefabData> cachedValidPrefabs =
        new List<ValidPrefabData>();

    private bool prefabCacheInitialized;

    public IEnumerator ExecuteStep()
    {
        GenerateField();
        yield return null;
    }

    public void GenerateField()
    {
        ClearField();

        if (ricePrefabs == null || ricePrefabs.Count == 0)
        {
            Debug.LogError("[PaddyFieldGenerator] ricePrefabs にPrefabが1つも設定されていません！", this);
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

        if (paddySoilTransform == null)
        {
            paddySoilTransform = transform;
        }

        MeshRenderer soilRenderer = paddySoilTransform.GetComponentInChildren<MeshRenderer>();

        if (soilRenderer == null)
        {
            Debug.LogError(
                $"[PaddyFieldGenerator] {paddySoilTransform.name} またはその子に MeshRenderer が見つかりません！",
                this
            );
            return;
        }

        if (!prefabCacheInitialized && !BuildPrefabCache())
        {
            return;
        }

        ValidPrefabData selectedPrefab =
            cachedValidPrefabs[Random.Range(0, cachedValidPrefabs.Count)];

        Debug.Log(
            $"[PaddyFieldGenerator] 今回の生成に使用するPrefab: {selectedPrefab.setting.prefab.name}",
            this
        );

        Bounds worldSoilBounds = soilRenderer.bounds;
        float mudSurfaceY = worldSoilBounds.max.y;

        // 生成されたイネをまとめる親オブジェクトを作成
        GameObject rootObj = new GameObject("GeneratedPaddyField");
        generatedRoot = rootObj.transform;
        generatedRoot.SetParent(paddySoilTransform, false);

        int totalCount = 0;

        for (
            float x = worldSoilBounds.min.x + (rowSpacing / 2f);
            x < worldSoilBounds.max.x;
            x += rowSpacing
        )
        {
            for (
                float z = worldSoilBounds.min.z + (plantSpacing / 2f);
                z < worldSoilBounds.max.z;
                z += plantSpacing
            )
            {
                int riceCount = Random.Range(minRicePerHill, maxRicePerHill + 1);

                for (int i = 0; i < riceCount; i++)
                {
                    // selectedPrefabはシーン全体で共通

                    float targetPlantHeight = selectedPrefab.setting.height;
                    float scaleRatio = targetPlantHeight / selectedPrefab.localMeshHeight;
                    Vector3 desiredWorldScale = Vector3.one * scaleRatio;
                    float scaledMinY = selectedPrefab.localMinY * scaleRatio;

                    Vector2 randomCircle = Random.insideUnitCircle * hillRadius;

                    Vector3 worldPos = new Vector3(
                        x + randomCircle.x,
                        mudSurfaceY - sinkDepth - scaledMinY,
                        z + randomCircle.y
                    );

                    Quaternion worldRot = Quaternion.Euler(
                        0f,
                        Random.Range(0f, 360f),
                        0f
                    );

                    GameObject riceInstance = Instantiate(
                        selectedPrefab.setting.prefab,
                        worldPos,
                        worldRot,
                        generatedRoot
                    );

                    Vector3 parentScale = generatedRoot.lossyScale;

                    riceInstance.transform.localScale = new Vector3(
                        desiredWorldScale.x / (parentScale.x != 0f ? parentScale.x : 1f),
                        desiredWorldScale.y / (parentScale.y != 0f ? parentScale.y : 1f),
                        desiredWorldScale.z / (parentScale.z != 0f ? parentScale.z : 1f)
                    );

                    totalCount++;
                }
            }
        }

        Debug.Log(
            $"[PaddyFieldGenerator] イネの生成が完了しました。（合計本数: {totalCount}）"
        );
    }

    private bool BuildPrefabCache()
    {
        cachedValidPrefabs.Clear();

        if (ricePrefabs == null || ricePrefabs.Count == 0)
        {
            Debug.LogError(
                "[PaddyFieldGenerator] ricePrefabs にPrefabが1つも設定されていません！",
                this
            );

            return false;
        }

        List<ValidPrefabData> validPrefabs = new List<ValidPrefabData>();

        foreach (RicePrefabSetting setting in ricePrefabs)
        {
            if (setting == null || setting.prefab == null)
            {
                continue;
            }

            if (setting.height <= 0.0001f)
            {
                Debug.LogWarning(
                    $"[PaddyFieldGenerator] Prefab「{setting.prefab.name}」の高さが無効です。",
                    this
                );

                continue;
            }

            MeshFilter prefabMeshFilter =
                setting.prefab.GetComponentInChildren<MeshFilter>();

            if (prefabMeshFilter == null || prefabMeshFilter.sharedMesh == null)
            {
                Debug.LogWarning(
                    $"[PaddyFieldGenerator] Prefab「{setting.prefab.name}」にMeshがありません。",
                    this
                );

                continue;
            }

            Mesh sourceMesh = prefabMeshFilter.sharedMesh;
            float localMeshHeight = sourceMesh.bounds.size.y;
            float localMinY = sourceMesh.bounds.min.y;

            if (localMeshHeight <= 0.0001f)
            {
                Debug.LogWarning(
                    $"[PaddyFieldGenerator] Prefab「{setting.prefab.name}」の高さが0です。",
                    this
                );

                continue;
            }

            validPrefabs.Add(new ValidPrefabData
            {
                setting = setting,
                localMeshHeight = localMeshHeight,
                localMinY = localMinY
            });
        }

        if (validPrefabs.Count == 0)
        {
            Debug.LogError("[PaddyFieldGenerator] 使用可能なPrefabがありません！");
            return false;
        }

        cachedValidPrefabs.AddRange(validPrefabs);

        prefabCacheInitialized = cachedValidPrefabs.Count > 0;

        return prefabCacheInitialized;
    }

    public void ClearField()
    {
        if (generatedRoot == null)
        {
            generatedRoot = paddySoilTransform != null
                ? paddySoilTransform.Find("GeneratedPaddyField")
                : transform.Find("GeneratedPaddyField");
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

    private void Awake()
    {
        BuildPrefabCache();
    }
}