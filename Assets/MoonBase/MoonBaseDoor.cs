using UnityEngine;

/// <summary>Drive the door pack's existing proximity parameter without replacing its animations.</summary>
public sealed class MoonBaseDoor : MonoBehaviour
{
    public Transform player;
    public float openDistance=2.5f;
    Animator animator;
    static readonly int Nearby=Animator.StringToHash("character_nearby");
    void Awake() { animator=GetComponent<Animator>(); }
    void Update()
    {
        if(!player || !animator) return;
        Vector3 delta=player.position-transform.position;
        bool nearby=Mathf.Abs(delta.y)<2.5f && new Vector2(delta.x,delta.z).sqrMagnitude<openDistance*openDistance;
        animator.SetBool(Nearby,nearby);
    }
}
