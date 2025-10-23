using UnityEngine;
using UnityEditor;

public class PrefabReplacer : EditorWindow
{
    [SerializeField]
    private GameObject sourcePrefab; // 置換元
    [SerializeField]
    private GameObject targetPrefab; // 置換先

    [MenuItem("Tools/Prefab Replacer")]
    static void OpenWindow()
    {
        GetWindow<PrefabReplacer>("Prefab Replacer");
    }

    private void OnGUI()
    {
        SerializedObject so = new SerializedObject(this);

        EditorGUILayout.PropertyField(so.FindProperty("sourcePrefab"), new GUIContent("置換元Prefab"));
        EditorGUILayout.PropertyField(so.FindProperty("targetPrefab"), new GUIContent("置換先Prefab"));

        so.ApplyModifiedProperties();

        if (GUILayout.Button("選択中のオブジェクトを置換"))
        {
            ReplaceSelected();
        }
    }

    private void ReplaceSelected()
    {
        if (sourcePrefab == null || targetPrefab == null)
        {
            Debug.LogError("置換元Prefabまたは置換先Prefabが設定されていません。");
            return;
        }

        foreach (GameObject selected in Selection.gameObjects)
        {
            // Prefabとの比較は PrefabUtility.GetCorrespondingObjectFromSource を使う
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(selected);

            if (prefabSource == sourcePrefab)
            {
                // 置き換え実行
                Transform parent = selected.transform.parent;
                Vector3 position = selected.transform.position;
                Quaternion rotation = selected.transform.rotation;
                Vector3 scale = selected.transform.localScale;

                Undo.DestroyObjectImmediate(selected);

                GameObject newObject = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, parent);
                newObject.transform.SetPositionAndRotation(position, rotation);
                newObject.transform.localScale = scale;
                Undo.RegisterCreatedObjectUndo(newObject, "Replace Prefab");
            }
        }
    }
}
