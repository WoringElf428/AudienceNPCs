using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class NPCDataGenerator : MonoBehaviour
{
    [Header("出力設定")]
    [Tooltip("出力ディレクトリ")]
    public string OutputFolder = "Assets/Tarutaru asset/NPC/Arraignment/NPCData";

    [Tooltip("NPCデータ")]
    public string AudienceDataAssetName = "AllNPCData.asset";

    [Tooltip("メッシュ名")]
    public string MeshAssetName = "NPCEmitMesh.asset";

    [Header("隣人探索設定")]
    [Tooltip("最大距離")]
    [SerializeField] float distanceThreshold = 3.0f;

    [Header("メッシュ生成設定")]
    [Tooltip("除外する中心点")]
    [SerializeField] private Transform exclusionCenter;
    [Tooltip("除外する半径")]
    [SerializeField] private float exclusionRadius = 0.5f;

    [Tooltip("新しく生成されるNPCのisParticleのデフォルト値（除外範囲外の場合の初期値）")]
    [SerializeField] private bool defaultIsParticleValue = true;

    [Header("デバッグ設定")] // デバッグ設定を追加
    [Tooltip("除外された位置に生成するデバッグ用Prefab")]
    [SerializeField] private GameObject debugPrefab;

    private List<Vector3> excludedPositions; // 除外された位置を記録するリスト

    [ContextMenu("Generate NPC Assets")]
    public void GenerateNPCAssets()
    {
        // 処理開始時に除外位置リストを初期化/クリア
        if (excludedPositions == null)
        {
            excludedPositions = new List<Vector3>();
        }
        else
        {
            excludedPositions.Clear();
        }

        // 既存のデバッグオブジェクトをクリア
        ClearDebugObjects();

        // 出力フォルダの作成
        EnsureOutputFolderExists(OutputFolder);

        // すべての子オブジェクトのTransformを取得
        List<Transform> allChildrenTransforms = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            allChildrenTransforms.Add(transform.GetChild(i));
        }

        if (allChildrenTransforms.Count == 0)
        {
            Debug.LogError("子オブジェクトが見つかりませんでした。");
            return;
        }

        // 1. 隣人探索とNPCStructDataリストの初期作成
        List<NPCStructData> npcStructList = ProcessNPCDataInitial(allChildrenTransforms);

        // 2. メッシュの生成とisParticleの最終判定
        // isParticleの判定結果がnpcStructListに反映される
        GenerateAndSaveMesh(allChildrenTransforms, ref npcStructList);

        // AllNPCDataContainer アセットの作成と保存はここで実行
        AllNPCDataContainer allDataContainer = ScriptableObject.CreateInstance<AllNPCDataContainer>();
        allDataContainer.audienceMembers = npcStructList;
        // isParticleの判定後にmaxNeighborsを計算
        allDataContainer.maxNeighbors = npcStructList.Any() ? npcStructList.Max(npc => npc.neighborIndex.Count) : 0; // Modified

        string assetPath = Path.Combine(OutputFolder, AudienceDataAssetName).Replace("\\", "/");
        SaveAsset(allDataContainer, assetPath);
        Debug.Log($"AllNPCDataContainer '{AudienceDataAssetName}' を '{OutputFolder}' に保存しました。");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("NPCデータとメッシュアセットの生成が完了しました。");
    }

    private void EnsureOutputFolderExists(string path)
    {
        string[] folders = path.Split('/');
        string currentPath = "Assets";
        for (int i = 1; i < folders.Length; i++)
        {
            string nextPath = Path.Combine(currentPath, folders[i]).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, folders[i]);
            }
            currentPath = nextPath;
        }
    }

    // 初期データ作成（isParticleは仮の値）
    private List<NPCStructData> ProcessNPCDataInitial(List<Transform> childrenTransforms)
    {
        List<NPCStructData> npcStructList = new List<NPCStructData>();

        for (int i = 0; i < childrenTransforms.Count; i++)
        {
            Transform currentTransform = childrenTransforms[i];

            NPCStructData newNPCData = new NPCStructData
            {
                neighborIndex = new List<int>(), // Modified
                neighborDist = new List<float>(), // Modified
                clip_index = new int[4],
                position = currentTransform.position,
                rotation = currentTransform.rotation,
                scale = currentTransform.localScale,
                isParticle = defaultIsParticleValue
            };

            // 隣人探索
            for (int j = 0; j < childrenTransforms.Count; j++)
            {
                if (i == j) continue;

                Transform neighborTransform = childrenTransforms[j];
                float dist = Vector3.Distance(currentTransform.position, neighborTransform.position);
                if (dist <= distanceThreshold)
                {
                    newNPCData.neighborIndex.Add(j); // Modified
                    newNPCData.neighborDist.Add(dist); // Modified
                }
            }

            npcStructList.Add(newNPCData);
        }
        return npcStructList;
    }

    private void GenerateAndSaveMesh(List<Transform> childrenTransforms, ref List<NPCStructData> npcStructList)
    {
        List<Vector3> vertices = new List<Vector3>();
        Dictionary<Transform, int> transformToIndexMap = new Dictionary<Transform, int>();

        for (int i = 0; i < childrenTransforms.Count; i++)
        {
            Transform child = childrenTransforms[i];
            NPCStructData currentNPCData = npcStructList[i];

            Vector3 worldPos = child.position;
            bool isInExclusionZone = false;
            if (exclusionCenter != null)
            {
                // y軸を含めずx-z平面上での距離を考慮
                Vector3 worldPosFlat = new Vector3(worldPos.x, 0f, worldPos.z);
                Vector3 exclusionCenterFlat = new Vector3(exclusionCenter.position.x, 0f, exclusionCenter.position.z);
                Vector3 flatOffset = worldPosFlat - exclusionCenterFlat;

                if (flatOffset.magnitude < exclusionRadius)
                {
                    isInExclusionZone = true;
                }
            }

            // isParticleの値を更新
            currentNPCData.isParticle = !isInExclusionZone; // 除外範囲外ならtrue、含まれるならfalse
            npcStructList[i] = currentNPCData; // Structなので変更をリストに反映

            // デバッグ用のログ出力と位置の記録
            if (isInExclusionZone)
            {
                Debug.Log($"NPC at position {worldPos} is in exclusion zone (isParticle = false).");
                excludedPositions.Add(worldPos); // 除外された位置を記録
            }

            // isParticleがtrueのNPCのみをメッシュに含める
            if (!currentNPCData.isParticle) continue;

            // 重複チェック
            if (transformToIndexMap.ContainsKey(child)) continue;

            int newIndex = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(worldPos));
            transformToIndexMap[child] = newIndex;
        }

        Mesh mesh = new Mesh();
        mesh.name = "GeneratedNPCEmitMesh";
        mesh.SetVertices(vertices);

        Vector3[] normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        mesh.normals = normals;

        mesh.RecalculateBounds();

        string meshAssetPath = Path.Combine(OutputFolder, MeshAssetName).Replace("\\", "/");
        SaveAsset(mesh, meshAssetPath);
        Debug.Log($"メッシュ '{MeshAssetName}' を '{OutputFolder}' に保存しました。");
    }

    private void SaveAsset(Object asset, string path)
    {
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
    }

    // 除外された位置にデバッグオブジェクトを生成する関数
    [ContextMenu("Spawn Excluded Debug Objects")]
    private void SpawnExcludedDebugObjects()
    {
        if (debugPrefab == null)
        {
            Debug.LogError("Debug Prefabが設定されていません。Inspectorで設定してください。");
            return;
        }

        if (excludedPositions == null || excludedPositions.Count == 0)
        {
            Debug.LogWarning("除外されたNPCがありません、またはアセットがまだ生成されていません。");
            return;
        }

        // 既存のデバッグオブジェクトをクリア
        ClearDebugObjects();

        GameObject debugParent = new GameObject("Excluded Debug Objects");
        debugParent.transform.SetParent(this.transform); // このスクリプトのアタッチされているGameObjectの子にする
        debugParent.hideFlags = HideFlags.DontSaveInEditor; // シーンファイルに保存しない

        foreach (Vector3 pos in excludedPositions)
        {
            GameObject obj = PrefabUtility.InstantiatePrefab(debugPrefab) as GameObject;
            if (obj != null)
            {
                obj.transform.position = pos;
                obj.transform.SetParent(debugParent.transform);
                obj.name = debugPrefab.name + "_Excluded";
            }
        }
        Debug.Log($"{excludedPositions.Count}個のデバッグオブジェクトを生成しました。");
    }

    // 生成されたデバッグオブジェクトをクリアする関数
    [ContextMenu("Clear Debug Objects")]
    private void ClearDebugObjects()
    {
        // 現在のGameObjectの子からデバッグオブジェクトを探して削除
        List<GameObject> debugObjectsToDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.name == "Excluded Debug Objects")
            {
                debugObjectsToDestroy.Add(child.gameObject);
            }
        }

        foreach (GameObject obj in debugObjectsToDestroy)
        {
            // EditorモードではDestroyImmediateを使用
            DestroyImmediate(obj);
        }
        Debug.Log("デバッグオブジェクトをクリアしました。");
    }
}