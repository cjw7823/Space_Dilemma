using System;
using SpaceDilemma.Power;
using SpaceDilemma.Research;
using SpaceDilemma.Results;
using UnityEngine;

namespace SpaceDilemma.Scenario
{
    [CreateAssetMenu(menuName = "Space Dilemma/Scenario Rules", fileName = "Scenario Rules")]
    public sealed class ScenarioRules : ScriptableObject
    {
        [InspectorName("시간과 SPE")] public ScenarioTime time = new();
        [InspectorName("방사선과 차폐")] public ScenarioRadiation radiation = new();
        [InspectorName("상호작용과 실내 판정")] public ScenarioInteraction interaction = new();
        [InspectorName("기지 전력")] public ScenarioPower power = new();
        [InspectorName("시료 보존 품질")] public ScenarioPreservation preservation = new();
        [InspectorName("시료 채취·보존")] public ScenarioMission samples = new(0, 60, 70, 80);
        [InspectorName("측정장비")] public ScenarioMission instruments = new(1, 40, 50, 60);
        [InspectorName("식물 재배")] public ScenarioMission plants = new(2, 70, 80, 90);
        [InspectorName("시작 시간·선량·시료")] public ScenarioStart start = new();

        public ScenarioMission Mission(ResearchMission mission) => mission switch
        {
            ResearchMission.Samples => samples,
            ResearchMission.Instruments => instruments,
            ResearchMission.Plants => plants,
            _ => throw new ArgumentOutOfRangeException(nameof(mission))
        };

        public ResearchWeights GetResearchWeights() =>
            new(samples.scoreWeight, instruments.scoreWeight, plants.scoreWeight);
    }

    [Serializable]
    public sealed class ScenarioTime
    {
        [InspectorName("제한 시간 (게임시간)"), Min(.001f)] public float durationHours = 48;
        [InspectorName("실제 1초당 게임시간"), Min(.0001f)]
        [Tooltip("0.2라면 실제 5초에 게임 시간 1시간이 흐릅니다.")] public float gameHoursPerSecond = .2f;
        [InspectorName("SPE 시작 시각 (게임시간)"), Min(0)]
        [Tooltip("제한 시간 이상으로 설정하면 플레이 중 SPE가 발생하지 않습니다.")] public float speStartHour = 24;
        [InspectorName("SPE 지속시간 (게임시간)"), Min(0)]
        [Tooltip("0이면 발생 후 임무 종료까지 유지됩니다. 양수면 해당 시간이 지난 뒤 평상시 선량률로 돌아갑니다.")]
        public float speDurationHours;
        [InspectorName("태양·그림자 갱신 간격 (초)"), Min(0)]
        [Tooltip("0이면 매 프레임 갱신합니다. 태양과 지구는 같은 임무 시계를 사용합니다.")]
        public float sunUpdateSeconds = 1;
    }

    [Serializable]
    public sealed class ScenarioRadiation
    {
        [InspectorName("RadLab 전후 평균 사용")] public bool useRadLabAverages;
        [InspectorName("게임 환산계수 k"), Min(0)]
        [Tooltip("관측 평균 × k / 1000으로 기준 선량률을 구합니다. 잔여량 보정 사용 시 SPE 선량률은 사건 순간 별도로 계산합니다. 인체 유효선량 환산이 아닙니다.")]
        public float gameGain = 200;
        [InspectorName("이벤트 전 평균 (μGy/h)"), Min(0)] public double preAverageUGyPerHour;
        [InspectorName("이벤트 후 평균 (μGy/h)"), Min(0)] public double postAverageUGyPerHour;
        [InspectorName("평상시 실외 선량률 (mSv/게임시간)"), Min(0)] public float normalRate = .1f;
        [InspectorName("SPE 실외 선량률 (mSv/게임시간)"), Min(0)] public float speRate = 3;
        [InspectorName("SPE 시작 시 남은 피폭 한도 보정")]
        [Tooltip("사건 순간 (위험 기준−누적 선량)/남은 게임시간 × 여유 배율로 실외 SPE 선량률을 한 번 고정합니다. 이후 실내·차폐로 줄어도 재계산하지 않습니다.")]
        public bool fitSpeToRemainingDose;
        [InspectorName("SPE 잔여량 보정 여유 배율"), Min(1)]
        [Tooltip("1이면 계속 실외에 있을 때 임무 종료 순간 100%, 1.1이면 남은 시간의 1/1.1에 한도에 도달합니다. 잔여량 보정이 켜져 있을 때만 사용합니다.")]
        public float speRemainingDoseMultiplier = 1.1f;
        public double NormalRate => useRadLabAverages ? Rate(preAverageUGyPerHour) : normalRate;
        public double SpeRate => useRadLabAverages ? Rate(postAverageUGyPerHour) : speRate;
        double Rate(double observed) => double.IsFinite(observed) && float.IsFinite(gameGain)
            ? Math.Max(0, observed) * Math.Max(0, gameGain) / 1000 : 0;
        [InspectorName("위험 기준 (mSv)"), Min(.001f)]
        [Tooltip("누적 선량이 이 값에 도달하면 사망합니다. 피폭도 게이지의 100% 기준입니다.")]
        public float lethalDose = 50;
        [InspectorName("실내 수동차폐 투과율"), Range(0, 1)]
        [Tooltip("실외 선량 중 실내에 도달하는 비율입니다. 0.35는 35%입니다.")] public float indoorTransmission = .35f;
        [InspectorName("능동차폐 최대 감소율"), Range(0, 1)]
        [Tooltip("실내 SPE에서 차폐 장치가 전력을 전부 공급받을 때의 추가 감소율입니다. 0.8은 80%입니다.")]
        public float maximumReduction = .8f;
    }

    [Serializable]
    public sealed class ScenarioInteraction
    {
        [InspectorName("연구 접근 거리 (m)"), Min(.1f)] public float researchRange = 2;
        [InspectorName("연구 E키 유지 시간 (초)"), Min(.05f)] public float holdSeconds = 1.25f;
        [InspectorName("조작 중단 시 진행 초기화")] public bool resetWhenInterrupted = true;
        [InspectorName("전력 콘솔 접근 거리 (m)"), Min(.1f)] public float consoleRange = 2;
        [InspectorName("실내 판정 높이 (m)"), Min(0)]
        [Tooltip("플레이어 위치에서 이 높이만큼 위의 점으로 실내를 판정합니다.")] public float indoorSampleHeight = 1;
    }

    [Serializable]
    public sealed class ScenarioPower
    {
        [InspectorName("총 가용 전력 (W)"), Min(0)] public float availableWatts = 1000;
        [InspectorName("생명유지 요구 전력 (W)"), Min(0)] public float lifeSupportWatts = 250;
        [InspectorName("차폐 최대 소비전력 (W)"), Min(0)] public float shieldMaximumWatts = 500;
        [InspectorName("시작 차폐 출력 요청"), Range(0, 1)]
        [Tooltip("0은 꺼짐, 1은 100% 요청입니다. 실제 출력은 공급 전력으로 결정됩니다.")]
        public float initialShieldOutput;
    }

    [Serializable]
    public sealed class ScenarioPreservation
    {
        [InspectorName("냉각 중단 유예 (게임시간)"), Min(0)] public float outageGraceHours = .5f;
        [InspectorName("유예 후 게임시간당 품질 손실"), Min(0)]
        [Tooltip("0.15는 매 게임 시간마다 품질 15%p 손실입니다.")] public float qualityLossPerHour = .15f;
        [InspectorName("연구불가 품질 기준"), Range(0, 1)]
        [Tooltip("품질이 이 값 이하이면 신규 연구를 중단합니다. 0.25는 품질 25%입니다.")]
        public float minimumUsableQuality = .25f;
    }

    [Serializable]
    public sealed class ScenarioMission
    {
        [InspectorName("목표 데이터량"), Min(.001f)] public float targetData = 72;
        [InspectorName("달성률 상한 (%)"), Range(0, 100)] public float maximumPercent = 100;
        [InspectorName("전체 연구 점수 비중"), Min(0)]
        [Tooltip("세 미션의 상대 비중을 합계 100%로 환산합니다.")] public float scoreWeight = 1;
        [InspectorName("시설 공급 우선순위")]
        [Tooltip("작은 숫자가 먼저 공급받습니다.")] public int supplyPriority;
        [InspectorName("시작 전력 요청 (W)"), Min(-1)]
        [Tooltip("-1이면 세 장치의 소비전력 합계를 요청합니다. 0은 시설 전원 요청을 끕니다.")]
        public float initialRequestedWatts = -1;
        [InspectorName("장치 1")] public ScenarioDevice first;
        [InspectorName("장치 2")] public ScenarioDevice second;
        [InspectorName("장치 3")] public ScenarioDevice third;

        public ScenarioMission(int priority, float firstWatts, float secondWatts, float thirdWatts)
        {
            supplyPriority = priority;
            first = new ScenarioDevice { watts = firstWatts, priority = 0 };
            second = new ScenarioDevice { watts = secondWatts, priority = 1 };
            third = new ScenarioDevice { watts = thirdWatts, priority = 2 };
        }

        public ScenarioDevice Device(int index) => index switch
        {
            0 => first,
            1 => second,
            2 => third,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        internal PowerFacilityRule ToPowerRule() => new()
        {
            priority = supplyPriority,
            devices = new[] { first.ToPowerRule(), second.ToPowerRule(), third.ToPowerRule() }
        };

        internal ResearchMissionRule ToResearchRule() => new()
        {
            targetData = targetData,
            maximumPercent = maximumPercent,
            devices = new[] { first.ToResearchRule(), second.ToResearchRule(), third.ToResearchRule() }
        };
    }

    [Serializable]
    public sealed class ScenarioDevice
    {
        [InspectorName("소비전력 (W)"), Min(0)] public float watts;
        [InspectorName("장치 공급 우선순위")] public int priority;
        [InspectorName("게임시간당 데이터"), Min(0)] public float unitsPerGameHour = 1;
        [InspectorName("누적 데이터 상한"), Min(0)] public float maximumData = 24;
        [InspectorName("미션 내 데이터 기여도"), Min(0)] public float contribution = 1;
        [InspectorName("시작 전원 요청")] public bool initiallyRequested = true;
        [InspectorName("시작 시 작업 완료")]
        [Tooltip("측정장비는 설치 완료, 재배 구획은 관리 시작입니다. 보관소는 시작 시료 배정으로 결정합니다.")]
        public bool initiallyCompleted;
        [InspectorName("시작 시 유효")] public bool initiallyValid = true;
        [InspectorName("시작 누적 데이터"), Min(0)]
        [Tooltip("작업을 완료했거나 시료가 보관된 장치에만 적용합니다. 누적 상한을 넘지 않습니다.")]
        public float initialData;

        internal PowerDeviceRule ToPowerRule() => new() { watts = watts, priority = priority };
        internal ResearchDeviceRule ToResearchRule() => new()
        {
            unitsPerGameHour = unitsPerGameHour,
            maximumData = maximumData,
            contribution = contribution
        };
    }

    public enum StartingSample
    {
        [InspectorName("빈 보관소")] None,
        [InspectorName("시료 1")] First,
        [InspectorName("시료 2")] Second,
        [InspectorName("시료 3")] Third
    }

    [Serializable]
    public sealed class ScenarioSampleSlot
    {
        [InspectorName("보관할 시료")]
        [Tooltip("배정된 시료는 채취 완료로 처리합니다. 같은 시료는 하나의 슬롯에만 배정하세요.")]
        public StartingSample sample;
        [InspectorName("시작 품질"), Range(0, 1)] public float quality = 1;
    }

    [Serializable]
    public sealed class ScenarioStart
    {
        [InspectorName("경과 시간 (게임시간)"), Min(0)]
        [Tooltip("이전 구간을 재생하지 않고 이 시각부터 시작합니다.")] public float elapsedHours;
        [InspectorName("누적 피폭량 (mSv)"), Min(0)] public float dose;
        [InspectorName("시료 1 채취 완료")] public bool sampleOneCollected;
        [InspectorName("시료 2 채취 완료")] public bool sampleTwoCollected;
        [InspectorName("시료 3 채취 완료")] public bool sampleThreeCollected;
        [InspectorName("보관소 1")] public ScenarioSampleSlot firstSlot = new();
        [InspectorName("보관소 2")] public ScenarioSampleSlot secondSlot = new();
        [InspectorName("보관소 3")] public ScenarioSampleSlot thirdSlot = new();

        public bool SampleCollected(int index) => index switch
        {
            0 => sampleOneCollected,
            1 => sampleTwoCollected,
            2 => sampleThreeCollected,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
        public ScenarioSampleSlot Slot(int index) => index switch
        {
            0 => firstSlot,
            1 => secondSlot,
            2 => thirdSlot,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }
}
