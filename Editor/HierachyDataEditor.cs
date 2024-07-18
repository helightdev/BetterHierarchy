using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HierarchyData))]
public class HierachyDataEditor : Editor {
        
    public override void OnInspectorGUI() {
        // Button to open editor
        if (GUILayout.Button("Reload")) {
            BetterHierarchy.ResetCache();
        }
        
        if (GUILayout.Button("Edit Rules")) {
            HierarchyEditor.ShowWindow((HierarchyData) target);
        }
        base.OnInspectorGUI();
    }
}