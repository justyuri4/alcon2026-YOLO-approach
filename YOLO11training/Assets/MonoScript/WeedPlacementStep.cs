using System.Collections;
using UnityEngine;

public class WeedPlacementStep : MonoBehaviour
{
    [Header("Weed Prefabs")]
    public GameObject[] weedPrefabs;

    [Header("Placement")]
    public Transform placementArea;
    public bool useGeneratedPaddyField = true;
    public bool placeOnGroundSurface = true;
    public LayerMask groundLayers = ~0;
    public float raycastStartHeight = 100f;
    public float raycastDistance = 200f;
    public float surfaceOffset = 0f;
    public int weedCount = 10;
    public Vector2 areaSize = new Vector2(10f, 10f);
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    public float height = 0f;

    [Header("Execution")]
    public bool clearBeforePlacement = true;
    public bool useFixedSeed = false;
    public int seed = 0;

    private Transform generatedRoot;

    private void Start()
    {
        StartCoroutine(ExecuteStep());
    }

    public IEnumerator ExecuteStep()
    {
        yield return null;
        PlaceWeeds();
    }

    [ContextMenu("Place Weeds")]
    public void PlaceWeeds()
    {
        ClearPlacedWeeds();

        if (weedPrefabs == null || weedPrefabs.Length == 0)
        {
            Debug.LogWarning("At least one weed prefab is required.");
            return;
        }

        if (weedCount <= 0)
        {
            return;
        }

        if (useFixedSeed)
        {
            Random.InitState(seed);
        }

        generatedRoot = new GameObject("GeneratedWeeds").transform;
        generatedRoot.SetParent(transform, false);

        Transform areaTransform = ResolvePlacementArea();
        Vector3 areaCenter = areaTransform.position;

        for (int index = 0; index < weedCount; index++)
        {
            GameObject weedPrefab = weedPrefabs[Random.Range(0, weedPrefabs.Length)];
            if (weedPrefab == null)
            {
                continue;
            }

            float x = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
            float z = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
            Vector3 position = areaCenter + new Vector3(x, height, z);

            if (placeOnGroundSurface && TryGetGroundPosition(position, areaTransform, out Vector3 groundPosition))
            {
                position = groundPosition;
            }

            GameObject weedInstance = Instantiate(weedPrefab, position, Quaternion.identity, generatedRoot);

            float yaw = Random.Range(0f, 360f);
            weedInstance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float scale = Random.Range(scaleRange.x, scaleRange.y);
            weedInstance.transform.localScale = Vector3.one * scale;
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

            Debug.LogWarning("GeneratedPaddyField was not found. Run PaddyFieldGenerator before placing weeds.");
        }

        return transform;
    }

    [ContextMenu("Clear Weeds")]
    public void ClearWeeds()
    {
        ClearPlacedWeeds(true);
    }

    private bool TryGetGroundPosition(Vector3 candidatePosition, Transform areaTransform, out Vector3 groundPosition)
    {
        Vector3 rayOrigin = new Vector3(
            candidatePosition.x,
            areaTransform.position.y + raycastStartHeight,
            candidatePosition.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayers))
        {
            if (hit.transform == areaTransform || hit.transform.IsChildOf(areaTransform))
            {
                groundPosition = hit.point + Vector3.up * surfaceOffset;
                return true;
            }
        }

        groundPosition = candidatePosition;
        return false;
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
