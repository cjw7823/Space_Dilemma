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
        rules.radiation.fitSpeToRemainingDose=true;
        rules.radiation.speRemainingDoseMultiplier=1.1f;
        EditorUtility.SetDirty(rules);AssetDatabase.SaveAssets();
        const string output="QA/RadLabCalibration/RemainingDose";
        Directory.CreateDirectory(output);
        string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Source))).Replace("-","");
        double before=rules.radiation.NormalRate,after=rules.radiation.SpeRate;
        double atEvent=before*rules.time.speStartHour;
        double remaining=rules.time.durationHours-rules.time.speStartHour;
        double adaptive=(rules.radiation.lethalDose-atEvent)/remaining*rules.radiation.speRemainingDoseMultiplier;
        File.WriteAllText(output+"/applied.txt",$"Source={Source}\nSHA256={hash}\nEvent UTC={data.sep_time_utc}\nInstrument={data.instrument_id}\nPre mean={data.pre_average_uGy_per_h:R} uGy/h\nPost mean={data.post_average_uGy_per_h:R} uGy/h\nk={data.game_gain_arbitrary}\nPre-event game rate={before:R}\nReference fixed post-event game rate (not used in adaptive mode)={after:R}\nObserved ratio={after/before:R}\nAdaptive SPE=true\nRemaining-dose multiplier={rules.radiation.speRemainingDoseMultiplier:R}\nSPE=38.4 game h = 480 play s\nFull duration=48 game h = 600 play s\nLimit=600 game mSv\nAll-outdoor cumulative at event={atEvent:R}\nAll-outdoor adaptive SPE rate={adaptive:R}\nAdaptive formula=(limit-dose at first SPE entry)/(duration-hours at first SPE entry)*remaining-dose multiplier\nContinuously outdoor after SPE reaches the limit after remaining time / multiplier. Indoors and shielding reduce this fixed rate without recalibration.\nThe observed ratio remains provenance, not a constraint on the adaptive gameplay rate. The tool's 24h-offset minute curve is not loaded.\n");
    }
}
