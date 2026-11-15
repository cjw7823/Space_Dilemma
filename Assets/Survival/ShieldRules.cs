using UnityEngine;
namespace SpaceDilemma.Survival
{
    [CreateAssetMenu(menuName="Space Dilemma/Shield Rules")]
    public sealed class ShieldRules : ScriptableObject
    {
        [Header("Temporary linear game model - not verified NASA shielding efficiency")]
        [Tooltip("Maximum fraction of SPE dose removed at full supplied output, indoors only.")]
        [Range(0,1)] public float maximumReduction=.8f;
    }
}
