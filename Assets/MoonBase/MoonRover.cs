using UnityEngine;

/// <summary>Drive the rover over walkable surfaces using a swept chassis collider.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class MoonRover : MonoBehaviour
{
    [Min(1)] public float driveSpeed = 14f;
    [Min(1)] public float reverseSpeed = 5f;
    [Min(1)] public float turnSpeed = 85f;
    [Min(0)] public float gravity = 1.62f;
    public Transform cameraTarget;
    public Transform driverSeat;
    public Transform model;
    public float CameraDistance => 8f;

    BoxCollider chassis;
    float velocity;
    float fallingSpeed;
    readonly RaycastHit[] sweepHits = new RaycastHit[16];
    readonly Collider[] overlapHits = new Collider[16];
    // The passenger has no active capsule. The chassis is on Ignore Raycast so the follow camera
    // and ground probes can clear it, while the physics collision matrix still blocks walking.
    int worldMask => Physics.DefaultRaycastLayers & ~(1 << 8);

    void Awake() => chassis = GetComponent<BoxCollider>();

    void Start()
    {
        // Resolve an authored parking height even before a driver enters.
        Physics.SyncTransforms();
        if (TryGround(transform.position, 100f, 1000f, out var ground))
            SetGroundContact(ground, 1f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        fallingSpeed += gravity * dt;
        float fall = fallingSpeed * dt;
        if (TryGround(transform.position, 2f, Mathf.Max(3f, fall + .1f), out var ground) &&
            transform.position.y - fall <= ground.point.y + .05f)
        {
            SetGroundContact(ground, dt);
            return;
        }
        transform.position += Vector3.down * fall;
    }

    void SetGroundContact(RaycastHit ground, float dt)
    {
        var position = transform.position;
        position.y = ground.point.y;
        transform.position = position;
        fallingSpeed = 0f;
        AlignModel(ground.normal, dt);
    }

    void AlignModel(Vector3 normal, float dt)
    {
        if (!model) return;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal);
        model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(forward, normal), 1f - Mathf.Exp(-10f * dt));
    }

    public bool CanBoard(Vector3 position)
    {
        Vector3 nearest = chassis.ClosestPoint(position);
        Vector3 delta = position - nearest;
        if (delta.sqrMagnitude > 2.5f * 2.5f || Mathf.Abs(delta.y) > 1.6f) return false;
        Vector3 sightOrigin = position + Vector3.up;
        nearest = chassis.ClosestPoint(sightOrigin);
        return !Physics.Linecast(sightOrigin, nearest, worldMask, QueryTriggerInteraction.Ignore);
    }

    public void Drive(Vector2 input, float dt)
    {
        if (dt <= 0f) return;
        float steering = Mathf.Clamp(input.x, -1f, 1f);
        float throttle = Mathf.Clamp(input.y, -1f, 1f);
        float speed = throttle >= 0f ? driveSpeed : reverseSpeed;
        velocity = Mathf.MoveTowards(velocity, throttle * speed, (Mathf.Abs(throttle) > .01f ? 12f : 24f) * dt);

        Quaternion rotation = transform.rotation;
        if (Mathf.Abs(steering) > .01f)
        {
            float reverse = velocity < -.1f ? -1f : 1f;
            Quaternion turn = Quaternion.AngleAxis(steering * turnSpeed * reverse * dt, Vector3.up) * rotation;
            if (HasChassisClearance(transform.position, turn)) rotation = turn;
        }

        Vector3 next = transform.position;
        Vector3 motion = rotation * Vector3.forward * (velocity * dt);
        if (motion.sqrMagnitude > .000001f)
        {
            Vector3 center = next + rotation * chassis.center;
            Vector3 half = chassis.size * .5f;
            // Terrain supports the rover via the ground probe. Sweeping a long, level box
            // against that same terrain mistakes ordinary uphill grades for walls.
            float travel = NearestObstacle(center + Vector3.up * .12f, half * .96f,
                motion.normalized, rotation, motion.magnitude + .025f);
            if (!float.IsPositiveInfinity(travel))
            {
                motion = motion.normalized * Mathf.Max(0f, travel - .025f);
                velocity = 0f;
            }
            next += motion;
        }

        if (!TryGround(next, 2f, 3f, out var ground) || Mathf.Abs(ground.point.y - transform.position.y) > .3f + motion.magnitude)
        {
            velocity = 0f;
            return;
        }

        next.y = ground.point.y;
        if (!HasChassisClearance(next, rotation))
        {
            velocity = 0f;
            return;
        }
        transform.SetPositionAndRotation(next, rotation);
        fallingSpeed = 0f;
        AlignModel(ground.normal, dt);
    }

    bool HasChassisClearance(Vector3 position, Quaternion rotation)
    {
        Vector3 center = position + rotation * chassis.center + Vector3.up * .12f;
        Vector3 half = chassis.size * .46f;
        int count = Physics.OverlapBoxNonAlloc(center, half, overlapHits, rotation, worldMask, QueryTriggerInteraction.Ignore);
        var hits = count == overlapHits.Length
            ? Physics.OverlapBox(center, half, rotation, worldMask, QueryTriggerInteraction.Ignore)
            : overlapHits;
        count = hits == overlapHits ? count : hits.Length;
        for (int i = 0; i < count; i++)
            if (hits[i] is not TerrainCollider) return false;
        return true;
    }

    float NearestObstacle(Vector3 center, Vector3 half, Vector3 direction, Quaternion rotation, float distance)
    {
        int count = Physics.BoxCastNonAlloc(center, half, direction, sweepHits, rotation, distance,
            worldMask, QueryTriggerInteraction.Ignore);
        var hits = count == sweepHits.Length
            ? Physics.BoxCastAll(center, half, direction, rotation, distance, worldMask, QueryTriggerInteraction.Ignore)
            : sweepHits;
        count = hits == sweepHits ? count : hits.Length;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (hits[i].collider is not TerrainCollider) nearest = Mathf.Min(nearest, hits[i].distance);
        return nearest;
    }

    public void Stop() => velocity = 0f;

    public bool TryDismount(CharacterController passenger, out Vector3 position)
    {
        Stop();
        Physics.SyncTransforms();
        float side = chassis.size.x * .5f + passenger.radius + .7f;
        float end = chassis.size.z * .5f + passenger.radius + .7f;
        foreach (Vector3 offset in new[] { Vector3.right * side, Vector3.left * side, Vector3.back * end, Vector3.forward * end })
        {
            if (!TryGround(transform.TransformPoint(offset), 1.5f, 3f, out var ground)) continue;
            float radius = passenger.radius + .06f;
            Vector3 lower = ground.point + Vector3.up * (radius + .03f);
            Vector3 upper = ground.point + Vector3.up * (passenger.height - radius);
            if (Physics.CheckCapsule(lower, upper, radius, worldMask, QueryTriggerInteraction.Ignore)) continue;
            position = ground.point + Vector3.up * (passenger.height * .5f - passenger.center.y + passenger.skinWidth + .003f);
            return true;
        }

        position = default;
        return false;
    }

    bool TryGround(Vector3 point, float above, float below, out RaycastHit hit)
    {
        return Physics.Raycast(point + Vector3.up * above, Vector3.down, out hit, above + below,
            worldMask, QueryTriggerInteraction.Ignore) && hit.normal.y > .78f;
    }
}
