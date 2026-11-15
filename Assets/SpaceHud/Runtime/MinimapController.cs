using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SpaceDilemma.Hud
{
    [ExecuteAlways]
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
        [SerializeField] Transform player;

        public Camera MapCamera => mapCamera;
        public RenderTexture Texture => mapCamera ? mapCamera.targetTexture : null;
        public RectTransform PlayerIcon => playerIcon;
        public RectTransform Viewport => viewport;

        public void Initialize(Transform target)
        {
            player = target;
            if (!player || !mapCamera || !mapImage || !viewport || !playerIcon || !mapCamera.targetTexture)
            { Debug.LogError("Minimap is missing a scene camera, RenderTexture, viewport or icon.", this); return; }
            Refresh();
        }
        void LateUpdate() { if (mapCamera && player && viewport && playerIcon) Refresh(); }
        public void RefreshPreview(HudMapMarker[] markers)
        {if(mapCamera&&player&&viewport&&playerIcon)Refresh(markers);}
        void Refresh(IEnumerable<HudMapMarker> markers=null)
        {
            Vector3 cameraPosition=player.position+Vector3.up*Mathf.Max(1,cameraHeight);
            if(mapCamera.transform.position!=cameraPosition)mapCamera.transform.position=cameraPosition;
            Vector3 mapUp = Vector3.ProjectOnPlane(mapCamera.transform.up, Vector3.up).normalized;
            Vector3 heading = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            float angle = Vector3.SignedAngle(mapUp, heading, Vector3.up);
            Quaternion rotation=Quaternion.Euler(0,0,playerIconUpRotation-angle);
            if(Quaternion.Angle(playerIcon.localRotation,rotation)>.001f)playerIcon.localRotation=rotation;

            foreach (var marker in markers??HudMapMarker.Active)
            {
                if (!marker || !marker.transform.IsChildOf(viewport)) continue;
                var icon = marker.Icon;
                if(!marker.target){icon.enabled=false;continue;}
                // Mission locations are a horizontal map coordinate even on a higher hill.
                Vector3 world=marker.target.position;world.y=player.position.y;
                Vector3 point = mapCamera.WorldToViewportPoint(world);
                icon.enabled = marker.showOnMap && (!marker.hideWhenTargetInactive||marker.target.gameObject.activeInHierarchy) && point.z > 0 &&
                    (marker.clampToEdge || (point.x >= .02f && point.x <= .98f && point.y >= .02f && point.y <= .98f));
                if(marker.clampToEdge)
                {
                    float padX=Mathf.Max(.02f,icon.rectTransform.rect.width*.5f/viewport.rect.width);
                    float padY=Mathf.Max(.02f,icon.rectTransform.rect.height*.5f/viewport.rect.height);
                    Vector2 delta=new Vector2(point.x-.5f,point.y-.5f);
                    float scale=1;
                    if(Mathf.Abs(delta.x)>.0001f)scale=Mathf.Min(scale,(.5f-padX)/Mathf.Abs(delta.x));
                    if(Mathf.Abs(delta.y)>.0001f)scale=Mathf.Min(scale,(.5f-padY)/Mathf.Abs(delta.y));
                    point.x=.5f+delta.x*scale;point.y=.5f+delta.y*scale;
                }
                var position=new Vector2((point.x-.5f)*viewport.rect.width,(point.y-.5f)*viewport.rect.height);
                if(Vector2.Distance(icon.rectTransform.anchoredPosition,position)>.001f)icon.rectTransform.anchoredPosition=position;
            }
        }
    }
}
