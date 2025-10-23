using UnityEngine;
public class KuramotoAgent : MonoBehaviour
{
    struct AgentData
    {
        public float phaseOffset;
        public float naturalFrequency;
    }

     [Header("Kuramoto Simulation Settings")]
    public float _phaseOffset;
    public float _naturalFrequency;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _materialPropertyBlock;
    private Animator _animator;
    
    private void Awake()
    {
        _renderer = GetComponentInChildren<MeshRenderer>();
        _materialPropertyBlock = new MaterialPropertyBlock();
        _animator = GetComponentInChildren<Animator>();
        //Initialize();
    }

    //デバック
    // public void Update()
    // {
    //     ApplyVisuals();
    // }
    public void Initialize()
    {
        _phaseOffset = Random.Range(0f, 2 * Mathf.PI);
        _naturalFrequency = Random.Range(0.8f, 1.2f);
        ApplyVisuals();
    }

    public void UpdatePhase(float delta, float dt)
    {
        _phaseOffset += delta * dt;
        _phaseOffset %= 2 * Mathf.PI;
    }
 
    public void ApplyVisuals()
    {
        float norPhase = _phaseOffset/(2 * Mathf.PI);
        if (_animator != null)
        {
            // 正規化された位相（0〜1）をAnimatorに渡す
            _animator.SetFloat("phase", norPhase);
        }
        
        _renderer.GetPropertyBlock(_materialPropertyBlock);
        //_materialPropertyBlock.SetFloat("_NorTime", (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime + _phaseOffset) % 1f);
        _materialPropertyBlock.SetFloat("_NorTime", Mathf.Clamp(norPhase % 1f,0.005f, 0.995f));

        _renderer.SetPropertyBlock(_materialPropertyBlock);
    }

}