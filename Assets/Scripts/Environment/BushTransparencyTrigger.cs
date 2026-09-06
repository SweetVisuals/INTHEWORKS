using System.Collections.Generic;
using UnityEngine;

namespace IsometricGame.Environment
{
    /// <summary>
    /// Attached to individual outdoor bushes.
    /// Allows the player to walk directly through the bush without collision,
    /// and smoothly reduces the opacity of only this bush by 25% (alpha: 1.0 -> 0.75)
    /// while the player is inside.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BushTransparencyTrigger : MonoBehaviour
    {
        [Header("Opacity Settings")]
        [Tooltip("Alpha when player is inside the bush (25% reduction = 0.75f).")]
        [SerializeField] private float walkThroughAlpha = 0.75f;

        [Tooltip("Alpha when no player is overlapping (default = 1.0f).")]
        [SerializeField] private float normalAlpha = 1.0f;

        [Tooltip("Fade transition speed.")]
        [SerializeField] private float fadeSpeed = 8.0f;

        private SpriteRenderer spriteRenderer;
        private readonly HashSet<Collider2D> overlappingColliders = new HashSet<Collider2D>();
        private float currentAlpha = 1.0f;

        public float WalkThroughAlpha
        {
            get => walkThroughAlpha;
            set => walkThroughAlpha = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                currentAlpha = spriteRenderer.color.a;
            }
        }

        public void SetTargetOpacity(float opacity)
        {
            walkThroughAlpha = Mathf.Clamp01(opacity);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayer(other))
            {
                overlappingColliders.Add(other);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other))
            {
                overlappingColliders.Remove(other);
            }
        }

        private bool IsPlayer(Collider2D col)
        {
            if (col == null) return false;
            return col.CompareTag("Player") || col.GetComponent<IsometricGame.Player.IsometricPlayerController>() != null;
        }

        private void Update()
        {
            if (spriteRenderer == null) return;

            // Clean up any destroyed or disabled colliders
            if (overlappingColliders.Count > 0)
            {
                overlappingColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            }

            float targetAlpha = overlappingColliders.Count > 0 ? walkThroughAlpha : normalAlpha;

            if (!Mathf.Approximately(currentAlpha, targetAlpha))
            {
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
                Color c = spriteRenderer.color;
                c.a = currentAlpha;
                spriteRenderer.color = c;
            }
        }

        private void OnDisable()
        {
            overlappingColliders.Clear();
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = normalAlpha;
                spriteRenderer.color = c;
            }
        }
    }
}
