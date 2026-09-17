using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaddyFieldGenerator : MonoBehaviour
{
    [Header("Base Prefabs")]
    public GameObject mudPrefab;
    public GameObject ricePrefab;

    [Header("Field Size")]
    public int fieldColumns = 12;
    public int fieldRows = 12;
    public float tileSize = 0.25f;

    [Header("Plant Count Per Tile")]
    public int minRicePerTile = 1;
    public int maxRicePerTile = 4;

    [Header("Placement")]
    public float plantPadding = 0.03f;
    public Vector2 riceScaleRange = new Vector2(0.08f, 0.12f);

    [Header("株（束）ごとのランダム倍率")]
    [Range(0.1f, 2.0f)] public float minHillScale = 0.9f; 
    [Range(0.1f, 2.0f)] public float maxHillScale = 1.1f; 

    [Header("イネの高さ範囲設定（メートル単位：1.0 = 1m）")]
    public float minPlantHeight = 0.75f; // 75cm
    public float maxPlantHeight = 0.90f; // 90cm

    private Transform generatedRoot;

    private void Start()
    {
        // 1. 既存のメッシュ・オブジェクトを破棄
        ClearField();

        // 2. メモリリーク対策: 未使用アセットの非同期解放完了を待機
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        // 3. 領域解放完了後にフィールドを再生成
        GenerateField();
    }

    [ContextMenu("Generate Field")]
    public void GenerateField()
    {
        if (ricePrefab == null)
        {
            Debug.LogError("[PaddyFieldGenerator] ricePrefab が設定されていません！", this);
            return;
        }
        if (rowSpacing <= 0.001f || plantSpacing <= 0.001f)
        {
            Debug.LogWarning("Mud prefab and rice prefab are required.");
            return;
        }

        if (useFixedSeed)
        {
            Random.InitState(seed);
        }

        generatedRoot = new GameObject("GeneratedPaddyField").transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.localPosition = Vector3.zero;
        generatedRoot.localRotation = Quaternion.identity;
        generatedRoot.localScale = Vector3.one;

        Vector3 fieldOrigin = transform.position;

        for (int row = 0; row < fieldRows; row++)
        {
            for (int column = 0; column < fieldColumns; column++)
            {
                Vector3 tilePosition = fieldOrigin + new Vector3(column * tileSize, 0f, row * tileSize);
                Transform tileRoot = CreateTile(tilePosition, row, column);

                int riceCount = Random.Range(minRicePerTile, maxRicePerTile + 1);

                List<Vector3> occupiedPositions = new List<Vector3>();

                SpawnPlants(tileRoot, ricePrefab, riceCount, riceScaleRange, occupiedPositions, true);
            }
        }
    }

    private void ClearGeneratedField()
    {
        if (generatedRoot != null)
        {
            if (Application.isPlaying)
            {
                // --- 【追加】株（束）ごとのランダム化パラメータ ---
                float hillScale = Random.Range(minHillScale, maxHillScale); // 株（束）全体のスケール(80%~120%)
                float hillYaw = Random.Range(0f, 360f);                    // 株（束）全体のYaw回転
                Quaternion hillRotation = Quaternion.Euler(0f, hillYaw, 0f);
                Vector3 hillCenterPos = new Vector3(x, mudSurfaceY - sinkDepth, z);

                int riceCount = Random.Range(minRicePerHill, maxRicePerHill + 1);

                for (int i = 0; i < riceCount; i++)
                {
                    if (verts.Count + sourceVertices.Length > MAX_VERTICES_PER_MESH)
                    {
                        CreateChunk(verts, normals, uvs, tris, sharedMaterial);
                        verts.Clear(); normals.Clear(); uvs.Clear(); tris.Clear();
                    }

                    // イネ個別の高さ・位置計算
                    float targetPlantHeight = Random.Range(minPlantHeight, maxPlantHeight);
                    float baseScaleRatio = targetPlantHeight / localMeshHeight;
                    
                    // 株全体のスケール(hillScale)を乗算
                    float finalScaleRatio = baseScaleRatio * hillScale;
                    Vector3 worldScale = Vector3.one * finalScaleRatio;
                    float scaledMinY = localMinY * finalScaleRatio;

                    // 株の中心からのオフセット計算（株全体のYaw回転を反映）
                    Vector2 randomCircle = Random.insideUnitCircle * (hillRadius * hillScale);
                    Vector3 localOffset = new Vector3(randomCircle.x, -scaledMinY, randomCircle.y);
                    Vector3 rotatedOffset = hillRotation * localOffset;

                    Vector3 worldPos = hillCenterPos + rotatedOffset;

                    // イネ個別のランダム回転 × 株全体のYaw回転
                    Quaternion individualRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    Quaternion finalRotation = hillRotation * individualRot;

                    Matrix4x4 worldTRS = Matrix4x4.TRS(worldPos, finalRotation, worldScale);
                    Matrix4x4 localTRS = paddySoilTransform.worldToLocalMatrix * worldTRS;

                    int vertexOffset = verts.Count;
                    for (int v = 0; v < sourceVertices.Length; v++)
                    {
                        verts.Add(localTRS.MultiplyPoint3x4(sourceVertices[v]));
                        if (sourceNormals.Length > v)
                        {
                            normals.Add(localTRS.MultiplyVector(sourceNormals[v]).normalized);
                        }
                    }

                    if (sourceUVs.Length > 0) uvs.AddRange(sourceUVs);

                    for (int t = 0; t < sourceTriangles.Length; t++)
                    {
                        tris.Add(sourceTriangles[t] + vertexOffset);
                    }
                }
            }

            generatedRoot = null;
        }
    }

    private Transform CreateTile(Vector3 tilePosition, int row, int column)
    {
        GameObject tileObject = Instantiate(mudPrefab, tilePosition, Quaternion.identity, generatedRoot);
        tileObject.name = $"Mud_{row}_{column}";
        return tileObject.transform;
    }

    private void SpawnPlants(Transform tileRoot, GameObject plantPrefab, int count, Vector2 scaleRange, List<Vector3> occupiedPositions, bool alignUpright)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnSinglePlant(tileRoot, plantPrefab, scaleRange, occupiedPositions, alignUpright);
        }
    }

    private bool SpawnSinglePlant(Transform tileRoot, GameObject plantPrefab, Vector2 scaleRange, List<Vector3> occupiedPositions, bool alignUpright)
    {
        if (plantPrefab == null)
        {
            return false;
        }

        Vector3 localPosition;
        if (!TryFindFreePosition(occupiedPositions, out localPosition))
        {
            return false;
        }

        generatedChunks.Clear();
        generatedMeshes.Clear();
    }

        float randomScale = Random.Range(scaleRange.x, scaleRange.y);
        plantInstance.transform.localScale = Vector3.one * randomScale;

        occupiedPositions.Add(localPosition);
        return true;
    }

    private bool TryFindFreePosition(List<Vector3> occupiedPositions, out Vector3 position)
    {
        const int maxAttempts = 24;
        float halfTile = tileSize * 0.5f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float x = Random.Range(-halfTile + plantPadding, halfTile - plantPadding);
            float z = Random.Range(-halfTile + plantPadding, halfTile - plantPadding);
            Vector3 candidate = new Vector3(x, 0f, z);

            bool isTooClose = false;
            for (int i = 0; i < occupiedPositions.Count; i++)
            {
                if (Vector3.Distance(candidate, occupiedPositions[i]) < plantPadding)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (!isTooClose)
            {
                position = candidate;
                return true;
            }
        }

        position = Vector3.zero;
        return false;
    }
}

internal interface IProcessStep
{
}