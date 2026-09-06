using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using IsometricGame.Player;
using IsometricGame.Tilemap;

namespace IsometricGame.Environment
{
    /// <summary>
    /// Manages smooth zone transitions (fade to black, teleport, fade back)
    /// between the Indoor Room and the Outdoor Isometric Grass World.
    /// </summary>
    public class ZoneTransitionManager : MonoBehaviour
    {
        private static ZoneTransitionManager instance;
        public static ZoneTransitionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<ZoneTransitionManager>();
                    if (instance == null)
                    {
                        GameObject ztObj = new GameObject("Zone_Transition_Manager");
                        instance = ztObj.AddComponent<ZoneTransitionManager>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Spawn Positions (World Space)")]
        [SerializeField] private Vector2 indoorSpawnPos = new Vector2(0f, 2.0f);   // Grid (4, 4)
        [SerializeField] private Vector2 outdoorSpawnPos = new Vector2(10f, 7.0f); // Grid (24, 4)

        [Header("Transition Timings")]
        [SerializeField] private float fadeDuration = 0.35f;
        [SerializeField] private float blackHoldDuration = 0.15f;

        [Header("UI Overlay")]
        [SerializeField] private CanvasGroup transitionCanvasGroup;

        private bool isTransitioning = false;
        public bool IsTransitioning => isTransitioning;

        public Vector2 IndoorSpawnPos { get => indoorSpawnPos; set => indoorSpawnPos = value; }
        public Vector2 OutdoorSpawnPos { get => outdoorSpawnPos; set => outdoorSpawnPos = value; }

        private void Awake()
        {
            if (instance == null) instance = this;
            else if (instance != this) { Destroy(gameObject); return; }

            EnsureFadeOverlay();
        }

        private void Start()
        {
            UpdateSpawnPositionsFromMap();

            // Default to Indoor Room active and Outside hidden (dark blue void)
            var worldMap = FindAnyObjectByType<IsometricWorldMap>();
            if (worldMap != null)
            {
                worldMap.SetZoneActive(false);
            }
        }

        public void UpdateSpawnPositionsFromMap()
        {
            var worldMap = FindAnyObjectByType<IsometricWorldMap>();
            if (worldMap != null)
            {
                indoorSpawnPos = worldMap.GetIndoorDoorSpawnWorld();
                outdoorSpawnPos = worldMap.GetOutdoorDoorSpawnWorld();
            }
        }

        private void EnsureFadeOverlay()
        {
            if (transitionCanvasGroup != null) return;

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                IsometricGame.UI.EnsureCanvasAndMoneyUI.EnsureAllUI();
                canvas = FindAnyObjectByType<Canvas>();
            }

            if (canvas != null)
            {
                Transform existing = canvas.transform.Find("Zone_Transition_Overlay");
                GameObject overlayObj = existing != null ? existing.gameObject : new GameObject("Zone_Transition_Overlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                overlayObj.transform.SetParent(canvas.transform, false);

                RectTransform rt = overlayObj.transform as RectTransform ?? overlayObj.GetComponent<RectTransform>() ?? overlayObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                Image img = overlayObj.GetComponent<Image>() ?? overlayObj.AddComponent<Image>();
                img.color = Color.black;
                img.raycastTarget = false;

                transitionCanvasGroup = overlayObj.GetComponent<CanvasGroup>() ?? overlayObj.AddComponent<CanvasGroup>();
                transitionCanvasGroup.alpha = 0f;
                transitionCanvasGroup.interactable = false;
                transitionCanvasGroup.blocksRaycasts = false;
            }
        }

        public void TransitionToOutdoors()
        {
            if (isTransitioning) return;
            UpdateSpawnPositionsFromMap();
            StartCoroutine(TransitionRoutine(outdoorSpawnPos, "Stepping Outdoors...", true));
        }

        public void TransitionToIndoors()
        {
            if (isTransitioning) return;
            UpdateSpawnPositionsFromMap();
            StartCoroutine(TransitionRoutine(indoorSpawnPos, "Entering Room...", false));
        }

        public void TransitionTo(Vector2 targetPos)
        {
            if (isTransitioning) return;
            bool isOutdoors = targetPos.x > 4.0f;
            StartCoroutine(TransitionRoutine(targetPos, null, isOutdoors));
        }

        public void TransitionTo(Vector2 targetPos, bool isOutdoors)
        {
            if (isTransitioning) return;
            StartCoroutine(TransitionRoutine(targetPos, null, isOutdoors));
        }

        private IEnumerator TransitionRoutine(Vector2 targetPos, string message, bool isOutdoors)
        {
            isTransitioning = true;
            EnsureFadeOverlay();

            // 1. Lock player movement
            if (IsometricPlayerController.Instance != null)
            {
                IsometricPlayerController.Instance.SetInputEnabled(false);
            }

            // 2. Fade to black
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.blocksRaycasts = true;
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    transitionCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                transitionCanvasGroup.alpha = 1f;
            }

            // 3. Teleport player while dark
            if (IsometricPlayerController.Instance != null)
            {
                IsometricPlayerController.Instance.TeleportTo(targetPos);
            }
            else
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    player.transform.position = new Vector3(targetPos.x, targetPos.y, 0f);
                }
            }

            // 4. Toggle Zone Active: Hide outside when indoors (revealing dark blue void), show outside when outdoors
            var worldMap = FindAnyObjectByType<IsometricWorldMap>();
            if (worldMap != null)
            {
                worldMap.SetZoneActive(isOutdoors);
            }

            // 5. Configure Camera Mode (Static for Indoor Room, Dynamic Follow for Outdoors)
            var followCam = FindAnyObjectByType<IsometricGame.CameraControl.IsometricFollowCamera>();
            if (followCam != null)
            {
                if (isOutdoors)
                {
                    followCam.SetFollowPlayer(true, snap: false);
                    followCam.SnapToTarget(targetPos);
                }
                else
                {
                    followCam.SetFollowPlayer(false, snap: false);
                    followCam.PositionOnRoomCenter();
                }
            }

            yield return new WaitForSeconds(blackHoldDuration);

            // 4. Fade back from black
            if (transitionCanvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    transitionCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                transitionCanvasGroup.alpha = 0f;
                transitionCanvasGroup.blocksRaycasts = false;
            }

            // 5. Restore player movement
            if (IsometricPlayerController.Instance != null)
            {
                IsometricPlayerController.Instance.SetInputEnabled(true);
            }

            isTransitioning = false;
        }
    }
}
