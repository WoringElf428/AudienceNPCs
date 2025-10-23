using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oculus.Platform;

public class Simulation4Portfolio : MonoBehaviour
{
    // 蔵本モデルのエージェントデータ
    struct KuramotoAgent
    {
        public float delay;
        public float naturalFrequency;
    }


    [Header("蔵本同期モデル 設定")]
    [SerializeField] public AllNPCDataContainer allAudienceData; 
    public float e_Delay = 0f;
    public float couplingStrength = 2f;
    public float tau = 0.01f;
    public float radius = 3f;
    public float updateInterval = 1.71428f;

    [Header("出力ユニット")]
    [SerializeField] private ParticleSystem ps;
    private Material particleMaterial;

    [SerializeField] private GameObject near_npc; 
    private ParticleSystem.Particle[] particles;

    [Header("モーション選択関係")]
    [SerializeField] public float weight_music = 1.0f;
    [SerializeField] public bool singleMotion = false;
    private Vector4 probMusic;
    

    [Header("ログ出力")]
    [SerializeField] public bool isHistorySave = false;

    // Jobシステム用のNativeArray
    private NativeArray<KuramotoAgent> agentData;        // delay, naturalFrequencyを格納
    private NativeArray<KuramotoAgent> newAgentData;
    private NativeArray<int> neighborCounts;
    private NativeArray<int> neighborIndices;
    private NativeArray<float> neighborDistances;

    // NPC関連の内部データ
    private bool[] NPC_outputIsParticle;
    private int[] NPC_clusterIndex;           // 現在の動作クラスターインデックス
    private int[][] NPC_clipIndex;        // クラスタ内のモーションの選択
    private Vector4[] NPC_probPersonal;  // NPC毎のモーションの選択確率 (未使用だが定義あり)

    private int agentCount;
    private int maxNeighbors;
    private float timer;
    private float orderR = 0f;
    private bool onMusicEffect = false;

    private List<Vector4> particleBuffer;   //(X:前サイクルの遅延, Y:現サイクルの遅延, Z:ペンライト色インデックス, W:0.0f)
    private List<Vector4> particleBuffer2;  //X:前サイクルの動作, Y:現サイクルの動作, 0, 0,)
    PrintParameter printer;

    void Awake()
    {
        Initialize();
        StartCoroutine(UpdateAgentsAsync());
    }

    void LateUpdate()
    {
        timer += Time.deltaTime;

        // マテリアルのプロパティを更新する
        if (particleMaterial != null)
        {
            // 1. _ManualTimeの計算 (updateInterval周期で0から1に変化)
            float manualTime = timer / updateInterval;

            float interpolateValue = 0f;
            if (manualTime > 0.6f)
            {
                interpolateValue = (manualTime - 0.6f) / (1.0f - 0.6f);
            }
            interpolateValue = Mathf.Clamp01(interpolateValue);

            particleMaterial.SetFloat("_ManualTime", manualTime);
            particleMaterial.SetFloat("_Interpolate", interpolateValue);
        }
        if (timer >= updateInterval)
        {
            UpdateCustomData();    // パーティクルに送るデータを更新
            timer = 0f;
            //StartCoroutine(UpdateAgentsAsync()); // 非同期シミュレーションを開始
            UpdateOrderParameter(); // シミュレーション後に秩序パラメータを更新
            UpdateNPCMotion();
        }
        SendParticleBuffer(); // パーティクルシステムにデータを送信
    }

    void Initialize()
    {
        if (allAudienceData == null)
        {
            Debug.LogError("AllAudienceDataContainer is not assigned in the Inspector. Please assign the generated AllAudienceData.asset.");
            return;
        }

        //今だけ横着
        ps = GetComponent<ParticleSystem>();

        if (ps == null)
        {
            Debug.LogError("ParticleSystem is not assigned in the Inspector. Please assign a ParticleSystem component.");
            return;
        }
        var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleMaterial = particleRenderer.material;
        }
        else
        {
            Debug.LogError("ParticleSystemRenderer not found on the GameObject.");
        }

        agentCount = allAudienceData.audienceMembers.Count;
        if (agentCount <= 0)
        {
            Debug.LogWarning("No audience members found in AllAudienceDataContainer. Initialization aborted.");
            return;
        }

        agentData = new NativeArray<KuramotoAgent>(agentCount, Allocator.Persistent);
        newAgentData = new NativeArray<KuramotoAgent>(agentCount, Allocator.Persistent);

        maxNeighbors = allAudienceData.maxNeighbors;
        neighborCounts = new NativeArray<int>(agentCount, Allocator.Persistent);
        neighborIndices = new NativeArray<int>(agentCount * maxNeighbors, Allocator.Persistent);
        neighborDistances = new NativeArray<float>(agentCount * maxNeighbors, Allocator.Persistent);

        NPC_outputIsParticle = new bool[agentCount];
        NPC_clusterIndex = new int[agentCount];
        NPC_clipIndex = new int[agentCount][];
        NPC_probPersonal = new Vector4[agentCount];

        // パーティクルシステムの初期化
        if (ps.main.maxParticles < agentCount)
        {
            Debug.LogWarning($"ParticleSystem's Max Particles ({ps.main.maxParticles}) is less than agentCount ({agentCount}). Some agents may not have corresponding particles.");
            var main = ps.main;
            main.maxParticles = agentCount;
        }
        // isParticleがtrueのNPCの数をカウントし、それに対応するパーティクルを生成
        int particleEnabledCount = 0;
        foreach (var member in allAudienceData.audienceMembers)//これは後回し
        {
            if (member.isParticle)
            {
                particleEnabledCount++;
            }
        }

        particles = new ParticleSystem.Particle[particleEnabledCount]; // isParticleがtrueのNPC数分のパーティクル配列を準備
        particleBuffer = new List<Vector4>();
        particleBuffer2 = new List<Vector4>();

        int currentParticleIndex = 0;
        for (int i = 0; i < agentCount; i++)
        {
            agentData[i] = new KuramotoAgent
            {
                delay = Mathf.Repeat(TarutaruUtil.GaussianRandom(2f * Mathf.PI * e_Delay,  0.05f * Mathf.PI), 2f * Mathf.PI),
                naturalFrequency = UnityEngine.Random.Range(0.5f * Mathf.PI - 0.3f, 0.5f * Mathf.PI + 0.3f),
                //naturalFrequency = 0,
            };

            NPCStructData currentAudience = allAudienceData.audienceMembers[i];
            NPC_outputIsParticle[i] = currentAudience.isParticle;

            Vector4 v = new Vector4(0f, 0.5f, 0.5f, 0f);
            NPC_clusterIndex[i] = TarutaruUtil.RandomIndexfromVec4(v.normalized);
            NPC_clipIndex[i] = new int[4];
            for (int j = 0; j < 4; j++)
            {
                if (singleMotion)
                {
                    NPC_clipIndex[i][j] = 0;
                }
                else
                {
                    NPC_clipIndex[i][j] = currentAudience.clip_index[j] - 1;
                }
            }

            NPC_probPersonal[i] = Vector4.zero;

            // 隣人関係をAllAudienceDataContainerから生成
            neighborCounts[i] = currentAudience.neighborIndex.Count;
            for (int k = 0; k < neighborCounts[i]; k++)
            {
                int neighborRefIndex = currentAudience.neighborIndex[k];

                if (neighborRefIndex >= 0 && neighborRefIndex < allAudienceData.audienceMembers.Count)
                {
                    int idx = i * maxNeighbors + k;
                    neighborIndices[idx] = neighborRefIndex;
                    neighborDistances[idx] = currentAudience.neighborDist[k];
                }
                else
                {
                    Debug.LogError($"Invalid neighbor index found for agent {i}. Neighbor index {neighborRefIndex} is out of bounds (0-{allAudienceData.audienceMembers.Count - 1}). Skipping this neighbor.");
                }
            }

            if (currentAudience.isParticle)
            {
                if (currentParticleIndex < particles.Length)
                {
                    particles[currentParticleIndex].rotation3D = currentAudience.rotation.eulerAngles;
                    float randomScale = UnityEngine.Random.Range(0.8f, 1.0f);
                    particles[currentParticleIndex].startSize = randomScale; // x,y,z同じ値

                    particleBuffer.Add(new Vector4(0f, agentData[i].delay / (2f * Mathf.PI), 0f, 1.0f));
                    var pos = calcFramePos(NPC_clusterIndex[i], NPC_clipIndex[i][NPC_clusterIndex[i]]);
                    particleBuffer2.Add(new Vector4(0f, 0f, pos.x, pos.y));

                    currentParticleIndex++;
                }
                else
                {
                    Debug.LogWarning($"Attempted to create more particles than allocated in 'particles' array. Consider increasing 'particleEnabledCount' or 'ps.main.maxParticles'.");
                }
            }
        }
        printer = new PrintParameter();

        int[] count = new int[4];
        foreach (var i in NPC_clusterIndex)
        {
            count[i]++;
        }
        printer.clusterA_Histrory.Add((float)count[0] / agentCount);
        printer.clusterB_Histrory.Add((float)count[1] / agentCount);
        printer.clusterC_Histrory.Add((float)count[2] / agentCount);
        printer.clusterD_Histrory.Add((float)count[3] / agentCount);

        Debug.Log($"Initialized with {agentCount} agents, {particleEnabledCount} ({ps.main.maxParticles}) particles enabled.");
        Debug.Log($"A:{(float)count[0] / agentCount}%\nB:{(float)count[1] / agentCount}%\nC:{(float)count[2] / agentCount}%\nD:{(float)count[3] / agentCount}%");

    }

    // パーティクルに送るカスタムデータを更新
    void UpdateCustomData()
    {
        if (agentCount <= 0 || particleBuffer == null || particleBuffer2 == null) return;

        for (int i = 0; i < ps.particleCount; i++)
        {
            Vector4 currentDelayData = particleBuffer[i];
            currentDelayData.x = currentDelayData.y;
            currentDelayData.y = currentDelayData.y;
            currentDelayData.z = currentDelayData.y;
            currentDelayData.w = 0.3f + orderR;
            particleBuffer[i] = currentDelayData;

            Vector4 currentFramePos = particleBuffer2[i];
            currentFramePos.x = currentFramePos.z;
            currentFramePos.y = currentFramePos.w;
            var nextFrame = calcFramePos(NPC_clusterIndex[i], NPC_clipIndex[i][NPC_clusterIndex[i]]);
            currentFramePos.z = nextFrame.x;
            currentFramePos.w = nextFrame.y;
            particleBuffer2[i] = currentFramePos;
        }
        
        // Debug.Log($"{particleBuffer.Count+",,,"+ps.particleCount}");
        string ss = "";
        for (int i = 0; i < Mathf.Min(5, agentCount); i++) // agentCountの範囲でループ
        {
            ss += $"buf1 : {particleBuffer[i]}, buf2 = {particleBuffer2[i]} \n";
        }
        Debug.Log(ss);
    }

    // パーティクルシステムにデータを送信
    void SendParticleBuffer()
    {
        ps.SetCustomParticleData(particleBuffer, ParticleSystemCustomData.Custom1);
        ps.SetCustomParticleData(particleBuffer2, ParticleSystemCustomData.Custom2);
    }

    // 各エージェントのモーションインデックスを更新
    public void UpdateNPCMotion()
    {
        if (onMusicEffect)
        {
            for (int i = 0; i < agentCount; i++)
            {
                Vector4 totalProb = probMusic.normalized;
                NPC_clusterIndex[i] = TarutaruUtil.RandomIndexfromVec4(totalProb.normalized);
            }
            onMusicEffect = false;

            int[] count = new int[4];
            foreach (var i in NPC_clusterIndex)
            {
                count[i]++;
            }

            printer.clusterA_Histrory.Add((float)count[0] / agentCount);
            printer.clusterB_Histrory.Add((float)count[1] / agentCount);
            printer.clusterC_Histrory.Add((float)count[2] / agentCount);
            printer.clusterD_Histrory.Add((float)count[3] / agentCount);
            Debug.Log($"A:{(float)count[0] / agentCount}% | B:{(float)count[1] / agentCount}% | C:{(float)count[2] / agentCount}% | D:{(float)count[3] / agentCount}%");
        }
    }

    //マジックナンバーだらけできもすぎ。あとで治す。
    private Vector2 calcFramePos(int cluster, int clip)
    {
        //最初はwating_pose=>なっていない馬鹿垂れ
        int startFrame = 500 * cluster + 125 * clip;
        int endFrame = startFrame + 119;
        return new float2(startFrame / 2000f, endFrame / 2000f);
    }

    // 音楽からのモーション選択確率を更新
    public void setMusicEffect(Vector4 vec)
    {
        probMusic = vec;
        onMusicEffect = true;
        Debug.LogWarning("MUSIC");
    }

    public void UpdateOrderParameter()
    {
        if (agentCount <= 0) return;

        float sumC = 0f, sumS = 0f;
        for (int i = 0; i < agentCount; i++)
        {
            float theta = agentData[i].delay;
            sumC += Mathf.Cos(theta);
            sumS += Mathf.Sin(theta);
        }
        orderR = Mathf.Sqrt(sumC * sumC + sumS * sumS) / agentCount;
        float psy = Mathf.Atan2(sumS / agentCount, sumC / agentCount);
        Debug.Log($"[Order] R={orderR:F4}, ψ={psy * Mathf.Rad2Deg:F2}°");
        printer.orderRHistory.Add(orderR);
        printer.timeHistory.Add(Time.time);
    }

    // 非同期でエージェントの遅延を更新するコルーチン
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
            dt = 0.02f,
        };

        JobHandle handle = job.Schedule(agentCount, 64);
        yield return new WaitUntil(() => handle.IsCompleted);
        handle.Complete();

        // 読み書きバッファをスワップ
        var tmp = agentData;
        agentData = newAgentData;
        newAgentData = tmp;

        UpdateOrderParameter(); // シミュレーション後に秩序パラメータを更新
        UpdateNPCMotion();      // 各エージェントのモーションインデックスを更新
  
    }

    // スクリプト破棄時にNativeArrayを解放
    void OnDestroy()
    {
        if (agentData.IsCreated) agentData.Dispose();
        if (newAgentData.IsCreated) newAgentData.Dispose();
        if (neighborCounts.IsCreated) neighborCounts.Dispose();
        if (neighborIndices.IsCreated) neighborIndices.Dispose();
        if (neighborDistances.IsCreated) neighborDistances.Dispose();
        
        if (isHistorySave)
            printer.SaveOrderRCsv();
    }

    // 蔵本モデル計算用Job
    struct KuramotoJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<KuramotoAgent> inputData;
        [WriteOnly] public NativeArray<KuramotoAgent> outputData;
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