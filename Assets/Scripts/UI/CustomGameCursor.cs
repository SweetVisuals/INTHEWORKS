using UnityEngine;

namespace IsometricGame.UI
{
    /// <summary>
    /// Manages the custom pixel-art game cursor with precise hotspot alignment.
    /// Sets Cursor.SetCursor with 'Assets/Sprites/game cursor.png' (16x16, hotspot (4,5)).
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    public class CustomGameCursor : MonoBehaviour
    {
        private static CustomGameCursor instance;
        public static CustomGameCursor Instance => instance;

        [Header("Cursor Settings")]
        [SerializeField] private Texture2D cursorTexture;
        [SerializeField] private Vector2 hotspot = new Vector2(4f, 5f);
        [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            EnsureCursorActive();
        }

        private void Awake()
        {
            instance = this;
            ApplyCursor();
        }

        private void OnEnable()
        {
            instance = this;
            ApplyCursor();
        }

        private void Start()
        {
            ApplyCursor();
        }

        public static void EnsureCursorActive()
        {
            CustomGameCursor cursorMgr = Object.FindAnyObjectByType<CustomGameCursor>();
            if (cursorMgr == null)
            {
                GameObject obj = new GameObject("CustomGameCursor");
                cursorMgr = obj.AddComponent<CustomGameCursor>();
            }
            cursorMgr.ApplyCursor();
        }

        public void ApplyCursor()
        {
            EnsureTextureLoaded();
            if (cursorTexture != null)
            {
                Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
            }
        }

        private void EnsureTextureLoaded()
        {
#if UNITY_EDITOR
            if (cursorTexture == null)
            {
                cursorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/game cursor.png");
            }
#endif
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
    }
}
