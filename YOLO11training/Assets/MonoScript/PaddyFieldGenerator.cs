using System.Collections.Generic;
using UnityEngine;

public class PaddyFieldGenerator : MonoBehaviour
{
    [Header("Base Prefabs")]
    public GameObject mudPrefab;
    public GameObject ricePrefab;
    public GameObject[] weedPrefabs;

    [Header("Field Size")]
    public int fieldColumns = 12;
    public int fieldRows = 12;
    public float tileSize = 0.25f;

    [Header("Plant Count Per Tile")]
    public int minRicePerTile = 1;
    public int maxRicePerTile = 4;
    public int minWeedPerTile = 0;
    public int maxWeedPerTile = 3;

    [Header("Placement")]
    public float plantPadding = 0.03f;
    public Vector2 riceScaleRange = new Vector2(0.08f, 0.12f);
    public Vector2 weedScaleRange = new Vector2(0.07f, 0.13f);

    [Header("Randomization")]
    public bool useFixedSeed = false;
    public int seed = 0;

    private Transform generatedRoot;

    private void Start()
    {
        GenerateField();
    }

    [ContextMenu("Generate Field")]
    public void GenerateField()
    {
        ClearGeneratedField();

        if (mudPrefab == null || ricePrefab == null)
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
                int weedCount = Random.Range(minWeedPerTile, maxWeedPerTile + 1);

                List<Vector3> occupiedPositions = new List<Vector3>();

                SpawnPlants(tileRoot, ricePrefab, riceCount, riceScaleRange, occupiedPositions, true);
                SpawnWeeds(tileRoot, weedCount, weedScaleRange, occupiedPositions);
            }
        }
    }

    private void ClearGeneratedField()
    {
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
    }

    private Transform CreateTile(Vector3 tilePosition, int row, int column)
    {
        GameObject tileObject = Instantiate(mudPrefab, tilePosition, Quaternion.identity, generatedRoot);
        tileObject.name = $"Mud_{row}_{column}";
        return tileObject.transform;
    }

    private void SpawnWeeds(Transform tileRoot, int weedCount, Vector2 scaleRange, List<Vector3> occupiedPositions)
    {
        if (weedPrefabs == null || weedPrefabs.Length == 0)
        {
            Debug.LogWarning("At least one weed prefab is required to spawn weeds.");
            return;
        }

        int spawnedWeedCount = 0;
        for (int i = 0; i < weedCount; i++)
        {
            GameObject weedPrefab = weedPrefabs[Random.Range(0, weedPrefabs.Length)];
            if (weedPrefab == null)
            {
                continue;
            }

            if (SpawnSinglePlant(tileRoot, weedPrefab, scaleRange, occupiedPositions, false))
            {
                spawnedWeedCount++;
            }
        }

        if (spawnedWeedCount < weedCount)
        {
            Debug.LogWarning($"Only {spawnedWeedCount} of {weedCount} requested weeds were spawned.");
        }
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

        Vector3 worldPosition = tileRoot.position + localPosition;
        GameObject plantInstance = Instantiate(plantPrefab, worldPosition, Quaternion.identity, tileRoot);

        float randomYaw = Random.Range(0f, 360f);
        plantInstance.transform.rotation = Quaternion.Euler(0f, randomYaw, 0f);

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