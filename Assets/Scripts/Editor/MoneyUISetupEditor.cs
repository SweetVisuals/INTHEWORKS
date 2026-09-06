#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IsometricGame.UI;
using Object = UnityEngine.Object;

namespace IsometricGame.Editor
{
    [InitializeOnLoad]
    public static class MoneyUISetupEditor
    {
        static MoneyUISetupEditor()
        {
            EditorApplication.delayCall += AutoEnsureInActiveScene;
            EditorApplication.update += CheckAndEnsureOnce;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (s, m) => AutoEnsureInActiveScene();
        }

        private static void CheckAndEnsureOnce()
        {
            if (Application.isPlaying) return;
            EditorApplication.update -= CheckAndEnsureOnce;
            AutoEnsureInActiveScene();
        }

        private static void AutoEnsureInActiveScene()
        {
            if (Application.isPlaying) return;
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null 
                || Object.FindAnyObjectByType<ClockUI>() == null 
                || Object.FindAnyObjectByType<EnergyBarUI>() == null 
                || Object.FindAnyObjectByType<XpBarUI>() == null)
            {
                SetupMoneyHUD();
            }
        }

        [MenuItem("GameObject/UI/Setup Full GUI HUD (Clock, Money, Hotbar, Energy & XP)", false, 20)]
        [MenuItem("GameObject/UI/Setup GUI HUD (Money, Hotbar & Energy)", false, 21)]
        [MenuItem("GameObject/UI/Setup Money HUD (Center)", false, 22)]
        public static void SetupMoneyHUD()
        {
            EnsureCanvasAndMoneyUI.EnsureAllUI();

            ClockUI clockUI = Object.FindAnyObjectByType<ClockUI>();
            if (clockUI != null) clockUI.SetLayout(new Vector2(-24f, -22f), new Vector2(244f, 56f));

            XpBarUI xpUI = Object.FindAnyObjectByType<XpBarUI>();
            if (xpUI != null) xpUI.SetLayout(new Vector2(-24f, -82f), new Vector2(244f, 28f));

            EnergyBarUI energyUI = Object.FindAnyObjectByType<EnergyBarUI>();
            if (energyUI != null) energyUI.SetLayout(new Vector2(-24f, -114f), new Vector2(244f, 28f));

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }
            Debug.Log("<color=green>[GUI Setup]</color> Successfully configured Clock, Money Card HUD, 4-Slot Hotbar, Energy Bar, XP Bar, Interaction Popup, and Sleep UI!");
        }
    }
}
#endif
