using System.Collections.Generic;
using UnityEngine;

public class PaddyFieldGeneratorCombine : MonoBehaviour
{
    [Header("イネの3Dモデル（単体）")]
    public GameObject ricePrefab;

    [Header("親オブジェクト（指定しない場合は自身の親を取得）")]
    public Transform paddySoilTransform;

    [Header("植え付け間隔設定（1ユニット = 1m）")]
    public float rowSpacing = 0.3f; 
    public float plantSpacing = 0.15f; 

    [Header("1株（塊）あたりの設定")]
    public int minRicePerHill = 3;   
    public int maxRicePerHill = 5;   
    public float hillRadius = 0.03f; 

    [Header("実際のサイズ設定（メートル単位）")]
    [Tooltip("苗の目標とする高さ（メートル）。例: 0.2 = 20cm")]
    public float targetHeight = 0.2f; 

    [Tooltip("苗ごとの高さのランダムなブレ幅（メートル）。例: 0.03 = ±3cm")]
    public float heightVariation = 0.03f; 

    [Header("めり込み調整")]
    [Tooltip("泥(mud)に埋め込む深さ(m)。正の数値で埋め込みます。例: 0.01 = 1cm埋め込む")]
    public float sinkDepth = 0.01f; // 1cm (0.01m) 埋め込む設定

    void Start()
    {
        GenerateAndCombineField();
    }

    void GenerateAndCombineField()
    {
        if (ricePrefab == null) return;

        if (paddySoilTransform == null && transform.parent != null)
        {
            paddySoilTransform = transform.parent;
        }

        if (paddySoilTransform == null) return;

        MeshRenderer soilRenderer = paddySoilTransform.GetComponent<MeshRenderer>();
        if (soilRenderer == null) return;

        // Prefabから元メッシュ情報を取得
        MeshFilter prefabMeshFilter = ricePrefab.GetComponentInChildren<MeshFilter>();
        if (prefabMeshFilter == null || prefabMeshFilter.sharedMesh == null)
        {
            Debug.LogError("ricePrefab から MeshFilter または Mesh を取得できませんでした。");
            return;
        }

        Mesh sharedMesh = prefabMeshFilter.sharedMesh;
        
        // メッシュのローカル座標での最底面(min.y)と全高を取得
        float localMinY = sharedMesh.bounds.min.y;
        float originalMeshHeight = sharedMesh.bounds.size.y;
        
        if (originalMeshHeight <= 0)
        {
            Debug.LogWarning("メッシュの高さが0のため、正しくスケーリングできません。");
            return;
        }

        // 泥(mud)の表面のY座標
        float mudSurfaceY = soilRenderer.bounds.max.y;

        // 生成した一時オブジェクトを格納するリスト
        List<GameObject> tempRiceObjects = new List<GameObject>();

        // 1. 苗を生成（最底面と泥の高さから配置座標を精密計算）
        for (float x = soilRenderer.bounds.min.x + (rowSpacing / 2f); x < soilRenderer.bounds.max.x; x += rowSpacing)
        {
            for (float z = soilRenderer.bounds.min.z + (plantSpacing / 2f); z < soilRenderer.bounds.max.z; z += plantSpacing)
            {
                int riceCount = Random.Range(minRicePerHill, maxRicePerHill + 1);
                
                for (int i = 0; i < riceCount; i++)
                {
                    // 実際の高さ(メートル)からスケール倍率を算出
                    float randomHeightOffset = Random.Range(-heightVariation, heightVariation);
                    float actualHeight = Mathf.Max(0.01f, targetHeight + randomHeightOffset);
                    float finalScaleFactor = actualHeight / originalMeshHeight;

                    // スケーリング後のローカル最底面位置(m)
                    float scaledMinY = localMinY * finalScaleFactor;

                    // 最底面が「泥の表面 - sinkDepth」の位置に来るように配置Y座標を逆算
                    // TransformPosition.y + scaledMinY = mudSurfaceY - sinkDepth
                    float spawnY = mudSurfaceY - sinkDepth - scaledMinY;

                    Vector2 randomCircle = Random.insideUnitCircle * hillRadius;
                    Vector3 pos = new Vector3(x + randomCircle.x, spawnY, z + randomCircle.y);

                    GameObject rice = Instantiate(ricePrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    rice.transform.localScale = new Vector3(finalScaleFactor, finalScaleFactor, finalScaleFactor);

                    tempRiceObjects.Add(rice);
                }
            }
        }

        if (tempRiceObjects.Count == 0) return;

        // 2. メッシュの結合（CombineMeshes）
        MeshFilter[] meshFilters = new MeshFilter[tempRiceObjects.Count];
        CombineInstance[] combine = new CombineInstance[tempRiceObjects.Count];

        Material sharedMaterial = null;

        for (int i = 0; i < tempRiceObjects.Count; i++)
        {
            meshFilters[i] = tempRiceObjects[i].GetComponentInChildren<MeshFilter>();
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = transform.worldToLocalMatrix * tempRiceObjects[i].transform.localToWorldMatrix;

            if (sharedMaterial == null)
            {
                sharedMaterial = tempRiceObjects[i].GetComponentInChildren<MeshRenderer>().sharedMaterial;
            }
        }

        MeshFilter myMeshFilter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer myMeshRenderer = gameObject.AddComponent<MeshRenderer>();

        myMeshFilter.mesh = new Mesh();
        myMeshFilter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        myMeshFilter.mesh.CombineMeshes(combine, true, true);
        myMeshRenderer.sharedMaterial = sharedMaterial;

        // 3. 不要になった一時GameObjectの削除
        foreach (GameObject obj in tempRiceObjects)
        {
            Destroy(obj);
        }
    }
}