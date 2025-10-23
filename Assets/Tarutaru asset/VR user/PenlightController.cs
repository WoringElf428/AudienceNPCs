using UnityEngine;
using System.Collections.Generic; // Listを使用するために必要
using System.IO;                // ファイル操作のために必要
using System.Text;              // StringBuilderを使用するために必要

public struct ControllerDataPoint
{
    public float timestamp;
    public string controllerType;
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 angularVelocity;

    public string ToCsvLine()
    {
        return $"{timestamp},{controllerType}," +
               $"{position.x},{position.y},{position.z}," +
               $"{velocity.x},{velocity.y},{velocity.z}," +
               $"{angularVelocity.x},{angularVelocity.y},{angularVelocity.z}";
    }
}


public class PenlightController : MonoBehaviour
{
    public enum ControllerType { Left, Right }
    [SerializeField]
    private ControllerType controllerType;

    [Header("Penlight Settings")]
    [SerializeField]
    private Renderer lightTipRenderer;
    [SerializeField]
    private float weakIntensity = 1.0f;
    [SerializeField]
    private float strongIntensity = 5.0f;
    
    [Header("Logger Settings")]
    [Tooltip("ログの保存先ディレクトリ。空の場合はデフォルトパス。")]
    [SerializeField]
    private string saveDirectory = "";

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private MaterialPropertyBlock propBlock;
    private bool isStrong = false;
    private OVRInput.Controller ovrController;

    private List<ControllerDataPoint> dataPoints = new List<ControllerDataPoint>();
    private float logTimer = 0f;
    private const float LOG_INTERVAL = 1.0f; // ログを記録する間隔（秒）


    void Start()
    {
        propBlock = new MaterialPropertyBlock();
        ovrController = (controllerType == ControllerType.Left) ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        lastPosition = OVRInput.GetLocalControllerPosition(ovrController);
        lastRotation = OVRInput.GetLocalControllerRotation(ovrController);
        UpdateIntensity();
    }

    void Update()
    {
        HandleLightControl();
        
        // --- ログ記録の頻度を制御 ---
        logTimer += Time.deltaTime;
        if (logTimer >= LOG_INTERVAL)
        {
            logTimer -= LOG_INTERVAL; // タイマーをリセット
            RecordMovementData();     // ログ記録処理を呼び出す
        }
    }
    
    // アプリケーション終了時に自動で呼ばれ、ファイルに書き出す
    private void OnApplicationQuit()
    {
        if (dataPoints.Count == 0) return;

        // 保存パスを決定
        string finalSavePath;
        if (!string.IsNullOrEmpty(saveDirectory))
        {
            finalSavePath = saveDirectory;
            try
            {
                if (!Directory.Exists(finalSavePath)) Directory.CreateDirectory(finalSavePath);
            }
            catch { finalSavePath = Application.persistentDataPath; }
        }
        else
        {
            finalSavePath = Application.persistentDataPath;
        }

        string side = controllerType.ToString(); // "Right" または "Left"
        string timestamp = System.DateTime.Now.ToString("MMdd_HHmm");
        string fileName = $"ControllerLog_{side}_{timestamp}.csv";
        string fullFilePath = Path.Combine(finalSavePath, fileName);
        
        // CSVヘッダーと内容を作成
        StringBuilder csvBuilder = new StringBuilder();
        string csvHeader = "Timestamp,Controller,PosX,PosY,PosZ,VelX,VelY,VelZ,AngVelX,AngVelY,AngVelZ";
        csvBuilder.AppendLine(csvHeader);
        foreach (var dataPoint in dataPoints)
        {
            csvBuilder.AppendLine(dataPoint.ToCsvLine());
        }

        // ファイルに書き込み
        try
        {
            File.WriteAllText(fullFilePath, csvBuilder.ToString());
            Debug.Log($"{side} コントローラーログを保存しました: {fullFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CSVファイルの保存に失敗しました: {e.Message}");
        }
    }


    // ペンライトの色と強度の制御 (変更なし)
    private void HandleLightControl()
    {
        Vector2 stickInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, ovrController);
        if (stickInput.sqrMagnitude > 0.1f)
        {
            float angleRad = Mathf.Atan2(stickInput.y, stickInput.x);
            float hueValue = (angleRad * Mathf.Rad2Deg + 180f) / 360f;
            SetMaterialFloat("_Hue", hueValue); 
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, ovrController))
        {
            isStrong = !isStrong;
            UpdateIntensity();
        }
    }

    private void RecordMovementData()
    {
        Vector3 currentPosition = OVRInput.GetLocalControllerPosition(ovrController);
        Quaternion currentRotation = OVRInput.GetLocalControllerRotation(ovrController);
        Vector3 velocity = (currentPosition - lastPosition) / Time.deltaTime;
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastRotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        angle *= Mathf.Deg2Rad;
        Vector3 angularVelocity = axis * angle / Time.deltaTime;

        ControllerDataPoint data = new ControllerDataPoint
        {
            timestamp = Time.time,
            controllerType = controllerType.ToString(),
            position = currentPosition,
            velocity = velocity,
            angularVelocity = angularVelocity
        };
        dataPoints.Add(data);

        lastPosition = currentPosition;
        lastRotation = currentRotation;
    }

    // 強度を更新しマテリアルに適用 (変更なし)
    void UpdateIntensity()
    {
        float targetIntensity = isStrong ? strongIntensity : weakIntensity;
        SetMaterialFloat("_Intensity", targetIntensity);
    }

    void SetMaterialFloat(string propertyName, float value)
    {
        if (lightTipRenderer != null)
        {
            lightTipRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(propertyName, value);
            lightTipRenderer.SetPropertyBlock(propBlock);
        }
    }
}