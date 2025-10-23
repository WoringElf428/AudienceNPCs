using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PSvisualizer : MonoBehaviour
{
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    // Custom Data streams
    // Custom1 (Vector, 1 component) を RandomDelay に使用
    // Custom2 (Color) を PenLightColor に使用
    private List<Vector4> delayData = new List<Vector4>();
    private List<Vector4> colorData = new List<Vector4>();

    private bool initialized = false;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void LateUpdate()
    {
        //if (initialized) return;

        // 現在の生存粒子を取得
        int maxParticles = ps.main.maxParticles;
        if (particles == null || particles.Length < maxParticles)
            particles = new ParticleSystem.Particle[maxParticles];

        int count = ps.GetParticles(particles);
        if (count <= 0) return;

        // リスト初期化
        delayData.Clear();
        colorData.Clear();
        delayData.Capacity = count;
        colorData.Capacity = count;

        // 各パーティクルごとにカスタムデータを設定
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = particles[i].position;
            // x座標が正なら delay=0, 赤  / 負なら delay=0.5, 青
            float delay = pos.x > 0f ? 0f : 0.5f;
            Color col = pos.x > 0f ? Color.red : Color.blue;

            // Custom1: Vector4( delay, 0, 0, 0 )
            delayData.Add(new Vector4(delay, 0f, 0f, 0f));
            // Custom2: Color(r,g,b,a)
            colorData.Add(new Vector4(col.r, col.g, col.b, col.a));
        }

        // スクリプトからカスタムデータを渡す
        ps.SetCustomParticleData(delayData, ParticleSystemCustomData.Custom1);
        ps.SetCustomParticleData(colorData, ParticleSystemCustomData.Custom2);

        initialized = true;
    }
}
