using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class SolarImportCheck
{
    const string Root = "Assets/Model/large-solar-panel-array/";
    static SolarImportCheck() { EditorApplication.update += Update; }
    static void Update()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || !File.Exists("Temp/SolarCheck.request")) return;
        File.Delete("Temp/SolarCheck.request");
        try
        {
            var log = new System.Collections.Generic.List<string>();
            foreach (var name in new[] {"LargeSolarPanelArray", "LargeSolarPanelArrayDamaged"})
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + "_URP.mat");
                if (!mat || mat.shader.name != "Universal Render Pipeline/Lit" || !mat.shader.isSupported) throw new Exception("Invalid shader " + name);
                foreach (var prop in new[] {"_BaseMap", "_BumpMap", "_MetallicGlossMap"})
                    if (!mat.GetTexture(prop)) throw new Exception("Missing " + prop);
                if (!mat.IsKeywordEnabled("_NORMALMAP") || !mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP")) throw new Exception("Missing keywords");
                foreach (var suffix in new[] {"Normal", "Metallic", "Roughness", "MetallicSmoothness"})
                {
                    var imp = (TextureImporter)AssetImporter.GetAtPath(Root + "textures/" + name + "_" + suffix + ".png");
                    if (imp.sRGBTexture || (suffix == "Normal" && imp.textureType != TextureImporterType.NormalMap)) throw new Exception("Invalid import " + suffix);
                }
                log.Add(name + ": supported URP Lit, all three texture slots and keywords, linear data and normal importer PASS");
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "source/LargeSolarPanelArray.fbx");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach(var mat in renderer.sharedMaterials)
                    if (!AssetDatabase.GetAssetPath(mat).StartsWith(Root + "Materials/")) throw new Exception("Model remap failed " + renderer.name);
            log.Add("Imported model material remaps PASS");
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var slots = renderer.sharedMaterials;
                var meshFilter = renderer.GetComponent<MeshFilter>();
                bool solarMesh = meshFilter && AssetDatabase.GetAssetPath(meshFilter.sharedMesh) == Root + "source/LargeSolarPanelArray.fbx";
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    var original = slots[i];
                    if (!solarMesh || (original && AssetDatabase.GetAssetPath(original).StartsWith(Root + "Materials/"))) continue;
                    log.Add("Repair scene slot " + renderer.name + " previous=" + (original ? original.name + " " + AssetDatabase.GetAssetPath(original) + " " + original.shader.name : "null"));
                    string materialName = meshFilter.sharedMesh.name.Contains("Damaged") ? "LargeSolarPanelArrayDamaged" : "LargeSolarPanelArray";
                    var replacement = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + materialName + "_URP.mat");
                    if (!replacement) continue;
                    slots[i] = replacement;
                    changed = true;
                }
                if (changed)
                {
                    Undo.RecordObject(renderer, "Connect solar panel URP material");
                    renderer.sharedMaterials = slots;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
                }
            }
            var expected = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/LargeSolarPanelArray_URP.mat");
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.sharedMaterials.Contains(expected)).ToArray();
            if (renderers.Length < 4) throw new Exception("Scene material slots missing: " + renderers.Length);
            log.Add("Scene renderers mapped=" + renderers.Length);
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var go = new GameObject("QA solar camera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 1000;
                camera.fieldOfView = 45;
                camera.transform.position = bounds.center + new Vector3(-1, .7f, -1).normalized * bounds.extents.magnitude * 2.8f;
                camera.transform.LookAt(bounds.center);
                MoonBaseValidation.Render(camera, 1280, "QA/SolarPanel/scene-1280.png");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            File.WriteAllLines("QA/SolarPanel/import-check.txt", log.Concat(new[] {"PASS"}));
        }
        catch(Exception e) { File.WriteAllText("QA/SolarPanel/import-check.txt", e.ToString()); }
    }
}
