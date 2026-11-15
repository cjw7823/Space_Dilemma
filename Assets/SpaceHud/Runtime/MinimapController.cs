using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Hud
{
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] Camera mapCamera;
        [SerializeField] RawImage mapImage;
        [SerializeField] RectTransform viewport;
        [SerializeField] RectTransform playerIcon;
        [Header("Follow")]
        [Min(1)] public float cameraHeight = 100;
        [Tooltip("Rotation needed to point the assigned sprite upwards. Arrow-right uses 90.")]
        public float playerIconUpRotation = 90;
        Transform player;

        public Camera MapCamera => mapCamera;
        public RenderTexture Texture => mapCamera ? mapCamera.targetTexture : null;
        public RectTransform PlayerIcon => playerIcon;

        public void Initialize(Transform target)
        {
            player = target;
            if (!player || !mapCamera || !mapImage || !viewport || !playerIcon || !mapCamera.targetTexture)
            { Debug.LogError("Minimap is missing a scene camera, RenderTexture, viewport or icon.", this); return; }
            Refresh();
        }
        void LateUpdate() { if (mapCamera && player && viewport && playerIcon) Refresh(); }
        void Refresh()
        {
            mapCamera.transform.position = player.position + Vector3.up * Mathf.Max(1, cameraHeight);
            Vector3 mapUp = Vector3.ProjectOnPlane(mapCamera.transform.up, Vector3.up).normalized;
            Vector3 heading = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            float angle = Vector3.SignedAngle(mapUp, heading, Vector3.up);
            playerIcon.localRotation = Quaternion.Euler(0, 0, playerIconUpRotation - angle);

            foreach (var marker in HudMapMarker.Active)
            {
                if (!marker || !marker.target || !marker.transform.IsChildOf(viewport)) continue;
                var icon = marker.Icon;
                Vector3 point = mapCamera.WorldToViewportPoint(marker.target.position);
                icon.enabled = marker.showOnMap && point.z > 0 && point.x >= .02f && point.x <= .98f && point.y >= .02f && point.y <= .98f;
                icon.rectTransform.anchoredPosition = new Vector2((point.x - .5f) * viewport.rect.width, (point.y - .5f) * viewport.rect.height);
            }
        }
    }
}
