using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class SolarGridWork
{
    static SolarGridWork() { EditorApplication.update += Update; }
    static void Update()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || !File.Exists("Temp/SolarGrid.request")) return;
        string mode = File.ReadAllText("Temp/SolarGrid.request").Trim();
        File.Delete("Temp/SolarGrid.request");
        try
        {
            var root = GameObject.Find("SolarPanels").transform;
            var rows = new System.Collections.Generic.List<string>();
            foreach (Transform t in root)
            {
                rows.Add($"TEMPLATE {t.name}, position={t.position:F4}, rotation={t.eulerAngles:F3}, scale={t.lossyScale:F4}");
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    rows.Add($"{r.name}: bounds={r.bounds}, mesh={r.GetComponent<MeshFilter>().sharedMesh.name}, material={string.Join(",",r.sharedMaterials.Select(m=>m ? m.name : "null"))}");
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    var vertices = mesh.vertices.Select(v => r.transform.TransformPoint(v)).ToArray();
                    rows.Add($"vertices={vertices.Length}, bottom={vertices.Min(v=>v.y):F6}");
                }
            }
            File.WriteAllLines("Temp/SolarGrid-info.txt",rows);
            if (mode == "place") Place(root);
        }
        catch (Exception e) { File.WriteAllText("Temp/SolarGrid-error.txt",e.ToString()); }
    }

    static float Height(Vector3 p)
    {
        foreach (var terrain in Terrain.activeTerrains)
        {
            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            if (p.x >= origin.x && p.x <= origin.x + size.x && p.z >= origin.z && p.z <= origin.z + size.z)
                return terrain.SampleHeight(p) + origin.y;
        }
        throw new Exception("No terrain at " + p);
    }

    static Vector3[] Foot(Transform panel)
    {
        var filter = panel.GetComponent<MeshFilter>();
        var vertices = filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
        float bottom = vertices.Min(v => v.y);
        return vertices.Where(v => v.y <= bottom + .003f).ToArray();
    }

    static void Place(Transform root)
    {
        if (root.childCount != 2) throw new Exception("Expected exactly two original panels");
        var originals = root.Cast<Transform>().ToArray();
        var start = originals[0].position;
        var column = originals[1].position - start;
        column.y = 0;
        float spacing = column.magnitude;
        if (spacing < 8) throw new Exception("Panel spacing too small");
        column.Normalize();
        var row = Vector3.Cross(Vector3.up, column);
        var plans = new System.Collections.Generic.List<Vector3>();
        for (int r = 0; r < 10; r++)
            for (int c = 0; c < 10; c++)
            {
                var p = start + (column * c + row * r) * spacing;
                Height(p);
                plans.Add(p);
            }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Arrange solar panels 10x10 on terrain");
        try
        {
            var log = new System.Collections.Generic.List<string> {"row,column,x,y,z,minFootGap,maxFootGap"};
            int index = 0;
            foreach (var p in plans)
            {
                int c = index % 10;
                int r = index / 10;
                Transform panel;
                if (index < 2) panel = originals[index];
                else
                {
                    panel = UnityEngine.Object.Instantiate(originals[c % 2].gameObject, root).transform;
                    Undo.RegisterCreatedObjectUndo(panel.gameObject, "Duplicate solar panel");
                }
                Undo.RecordObject(panel, "Ground solar panel");
                panel.name = $"Solar Panel {r+1:D2}-{c+1:D2}";
                panel.position = p;
                var foot = Foot(panel);
                float lift = foot.Max(v => Height(v) - v.y);
                panel.position += Vector3.up * lift;
                foot = Foot(panel);
                float minGap = foot.Min(v => v.y - Height(v));
                float maxGap = foot.Max(v => v.y - Height(v));
                log.Add($"{r+1},{c+1},{panel.position.x:R},{panel.position.y:R},{panel.position.z:R},{minGap:R},{maxGap:R}");
                PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
                index++;
            }
            if (root.childCount != 100) throw new Exception("Incorrect panel count");
            Directory.CreateDirectory("QA/SolarPanel");
            File.WriteAllLines("QA/SolarPanel/grid-heights.csv", log);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            File.WriteAllText("Temp/SolarGrid-done.txt", $"100 panels, spacing {spacing:R}m, row={row}, columns={column}");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
}
