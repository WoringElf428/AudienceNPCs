using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oculus.Platform;
using UnityEditorInternal;
using System;

public class KuramotoSimulationAsync : MonoBehaviour
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
    private int[] NPC_clusterIndex;       // 現在の動作クラスターインデックス
    private int[][] NPC_clipIndex;          // クラスタ内のモーションの選択
    private Vector4[] NPC_probPersonal;     // NPC毎のモーションの選択確率 (未使用だが定義あり)

    private int agentCount;
    private int maxNeighbors;
    private float timer;
    private float orderR = 0f;
    int isTrans = -1;
    int countTrans = 0;
    [SerializeField] float transition = 0f;
    private List<Vector4> particleBuffer;   //(X:前サイクルの遅延, Y:現サイクルの遅延, Z:ペンライト色インデックス, W:0.0f)
    private List<Vector4> particleBuffer2;  //X:前サイクルの動作, Y:現サイクルの動作, 0, 0,)
    PrintParameter printer = new PrintParameter();

    void Awake()
    {
        Initialize();
        StartCoroutine(UpdateAgentsAsync());
    }
    void LateUpdate()
    {
        timer += Time.deltaTime;
        float manualTime = Mathf.Clamp01(timer / updateInterval);
        if (isTrans > 0)
        {
            if (countTrans % 2 == 0)
            {
                transition = 1.0f - manualTime;
            }
            else
            {
                transition = manualTime;
            }
        }
        else
        {
            if (countTrans % 2 == 0)
            {
                transition = 0f;
            }
            else
            {
                transition = 1f;
            }
        }

        if (particleMaterial != null)
        {
            particleMaterial.SetFloat("_ManualTime", manualTime);
            particleMaterial.SetFloat("_Transition", transition);

            // float interpolateValue = 0f;
            // if (manualTime > 0.6f)
            // {
            //     interpolateValue = (manualTime - 0.6f) / (1.0f - 0.6f);
            // }
            // interpolateValue = Mathf.Clamp01(interpolateValue);
            //particleMaterial.SetFloat("_Interpolate", interpolateValue);
        }
        if (timer >= updateInterval)
        {
            timer = 0f;
            isTrans *= -1;
            countTrans += Mathf.Max(0, isTrans);    //isTransがtrue(=1)の時だけ+1

            //UpdateCustomData();                     // パーティクルに送るデータを更新
            if (isTrans < 0)
            {
                StartCoroutine(UpdateAgentsAsync());
            }
            // デバッグログは必要に応じて残す
            // string s = "";
            // for (int i = 0; i < Mathf.Min(5, agentCount); i++) // agentCountの範囲でループ
            // {
            //     s += $"Agent {i}: Delay = {particleBuffer[i]}, ClusterIndex = {NPC_clusterIndex[i]}, CipIndex ={NPC_clipIndex[i][NPC_clusterIndex[i]]}\n";
            // }
            // Debug.Log(s);
        }
        SendParticleBuffer();   //出力系にデータを送信
    }
    void Initialize()
    {
        if (allAudienceData == null)
        {
            Debug.LogError("AllAudienceDataContainer is not assigned in the Inspector. Please assign the generated AllAudienceData.asset.");
            return;
        }

        agentCount = allAudienceData.audienceMembers.Count;
        if (agentCount <= 0)
        {
            Debug.LogWarning("No audience members found in AllAudienceDataContainer. Initialization aborted.");
            return;
        }

        //シミュレーション用変数の初期化
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

        for (int i = 0; i < agentCount; i++)
        {
            agentData[i] = new KuramotoAgent
            {
                delay = Mathf.Repeat(TarutaruUtil.GaussianRandom(2f * Mathf.PI * e_Delay, 0.6f * Mathf.PI), 2f * Mathf.PI),
                naturalFrequency = UnityEngine.Random.Range(0.25f * Mathf.PI - 0.1f, 0.25f * Mathf.PI + 0.1f),
                //naturalFrequency = 0,
            };

            NPCStructData currentAudience = allAudienceData.audienceMembers[i];
            NPC_outputIsParticle[i] = currentAudience.isParticle;

            NPC_clusterIndex[i] = (int)UnityEngine.Random.Range(0f, 4f);

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
                    neighborIndices[idx] = neighborRefIndex;    //結合が疎な場合メモリ使用量が大きくて無駄かも
                    neighborDistances[idx] = currentAudience.neighborDist[k];
                }
                else
                {
                    Debug.LogError($"Invalid neighbor index found for agent {i}. Neighbor index {neighborRefIndex} is out of bounds (0-{allAudienceData.audienceMembers.Count - 1}). Skipping this neighbor.");
                }
            }
        }

        // 出力系(ParticleSystem)の初期化
        //printer = new PrintParameter();

        UpdateOrderParameter();
        InitOutputs();

        //デバック用出力
        int[] count = new int[4];
        foreach (var i in NPC_clusterIndex)
        {
            count[i]++;
        }
        printer.clusterA_Histrory.Add((float)count[0] / agentCount);
        printer.clusterB_Histrory.Add((float)count[1] / agentCount);
        printer.clusterC_Histrory.Add((float)count[2] / agentCount);
        printer.clusterD_Histrory.Add((float)count[3] / agentCount);

        Debug.Log($"A:{(float)count[0] / agentCount}%\nB:{(float)count[1] / agentCount}%\nC:{(float)count[2] / agentCount}%\nD:{(float)count[3] / agentCount}%");

    }
    void InitOutputs()  //*出力系の初期化
    {
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

        // パーティクルシステムの初期化
        if (ps.main.maxParticles < agentCount)
        {
            Debug.LogWarning($"ParticleSystem's Max Particles ({ps.main.maxParticles}) is less than agentCount ({agentCount}). Some agents may not have corresponding particles.");
            var main = ps.main;
            main.maxParticles = agentCount;
        }

        // isParticleがtrueのNPCの数をカウントし、それに対応するパーティクルを生成
        int particleEnabledCount = 0;
        foreach (var member in allAudienceData.audienceMembers)
        {
            if (member.isParticle)
            {
                particleEnabledCount++;
            }
        }

        particles = new ParticleSystem.Particle[particleEnabledCount]; //isParticleがtrueのNPC数分のパーティクル配列を準備
        particleBuffer = new List<Vector4>();
        particleBuffer2 = new List<Vector4>();

        int currentParticleIndex = 0;
        for (int i = 0; i < agentCount; i++)
        {
            if (NPC_outputIsParticle[i])
            {
                if (currentParticleIndex < particles.Length)
                {
                    NPCStructData currentAudience = allAudienceData.audienceMembers[i];

                    particles[currentParticleIndex].rotation3D = currentAudience.rotation.eulerAngles;
                    float randomScale = UnityEngine.Random.Range(0.8f, 1.0f);
                    particles[currentParticleIndex].startSize = randomScale;
                    var pos = calcFramePos(NPC_clusterIndex[i], NPC_clipIndex[i][NPC_clusterIndex[i]]);

                    particleBuffer.Add(new Vector4(pos.x, agentData[i].delay / (2f * Mathf.PI), 0f, 0f));
                    particleBuffer2.Add(new Vector4(0f, agentData[i].delay / (2 * Mathf.PI),  0.3f + orderR, 0f));

                    currentParticleIndex++;
                }
                else
                {
                    Debug.LogWarning($"Attempted to create more particles than allocated in 'particles' array. Consider increasing 'particleEnabledCount' or 'ps.main.maxParticles'.");
                }
            }
        }

        Debug.Log($"Initialized with {agentCount} agents, {particleEnabledCount} ({ps.main.maxParticles}) particles enabled.");
    }
    void UpdateCustomData()         // *パーティクルに送るカスタムデータを更新
    {
        if (agentCount <= 0 || particleBuffer == null || particleBuffer2 == null) return;

        for (int i = 0; i < ps.particleCount; i++)
        {
            //Vector4 currentMotionData = particleBuffer[i];
            Vector4 currentFramePos = particleBuffer2[i];
            var nextFrame = calcFramePos(NPC_clusterIndex[i], NPC_clipIndex[i][NPC_clusterIndex[i]]);

            var vec = particleBuffer[i];
            if (countTrans % 2 == 1)
            {
                //xyにAgentData
                vec.x = nextFrame.x;                    //startPointA
                vec.y = agentData[i].delay / (2 * Mathf.PI);  //delayA
            }
            else
            {
                //zwにAgentData
                vec.z = nextFrame.x;                          //startPointB
                vec.w = agentData[i].delay / (2 * Mathf.PI);  //delayB
            }
            particleBuffer[i] = vec;

            currentFramePos.x = transition;                             //Transition A-B
            currentFramePos.y = agentData[i].delay / (2 * Mathf.PI);      //色相
            currentFramePos.z = 0.3f + orderR;                            //強度
            currentFramePos.w = nextFrame.y;
            particleBuffer2[i] = currentFramePos;
        }
    }
    void SendParticleBuffer()    // *パーティクルシステムにデータを送信
    {
        ps.SetCustomParticleData(particleBuffer, ParticleSystemCustomData.Custom1);
        ps.SetCustomParticleData(particleBuffer2, ParticleSystemCustomData.Custom2);
    }
    public void UpdateNPCMotion()    // 各エージェントのモーションインデックスを更新
    {
        //weight_music = 0;
        for (int i = 0; i < agentCount; i++)
        {
            //いったん難しいことはやめる
            var v = new Vector4(0, 0, 0, 0);
            v[NPC_clusterIndex[i]] = 0.1f;
            int[] n_count = new int[4];
            for (int j = 0; j < 4; j++)
            {
                n_count[j] = NPC_clusterIndex.Count(x => x == j);
            }
            Vector4 probNeighbor = new Vector4(n_count[0], n_count[1], n_count[2], n_count[3]);
            Vector4 totalProb = weight_music * probMusic.normalized + (1 - weight_music) * orderR * probNeighbor.normalized + (1 - weight_music) * (1 - orderR) * v.normalized;
            //Vector4 totalProb =  weight_music * probMusic.normalized  + (1 - weight_music) * v.normalized;
            if (i == 300)
                Debug.LogWarning($"Total: {totalProb.normalized} | Music: {probMusic.normalized} | Neighbors: {probNeighbor.normalized} | random: {v.normalized}");
            NPC_clusterIndex[i] = TarutaruUtil.RandomIndexfromVec4(totalProb.normalized);
        }
    }

    private Vector2 calcFramePos(int cluster, int clip)    //マジックナンバーだらけできもすぎ。あとで治す。
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
        printer.orderRHistory.Add(orderR);
        printer.timeHistory.Add(Time.time);
        printer.musicHistory.Add(weight_music);
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
            dt = 0.05f,
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
        UpdateCustomData();     // バッファー更新

        //debug系
        //Debug.Log($"[Order] R={orderR:F4}, ψ={psy * Mathf.Rad2Deg:F2}°");
        int[] count = new int[4];
        foreach (var i in NPC_clusterIndex)
        {
            count[i]++;
        }
        printer.clusterA_Histrory.Add((float)count[0] / agentCount);
        printer.clusterB_Histrory.Add((float)count[1] / agentCount);
        printer.clusterC_Histrory.Add((float)count[2] / agentCount);
        printer.clusterD_Histrory.Add((float)count[3] / agentCount);
        //Debug.Log($"A:{(float)count[0] / agentCount} | B:{(float)count[1] / agentCount} | C:{(float)count[2] / agentCount} | D:{(float)count[3] / agentCount}");
        // Debug.Log($"{particleBuffer.Count+",,,"+ps.particleCount}");
        string ss = "";
        for (int i = 0; i < Mathf.Min(5, agentCount); i++) // agentCountの範囲でループ
        {
            ss += $"buf1 : {particleBuffer[i]}, buf2 = {particleBuffer2[i]} \n";
        }
        //Debug.Log(ss);
        //Debug.Log($"Transition ({transition}) {countTrans}");
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

}