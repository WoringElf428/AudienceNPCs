using UnityEngine;
using System.Linq;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

public class AudienceMotionAssignment : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject rig;

    [Tooltip("出力/入力 AllNPCData.asset のパス")]
    public string AllNPCDataAssetPath = "Assets/Tarutaru asset/NPC/Arraignment/NPCData/AllNPCData.asset";

    [SerializeField] private float penaltyFactor = 0.5f; // インスペクタから設定可能なペナルティ係数

#if UNITY_EDITOR
    private AllNPCDataContainer LoadNPCDataContainer()
    {
        var allNPCDataContainer = AssetDatabase.LoadAssetAtPath<AllNPCDataContainer>(AllNPCDataAssetPath);
        if (allNPCDataContainer == null)
        {
            Debug.LogError($"AllNPCDataContainer not found at path: {AllNPCDataAssetPath}. Please ensure the asset exists.");
        }
        return allNPCDataContainer;
    }

    [ContextMenu("Assign Clip to NPC with FMdistance")]
    private void AssignClipFMDsitance()
    {
        var allNPCDataContainer = LoadNPCDataContainer();
        if (allNPCDataContainer == null) return;

        if (animator == null)
        {
            Debug.LogError("Animator not assigned.");
            return;
        }
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            Debug.LogError("runtimeAnimatorController is not a valid AnimatorController.");
            return;
        }

        List<NPCStructData> npcMembers = allNPCDataContainer.audienceMembers;

        for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
        {
            Debug.Log($"Processing Layer: {controller.layers[layerIndex].name} (Index: {layerIndex})");

            var clips = new List<AnimationClip>();
            var sm = controller.layers[layerIndex].stateMachine;
            foreach (var child in sm.states)
            {
                if (child.state.motion is AnimationClip clip)
                    clips.Add(clip);
            }
            int clipCount = clips.Count;
            if (clipCount == 0)
            {
                Debug.LogWarning($"No AnimationClips found in layer {layerIndex} state machine. Skipping this layer.");
                continue;
            }

            var matrix = new float[clipCount, clipCount];
            string clip_log = $"Motion Distance for Layer {layerIndex}:\n";
            for (int i = 0; i < clipCount; i++)
            {
                for (int j = i; j < clipCount; j++)
                {
                    // MotionDistanceCalculator は rig を必要とする
                    float dist = MotionDistanceCalculator.CalculateFMPartDistances(rig, clips[i], clips[j])[0];
                    matrix[i, j] = dist;
                    matrix[j, i] = dist;
                    clip_log += $"{clips[i].name}-{clips[j].name} > {dist}\n";
                }
            }
            Debug.Log(clip_log);

            int[] assignedCount = new int[clipCount];
            float avg_dist = 0f;

            for (int idx = 0; idx < npcMembers.Count; idx++)
            {
                NPCStructData currentNPC = npcMembers[idx]; // structなのでコピーされる

                // clip_indexのサイズチェックとリサイズ
                if (currentNPC.clip_index == null || currentNPC.clip_index.Length <= layerIndex)
                {
                    Debug.LogWarning($"NPCStructData at index {idx} has clip_index array too small for layer {layerIndex}. Resizing it.");
                    int[] newClipIndex = new int[layerIndex + 1];
                    if (currentNPC.clip_index != null)
                    {
                        System.Array.Copy(currentNPC.clip_index, newClipIndex, currentNPC.clip_index.Length);
                    }
                    currentNPC.clip_index = newClipIndex;
                }

                float[] scores = new float[clipCount];
                bool hasNeighborAssigned = false;

                //Debug.Log(currentNPC.neighborIndex.Count); // Modified

                for (int i = 0; i < currentNPC.neighborIndex.Count; i++) // Modified
                {
                    int nIdx = currentNPC.neighborIndex[i]; // Modified
                    if (nIdx >= 0 && nIdx < idx)
                    {
                        // 既に割り当て済みの隣人のクリップインデックスを取得
                        // npcMembers[nIdx] の clip_index を参照
                        if (nIdx < npcMembers.Count)
                        {
                            // NPCStructDataはstructなので、npcMembers[nIdx]はコピーを返す。
                            // しかし、ここではそのコピーのclip_indexを参照するだけで良い。
                            int neighborClip = npcMembers[nIdx].clip_index[layerIndex] - 1;
                            if (neighborClip >= 0 && neighborClip < clipCount)
                            {
                                hasNeighborAssigned = true;
                                for (int k = 0; k < clipCount; k++)
                                    scores[k] += matrix[k, neighborClip];
                            }
                        }
                    }
                }

                int chosen = 0;
                if (!hasNeighborAssigned)
                {
                    chosen = Random.Range(0, clipCount);
                }
                else
                {
                    float bestScore = float.MinValue;
                    for (int k = 0; k < clipCount; k++)
                    {
                        float adjScore = scores[k] - penaltyFactor * assignedCount[k];
                        if (adjScore > bestScore)
                        {
                            bestScore = adjScore;
                            chosen = k;
                        }
                    }
                    avg_dist += bestScore;
                }

                currentNPC.clip_index[layerIndex] = chosen + 1; // 1-based index
                assignedCount[chosen]++;

                npcMembers[idx] = currentNPC; // 変更をリストに反映
            }

            var log = $"Assigned for Layer {layerIndex} ({controller.layers[layerIndex].name}):\n";
            for (int i = 0; i < clipCount; i++)
                log += $"{clips[i].name} : {assignedCount[i]}\n";
            log += $"avg dist for Layer {layerIndex} :{avg_dist / npcMembers.Count}";
            Debug.Log(log);
        }

        // 処理完了後、ScriptableObjectを保存
        EditorUtility.SetDirty(allNPCDataContainer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"AllNPCData.asset updated successfully at {AllNPCDataAssetPath}");
    }

    [ContextMenu("Assign Clip (Weighted Random by FM-distance)")]
    private void AssignClipWeightedRandom()
    {
        var allNPCDataContainer = LoadNPCDataContainer();
        if (allNPCDataContainer == null) return;

        if (animator == null) { Debug.LogError("Animator not assigned."); return; }
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null) { Debug.LogError("Invalid AnimatorController."); return; }

        List<NPCStructData> npcMembers = allNPCDataContainer.audienceMembers;

        for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
        {
            Debug.Log($"Processing Weighted Random for Layer: {controller.layers[layerIndex].name} (Index: {layerIndex})");

            var clips = new List<AnimationClip>();
            var sm = controller.layers[layerIndex].stateMachine;
            foreach (var cs in sm.states)
                if (cs.state.motion is AnimationClip clip)
                    clips.Add(clip);
            int clipCount = clips.Count;
            if (clipCount == 0)
            {
                Debug.LogWarning($"No clips found in layer {layerIndex}. Skipping.");
                continue;
            }

            var matrix = new float[clipCount, clipCount];
            for (int i = 0; i < clipCount; i++)
            {
                for (int j = i; j < clipCount; j++)
                {
                    float d = MotionDistanceCalculator.CalculateFMPartDistances(rig, clips[i], clips[j])[0];
                    matrix[i, j] = matrix[j, i] = d;
                }
            }

            int[] assignedCount = new int[clipCount];

            for (int idx = 0; idx < npcMembers.Count; idx++)
            {
                NPCStructData currentNPC = npcMembers[idx]; // structなのでコピーされる

                // clip_indexのサイズチェックとリサイズ
                if (currentNPC.clip_index == null || currentNPC.clip_index.Length <= layerIndex)
                {
                    Debug.LogWarning($"NPCStructData at index {idx} has clip_index array too small for layer {layerIndex}. Resizing it.");
                    int[] newClipIndex = new int[layerIndex + 1];
                    if (currentNPC.clip_index != null)
                    {
                        System.Array.Copy(currentNPC.clip_index, newClipIndex, currentNPC.clip_index.Length);
                    }
                    currentNPC.clip_index = newClipIndex;
                }

                var scores = new float[clipCount];
                bool hasNeighbor = false;
                for (int i = 0; i < currentNPC.neighborIndex.Count; i++) // Modified
                {
                    int nIdx = currentNPC.neighborIndex[i]; // Modified
                    if (nIdx >= 0 && nIdx < idx)
                    {
                        if (nIdx < npcMembers.Count)
                        {
                            int nc = npcMembers[nIdx].clip_index[layerIndex] - 1;
                            if (nc >= 0 && nc < clipCount)
                            {
                                hasNeighbor = true;
                                for (int k = 0; k < clipCount; k++)
                                    scores[k] += matrix[k, nc];
                            }
                        }
                    }
                }

                int chosen;
                if (!hasNeighbor)
                {
                    chosen = Random.Range(0, clipCount);
                }
                else
                {
                    float total = 0f;
                    for (int k = 0; k < clipCount; k++)
                    {
                        float penalizedScore = Mathf.Max(0f, scores[k] - penaltyFactor * assignedCount[k]);
                        total += penalizedScore;
                    }

                    float r = Random.value * total;
                    float acc = 0f;
                    chosen = 0;
                    for (int k = 0; k < clipCount; k++)
                    {
                        float penalizedScore = Mathf.Max(0f, scores[k] - penaltyFactor * assignedCount[k]);
                        acc += penalizedScore;
                        if (r <= acc)
                        {
                            chosen = k;
                            break;
                        }
                    }
                }

                currentNPC.clip_index[layerIndex] = chosen + 1;
                assignedCount[chosen]++;

                npcMembers[idx] = currentNPC; // 変更をリストに反映
            }

            var log = $"Weighted Random Assigned for Layer {layerIndex} ({controller.layers[layerIndex].name}):\n";
            for (int i = 0; i < clipCount; i++)
                log += $"{clips[i].name} : {assignedCount[i]}\n";
            Debug.Log(log);
        }

        EditorUtility.SetDirty(allNPCDataContainer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"AllNPCData.asset updated successfully at {AllNPCDataAssetPath}");
    }

    [ContextMenu("Assign Clip (Completely Random)")]
    private void AssignRandomClip()
    {
        var allNPCDataContainer = LoadNPCDataContainer();
        if (allNPCDataContainer == null) return;

        if (animator == null)
        {
            Debug.LogError("Animator not assigned.");
            return;
        }
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            Debug.LogError("runtimeAnimatorController is not a valid AnimatorController.");
            return;
        }

        List<NPCStructData> npcMembers = allNPCDataContainer.audienceMembers;

        for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
        {
            Debug.Log($"Processing Completely Random for Layer: {controller.layers[layerIndex].name} (Index: {layerIndex})");

            var clips = new List<AnimationClip>();
            var sm = controller.layers[layerIndex].stateMachine;
            foreach (var state in sm.states)
                if (state.state.motion is AnimationClip clip)
                    clips.Add(clip);

            int clipCount = clips.Count;
            if (clipCount == 0)
            {
                Debug.LogWarning($"No AnimationClips found in layer {layerIndex} state machine. Skipping.");
                continue;
            }

            var assignedCount = new int[clipCount];

            for (int idx = 0; idx < npcMembers.Count; idx++)
            {
                NPCStructData currentNPC = npcMembers[idx]; // structなのでコピーされる

                // clip_indexのサイズチェックとリサイズ
                if (currentNPC.clip_index == null || currentNPC.clip_index.Length <= layerIndex)
                {
                    Debug.LogWarning($"NPCStructData at index {idx} has clip_index array too small for layer {layerIndex}. Resizing it.");
                    int[] newClipIndex = new int[layerIndex + 1];
                    if (currentNPC.clip_index != null)
                    {
                        System.Array.Copy(currentNPC.clip_index, newClipIndex, currentNPC.clip_index.Length);
                    }
                    currentNPC.clip_index = newClipIndex;
                }

                int chosen = Random.Range(0, clipCount);
                currentNPC.clip_index[layerIndex] = chosen + 1;
                assignedCount[chosen]++;

                npcMembers[idx] = currentNPC; // 変更をリストに反映
            }

            string log = $"Random Assigned for Layer {layerIndex} ({controller.layers[layerIndex].name}):\n";
            for (int i = 0; i < clipCount; i++)
                log += $"{clips[i].name} : {assignedCount[i]}\n";
            Debug.Log(log);
        }

        EditorUtility.SetDirty(allNPCDataContainer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"AllNPCData.asset updated successfully at {AllNPCDataAssetPath}");
    }
#endif
}