using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class PrintParameter
{
    public List<float> orderRHistory = new List<float>();
    public List<float> musicHistory   = new List<float>();
    public List<float> timeHistory = new List<float>();
    public List<float> clusterA_Histrory = new List<float>();
    public List<float> clusterB_Histrory = new List<float>();
    public List<float> clusterC_Histrory = new List<float>();
    public List<float> clusterD_Histrory = new List<float>();


    public string dirPath = "Assets/Tarutaru asset/NPC/Kuramoto simulation/Simulation Log";
    public void SaveOrderRCsv()
    {
        if (orderRHistory.Count == 0) return;

        // ファイル名に現在の日時を追加して、ファイルが上書きされないようにする
        string timestamp = DateTime.Now.ToString("MMdd_HHmm");
        string fileName = $"orderR_{timestamp}.csv";
        string path = Path.Combine(dirPath, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("Time,Order parameter, A,B,C,D, music");
        for (int i = 0; i < orderRHistory.Count; i++)
        {
            sb.AppendLine($"{timeHistory[i]:F4},{orderRHistory[i]:F6},{clusterA_Histrory[i]:F6},{clusterB_Histrory[i]:F6},{clusterC_Histrory[i]:F6},{clusterD_Histrory[i]:F6},{musicHistory[i]:F6}");
        }

        // ディレクトリが存在しない場合は作成する
        Directory.CreateDirectory(dirPath);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[OrderR CSV] {path}");
    }
}