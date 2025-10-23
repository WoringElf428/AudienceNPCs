using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class KuramotoParticleSystemAsync_fix : MonoBehaviour
{
    // Agent state struct
    struct AgentData
    {
        public float delay;
        public float naturalFrequency;
        public int sequence_index;
    }

    [Header("Kuramoto Settings")]
    public float eDelay = 0.125f;
    public float couplingStrength = 2f;
    public float tau = 0.01f;
    public float radius = 3f;
    public float updateInterval = 1.71428f;

    private float timer;
    private float R = 0f;
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    private int agentCount;
    private int maxNeighbors;

    // Double buffers for job-safe read/write
    private NativeArray<AgentData> agentData;
    private NativeArray<AgentData> newAgentData;

    private NativeArray<int> neighborCounts;
    private NativeArray<int> neighborIndices;
    private NativeArray<float> neighborDistances;
    private bool isInitialized = false;
    private List<Vector4> particleBuffer1;
    private List<Vector4> particlebuffer2;

    void LateUpdate()
    {
        //int i = 0;
        if (!isInitialized)
        {
            Initialize();
            StartCoroutine(UpdateAgentsAsync());

            isInitialized = true;
            Debug.Log(agentCount);
            return;
        }

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            UpdateCustomData();
            timer = 0f;
            StartCoroutine(UpdateAgentsAsync());
            string s = "";
            for (int i = 0; i < 5; i++)
            {
                s += i + ":" + particleBuffer1[i] + ": " +neighborCounts[i]+ "\n";
            }
            Debug.Log(s);
        }

        SendDelayData();
    }

    void Initialize()
    {
        ps = GetComponent<ParticleSystem>();
        int maxParticles = ps.main.maxParticles;
        particles = new ParticleSystem.Particle[maxParticles];

        agentCount = ps.GetParticles(particles);
        if (agentCount <= 0) return;

        agentData = new NativeArray<AgentData>(agentCount, Allocator.Persistent);
        newAgentData = new NativeArray<AgentData>(agentCount, Allocator.Persistent);

        maxNeighbors = agentCount;
        neighborCounts = new NativeArray<int>(agentCount, Allocator.Persistent);
        neighborIndices = new NativeArray<int>(agentCount * maxNeighbors, Allocator.Persistent);
        neighborDistances = new NativeArray<float>(agentCount * maxNeighbors, Allocator.Persistent);

        for (int i = 0; i < agentCount; i++)
        {
            //初期化パラメタ
            agentData[i] = new AgentData
            {
                //ガウス分布に変更
                delay = Mathf.Repeat(TarutaruUtil.GaussianRandom(2f * Mathf.PI * eDelay, 2f * Mathf.PI * 0.25f), 2f * Mathf.PI),

                naturalFrequency = UnityEngine.Random.Range(0f, 2 * Mathf.PI),

                sequence_index = (int)UnityEngine.Random.Range(0f,4f)
            };
            
            //隣人関係を事前計算-> audienceDataから生成
            int count = 0;
            for (int j = 0; j < agentCount; j++)
            {
                if (i == j) continue;
                float dist = Vector3.Distance(particles[i].position, particles[j].position);
                if (dist <= radius)
                {
                    int idx = i * maxNeighbors + count;
                    neighborIndices[idx] = j;
                    neighborDistances[idx] = dist;
                    count++;
                }
            }
            neighborCounts[i] = count;
        }

        particleBuffer1  = new List<Vector4>();
        particlebuffer2 = new List<Vector4>();
        for (int i = 0; i < agentCount; i++)
        {
            particleBuffer1.Add(new Vector4(0f, agentData[i].delay / (2 * Mathf.PI), agentData[i].delay / (2 * Mathf.PI), 0.3f +R));
            var offset = (agentData[i].sequence_index == 0) ? 0 : 1;
            float start_offset = (float)(agentData[i].sequence_index * 125 + offset) / 1200.0f;
            particlebuffer2.Add(new Vector4(start_offset,start_offset,0f, 0f));
        }
    }

    void UpdateCustomData(){
        if (agentCount <= 0 || particleBuffer1 == null) return;
        //tempでリストを確保するのはメモリ効率が悪い
        float[] tempY = new float[particleBuffer1.Count];
        for (int i = 0; i < particleBuffer1.Count; i++)
        {
            tempY[i] = particleBuffer1[i].y;
        }

        particleBuffer1.Clear();
        for (int i = 0; i < tempY.Length; i++)
        { 
            particleBuffer1.Add(new Vector4(tempY[i], agentData[i].delay / (2 * Mathf.PI), agentData[i].delay / (2 * Mathf.PI), 0.3f +R));
        }     
    }

    void SendDelayData()
    {
        ps.SetCustomParticleData(particleBuffer1, ParticleSystemCustomData.Custom1);
        ps.SetCustomParticleData(particlebuffer2, ParticleSystemCustomData.Custom2);

    }

    // Calculate and log the global order parameter R and mean phase ψ
    public void PrintOrderParameter()
    {
        if (agentCount <= 0) return;

        float sumC = 0f, sumS = 0f;
        for (int i = 0; i < agentCount; i++)
        {
            float theta = agentData[i].delay;
            sumC += Mathf.Cos(theta);
            sumS += Mathf.Sin(theta);
        }
        R = Mathf.Sqrt(sumC * sumC + sumS * sumS) / agentCount;
        float psy = Mathf.Atan2(sumS / agentCount, sumC / agentCount);
        Debug.Log($"[Order] R={R:F4}, ψ={psy * Mathf.Rad2Deg * 0.5f / Mathf.PI:F2} (0-1)");
    }
    IEnumerator UpdateAgentsAsync()
    {
        var job = new KuramotoJob
        {
            inputData = agentData,
            outputData = newAgentData,
            neighborCounts = neighborCounts,
            neighborIndices = neighborIndices,
            neighborDistances = neighborDistances,
            maxNeighbors = maxNeighbors,
            couplingStrength = couplingStrength,
            tau = tau,
            dt = Time.deltaTime
        };

        JobHandle handle = job.Schedule(agentCount, 64);
        yield return new WaitUntil(() => handle.IsCompleted);
        handle.Complete();

        // swap read/write buffers
        var tmp = agentData;
        agentData = newAgentData;
        newAgentData = tmp;

        PrintOrderParameter();
    }
    void OnDestroy()
    {
        if (agentData.IsCreated) agentData.Dispose();
        if (newAgentData.IsCreated) newAgentData.Dispose();
        if (neighborCounts.IsCreated) neighborCounts.Dispose();
        if (neighborIndices.IsCreated) neighborIndices.Dispose();
        if (neighborDistances.IsCreated) neighborDistances.Dispose();
    }

    // Job: reads only from inputData, writes only to outputData
    struct KuramotoJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AgentData> inputData;
        [WriteOnly]public NativeArray<AgentData> outputData;
        [ReadOnly] public NativeArray<int> neighborCounts;
        [ReadOnly] public NativeArray<int> neighborIndices;
        [ReadOnly] public NativeArray<float> neighborDistances;
        public int maxNeighbors;
        public float couplingStrength;
        public float tau;
        public float dt;

        public void Execute(int i)
        {
            var info = inputData[i];
            float dTheta = info.naturalFrequency;
            float myPhase = info.delay;

            for (int k = 0; k < neighborCounts[i]; k++)
            {
                int idx = i * maxNeighbors + k;
                int ni = neighborIndices[idx];
                float dist = neighborDistances[idx];
                float kv = couplingStrength * math.exp(-tau * dist);
                dTheta -= kv * math.sin(myPhase - inputData[ni].delay);
            }

            info.delay = (myPhase + dTheta * dt) % (2 * Mathf.PI);
            outputData[i] = info;
        }
    }
}