using System.Collections.Generic;
using SpaceDilemma.Research;

namespace SpaceDilemma.Results
{
    public static class EndingMessage
    {
        public static string Survival(FinalReport report,bool english)
        {
            var power=new List<string>();
            var missions=new List<string>();
            string[] sources=english?new[]{"sample-storage cooling","measuring instruments","plant-growth equipment"}:
                new[]{"시료 보관소 냉각 장치","측정장비","식물 재배 장치"};
            string[] names=english?new[]{"sample collection and preservation","instrument research","plant research"}:
                new[]{"시료 채취·보존","측정장비 연구","식물 재배"};
            for(int i=0;i<3;i++) if(report.ShieldInterrupted((ResearchMission)i))
            {
                var devices=new List<string>();
                for(int d=0;d<3;d++) if(report.ShieldAbandoned((ResearchMission)i,d)) devices.Add((d+1).ToString());
                power.Add(sources[i]+(english?" #":" ")+string.Join("·",devices)+(english?"":"번"));
                missions.Add(names[i]);
            }
            string sacrifice=power.Count==0?"":english?
                $"To stay safe from the solar storm, you had to stop supplying power to {string.Join(", ",power)}. As a result, you could not complete these missions: {string.Join(", ",missions)}.\n\n":
                $"당신은 태양풍으로부터 안전하기 위해 {string.Join(", ",power)}에 전력 공급을 중단할 수밖에 없었어요. 이로 인해 안타깝게도 {string.Join(", ",missions)} 미션을 완수할 수 없었습니다.\n\n";
            return sacrifice+(english?"Thanks to you, we gained valuable research data and resources.\nThank you so much!":
                "당신 덕분에 소중한 연구 자료와 자원을 얻게 되었어요.\n정말 감사합니다!");
        }
    }
}
