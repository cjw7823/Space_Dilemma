using TMPro;
using UnityEngine;

namespace SpaceDilemma.Hud
{
    public sealed class MissionMapLegend : MonoBehaviour
    {
        public HudGameSettings language;
        public TMP_Text[] labels;
        static readonly string[] English={"SAMPLE","INST","PLANT"};
        static readonly string[] Korean={"시료","장비","식물"};
        void LateUpdate()
        {
            var names=language&&language.English?English:Korean;
            for(int i=0;i<labels.Length&&i<names.Length;i++)if(labels[i])labels[i].text=names[i];
        }
    }
}
