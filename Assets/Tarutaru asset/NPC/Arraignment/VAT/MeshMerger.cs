#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MeshMerger : MonoBehaviour
{
    [MenuItem("Tools/Merge SkinnedMeshRenderers")]
    static void MergeMeshes()
    {
        GameObject targetObject = Selection.activeGameObject;
        if (targetObject == null)
        {
            Debug.LogError("No GameObject selected!");
            return;
        }

        var skinnedMeshRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        var meshRenderers = targetObject.GetComponentsInChildren<MeshRenderer>();
        var filters = targetObject.GetComponentsInChildren<MeshFilter>();

        if (skinnedMeshRenderers.Length == 0 && meshRenderers.Length == 0)
        {
            Debug.LogError("No MeshRenderer or SkinnedMeshRenderer found!");
            return;
        }

        // 統合するボーンリスト
        List<Transform> bones = new List<Transform>();
        List<BoneWeight> boneWeights = new List<BoneWeight>();
        List<CombineInstance> combineInstances = new List<CombineInstance>();
        Dictionary<string, int> boneIndexMap = new Dictionary<string, int>();
        List<Material> materials = new List<Material>();
        List<Texture2D> textures = new List<Texture2D>();

        //ここおかしい。SkeltonのBoneから取得するべき
        Transform rootBone = skinnedMeshRenderers.FirstOrDefault()?.rootBone;

        Debug.Log($"rootBone: {rootBone.name}");
        Debug.Log($"---Bone info---");
        foreach (var smr in skinnedMeshRenderers)
        { 
            Debug.Log($"{smr.name} : vert {smr.sharedMesh.vertexCount}, boneW {smr.sharedMesh.boneWeights.Length}");
        }

            // SkinnedMeshRenderer 統合処理
        foreach (var smr in skinnedMeshRenderers)
        {
            Mesh mesh = smr.sharedMesh;
            int boneOffset = bones.Count;

            // ボーン情報を追加
            foreach (Transform bone in smr.bones)
            {
                if (!boneIndexMap.ContainsKey(bone.name))
                {
                    boneIndexMap[bone.name] = bones.Count;
                    bones.Add(bone);
                }
            }

            // BoneWeight を変換
            foreach (BoneWeight bw in mesh.boneWeights)
            {
                BoneWeight newBw = bw;
                newBw.boneIndex0 = Mathf.Clamp(newBw.boneIndex0 + boneOffset, 0, bones.Count - 1);
                newBw.boneIndex1 = Mathf.Clamp(newBw.boneIndex1 + boneOffset, 0, bones.Count - 1);
                newBw.boneIndex2 = Mathf.Clamp(newBw.boneIndex2 + boneOffset, 0, bones.Count - 1);
                newBw.boneIndex3 = Mathf.Clamp(newBw.boneIndex3 + boneOffset, 0, bones.Count - 1);
                boneWeights.Add(newBw);
            }

            // メッシュ統合
            CombineInstance ci = new CombineInstance();
            ci.mesh = mesh;
            ci.transform = smr.transform.localToWorldMatrix;
            combineInstances.Add(ci);
            materials.Add(smr.sharedMaterial);
            textures.Add((Texture2D)smr.sharedMaterial.mainTexture);
        }

        // 静的 MeshRenderer も統合
        foreach (var mf in filters)
        {
            Mesh mesh = mf.sharedMesh;
            CombineInstance ci = new CombineInstance();
            ci.mesh = mesh;
            ci.transform = mf.transform.localToWorldMatrix;
            combineInstances.Add(ci);
            materials.Add(mf.GetComponent<MeshRenderer>().sharedMaterial);
            textures.Add((Texture2D)mf.GetComponent<MeshRenderer>().sharedMaterial.mainTexture);

            // staticなBone用のBoneWeight の追加(デフォルト)
            foreach(var v in mesh.vertices)
                boneWeights.Add(new BoneWeight());
        }

        // 統合メッシュの生成
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

        if (boneWeights.Count != combinedMesh.vertexCount)
        {
            Debug.LogWarning($"BoneWeights count mismatch. (BW:{boneWeights.Count}, VC:{combinedMesh.vertexCount})Adjusting.");
            while (boneWeights.Count < combinedMesh.vertexCount)
            {
                boneWeights.Add(new BoneWeight());
            }
        }
        combinedMesh.boneWeights = boneWeights.ToArray();
        combinedMesh.bindposes = bones.Select(b => b.worldToLocalMatrix * rootBone.localToWorldMatrix).ToArray();

        // マテリアル統合 (Texture Atlas の作成)
        Texture2D atlas = new Texture2D(2048, 2048);
        Rect[] rects = atlas.PackTextures(textures.ToArray(), 2, 2048);
        Material newMaterial = new Material(Shader.Find("Standard"));
        newMaterial.mainTexture = atlas;

        // UV 座標の変更
        Vector2[] originalUVs = combinedMesh.uv;
        Vector2[] newUVs = new Vector2[originalUVs.Length];
        int rectIndex = 0;
        for (int i = 0; i < newUVs.Length; i++)
        {
            newUVs[i].x = Mathf.Lerp(rects[rectIndex].xMin, rects[rectIndex].xMax, originalUVs[i].x);
            newUVs[i].y = Mathf.Lerp(rects[rectIndex].yMin, rects[rectIndex].yMax, originalUVs[i].y);
        }
        combinedMesh.uv = newUVs;

        // 新しい SkinnedMeshRenderer の作成
        GameObject mergedObject = new GameObject(targetObject.name + "_Merged");
        SkinnedMeshRenderer newSmr = mergedObject.AddComponent<SkinnedMeshRenderer>();
        newSmr.sharedMesh = combinedMesh;
        newSmr.bones = bones.ToArray();
        newSmr.rootBone = rootBone;
        newSmr.sharedMaterial = newMaterial;

        // 結果を保存
        AssetDatabase.CreateAsset(combinedMesh, "Assets/MergedMesh.asset");
        AssetDatabase.CreateAsset(newMaterial, "Assets/MergedMaterial.mat");
        AssetDatabase.SaveAssets();

        Debug.Log("Mesh merging completed successfully!");
    }

    [MenuItem("Tools/Merge SkinnedMeshRenderers (Optimized)")]
    static void MergeMeshesOptimized()
    {
        GameObject targetObject = Selection.activeGameObject;
        if (targetObject == null)
        {
            Debug.LogError("No GameObject selected!");
            return;
        }

        // 子階層から全ての SkinnedMeshRenderer と MeshRenderer を取得
        var skinnedMeshRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        var meshRenderers = targetObject.GetComponentsInChildren<MeshRenderer>();

        if (skinnedMeshRenderers.Length == 0 && meshRenderers.Length == 0)
        {
            Debug.LogError("No MeshRenderer or SkinnedMeshRenderer found!");
            return;
        }

        Debug.Log($"---Skinned Mesh info---");
        foreach (var smr in skinnedMeshRenderers)
        {
            Debug.Log($"{smr.name} : vert {smr.sharedMesh.vertexCount}, boneW {smr.sharedMesh.boneWeights.Length}");
        }
        Debug.Log($"---Mesh info---");
        foreach (var mr in meshRenderers)
        {
            var mf = mr.GetComponent<MeshFilter>();
            Debug.Log($"{mf.sharedMesh.name} : vert {mf.sharedMesh.vertexCount}, boneW {mf.sharedMesh.boneWeights.Length}");
        }

        // ボーン、ボーンウェイト、結合用メッシュリストを準備
        List<Transform> bones = new List<Transform>();
        List<BoneWeight> boneWeights = new List<BoneWeight>();
        List<CombineInstance> combineInstances = new List<CombineInstance>();

        // ボーン参照→統合後インデックスのマップ
        Dictionary<Transform, int> boneIndexMap = new Dictionary<Transform, int>();

        // 統合後に使用するルートボーンを決定（最初のSMRのrootBone。なければ対象オブジェクトのTransform）
        Transform rootBone = null;
        if (skinnedMeshRenderers.Length > 0)
            rootBone = skinnedMeshRenderers[0].rootBone;
        if (rootBone == null)
            rootBone = targetObject.transform;

        // 1. SkinnedMeshRendererの統合処理
        foreach (var smr in skinnedMeshRenderers)
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null) continue;
            // このSMRのボーン配列から、統合後ボーン配列へのインデックス対応表を作成
            int[] boneIndexRemap = new int[smr.bones.Length];
            for (int j = 0; j < smr.bones.Length; j++)
            {
                Transform bone = smr.bones[j];
                if (bone == null) continue;
                if (!boneIndexMap.ContainsKey(bone))
                {
                    // 新しいボーンとして追加
                    boneIndexMap[bone] = bones.Count;
                    bones.Add(bone);
                }
                // マッピング（元ボーン配列j番 -> 統合後インデックス）
                boneIndexRemap[j] = boneIndexMap[bone];

            }
            // BoneWeightのボーンインデックスを変換して追加
            foreach (BoneWeight bw in mesh.boneWeights)
            {
                BoneWeight newBW = new BoneWeight();
                newBW.boneIndex0 = boneIndexRemap[bw.boneIndex0];
                newBW.boneIndex1 = boneIndexRemap[bw.boneIndex1];
                newBW.boneIndex2 = boneIndexRemap[bw.boneIndex2];
                newBW.boneIndex3 = boneIndexRemap[bw.boneIndex3];
                newBW.weight0 = bw.weight0;
                newBW.weight1 = bw.weight1;
                newBW.weight2 = bw.weight2;
                newBW.weight3 = bw.weight3;
                boneWeights.Add(newBW);
            }

            // メッシュを結合リストに追加（各サブメッシュごとに追加して後でマテリアル順を揃える）
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                CombineInstance ci = new CombineInstance();
                ci.mesh = mesh;                  // 同一のmeshを参照
                ci.subMeshIndex = sub;          // サブメッシュindexのみ指定
                ci.transform = smr.transform.localToWorldMatrix;
                combineInstances.Add(ci);
                
            // ローカル→ワールド変換行列を適用
            Debug.LogWarning($"vert count ({smr.name} done): ({combineInstances.Last().mesh.vertexCount}");
            }
        }

        // 2. 静的 MeshRendererの統合処理
        foreach (var mr in meshRenderers)
        {
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh mesh = mf.sharedMesh;
            // 静的メッシュ用のボーン（このオブジェクト自身のTransform）を追加
            Transform staticBone = mr.gameObject.transform;
            if (!boneIndexMap.ContainsKey(staticBone))
            {
                boneIndexMap[staticBone] = bones.Count;
                bones.Add(staticBone);
            }

            int staticBoneIndex = boneIndexMap[staticBone];
            // このメッシュの全頂点に対し、staticBoneへのウェイト1.0のBoneWeightを作成
            for (int v = 0; v < mesh.vertexCount; v++)
            {
                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = staticBoneIndex;
                bw.weight0 = 1.0f;
                // 他のボーンウェイトは0のまま
                boneWeights.Add(bw);
            }
            
            // 静的メッシュもCombineリストへ（サブメッシュ単位で追加）
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                CombineInstance ci = new CombineInstance();
                ci.mesh = mesh;
                ci.subMeshIndex = sub;
                ci.transform = mr.transform.localToWorldMatrix;
                combineInstances.Add(ci);
            }

            Debug.LogWarning($"vert count ({mr.name} done): ({combineInstances.Last().mesh.vertexCount}");
        }
        
        // 3. メッシュの結合（サブメッシュは統合せずそのまま、行列適用あり）
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combineInstances.ToArray(), mergeSubMeshes: false, useMatrices: true);

        // BoneWeight数と頂点数が一致しない場合の調整（念のため）
        if (boneWeights.Count != combinedMesh.vertexCount)
        {
            Debug.LogWarning($"BoneWeights count ({boneWeights.Count}) != vertex count ({combinedMesh.vertexCount}), adjusting...");
            while (boneWeights.Count < combinedMesh.vertexCount)
            {
                boneWeights.Add(new BoneWeight());
            }
        }

        // 4. 未使用ボーンの削除: 実際に参照されているボーンインデックスを収集
        HashSet<int> usedBones = new HashSet<int>();
        foreach (var bw in boneWeights)
        {
            if (bw.weight0 > 0) usedBones.Add(bw.boneIndex0);
            if (bw.weight1 > 0) usedBones.Add(bw.boneIndex1);
            if (bw.weight2 > 0) usedBones.Add(bw.boneIndex2);
            if (bw.weight3 > 0) usedBones.Add(bw.boneIndex3);
        }
        // rootBoneはウェイト0でも残す
        if (rootBone != null && boneIndexMap.ContainsKey(rootBone))
            usedBones.Add(boneIndexMap[rootBone]);

        // 使用ボーンだけの新リストを作成し、インデックスマップを再構築
        List<Transform> finalBones = new List<Transform>();
        int[] indexMap = new int[bones.Count];
        for (int i = 0; i < bones.Count; i++)
        {
            if (usedBones.Contains(i))
            {
                indexMap[i] = finalBones.Count;
                finalBones.Add(bones[i]);
            }
            else
            {
                indexMap[i] = -1;
            }
        }
        // BoneWeightのボーンインデックスを詰め直し
        for (int i = 0; i < boneWeights.Count; i++)
        {
            BoneWeight bw = boneWeights[i];
            bw.boneIndex0 = indexMap[bw.boneIndex0];
            bw.boneIndex1 = indexMap[bw.boneIndex1];
            bw.boneIndex2 = indexMap[bw.boneIndex2];
            bw.boneIndex3 = indexMap[bw.boneIndex3];
            boneWeights[i] = bw;
        }

        // 5. 結合メッシュにBoneWeightとbindposeを適用
        combinedMesh.boneWeights = boneWeights.ToArray();
        // bindpose計算（rootBone基準）
        Matrix4x4 rootMatrix = rootBone != null ? rootBone.localToWorldMatrix : targetObject.transform.localToWorldMatrix;
        combinedMesh.bindposes = finalBones.Select(b => b.worldToLocalMatrix * rootMatrix).ToArray();

        // 6. マテリアル統合とUV調整
        // 元のレンダラーからマテリアルを順に取得（CombineInstanceと対応する順序）
        List<Material> sourceMaterials = new List<Material>();
        foreach (var smr in skinnedMeshRenderers)
            sourceMaterials.AddRange(smr.sharedMaterials);
        foreach (var mr in meshRenderers)
            sourceMaterials.AddRange(mr.sharedMaterials);

        Material combinedMaterial;
        if (sourceMaterials.Distinct().Count() > 1)
        {
            // 複数マテリアル → テクスチャをアトラス化
            Texture2D atlas = new Texture2D(2048, 2048);
            Texture2D[] texArray = sourceMaterials.Select(mat =>
                mat.mainTexture as Texture2D ?? Texture2D.whiteTexture).ToArray();
            Rect[] rects = atlas.PackTextures(texArray, 2, 2048);
            combinedMaterial = new Material(Shader.Find("Standard"));
            combinedMaterial.mainTexture = atlas;
            // UV再計算：各サブメッシュの頂点UVを、それぞれのテクスチャが配置されたrect領域にスケーリング
            Vector2[] origUVs = combinedMesh.uv;
            Vector2[] newUVs = new Vector2[origUVs.Length];
            int vertexOffset = 0;
            for (int subMeshIndex = 0; subMeshIndex < combinedMesh.subMeshCount; subMeshIndex++)
            {
                Rect r = rects[subMeshIndex];
                // サブメッシュの頂点範囲を取得
                int startIndex = vertexOffset;
                int count = combinedMesh.GetSubMesh(subMeshIndex).vertexCount;
                for (int k = 0; k < count; k++)
                {
                    Vector2 uv = origUVs[startIndex + k];
                    // rect領域内にUVを再マップ
                    newUVs[startIndex + k] = new Vector2(
                        Mathf.Lerp(r.xMin, r.xMax, uv.x),
                        Mathf.Lerp(r.yMin, r.yMax, uv.y)
                    );
                }
                vertexOffset += count;
            }
            combinedMesh.uv = newUVs;
        }
        else
        {
            // マテリアルが1種類のみ
            combinedMaterial = new Material(sourceMaterials[0]);
        }

        // 7. 新しいGameObjectを作成し、統合メッシュを設定
        GameObject mergedObject = new GameObject(targetObject.name + "_Merged");
        SkinnedMeshRenderer newSmr = mergedObject.AddComponent<SkinnedMeshRenderer>();
        newSmr.sharedMesh = combinedMesh;
        newSmr.bones = finalBones.ToArray();
        newSmr.rootBone = rootBone;
        newSmr.sharedMaterial = combinedMaterial;

        // 8. アセットとして保存（必要に応じて）
        AssetDatabase.CreateAsset(combinedMesh, $"Assets/{targetObject.name}_MergedMesh.asset");
        AssetDatabase.CreateAsset(combinedMaterial, $"Assets/{targetObject.name}_MergedMat.mat");
        AssetDatabase.SaveAssets();

        Debug.Log("Mesh merging completed successfully!");
    }
}
#endif
