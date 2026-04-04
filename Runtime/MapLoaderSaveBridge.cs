#if SAVEMANAGER_MLF
using MapLoaderFramework.Runtime;
using UnityEngine;

namespace SaveManager.Runtime
{
    /// <summary>
    /// <b>MapLoaderSaveBridge</b> connects SaveManager to MapLoaderFramework without creating
    /// a hard compile-time dependency in either package.
    /// <para>
    /// When <c>SAVEMANAGER_MLF</c> is defined:
    /// <list type="bullet">
    /// <item>Subscribes to <c>MapLoaderFramework.OnMapLoaded</c> to record the current map id.</item>
    /// <item>Subscribes to <c>MapLoaderFramework.OnChapterChanged</c> to record chapter progress
    /// and optionally auto-save.</item>
    /// </list>
    /// </para>
    /// </summary>
    [AddComponentMenu("SaveManager/Map Loader Save Bridge")]
    [DisallowMultipleComponent]
    public class MapLoaderSaveBridge : MonoBehaviour
    {
        private SaveManager _save;
        private MapLoaderFramework _framework;

        [Tooltip("If true, automatically call Save() when the chapter changes.")]
        [SerializeField] private bool autoSaveOnChapterChange = true;

        private void Awake()
        {
            _save      = GetComponent<SaveManager>() ?? FindObjectOfType<SaveManager>();
            _framework = GetComponent<MapLoaderFramework>() ?? FindObjectOfType<MapLoaderFramework>();

            if (_save == null)
            {
                Debug.LogWarning("[MapLoaderSaveBridge] SaveManager not found in scene.");
                return;
            }

            if (_framework != null)
            {
                _framework.OnMapLoaded      += OnMapLoaded;
                _framework.OnChapterChanged += OnChapterChanged;
                Debug.Log("[MapLoaderSaveBridge] Hooked into MapLoaderFramework events.");
            }
            else
            {
                Debug.LogWarning("[MapLoaderSaveBridge] MapLoaderFramework not found — save auto-tracking disabled.");
            }
        }

        private void OnDestroy()
        {
            if (_framework == null) return;
            _framework.OnMapLoaded      -= OnMapLoaded;
            _framework.OnChapterChanged -= OnChapterChanged;
        }

        private void OnMapLoaded(MapData mapData)
        {
            if (mapData == null) return;
            _save.SetMap(mapData.id);
        }

        private void OnChapterChanged(int previous, int current)
        {
            _save.SetChapter(current);
            _save.UnlockChapter(current);
            if (autoSaveOnChapterChange)
                _save.Save();
        }
    }
}
#endif
