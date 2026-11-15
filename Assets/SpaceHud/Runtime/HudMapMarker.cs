using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDilemma.Hud
{
    // Put this on an existing Image under the minimap viewport and assign a world target.
    [RequireComponent(typeof(Image))]
    public sealed class HudMapMarker : MonoBehaviour
    {
        static readonly HashSet<HudMapMarker> markers = new HashSet<HudMapMarker>();
        public static IReadOnlyCollection<HudMapMarker> Active => markers;
        [Tooltip("World object that this scene-authored icon follows.")]
        public Transform target;
        public bool showOnMap = true;
        public Image Icon => GetComponent<Image>();
        void OnEnable() => markers.Add(this);
        void OnDisable() => markers.Remove(this);
    }
}
