using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[ExecuteInEditMode]
public class RandomSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int spawnCount = 10;

    [ContextMenu("Spawn In All Areas")]
    public void Spawn()
    {
        if (prefab == null) return;

        List<Transform> areas = new List<Transform>();
        foreach (Transform child in transform)
        {
            BoxCollider box = child.GetComponent<BoxCollider>();
            if (box != null)
                areas.Add(child);
        }

        for (int i = 0; i < spawnCount; i++)
        {
            Transform area = areas[Random.Range(0, areas.Count)];
            Vector3 localPos = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f)
            );

            BoxCollider col = area.GetComponent<BoxCollider>();
            Vector3 worldPos = area.TransformPoint(Vector3.Scale(localPos, col.size));

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Spawn Object");
            go.transform.position = worldPos;
        }
    }
}
