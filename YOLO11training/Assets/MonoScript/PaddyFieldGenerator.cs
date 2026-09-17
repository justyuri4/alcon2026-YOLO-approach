using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaddyFieldGenerator : MonoBehaviour, IProcessStep
{
    [Header("イネの3Dモデル（単体）")]
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

    [Header("株（束）ごとのランダム倍率")]
    [Range(0.1f, 2.0f)] public float minHillScale = 0.9f; 
    [Range(0.1f, 2.0f)] public float maxHillScale = 1.1f; 

    [Header("イネの高さ範囲設定（メートル単位：1.0 = 1m）")]
    public float minPlantHeight = 0.75f; // 75cm
    public float maxPlantHeight = 0.90f; // 90cm

    [Header("めり込み調整（メートル単位）")]
    public float sinkDepth = 0.01f;      // 1cmめり込ませる

    private const int MAX_VERTICES_PER_MESH = 60000;
    private List<GameObject> generatedChunks = new List<GameObject>();
    private List<Mesh> generatedMeshes = new List<Mesh>();

    public IEnumerator ExecuteStep()
    {
        // 1. 既存のメッシュ・オブジェクトを破棄
        ClearField();

        // 2. メモリリーク対策: 未使用アセットの非同期解放完了を待機
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        // 3. 領域解放完了後にフィールドを再生成
        GenerateField();
        yield return null;
    }

    public void GenerateField()
    {
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
        MeshRenderer prefabRenderer = ricePrefab.GetComponentInChildren<MeshRenderer>();

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

        Material sharedMaterial = prefabRenderer != null ? prefabRenderer.sharedMaterial : null;
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

        Vector3[] sourceVertices = sourceMesh.vertices;
        Vector3[] sourceNormals = sourceMesh.normals;
        Vector2[] sourceUVs = sourceMesh.uv;
        int[] sourceTriangles = sourceMesh.triangles;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (float x = worldSoilBounds.min.x + (rowSpacing / 2f); x < worldSoilBounds.max.x; x += rowSpacing)
        {
            for (float z = worldSoilBounds.min.z + (plantSpacing / 2f); z < worldSoilBounds.max.z; z += plantSpacing)
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
        }

        if (verts.Count > 0)
        {
            CreateChunk(verts, normals, uvs, tris, sharedMaterial);
        }

        Debug.Log($"[PaddyFieldGenerator] イネの生成が完了しました。（Chunk数: {generatedChunks.Count}）");
    }

    public void ClearField()
    {
        foreach (GameObject chunk in generatedChunks)
        {
            if (chunk != null) DestroyImmediate(chunk);
        }
        foreach (Mesh mesh in generatedMeshes)
        {
            if (mesh != null) DestroyImmediate(mesh);
        }

        generatedChunks.Clear();
        generatedMeshes.Clear();
    }

    private void CreateChunk(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris, Material mat)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);

        GameObject chunk = new GameObject("PaddyChunk");
        chunk.transform.SetParent(paddySoilTransform, false);

        chunk.transform.localPosition = Vector3.zero;
        chunk.transform.localRotation = Quaternion.identity;
        chunk.transform.localScale = Vector3.one;

        MeshFilter mf = chunk.AddComponent<MeshFilter>();
        MeshRenderer mr = chunk.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = mat;

        generatedChunks.Add(chunk);
    }

    private void OnDestroy()
    {
        ClearField();
    }
}

internal interface IProcessStep
{
}