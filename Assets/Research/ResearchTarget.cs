using UnityEngine;

namespace SpaceDilemma.Research
{
    public enum ResearchKind { Sample, Storage, Instrument, Plant }

    public sealed class ResearchTarget : MonoBehaviour
    {
        public ResearchKind kind;
        [Range(0, 2)] public int index;
        public string englishName;
        public string koreanName;
        Bounds? sampleBounds;

        public Vector3 InteractionPoint(Vector3 from)
        {
            Vector3 position = transform.position;
            return sampleBounds.HasValue ? position + sampleBounds.Value.ClosestPoint(from - position) : position;
        }

        internal void PlaceOnGround()
        {
            Vector3 position = transform.position;
            Bounds? model = null;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Bounds bounds = model ?? renderer.bounds;
                bounds.Encapsulate(renderer.bounds);
                model = bounds;
            }
            if (model.HasValue)
                sampleBounds = new Bounds(model.Value.center - position, model.Value.size);

            Vector3 origin = model.HasValue ? model.Value.center : position;
            float bottom = model?.min.y ?? position.y;
            float groundHeight = float.NegativeInfinity;
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain.gameObject.scene != gameObject.scene || !terrain.terrainData) continue;
                Vector3 local = origin - terrain.GetPosition();
                Vector3 size = terrain.terrainData.size;
                if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
                groundHeight = Mathf.Max(groundHeight, terrain.GetPosition().y + terrain.SampleHeight(origin));
            }

            // From the authored height, also find a walkable surface such as a base floor.
            origin.y = Mathf.Max(bottom, groundHeight) + .25f;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, Mathf.Infinity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y <= 0 || hit.collider is CharacterController || hit.collider.attachedRigidbody ||
                    hit.collider.GetComponentInParent<ResearchTarget>()) continue;
                groundHeight = Mathf.Max(groundHeight, hit.point.y);
            }
            if (float.IsNegativeInfinity(groundHeight)) return;

            position.y += groundHeight - bottom;
            transform.position = position;
        }
    }
}
