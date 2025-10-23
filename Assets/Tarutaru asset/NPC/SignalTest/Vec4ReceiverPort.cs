using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class Vector4SignalPort : MonoBehaviour, INotificationReceiver
{
    public Simulation4Portfolio sim;

    public void Awake()
    {
        if (sim is null)
            Debug.LogError("SimulationAsync is null fuck2");
    }
    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is Vector4SignalEmitter emitter)
        {
            Vector4 value = emitter.valueToSend;
            Debug.Log($"[Receiver] Received Vector4 from emitter: {value}");
            // ここで受信値を他に渡す処理など追加可能
            sim.setMusicEffect(value);
        }
    }
}
