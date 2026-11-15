using UnityEngine;

namespace SpaceDilemma.Research
{
    [CreateAssetMenu(menuName = "Research/Lunar Sample Information")]
    public sealed class LunarSampleInformation : ScriptableObject
    {
        public string sampleId;
        public int apolloMission;
        public string classificationEnglish, classificationKorean;
        public string locationEnglish, locationKorean;
        [TextArea(3, 8)] public string descriptionEnglish, descriptionKorean;
        public string sourceUrl;
    }
}
