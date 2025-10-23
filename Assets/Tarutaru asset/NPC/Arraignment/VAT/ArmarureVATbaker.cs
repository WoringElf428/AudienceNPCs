#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public static class ArmatureVATbaker
{
    public static int TargetFrameRate = 60;
    public static int MotionDataMergin = 5;

    // Bake結果＋Boundsをまとめて返す構造体
    public struct AnimTexResult
    {
        public Texture2D posTex;
        public Texture2D norTex;
        public Vector3 minBounds;
        public Vector3 maxBounds;
    }

    public static void GenerateVATFromArmature(GameObject targetObject, IEnumerable<AnimationClip> clips, SkinnedMeshRenderer skinnedMeshRenderer, string selectionPath)
    {
        int totalFrame = clips.Aggregate(0, (sum, c) => sum + Mathf.CeilToInt(c.length * TargetFrameRate) + MotionDataMergin);
        var texSize = GetCalculatedTextureBoundary(skinnedMeshRenderer.sharedMesh.vertexCount * totalFrame, totalFrame);

        // 1) テクスチャ＋Bounds を生成
        var animTex = GenerateAnimationTexture(targetObject, clips, skinnedMeshRenderer, totalFrame, texSize);
        AssetDatabase.CreateAsset(animTex.posTex, $"{selectionPath}/{targetObject.name}_Position.asset");
        AssetDatabase.CreateAsset(animTex.norTex, $"{selectionPath}/{targetObject.name}_Normal.asset");

        // 2) VAT用静的メッシュ生成
        var vatMesh = GenerateUvBoneWeightedMesh(skinnedMeshRenderer, clips, totalFrame, texSize);
        AssetDatabase.CreateAsset(vatMesh, $"{selectionPath}/{targetObject.name}_Mesh.asset");

        // 3) マテリアル生成：BoundsMin/Max をセット
        var mat = GenerateMaterial(skinnedMeshRenderer, animTex.posTex, animTex.norTex, clips, totalFrame, animTex.minBounds, animTex.maxBounds);
        AssetDatabase.CreateAsset(mat, $"{selectionPath}/{targetObject.name}_Material.asset");

        // 4) シーン上にVATオブジェクトを生成
        GenerateMeshRendererObject(targetObject, vatMesh, mat, clips);

        AssetDatabase.SaveAssets();
    }

    static Mesh GenerateUvBoneWeightedMesh(SkinnedMeshRenderer smr, IEnumerable<AnimationClip> clips, int totalFrame, Vector2 textureSize)
    {
        var mesh = new Mesh();
        var animator = smr.GetComponentInParent<Animator>();

        // アニメーションクリップの最初のフレームでBakeMesh
        var firstClip = clips.FirstOrDefault();
        if (firstClip == null)
        {
            Debug.LogError("No animation clips found.");
            return null;
        }

        bool wasEnabled = animator.enabled;
        animator.enabled = false;

        firstClip.SampleAnimation(animator.gameObject, 0f);
        smr.BakeMesh(mesh);
        mesh.name = smr.name + "_BakedPose";

        animator.enabled = wasEnabled;

        // サブメッシュの統合
        if (mesh.subMeshCount > 1)
        {
            List<int> allTriangles = new List<int>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                allTriangles.AddRange(mesh.GetTriangles(i));
            }
            mesh.triangles = allTriangles.ToArray();
            mesh.subMeshCount = 1;
        }

        // VAT用UV1設定
        var segNum = (int)textureSize.x / totalFrame;
        var segWidth = (float)totalFrame / textureSize.x;
        var uv1 = new Vector4[mesh.vertexCount];

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            uv1[i] = new Vector4(
                segWidth * (i % segNum),
                (float)(i / segNum) / textureSize.y,
                segWidth * (i % segNum),
                (float)(i / segNum) / textureSize.y
            );
        }

        mesh.SetUVs(1, uv1);

        // 頂点カラー設定：元の sharedMesh からコピー
        var originalColors = smr.sharedMesh.colors;
        if (originalColors != null && originalColors.Length == mesh.vertexCount)
        {
            mesh.colors = originalColors;
            Debug.Log($"Copied vertex colors from original mesh ({originalColors.Length} colors)");
        }
        else
        {
            // fallback: 全頂点カラーを(0,0,0,0)に
            Color[] defaultColors = new Color[mesh.vertexCount];
            for (int i = 0; i < mesh.vertexCount; i++)
                defaultColors[i] = new Color(0, 0, 0, 0);
            mesh.colors = defaultColors;
            Debug.Log("Original mesh has no valid vertex colors. Defaulted to (0,0,0,0)");
        }

        Debug.Log($"Created VAT static mesh: {mesh.name}");
        return mesh;
    }

    /// <summary>
    /// VAT 用の座標・法線テクスチャを生成し、pos/nor テクスチャと
    /// 正規化に使った Bounds を返します。
    /// Root Motion とスケールキーを打ち消す処理を追加済み。
    /// </summary>
    static AnimTexResult GenerateAnimationTexture(GameObject targetObject, IEnumerable<AnimationClip> clips, SkinnedMeshRenderer smr, int totalFrame, Vector2 textureSize)
    {
        var mesh = new Mesh();
        var texturePos = new Texture2D((int)textureSize.x, (int)textureSize.y, TextureFormat.RGBAHalf, false, true);
        var pixelsPos = texturePos.GetPixels();
        var textureNor = new Texture2D((int)textureSize.x, (int)textureSize.y, TextureFormat.RGBAHalf, false, true);
        var pixelsNor = textureNor.GetPixels();

        int vertexCount = smr.sharedMesh.vertexCount;
        var clips_pos = new Color[vertexCount, totalFrame];
        var clips_nor = new Color[vertexCount, totalFrame];

        // 初期 Transform を記録
        Vector3 initPos = targetObject.transform.position;
        Quaternion initRot = targetObject.transform.rotation;
        Vector3 initScale = targetObject.transform.localScale;

        // バウンディングボックス集計
        Vector3 minB = Vector3.positiveInfinity, maxB = Vector3.negativeInfinity;
        foreach (var clip in clips)
        {
            Debug.Log($"processing:{clip.name}");
            int clipFrames = Mathf.CeilToInt(clip.length * TargetFrameRate);
            for (int f = 0; f <= clipFrames; f++)
            {
                float t = Mathf.Min(f / (float)TargetFrameRate, clip.length - 1e-4f);
                clip.SampleAnimation(targetObject, t);

                // スケールキーを打ち消す
                targetObject.transform.localScale = initScale;

                smr.BakeMesh(mesh);

                // Root Motion 差分
                Vector3 currPos = targetObject.transform.position;
                Quaternion currRot = targetObject.transform.rotation;
                Quaternion rotDiff = currRot * Quaternion.Inverse(initRot);
                Vector3 offsetLocal = Quaternion.Inverse(initRot) * (currPos - initPos);

                foreach (var v in mesh.vertices)
                {
                    Vector3 vRel = rotDiff * v + offsetLocal;
                    minB = Vector3.Min(minB, vRel);
                    maxB = Vector3.Max(maxB, vRel);
                }
            }
        }

        // 位置・法線テクスチャ生成
        int frameIdx = 0;
        foreach (var clip in clips)
        {
            int clipFrames = Mathf.CeilToInt(clip.length * TargetFrameRate);
            for (int f = 0; f < clipFrames; f++)
            {
                float t = Mathf.Min(f / (float)TargetFrameRate, clip.length - 1e-4f);
                clip.SampleAnimation(targetObject, t);

                // スケールキーを打ち消す
                targetObject.transform.localScale = initScale;

                smr.BakeMesh(mesh);

                // Root Motion 差分
                Vector3 currPos = targetObject.transform.position;
                Quaternion currRot = targetObject.transform.rotation;
                Quaternion rotDiff = currRot * Quaternion.Inverse(initRot);
                Vector3 offsetLocal = Quaternion.Inverse(initRot) * (currPos - initPos);

                var verts = mesh.vertices;
                var norms = mesh.normals;
                for (int i = 0; i < vertexCount; i++)
                {
                    Vector3 vRel = rotDiff * verts[i] + offsetLocal;
                    Vector3 nRel = rotDiff * norms[i];

                    // 正規化
                    Vector3 pN = new Vector3(
                        (vRel.x - minB.x) / (maxB.x - minB.x),
                        (vRel.y - minB.y) / (maxB.y - minB.y),
                        (vRel.z - minB.z) / (maxB.z - minB.z)
                    );
                    Vector3 nN = nRel.normalized * 0.5f + Vector3.one * 0.5f;

                    clips_pos[i, frameIdx] = new Color(pN.x, pN.y, pN.z, 1f);
                    clips_nor[i, frameIdx] = new Color(nN.x, nN.y, nN.z, 0f);
                }
                frameIdx++;
            }
            frameIdx += MotionDataMergin;
        }

        // 元の Transform に復帰
        targetObject.transform.SetPositionAndRotation(initPos, initRot);
        targetObject.transform.localScale = initScale;

        // テクスチャに書き込み
        int cursor = 0;
        int segNum = (int)textureSize.x / totalFrame;
        for (int i = 0; i < vertexCount; i++)
        {
            for (int j = 0; j < totalFrame; j++)
            {
                pixelsPos[cursor] = clips_pos[i, j];
                pixelsNor[cursor] = clips_nor[i, j];
                cursor++;
            }
            if ((i + 1) % segNum == 0)
                cursor += ((int)textureSize.x % totalFrame);
        }

        texturePos.SetPixels(pixelsPos);
        texturePos.Apply();
        texturePos.filterMode = FilterMode.Point;
        texturePos.wrapMode = TextureWrapMode.Clamp;

        textureNor.SetPixels(pixelsNor);
        textureNor.Apply();
        textureNor.filterMode = FilterMode.Point;
        textureNor.wrapMode = TextureWrapMode.Clamp;

        return new AnimTexResult
        {
            posTex = texturePos,
            norTex = textureNor,
            minBounds = minB,
            maxBounds = maxB
        };
    }

    /// <summary>
    /// マテリアル生成。BoundsMin／BoundsMax をシェーダーに渡します。
    /// </summary>
    static Material GenerateMaterial(SkinnedMeshRenderer smr, Texture posTex, Texture norTex, IEnumerable<AnimationClip> clips, int totalFrame, Vector3 minBounds, Vector3 maxBounds)
    {
        var mat = Object.Instantiate(smr.sharedMaterial);
        mat.shader = Shader.Find("ShirayuriMeshibe/URP/MeshAudience(Non Particle)");
        mat.SetTexture("_BaseMap", smr.sharedMaterial.mainTexture);
        mat.SetTexture("_PositionTexture", posTex);
        mat.SetTexture("_NormalTexture", norTex);
        mat.SetFloat("_Speed", 0.5f);
        mat.SetInt("_Framecount", totalFrame);
        mat.SetVector("_BoundsMin", new Vector4(minBounds.x, minBounds.y, minBounds.z, 0));
        mat.SetVector("_BoundsMax", new Vector4(maxBounds.x, maxBounds.y, maxBounds.z, 0));

        int idx = 0;
        float start = 0;
        float texsize = 1.0f / totalFrame;
        int offset = 0;
        Debug.Log("UV offset");
        foreach (var clip in clips)
        {
            int clipLen = Mathf.CeilToInt(clip.length * TargetFrameRate);
            var uv = new Vector4(start + offset, start + clipLen - 1, clipLen, MotionDataMergin) * texsize;
            mat.SetVector($"_Anime{idx}", uv);
            start += clipLen + MotionDataMergin;
            idx++;
            offset = 1;
            Debug.Log($"{uv.x}f, {uv.y}f");
        }
        for (; idx < 5; idx++)
            mat.SetVector($"_Anime{idx}", Vector4.zero);

        mat.enableInstancing = true;
        return mat;
    }

    /// <summary>
    /// VAT メッシュを描画する GameObject を生成
    /// </summary>
    static void GenerateMeshRendererObject(GameObject targetObject, Mesh mesh, Material material, IEnumerable<AnimationClip> clips)
    {
        // 初期 Transform を再取得（GenerateAnimationTexture で復帰済）
        Vector3 initPos = targetObject.transform.position;
        Quaternion initRot = targetObject.transform.rotation;

        var go = new GameObject(targetObject.name + "_VAT");
        go.transform.SetPositionAndRotation(initPos, initRot);
        go.transform.localScale = Vector3.one;

        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.lightProbeUsage = LightProbeUsage.Off;
    }

    /// <summary>
    /// 必要ピクセル数とセグメント長から最適なテクスチャ幅/高さを計算
    /// </summary>
    private static Vector2 GetCalculatedTextureBoundary(int totalPixels, int segmentSize)
    {
        int w = 1, h = 1;
        while ((w - w % segmentSize) * h < totalPixels)
        {
            if (w <= h) w <<= 1;
            else h <<= 1;
        }
        return new Vector2(w, h);
    }
}
#endif