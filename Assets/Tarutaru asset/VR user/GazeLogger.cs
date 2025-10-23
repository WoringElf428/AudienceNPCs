using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

// 視線データを格納するためのシンプルな構造体
public struct GazeDataPoint
{
    public float timestamp;
    public Vector3 headPosition;
    public Vector3 gazeDirection; // transform.forward

    public string ToCsvLine()
    {
        return $"{timestamp}," +
               $"{headPosition.x},{headPosition.y},{headPosition.z}," +
               $"{gazeDirection.x},{gazeDirection.y},{gazeDirection.z}";
    }
}

public class GazeLogger : MonoBehaviour
{
    [Header("Target Transform")]
    [Tooltip("OVRCameraRig内のCenterEyeAnchorのTransformをここに設定します。")]
    [SerializeField]
    private Transform centerEyeAnchorTransform;

    [Header("CSV Save Settings")]
    [Tooltip("視線ログの保存先ディレクトリ。空の場合はデフォルトパス。")]
    [SerializeField]
    private string saveDirectory = "";

    // --- ログ機能のための変数 ---
    private List<GazeDataPoint> gazeDataPoints = new List<GazeDataPoint>();
    private readonly string csvHeader = "Timestamp,HeadPosX,HeadPosY,HeadPosZ,GazeX,GazeY,GazeZ";
    
    // --- ★記録頻度を制御するための新しい変数 ---
    private float logTimer = 0f;
    private const float LOG_INTERVAL = 1.0f; // ログを記録する間隔（秒）


    void Start()
    {
        if (centerEyeAnchorTransform == null)
        {
            Debug.LogError("CenterEyeAnchorのTransformが設定されていません。GazeLoggerを無効にします。");
            this.enabled = false;
        }
    }

    void Update()
    {
        // --- ★タイマーを使って記録頻度を1秒に1回に制御 ---
        logTimer += Time.deltaTime;
        if (logTimer >= LOG_INTERVAL)
        {
            logTimer -= LOG_INTERVAL; // タイマーをリセット

            // 新しい視線データを作成してリストに追加
            gazeDataPoints.Add(new GazeDataPoint
            {
                timestamp = Time.time,
                headPosition = centerEyeAnchorTransform.position,
                gazeDirection = centerEyeAnchorTransform.forward
            });
        }
    }

    private void OnApplicationQuit()
    {
        SaveDataToCsv();
    }

    private void SaveDataToCsv()
    {
        if (gazeDataPoints.Count == 0) return;

        string finalSavePath;
        if (!string.IsNullOrEmpty(saveDirectory))
        {
            finalSavePath = saveDirectory;
            try
            {
                if (!Directory.Exists(finalSavePath))
                {
                    Directory.CreateDirectory(finalSavePath);
                }
            }
            catch { finalSavePath = Application.persistentDataPath; }
        }
        else
        {
            finalSavePath = Application.persistentDataPath;
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"GazeLog_{timestamp}.csv"; // 視線ログ専用のファイル名
        string fullFilePath = Path.Combine(finalSavePath, fileName);

        StringBuilder csvBuilder = new StringBuilder();
        csvBuilder.AppendLine(csvHeader);
        foreach (var dataPoint in gazeDataPoints)
        {
            csvBuilder.AppendLine(dataPoint.ToCsvLine());
        }

        try
        {
            File.WriteAllText(fullFilePath, csvBuilder.ToString());
            Debug.Log($"視線ログを保存しました: {fullFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"視線ログのCSVファイル保存に失敗しました: {e.Message}");
        }
    }
}