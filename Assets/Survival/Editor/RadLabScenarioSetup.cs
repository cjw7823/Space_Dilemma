using System;
using System.IO;
using System.Security.Cryptography;
using SpaceDilemma.Scenario;
using UnityEditor;
using UnityEngine;

static class RadLabScenarioSetup
{
    public const string Source="MoonData/Radiation_Analyzer/output_unity_k570/analysis_summary.json";
    public const string RulesPath="Assets/Scenario/Scenario Rules.asset";
    [Serializable] public sealed class Analysis
    {
        public string sep_time_utc,instrument_id,original_unit;
        public bool data_window_qualified;
        public double pre_average_uGy_per_h,post_average_uGy_per_h,game_gain_arbitrary;
    }
    [MenuItem("Tools/Survival/Apply RadLab Average Calibration")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        var data=JsonUtility.FromJson<Analysis>(File.ReadAllText(Source));
        if(!data.data_window_qualified||data.original_unit!="uGy/h"||data.pre_average_uGy_per_h<=0||data.post_average_uGy_per_h<=0)
            throw new InvalidDataException("A qualified RadLab analysis with positive pre/post averages in uGy/h is required.");
        var rules=AssetDatabase.LoadAssetAtPath<ScenarioRules>(RulesPath);Undo.RecordObject(rules,"Apply RadLab average dose rates and eight-minute SPE");
        rules.time.durationHours=48;rules.time.gameHoursPerSecond=.08f;rules.time.speStartHour=38.4f;rules.time.speDurationHours=0;
        rules.radiation.useRadLabAverages=true;rules.radiation.gameGain=(float)data.game_gain_arbitrary;
        rules.radiation.preAverageUGyPerHour=data.pre_average_uGy_per_h;rules.radiation.postAverageUGyPerHour=data.post_average_uGy_per_h;
        rules.radiation.lethalDose=600;
        EditorUtility.SetDirty(rules);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("QA/RadLabCalibration");
        string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Source))).Replace("-","");
        double before=rules.radiation.NormalRate,after=rules.radiation.SpeRate;
        double atEvent=before*38.4,total=atEvent+after*9.6;
        File.WriteAllText("QA/RadLabCalibration/applied.txt",$"Source={Source}\nSHA256={hash}\nEvent UTC={data.sep_time_utc}\nInstrument={data.instrument_id}\nPre mean={data.pre_average_uGy_per_h:R} uGy/h\nPost mean={data.post_average_uGy_per_h:R} uGy/h\nk={data.game_gain_arbitrary}\nGame rates={before:R} -> {after:R} game mSv/game h\nRatio={after/before:R}\nSPE=38.4 game h = 480 play s\nFull duration=48 game h = 600 play s\nLimit=600 game mSv\nOutdoor cumulative at event={atEvent:R}\nOutdoor cumulative at 600s without terminal cutoff={total:R}\nPredicted outdoor game-over seconds={480+(600-atEvent)/(after*.08):R}\nAverages are applied as two constant gameplay segments; the tool's original 24h-offset minute curve is not loaded into this session.\n");
    }
}
