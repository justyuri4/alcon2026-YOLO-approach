using UnityEngine;

public class SimpleHillGenerator : MonoBehaviour
{
    [Header("イネの3Dモデル（単体）")]
    public GameObject ricePrefab;

    [Header("1株（塊）あたりの本数")]
    public int minRicePerHill = 3;   
    public int maxRicePerHill = 5;   
    public float hillRadius = 0.03f;  

    [Header("ランダム設定")]
    public Vector3 baseScale = new Vector3(0.1f, 0.1f, 0.1f); 
    public float scaleVariation = 0.02f; 

    void Start()
    {
        GenerateSingleHill();
    }

    void GenerateSingleHill()
    {
        if (ricePrefab == null) return;

        // このスクリプトが置かれた位置（中心）に1株だけ作る
        int riceCount = Random.Range(minRicePerHill, maxRicePerHill + 1);
        
        for (int i = 0; i < riceCount; i++)
        {
            // 半径内のランダムな位置
            Vector2 randomCircle = Random.insideUnitCircle * hillRadius;
            Vector3 finalRicePos = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            // 生成
            GameObject riceInstance = Instantiate(ricePrefab, finalRicePos, Quaternion.identity);
            
            // --- 【修正部分】回転をヨー（Y軸）方向だけランダムにする ---
            float randomYaw = Random.Range(0f, 360f); // 0〜360度
            riceInstance.transform.rotation = Quaternion.Euler(0f, randomYaw, 0f);

            // スケールをランダムに
            float randomScaleY = Random.Range(-scaleVariation, scaleVariation);
            riceInstance.transform.localScale = new Vector3(
                baseScale.x + (randomScaleY * 0.5f),
                baseScale.y + randomScaleY,
                baseScale.z + (randomScaleY * 0.5f)
            );

            // 管理しやすいように自分の子オブジェクトにする
            riceInstance.transform.parent = this.transform;
        }
    }
}