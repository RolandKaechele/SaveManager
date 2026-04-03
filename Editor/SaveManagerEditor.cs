#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SaveManager.Editor
{
    /// <summary>
    /// Custom Inspector for <see cref="SaveManager.Runtime.SaveManager"/>.
    /// Shows slot status and provides save/load/delete controls at runtime.
    /// </summary>
    [CustomEditor(typeof(SaveManager.Runtime.SaveManager))]
    public class SaveManagerEditor : UnityEditor.Editor
    {
        private string _flagToCheck = "";
        private Texture2D[] _screenshots;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var mgr = (SaveManager.Runtime.SaveManager)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use runtime controls.", MessageType.Info);
                return;
            }

            // Slot table
            EditorGUILayout.LabelField("Save Slots", EditorStyles.miniBoldLabel);
            var headers = mgr.GetSlotHeaders();

            if (_screenshots == null || _screenshots.Length != headers.Count)
                _screenshots = new Texture2D[headers.Count];

            for (int i = 0; i < headers.Count; i++)
            {
                var h = headers[i];

                // Load screenshot once; clear it when Save or Delete is pressed.
                if (_screenshots[i] == null && mgr.HasSave(i))
                    _screenshots[i] = mgr.GetScreenshot(i);

                EditorGUILayout.BeginHorizontal();

                if (_screenshots[i] != null)
                    GUILayout.Label(_screenshots[i], GUILayout.Width(64), GUILayout.Height(36));
                else
                    GUILayout.Box(GUIContent.none, GUILayout.Width(64), GUILayout.Height(36));

                string label = h != null
                    ? $"Slot {i}  Chp {h.currentChapter} — {h.currentMapId} — {h.lastSaveTime}"
                    : $"Slot {i}  (empty)";
                EditorGUILayout.LabelField(label);
                if (GUILayout.Button("Save", GUILayout.Width(55))) { mgr.Save(i); _screenshots[i] = null; }
                GUI.enabled = mgr.HasSave(i);
                if (GUILayout.Button("Load", GUILayout.Width(55))) mgr.Load(i);
                if (GUILayout.Button("Del",  GUILayout.Width(40))) { mgr.Delete(i); _screenshots[i] = null; }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Refresh Screenshots"))
                _screenshots = null;

            EditorGUILayout.Space(4);

            // Current state
            EditorGUILayout.LabelField("Current State", EditorStyles.miniBoldLabel);
            var cd = mgr.Current;
            EditorGUILayout.LabelField("Chapter",   cd.currentChapter.ToString());
            EditorGUILayout.LabelField("Map",       cd.currentMapId  ?? "(none)");
            EditorGUILayout.LabelField("Flags set", cd.flags.All.Count.ToString());
            EditorGUILayout.LabelField("Maps visited", cd.visitedMapIds.Count + " map(s)");

            EditorGUILayout.Space(4);

            // Flag check
            EditorGUILayout.LabelField("Flag Check", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _flagToCheck = EditorGUILayout.TextField("Flag name", _flagToCheck);
            bool isSet = !string.IsNullOrEmpty(_flagToCheck) && mgr.IsSet(_flagToCheck);
            EditorGUILayout.LabelField(isSet ? "SET" : "not set", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
