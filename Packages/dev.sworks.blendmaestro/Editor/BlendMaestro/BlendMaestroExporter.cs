using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class BlendMaestroExporter : EditorWindow
{
    private GameObject selectedFbx;
    private Mesh selectedMesh;
    private int selectedMeshIndex = 0;
    private int newSelectedMeshIndex = 0;
    private List<Mesh> meshList = new List<Mesh>();
    private List<string> blendShapeNames = new List<string>();
    private List<int> selectedBlendShapeIndices = new List<int>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/BlendMaestro/Exporter")]
    public static void ShowWindow()
    {
        GetWindow<BlendMaestroExporter>("BlendMaestro-Exporter");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("FBXファイルを選択してください:", EditorStyles.boldLabel);
        GameObject newSelectedFbx = (GameObject)EditorGUILayout.ObjectField(selectedFbx, typeof(GameObject), true);

        if (newSelectedFbx != selectedFbx)
        {
            selectedFbx = newSelectedFbx;
            if (selectedFbx != null)
            {
                LoadMeshesWithBlendShapes(selectedFbx);
            }
        }

        if (meshList.Count > 0)
        {
            selectedMeshIndex = EditorGUILayout.Popup("メッシュを選択:", selectedMeshIndex, GetMeshNames());
            selectedMesh = meshList[selectedMeshIndex];
            if (newSelectedMeshIndex != selectedMeshIndex)
            {
                LoadBlendShapes(selectedMesh);
            }
            newSelectedMeshIndex = selectedMeshIndex;
            if (selectedMesh != null)
            {
                if (blendShapeNames.Count > 0)
                {
                    EditorGUILayout.LabelField("ブレンドシェイプを選択:", EditorStyles.boldLabel);
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
                    for (int i = 0; i < blendShapeNames.Count; i++)
                    {
                        bool isSelected = selectedBlendShapeIndices.Contains(i);
                        bool newSelected = EditorGUILayout.Toggle(blendShapeNames[i], isSelected);
                        if (newSelected && !isSelected)
                        {
                            selectedBlendShapeIndices.Add(i);
                        }
                        else if (!newSelected && isSelected)
                        {
                            selectedBlendShapeIndices.Remove(i);
                        }
                    }
                    EditorGUILayout.EndScrollView();

                    if (GUILayout.Button("JSONにエクスポート"))
                    {
                        ExportBlendShapesToJson();
                    }
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField("ブレンドシェイプが存在するメッシュが見つかりませんでした");
        }
    }

    private void LoadMeshesWithBlendShapes(GameObject fbx)
    {
        meshList.Clear();
        var meshFilters = fbx.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh.blendShapeCount > 0)
            {
                meshList.Add(mf.sharedMesh);
            }
        }
        selectedMeshIndex = 0; // Reset the selected mesh index

        if (meshList.Count > 0)
        {
            selectedMesh = meshList[0];
            Debug.Log("ブレンドシェイプが存在するメッシュが正常に読み込まれました");
        }
        else
        {
            Debug.LogWarning("ブレンドシェイプが存在するメッシュが見つかりませんでした");
        }
    }

    private void LoadBlendShapes(Mesh mesh)
    {
        blendShapeNames.Clear();
        selectedBlendShapeIndices.Clear();
        int blendShapeCount = mesh.blendShapeCount;
        for (int i = 0; i < blendShapeCount; i++)
        {
            blendShapeNames.Add(mesh.GetBlendShapeName(i));
        }
    }

    private string[] GetMeshNames()
    {
        List<string> names = new List<string>();
        foreach (var mesh in meshList)
        {
            names.Add(mesh.name);
        }
        return names.ToArray();
    }

    private void ExportBlendShapesToJson()
    {
        if (selectedMesh == null || selectedBlendShapeIndices.Count == 0)
        {
            Debug.LogError("メッシュまたはブレンドシェイプが選択されていません");
            return;
        }

        List<BlendShapeData> blendShapesData = new List<BlendShapeData>();
        foreach (var index in selectedBlendShapeIndices)
        {
            BlendShapeData data = new BlendShapeData
            {
                name = selectedMesh.GetBlendShapeName(index),
                deltaVertices = new Vector3[selectedMesh.vertexCount],
                deltaNormals = new Vector3[selectedMesh.vertexCount],
                deltaTangents = new Vector3[selectedMesh.vertexCount]
            };
            selectedMesh.GetBlendShapeFrameVertices(index, 0, data.deltaVertices, data.deltaNormals, data.deltaTangents);
            blendShapesData.Add(data);
        }

        BlendShapeExportData exportData = new BlendShapeExportData
        {
            meshName = selectedMesh.name,
            blendShapes = blendShapesData
        };

        string json = JsonUtility.ToJson(exportData, true);
        string path = EditorUtility.SaveFilePanel("JSONにエクスポート", "", "blendshapes.json", "json");

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("完了", "エクスポートが完了しました", "OK");
        }
    }

    [System.Serializable]
    public class BlendShapeData
    {
        public string name;
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;
    }

    [System.Serializable]
    public class BlendShapeExportData
    {
        public string meshName;
        public List<BlendShapeData> blendShapes;
    }
}
