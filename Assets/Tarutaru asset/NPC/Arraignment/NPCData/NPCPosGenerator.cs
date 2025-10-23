using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NPCPosGenerator : MonoBehaviour
{
    [System.Serializable]
    private struct SpawnProperty
    {
        public float distanceX;
        public float distanceZ;
        public float randomDistance;
    }

    [Header("スポーン範囲")]
    [SerializeField] private bool corpY = false;
    [SerializeField] private bool initWipe = false;

    [Header("オーディエンス設定1f")]
    [SerializeField] private SpawnProperty property1f = new SpawnProperty { distanceX = 0.3f, distanceZ = 0.3f, randomDistance = 0.1f };

    [Header("オーディエンス設定2f")]
    [SerializeField] private SpawnProperty property2f = new SpawnProperty { distanceX = 0.8f, distanceZ = 0.8f, randomDistance = 0.3f };

    [Header("オーディエンス設定3f")]
    [SerializeField] private SpawnProperty property3f = new SpawnProperty { distanceX = 1.1f, distanceZ = 1.1f, randomDistance = 0.5f };

    [Header("生成するPrefab")]
    [SerializeField] private GameObject audiencePrefab;

    [ContextMenu("Generate Audience")]
    public void GenerateAudience()
    {
        if (audiencePrefab == null)
        {
            Debug.LogError("Prefab is null");
            return;
        }

        if (initWipe)
        {
#if UNITY_EDITOR
            Undo.RegisterFullObjectHierarchyUndo(gameObject, "Clear old audience");
#endif
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.GetComponent<MeshCollider>() == null)
                    DestroyImmediate(child.gameObject);
            }
        }

        foreach (Transform child in transform)
        {
            MeshCollider col = child.GetComponent<MeshCollider>();
            if (col == null) continue;

            GenerateAudienceOnMesh(child, col);
        }
    }

    private void GenerateAudienceOnMesh(Transform areaTransform, MeshCollider meshCol)
    {
        Debug.Log("Generating on: " + meshCol.name);

        var mesh = meshCol.sharedMesh;
        var bounds = mesh.bounds;

        // 名前に応じて設定を切り替える
        SpawnProperty property = meshCol.name.Contains("3f") ? property3f : meshCol.name.Contains("2f") ? property2f : property1f;;

        float audienceDistanceX = property.distanceX;
        float audienceDistanceZ = property.distanceZ;
        float audienceRandomDistance = property.randomDistance;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        int countX = Mathf.FloorToInt((max.x - min.x) / audienceDistanceX);
        int countZ = Mathf.FloorToInt((max.z - min.z) / audienceDistanceZ);

        Debug.Log($"X:{countX}, Z:{countZ}");
        for (int z = 0; z < countZ; z++)
        {
            for (int x = 0; x < countX; x++)
            {
                float localX = min.x + x * audienceDistanceX + audienceDistanceX / 2 + Random.Range(-audienceRandomDistance, audienceRandomDistance);
                float localZ = min.z + z * audienceDistanceZ + audienceDistanceZ / 2 + Random.Range(-audienceRandomDistance, audienceRandomDistance);

                Vector3 localRayOrigin = new Vector3(localX, bounds.max.y + 10f, localZ);
                Ray ray = new Ray(areaTransform.TransformPoint(localRayOrigin), -areaTransform.up);

                if (Physics.Raycast(ray, out var hit, 100f))
                {
                    if (hit.collider == meshCol)
                    {
                        CreateAudience(hit.point);
                    }
                }
            }
        }
    }

    private void CreateAudience(Vector3 pos)
    {
        GameObject obj;
#if UNITY_EDITOR
        obj = (GameObject)PrefabUtility.InstantiatePrefab(audiencePrefab);
#endif

        if (obj == null)
        {
            // フォールバック: 通常の Instantiate を使用
            obj = Instantiate(audiencePrefab);
        }

        obj.transform.position = pos;

        Transform bleachers = transform.Find("Audience Pos");
        if (bleachers == null)
        {
            bleachers = new GameObject("Audience Pos").transform;
            bleachers.SetParent(transform);
            bleachers.localPosition = Vector3.zero;
        }

        obj.transform.SetParent(bleachers);

    #if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(obj, "Create Audience");
    #endif
    }
}