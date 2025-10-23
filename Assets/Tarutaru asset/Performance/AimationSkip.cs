using UnityEngine;

public class AnimationControl : MonoBehaviour
{
    [SerializeField] private Animator performer;

    private const float skipSeconds = 130.29f;
    void Awake()
    {
        if (performer == null)
        {
            Debug.LogError("Animator 'performer' is not assigned in AnimationControl. Application will now quit.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // エディタ内ではプレイを止める
#else
            Application.Quit(); // ビルド時はアプリを終了
#endif
        }
    }

    public void Skip()
    {
        Debug.Log("Skipping");
        float crossfade = 0.5f;
        var info = performer.GetCurrentAnimatorStateInfo(0);
        float newNormalizedTime = info.normalizedTime + skipSeconds / info.length;
        // 修正ポイント:
        performer.CrossFade(info.fullPathHash, crossfade / info.length, 0, newNormalizedTime);
    }
}