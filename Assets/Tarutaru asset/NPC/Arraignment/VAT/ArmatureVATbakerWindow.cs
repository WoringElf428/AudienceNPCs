using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

public class ArmatureVATbakerWindow : EditorWindow
{
    private GameObject targetObject;
    private List<AnimationClip> animationClips = new List<AnimationClip>();
    private int targetFrameRate = 60;
    public static string savePath = "Assets/Tarutaru asset/NPC/Arraignment/VAT baker/";

    [MenuItem("Tools/Armature VAT Baker fix")]
    private static void ShowWindow()
    {
        var window = GetWindow<ArmatureVATbakerWindow>("Armature VAT Baker");
        window.LoadCurrentStaticFields();
        window.Show();
    }

    private void LoadCurrentStaticFields()
    {
        targetFrameRate = ArmatureVATbaker.TargetFrameRate;
    }

    private void OnEnable()
    {
        if (animationClips == null)
            animationClips = new List<AnimationClip>();
    }

    private void OnGUI()
    {
        GUILayout.Label("Armature VAT Baker Settings", EditorStyles.boldLabel);

        targetFrameRate = EditorGUILayout.IntField("Target Frame Rate", targetFrameRate);
        EditorGUILayout.Space();
        GUILayout.Label("Save Path", EditorStyles.boldLabel);
        savePath = EditorGUILayout.TextField("Output Folder", savePath);

        if (GUILayout.Button("Select Folder"))
        {
            var selectedPath = EditorUtility.OpenFolderPanel("Select Save Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                savePath = selectedPath;
            }
        }

        if (GUILayout.Button("Apply to Static Fields"))
        {
            ArmatureVATbaker.TargetFrameRate = targetFrameRate;
            Debug.Log($"Applied new settings:\n" +
                       $"TargetFrameRate = {targetFrameRate}\n" +
                       $"SelectionPath = {savePath}");
        }

        EditorGUILayout.Space();
        GUILayout.Label("Target GameObject (with SkinnedMeshRenderer)", EditorStyles.boldLabel);
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("Animation Clips", EditorStyles.boldLabel);

        // Ensure list has at least one entry
        if (animationClips.Count == 0)
            animationClips.Add(null);

        // Render each clip field with remove button
        for (int i = 0; i < animationClips.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            animationClips[i] = (AnimationClip)EditorGUILayout.ObjectField($"Clip {i}", animationClips[i], typeof(AnimationClip), false);
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                animationClips.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        // Button to add new clip field
        if (GUILayout.Button("Add Clip"))
        {
            animationClips.Add(null);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate VAT Asset"))
        {
            // Update static settings
            ArmatureVATbaker.TargetFrameRate = targetFrameRate;

            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("Error", "No target GameObject assigned!", "OK");
                return;
            }

            var skinnedMeshRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            Debug.Log($"{skinnedMeshRenderers.Length}");

            var validClips = animationClips.Where(c => c != null).ToArray();
            if (validClips.Length < 1)
            {
                EditorUtility.DisplayDialog("Warning", "Select some animation clips.", "OK");
                return;
            }

            ArmatureVATbaker.GenerateVATFromArmature(targetObject, validClips, skinnedMeshRenderers.FirstOrDefault(), savePath);
        }
    }
}
