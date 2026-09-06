#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using IsometricGame.Environment;

namespace IsometricGame.Editor
{
    [CustomEditor(typeof(IsometricRoomBuilder))]
    public class IsometricRoomBuilderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            IsometricRoomBuilder builder = (IsometricRoomBuilder)target;

            GUILayout.Space(12);
            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.45f);
            if (GUILayout.Button("Rebuild / Refresh Room Now", GUILayout.Height(36)))
            {
                builder.RebuildRoom();
                EditorUtility.SetDirty(builder);
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
#endif
