using UnityEngine;
using System.Collections.Generic;

public class PaddyFieldGenerator : MonoBehaviour
{
    [Header("土のPrefab")]
    public GameObject soilPrefab;

    [Header("米のPrefab")]
    public GameObject ricePrefab;

    [Header("雑草のPrefab")]
    public GameObject weedPrefab;

    [Header("米の本数（範囲）")]
    public int minRiceCount = 90;
    public int maxRiceCount = 110;

    [Header("米同士の最低距離")]
    public float riceMinDistance = 0.12f;

    [Header("雑草同士の最低距離")]
    public float weedMinDistance = 0.14f;

    [Header("米と雑草の最低距離")]
    public float riceToWeedMinDistance = 0.10f;

    [Header("雑草の本数")]
    public int weedCount = 30;

    [Header("局所ランダム設定")]
    public Vector3 riceBaseScale = new Vector3(0.12f, 0.12f, 0.12f);
    public float riceScaleVariation = 0.03f;
    public Vector3 weedBaseScale = new Vector3(0.10f, 0.10f, 0.10f);
    public float weedScaleVariation = 0.02f;

    private Bounds soilBounds;

    void Start()
    {
        GenerateField();
    }

    void GenerateField()
    {
        if (soilPrefab == null)
        {
            Debug.LogWarning("土のPrefabが設定されていません。");
            return;
        }

        // 土を1つ生成
        GameObject soil = Instantiate(soilPrefab, transform.position, Quaternion.identity, transform);
        soilBounds = GetBounds(soil);

        if (ricePrefab == null)
        {
            Debug.LogWarning("米のPrefabが設定されていません。");
            return;
        }

        List<Vector3> ricePositions = new List<Vector3>();
        int riceTargetCount = Random.Range(minRiceCount, maxRiceCount + 1);

        for (int i = 0; i < riceTargetCount; i++)
        {
            Vector3 spawnPos;
            int tries = 0;
            do
            {
                spawnPos = RandomPointInsideBounds(soilBounds);
                tries++;
            }
            while (tries < 200 && IsTooCloseToAny(spawnPos, ricePositions, riceMinDistance));

            if (tries >= 200)
            {
                continue;
            }

            GameObject rice = Instantiate(ricePrefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            ApplyRandomScale(rice.transform, riceBaseScale, riceScaleVariation);
            ricePositions.Add(spawnPos);
        }

        List<Vector3> weedPositions = new List<Vector3>();
        for (int i = 0; i < weedCount; i++)
        {
            Vector3 spawnPos;
            int tries = 0;
            do
            {
                spawnPos = RandomPointInsideBounds(soilBounds);
                tries++;
            }
            while (
                tries < 300 &&
                (
                    IsTooCloseToAny(spawnPos, ricePositions, riceToWeedMinDistance) ||
                    IsTooCloseToAny(spawnPos, weedPositions, weedMinDistance)
                )
            );

            if (tries >= 300)
            {
                continue;
            }

            if (weedPrefab != null)
            {
                GameObject weed = Instantiate(weedPrefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                ApplyRandomScale(weed.transform, weedBaseScale, weedScaleVariation);
                weedPositions.Add(spawnPos);
            }
        }

        Debug.Log($"土の範囲内に米 {ricePositions.Count} 本、雑草 {weedPositions.Count} 本を配置しました。");
    }

    private Bounds GetBounds(GameObject target)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        Collider collider = target.GetComponent<Collider>();

        if (renderer != null)
        {
            return renderer.bounds;
        }

        if (collider != null)
        {
            return collider.bounds;
        }

        Vector3 size = new Vector3(2f, 0.2f, 2f);
        return new Bounds(target.transform.position, size);
    }

    private Vector3 RandomPointInsideBounds(Bounds bounds)
    {
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);
        float y = bounds.min.y + 0.05f;
        return new Vector3(x, y, z);
    }

    private bool IsTooCloseToAny(Vector3 point, List<Vector3> positions, float minDistance)
    {
        foreach (Vector3 pos in positions)
        {
            if (Vector3.Distance(point, pos) < minDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyRandomScale(Transform target, Vector3 baseScale, float variation)
    {
        float randomX = Random.Range(-variation, variation);
        float randomY = Random.Range(-variation, variation);
        float randomZ = Random.Range(-variation, variation);

        target.localScale = new Vector3(
            baseScale.x + randomX,
            baseScale.y + randomY,
            baseScale.z + randomZ
        );
    }
}