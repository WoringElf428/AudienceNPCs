using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;

public class KuramotoSystemAsync : MonoBehaviour
{
    public List<KuramotoAgent> agents = new List<KuramotoAgent>();
    public float couplingStrength = 0.5f;
    public float tau = 0.05f;
    public float radius = 3f;
    public float updateInterval = 2f;

    private float timer;

    private NativeArray<Vector3> positions;
    private NativeArray<float> frequencies;
    private NativeArray<int> neighborCounts;
    private NativeArray<int> neighborIndices; // 一次元化された隣人リスト
    private NativeArray<float> neighborDistances; 
    private int maxNeighbors;
    private int agentCount;

    void Start()
    {
        agentCount = agents.Count;
        positions = new NativeArray<Vector3>(agentCount, Allocator.Persistent);
        frequencies = new NativeArray<float>(agentCount, Allocator.Persistent);

        for (int i = 0; i < agentCount; i++)
        {
            agents[i].Initialize();
            positions[i] = agents[i].transform.position;
            frequencies[i] = agents[i]._naturalFrequency;
        }

        // 隣人関係を構築
        PrecomputeNeighbors();
    }

    void PrecomputeNeighbors()
    {
        maxNeighbors = agentCount;
        neighborCounts = new NativeArray<int>(agentCount, Allocator.Persistent);
        neighborIndices = new NativeArray<int>(agentCount * maxNeighbors, Allocator.Persistent);    //最大サイズに合わせてメモリ確保。この実装だと疎な関係では効率が悪いためmaxNeighborsで調整したほうが良い
        neighborDistances = new NativeArray<float>(agentCount * maxNeighbors, Allocator.Persistent);

        for (int i = 0; i < agentCount; i++)
        {
            int count = 0;
            Vector3 p = positions[i];
            for (int j = 0; j < agentCount; j++)
            {
                if (i == j) continue;
                float dist = Vector3.Distance(p, positions[j]);
                if (dist <= radius)
                {
                    int index = i * maxNeighbors + count;
                    neighborIndices[index] = j;
                    neighborDistances[index] = dist;
                    count++;
                }
            }
            neighborCounts[i] = count;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            StartCoroutine(UpdateAgentsAsync());
            PrintOrderParameter();
        }
    }

    /// <summary>
    /// 蔵本モデルの秩序変数 R（同期度）と平均位相 ψ を計算してログ出力する
    /// </summary>
    public void PrintOrderParameter()
    {
        if (agents.Count == 0) return;

        float sumCos = 0f;
        float sumSin = 0f;

        foreach (var agent in agents)
        {
            float theta = agent._phaseOffset;
            sumCos += Mathf.Cos(theta);
            sumSin += Mathf.Sin(theta);
        }

        float R = Mathf.Sqrt(sumCos * sumCos + sumSin * sumSin) / agents.Count;
        float psi = Mathf.Atan2(sumSin/agents.Count, sumCos/agents.Count);  // ラジアン

        Debug.Log($"[Order Parameter] R = {R:F4}, ψ = {psi * Mathf.Rad2Deg:F2}°");
    }

    /// <summary>
    /// 距離を考慮した局所蔵本モデル
    /// </summary>
    /// <returns></returns>
    IEnumerator UpdateAgentsAsync()
    {
        NativeArray<float> phases = new NativeArray<float>(agentCount, Allocator.TempJob);
        NativeArray<float> dThetas = new NativeArray<float>(agentCount, Allocator.TempJob);

        for (int i = 0; i < agentCount; i++)
        {
            phases[i] = agents[i]._phaseOffset;
        }

        var job = new KuramotoJob
        {
            phases = phases,
            frequencies = frequencies,
            dThetas = dThetas,
            couplingStrength = couplingStrength,
            tau = tau,
            neighborCounts = neighborCounts,
            neighborIndices = neighborIndices,
            neighborDistances = neighborDistances,
            maxNeighbors = maxNeighbors
        };

        JobHandle handle = job.Schedule(agentCount, 64);
        yield return new WaitUntil(() => handle.IsCompleted);
        handle.Complete();

        float dt = Time.deltaTime;
        for (int i = 0; i < agentCount; i++)
        {
            agents[i].UpdatePhase(dThetas[i], dt);
            agents[i].ApplyVisuals();
        }

        phases.Dispose();
        dThetas.Dispose();
    }

    void OnDestroy()
    {
        if (positions.IsCreated) positions.Dispose();
        if (frequencies.IsCreated) frequencies.Dispose();
        if (neighborCounts.IsCreated) neighborCounts.Dispose();
        if (neighborIndices.IsCreated) neighborIndices.Dispose();
        if (neighborDistances.IsCreated) neighborDistances.Dispose();
    }

    struct KuramotoJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> phases;
        [ReadOnly] public NativeArray<float> frequencies;
        [WriteOnly] public NativeArray<float> dThetas;

        [ReadOnly] public NativeArray<int> neighborCounts;
        [ReadOnly] public NativeArray<int> neighborIndices;
        [ReadOnly] public NativeArray<float> neighborDistances;
        public int maxNeighbors;

        public float couplingStrength;
        public float tau;

        public void Execute(int index)
        {
            float dTheta = frequencies[index];
            float myPhase = phases[index];

            int count = neighborCounts[index];
            for (int k = 0; k < count; k++)
            {
                int flatIndex = index * maxNeighbors + k;
                int neighborIndex = neighborIndices[flatIndex];
                float dist = neighborDistances[flatIndex];

                float kv = couplingStrength * math.exp(-tau * dist);
                float phaseDiff = myPhase - phases[neighborIndex];
                dTheta -= kv * math.sin(phaseDiff);
            }

            dThetas[index] = dTheta;
        }
    }

    /// <summary>
    /// ノードが全結合だと仮定した高速化バージョン
    /// </summary>
    IEnumerator UpdateAgentsGlobalAsync()
    {
        NativeArray<float> phases = new NativeArray<float>(agentCount, Allocator.TempJob);
        NativeArray<float> dThetas = new NativeArray<float>(agentCount, Allocator.TempJob);

        float sumCos = 0f;
        float sumSin = 0f;

        for (int i = 0; i < agentCount; i++)
        {
            phases[i] = agents[i]._phaseOffset;
            sumCos += Mathf.Cos(phases[i]);
            sumSin += Mathf.Sin(phases[i]);
        }

        float R = Mathf.Sqrt(sumCos * sumCos + sumSin * sumSin) / agentCount;
        float psi = Mathf.Atan2(sumSin, sumCos);

        var job = new KuramotoGlobalJob
        {
            phases = phases,
            frequencies = frequencies,
            dThetas = dThetas,
            couplingStrength = couplingStrength,
            R = R,
            psi = psi,
            N = agentCount
        };

        JobHandle handle = job.Schedule(agentCount, 64);
        yield return new WaitUntil(() => handle.IsCompleted);
        handle.Complete();

        float dt = Time.deltaTime;
        for (int i = 0; i < agentCount; i++)
        {
            agents[i].UpdatePhase(dThetas[i], dt);
            agents[i].ApplyVisuals();
        }

        phases.Dispose();
        dThetas.Dispose();
    }
 
    struct KuramotoGlobalJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> phases;
        [ReadOnly] public NativeArray<float> frequencies;
        [WriteOnly] public NativeArray<float> dThetas;

        public float couplingStrength;
        public float R;
        public float psi;
        public int N;

        public void Execute(int index)
        {
            float theta = phases[index];
            float dTheta = frequencies[index] - couplingStrength * N * R * math.sin(theta - psi);
            dThetas[index] = dTheta;
        }
    }

}