using UnityEngine;

namespace SpaceDilemma.Research
{
    // Both presentations are authored children; installation only changes their visibility.
    public sealed class InstrumentInstallation : MonoBehaviour
    {
        public GameObject placementOutline;
        public GameObject installedModel;
        public bool IsInstalled { get; private set; }

        public void SetInstalled(bool installed)
        {
            IsInstalled=installed;
            if(placementOutline)placementOutline.SetActive(!installed);
            if(installedModel)installedModel.SetActive(installed);
        }

        public void PlaceOnGround()
        {
            Vector3 position=transform.position;
            if(TryGround(position,out float height))position.y=height;
            transform.position=position;
            if(installedModel && TryModelBounds(installedModel.transform,out var model))
            {
                installedModel.transform.position+=new Vector3(position.x-model.center.x,position.y-model.min.y,position.z-model.center.z);
                installedModel.transform.position+=Vector3.up*GroundOffset(installedModel.transform);
            }
            if(placementOutline)
            {
                // The shared outline mesh has its bottom at local y=0.
                placementOutline.transform.position=position;
                placementOutline.transform.position+=Vector3.up*GroundOffset(placementOutline.transform);
            }
        }

        float GroundOffset(Transform visual)
        {
            float correction=float.NegativeInfinity;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled)continue;
                var local=renderer.localBounds;Bounds world=default;
                for(int i=0;i<8;i++)
                {
                    var point=renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(i==0)world=new Bounds(point,Vector3.zero);else world.Encapsulate(point);
                }
                for(int i=0;i<5;i++)
                {
                    Vector3 point=i==4?new Vector3(world.center.x,world.min.y,world.center.z):
                        new Vector3((i&1)==0?world.min.x:world.max.x,world.min.y,(i&2)==0?world.min.z:world.max.z);
                    if(TryGround(point,out float height))correction=Mathf.Max(correction,height-point.y);
                }
            }
            return float.IsNegativeInfinity(correction)?0:correction;
        }

        public static bool TryModelBounds(Transform root,out Bounds bounds)
        {
            bounds=default;bool found=false;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled)continue;
                var local=renderer.localBounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=renderer.transform.TransformPoint(corner);
                    if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                }
            }
            return found;
        }

        bool TryGround(Vector3 point,out float height)
        {
            height=float.NegativeInfinity;
            foreach(var terrain in Terrain.activeTerrains)
            {
                if(terrain.gameObject.scene!=gameObject.scene||!terrain.terrainData)continue;
                Vector3 local=point-terrain.GetPosition(),size=terrain.terrainData.size;
                if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z)
                    height=Mathf.Max(height,terrain.GetPosition().y+terrain.SampleHeight(point));
            }
            var origin=point;origin.y=Mathf.Max(point.y,height)+.25f;
            foreach(var hit in Physics.RaycastAll(origin,Vector3.down,Mathf.Infinity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(hit.normal.y<=0||hit.collider is CharacterController||hit.collider.attachedRigidbody||
                    hit.collider.GetComponentInParent<ResearchTarget>())continue;
                height=Mathf.Max(height,hit.point.y);
            }
            return !float.IsNegativeInfinity(height);
        }
    }
}
