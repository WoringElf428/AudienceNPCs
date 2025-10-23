using UnityEngine;
using UnityEditor;
using System.IO;
public class BakeAtlasWindow : EditorWindow {
    private const int ClusterCount = 4;

    private Texture2D[] clusterTextures = new Texture2D[ClusterCount];
    private Vector3[] clusterMinBounds = new Vector3[ClusterCount];
    private Vector3[] clusterMaxBounds = new Vector3[ClusterCount];
    private string assetPath = "Assets/Tarutaru asset/NPC/Arraignment/VAT baker/ClusterAtlas.asset";

    [MenuItem("Tools/Bake Cluster Atlas")] 
    public static void ShowWindow() {
        GetWindow<BakeAtlasWindow>("Bake Cluster Atlas");
    }

    private void OnGUI() {
        EditorGUILayout.LabelField("Cluster Textures (Position or Normal)", EditorStyles.boldLabel);
        for (int i = 0; i < ClusterCount; i++) {
            EditorGUILayout.BeginVertical("box");
            clusterTextures[i] = (Texture2D)EditorGUILayout.ObjectField($"Cluster {i+1} Texture", clusterTextures[i], typeof(Texture2D), false);
            clusterMinBounds[i] = EditorGUILayout.Vector3Field($"Cluster {i+1} Min Bounds", clusterMinBounds[i]);
            clusterMaxBounds[i] = EditorGUILayout.Vector3Field($"Cluster {i+1} Max Bounds", clusterMaxBounds[i]);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();
        assetPath = EditorGUILayout.TextField("Asset Path", assetPath);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Atlas")) {
            GenerateAtlas(clusterTextures, clusterMinBounds, clusterMaxBounds, assetPath);
        }
    }


    private static void GenerateAtlas(Texture2D[] textures, Vector3[] minBounds, Vector3[] maxBounds, string assetPath)
    {
        // Validation
        if (textures == null || textures.Length != ClusterCount) {
            Debug.LogError($"[BakeAtlas] Exactly {ClusterCount} textures required.");
            return;
        }
        for (int i = 0; i < ClusterCount; i++) {
            if (textures[i] == null) {
                Debug.LogError($"[BakeAtlas] Cluster {i+1} texture is null.");
                return;
            }
        }

        // Compute global bounding
        Vector3 globalMin = minBounds[0];
        Vector3 globalMax = maxBounds[0];
        for (int i = 1; i < ClusterCount; i++) {
            globalMin = Vector3.Min(globalMin, minBounds[i]);
            globalMax = Vector3.Max(globalMax, maxBounds[i]);
        }
        Vector3 globalSize = globalMax - globalMin;
        Debug.Log($"[BakeAtlas] Final Global Bounding Box -> Min: {globalMin}, Max: {globalMax}, Size: {globalSize}");

        // Determine grid layout (approx square)
        int cols = Mathf.CeilToInt(Mathf.Sqrt(ClusterCount));
        int rows = Mathf.CeilToInt((float)ClusterCount / cols);

        // Compute cell sizes and atlas dimensions
        int cellWidth = 0;
        int cellHeight = 0;
        foreach (var tex in textures) {
            cellWidth = Mathf.Max(cellWidth, tex.width);
            cellHeight = Mathf.Max(cellHeight, tex.height);
        }
        int atlasWidth = Mathf.NextPowerOfTwo(cellWidth * cols);
        int atlasHeight = Mathf.NextPowerOfTwo(cellHeight * rows);

        // Prepare placement data arrays
        int[] placementCols = new int[ClusterCount];
        int[] placementRows = new int[ClusterCount];
        int[] placementOffsetX = new int[ClusterCount];
        int[] placementOffsetY = new int[ClusterCount];
        Rect[] placementUV = new Rect[ClusterCount];

        // Create the atlas
        TextureFormat fmt = textures[0].format;
        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, fmt, false);

        // Fill atlas with normalized cluster data and record placements
        for (int i = 0; i < ClusterCount; i++) {
            int col = i % cols;
            int row = i / cols;
            int offsetX = col * cellWidth;
            int offsetY = row * cellHeight;

            placementCols[i] = col;
            placementRows[i] = row;
            placementOffsetX[i] = offsetX;
            placementOffsetY[i] = offsetY;
            placementUV[i] = new Rect((float)offsetX / atlasWidth, (float)offsetY / atlasHeight, (float)textures[i].width / atlasWidth, (float)textures[i].height / atlasHeight);

            Texture2D tex = textures[i];
            Color[] src = tex.GetPixels();
            Color[] dst = new Color[src.Length];
            Vector3 size = maxBounds[i] - minBounds[i];

            for (int p = 0; p < src.Length; p++) {
                Vector3 pN = new Vector3(src[p].r, src[p].g, src[p].b);
                Vector3 vLocal = new Vector3(
                    pN.x * size.x + minBounds[i].x,
                    pN.y * size.y + minBounds[i].y,
                    pN.z * size.z + minBounds[i].z
                );
                Vector3 pGlobal = new Vector3(
                    (vLocal.x - globalMin.x) / globalSize.x,
                    (vLocal.y - globalMin.y) / globalSize.y,
                    (vLocal.z - globalMin.z) / globalSize.z
                );
                dst[p] = new Color(pGlobal.x, pGlobal.y, pGlobal.z, src[p].a);
            }

            atlas.SetPixels(offsetX, offsetY, tex.width, tex.height, dst);
        }
        atlas.Apply();

        // Summary log of placements
        Debug.Log("[BakeAtlas] Cluster Atlas Placement Summary:");
        for (int i = 0; i < ClusterCount; i++) {
            Debug.Log($"  - Cluster {i+1}: Cell ({placementCols[i]}, {placementRows[i]}) Offset ({placementOffsetX[i]}, {placementOffsetY[i]}) UV [{placementUV[i].x:0.###},{placementUV[i].y:0.###},{placementUV[i].width:0.###},{placementUV[i].height:0.###}]");
        }

        // Ensure asset path
        if (!assetPath.EndsWith(".asset")) assetPath += ".asset";
        string dir = Path.GetDirectoryName(assetPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Save atlas asset
        AssetDatabase.CreateAsset(atlas, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = atlas;
        EditorUtility.FocusProjectWindow();
        Debug.Log($"[BakeAtlas] Atlas created at '{assetPath}' ({atlasWidth}x{atlasHeight})");
    }
}
