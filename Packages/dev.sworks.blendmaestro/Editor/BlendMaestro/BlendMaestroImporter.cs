using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class BlendMaestroImporter : EditorWindow
{
    private GameObject selectedFbx;
    private Mesh selectedMesh;
    private SkinnedMeshRenderer selectedMeshRenderer;
    private int selectedMeshIndex = 0;
    private List<Mesh> meshList = new List<Mesh>();
    private List<SkinnedMeshRenderer> meshRendererList = new List<SkinnedMeshRenderer>();
    private string jsonFilePath;

    [MenuItem("Tools/BlendMaestro/Importer")]
    public static void ShowWindow()
    {
        GetWindow<BlendMaestroImporter>("BlendMaestro-Importer");
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
            selectedMeshRenderer = meshRendererList[selectedMeshIndex];
        }
        else
        {
            EditorGUILayout.LabelField("ブレンドシェイプが存在するメッシュが見つかりませんでした");
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("JSONファイルを選択してください:", EditorStyles.boldLabel);
        if (GUILayout.Button("JSONを選択"))
        {
            jsonFilePath = EditorUtility.OpenFilePanel("JSONを選択", "", "json");
        }
        EditorGUILayout.LabelField("選択されたJSONファイル: " + jsonFilePath);

        EditorGUILayout.Space();

        if (GUILayout.Button("ブレンドシェイプをインポート"))
        {
            ImportBlendShapesFromJson();
        }
    }

    private void LoadMeshesWithBlendShapes(GameObject fbx)
    {
        meshList.Clear();
        meshRendererList.Clear();
        var meshFilters = fbx.GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh.blendShapeCount > 0)
            {
                meshRendererList.Add(mf);
                meshList.Add(mf.sharedMesh);
            }
        }
        selectedMeshIndex = 0; // Reset the selected mesh index

        if (meshList.Count > 0)
        {
            selectedMesh = meshList[selectedMeshIndex];
            selectedMeshRenderer = meshRendererList[selectedMeshIndex];
            Debug.Log("ブレンドシェイプが存在するメッシュが正常に読み込まれました");
        }
        else
        {
            Debug.LogWarning("ブレンドシェイプが存在するメッシュが見つかりませんでした");
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

    private void ImportBlendShapesFromJson()
    {
        if (string.IsNullOrEmpty(jsonFilePath))
        {
            Debug.LogError("JSONファイルが選択されていません");
            return;
        }

        if (selectedMesh == null)
        {
            Debug.LogError("メッシュが選択されていません");
            return;
        }

        string json = File.ReadAllText(jsonFilePath);
        BlendShapeExportData importData = JsonUtility.FromJson<BlendShapeExportData>(json);

        if (importData == null)
        {
            Debug.LogError("JSONデータの読み込みに失敗しました");
            return;
        }

        if (selectedMesh.name != importData.meshName)
        {
            Debug.LogError("選択したメッシュとJSONデータのメッシュ名が一致しません");
            return;
        }

        if (BackupFbxFile())
        {
            foreach (var blendShapeData in importData.blendShapes)
            {
                int existingIndex = selectedMesh.GetBlendShapeIndex(blendShapeData.name);
                if (existingIndex != -1)
                {
                    bool overwrite = EditorUtility.DisplayDialog(
                        "上書き確認",
                        $"ブレンドシェイプ '{blendShapeData.name}' は既に存在します。上書きしますか？",
                        "はい", "いいえ"
                    );
                    if (!overwrite)
                    {
                        continue;
                    }
                    // 上書きするために既存のブレンドシェイプを削除
                    RemoveBlendShape(selectedMeshRenderer, blendShapeData.name);
                    Debug.Log(selectedMeshRenderer);
                }

                selectedMesh.AddBlendShapeFrame(
                    blendShapeData.name,
                    100.0f,  // Weight of the blend shape
                    blendShapeData.deltaVertices,
                    blendShapeData.deltaNormals,
                    blendShapeData.deltaTangents
                );
            }
            EditorUtility.DisplayDialog("完了", "インポートが完了しました", "OK");
            Debug.Log("ブレンドシェイプデータがインポートされました");
        }
    }

    private void RemoveBlendShape(SkinnedMeshRenderer smr, string name)
    {
        Mesh mesh = smr.sharedMesh;
        int blendShapeIndex = mesh.GetBlendShapeIndex(name);

        if (blendShapeIndex < 0)
        {
            Debug.LogError("BlendShape " + name + " が見つかりませんでした。");
            return;
        }

        // 新しいメッシュを作成し、古いメッシュからデータをコピー
        Mesh newMesh = new Mesh();
        newMesh.name = mesh.name;
        newMesh.vertices = mesh.vertices;
        newMesh.normals = mesh.normals;
        newMesh.tangents = mesh.tangents;
        newMesh.uv = mesh.uv;
        newMesh.triangles = mesh.triangles;
        newMesh.boneWeights = mesh.boneWeights;
        newMesh.bindposes = mesh.bindposes;

        // BlendShapeを除く他のBlendShapeを新しいメッシュに追加
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (i == blendShapeIndex) continue;

            string shapeName = mesh.GetBlendShapeName(i);
            int frameCount = mesh.GetBlendShapeFrameCount(i);

            for (int j = 0; j < frameCount; j++)
            {
                float frameWeight = mesh.GetBlendShapeFrameWeight(i, j);
                Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(i, j, deltaVertices, deltaNormals, deltaTangents);
                newMesh.AddBlendShapeFrame(shapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
            }
        }

        // 新しいメッシュをSkinnedMeshRendererに設定
        Debug.Log("BlendShape " + name + " を削除しました。");
        selectedMesh = newMesh;
        smr.sharedMesh = newMesh;
        Debug.Log("mesh " + selectedMesh + " を削除しました。");
    }
    private bool BackupFbxFile()
    {
        if (selectedFbx == null) return false;

        string assetPath = AssetDatabase.GetAssetPath(selectedFbx);
        if (string.IsNullOrEmpty(assetPath)) return false;

        string backupPath = assetPath.Replace(".fbx", "_origin.fbx");

        if (File.Exists(backupPath))
        {
            bool overwrite = EditorUtility.DisplayDialog("バックアップファイルが存在します",
                "バックアップファイルが既に存在します。上書きしますか？", "はい", "いいえ");
            if (!overwrite)
            {
                return false;
            }
        }

        try
        {
            File.Copy(assetPath, backupPath, true);
            AssetDatabase.Refresh();
            return true;
        }
        catch (IOException e)
        {
            Debug.LogError($"Failed to backup FBX file: {e.Message}");
            return false;
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
