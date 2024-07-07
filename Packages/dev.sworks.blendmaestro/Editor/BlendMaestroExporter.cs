using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using dev.sworks.blendmaestro.runtime.model;
using dev.sworks.blendmaestro.runtime.utils;

namespace dev.sworks.blendmaestro.runtime.Editor
{
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
        private static readonly Vector2 initialSize = new Vector2(400, 300);

        [MenuItem("Tools/BlendMaestro/Exporter")]
        [MenuItem("GameObject/BlendMaestro/Exporter")]

        public static void ShowWindow(MenuCommand menuCommand)
        {
            BlendMaestroExporter window = GetWindow<BlendMaestroExporter>("BlendMaestro-Exporter");

            window.selectedFbx = menuCommand.context as GameObject;
            window.minSize = initialSize;
            window.maxSize = initialSize;
        }

        // メニューアイテムの有効/無効を切り替える条件を設定
        [MenuItem("GameObject/BlendMaestro/Exporter", true)]
        private static bool ValidateShowWindow()
        {
            return Selection.activeObject is GameObject;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("アバターを選択してください:", EditorStyles.boldLabel);
            GameObject newSelectedFbx = (GameObject)EditorGUILayout.ObjectField(selectedFbx, typeof(GameObject), true);

            if (newSelectedFbx != selectedFbx)
            {
                selectedFbx = newSelectedFbx;
                if (selectedFbx != null)
                {
                    LoadMeshesWithBlendShapes(selectedFbx);
                    LoadBlendShapes(selectedMesh);
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

                        if (GUILayout.Button("Export DataAsset"))
                        {
                            ExportBlendShapesToAsset();
                        }
                    }
                }
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
            }
            else
            {
                EditorUtility.DisplayDialog("ERROR", "ブレンドシェイプが存在するメッシュが見つかりませんでした\nFBXのデータを確認してください", "OK");
                return;
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

        private bool checkFBXImporter()
        {
            GameObject fbx = selectedFbx;
            var type = PrefabUtility.GetPrefabInstanceStatus(fbx);
            if (type != PrefabInstanceStatus.NotAPrefab)
            {
                fbx = PrefabUtility.GetCorrespondingObjectFromSource(selectedFbx) as GameObject;
            }

            string fbxPath = AssetDatabase.GetAssetPath(fbx);
            ModelImporter modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (modelImporter == null)
            {
                EditorUtility.DisplayDialog("ERROR", "FBXのパス取得に失敗しました。\nFBXデータを確認してください。", "OK");
                return false;
            }

            // check legacy blencshape normals
            string pName = "legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes";
            PropertyInfo prop = modelImporter.GetType().GetProperty(pName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (!(bool)prop.GetValue(modelImporter))
            {
                // Enable Legacy BlendShape Normals
                prop.SetValue(modelImporter, true);
                EditorUtility.SetDirty(modelImporter);
                modelImporter.SaveAndReimport();
                return true;
            }
            return true;
        }

        private void ExportBlendShapesToAsset()
        {
            if (selectedMesh == null || selectedBlendShapeIndices.Count == 0)
            {
                EditorUtility.DisplayDialog("ERROR", "ブレンドシェイプキーを1つ以上選択してください。", "OK");
                return;
            }

            if (!checkFBXImporter())
            {
                EditorUtility.DisplayDialog("ERROR", "Legacy BlendShape NormalsがOFFになっています\n自動ONに失敗したため、手動でONにしてください。", "OK");
                return;
            }
            List<BlendMaestroData> blendShapesData = new List<BlendMaestroData>();
            foreach (var index in selectedBlendShapeIndices)
            {
                BlendMaestroData data = new BlendMaestroData
                {
                    name = selectedMesh.GetBlendShapeName(index),
                    deltaVertices = new Vector3[selectedMesh.vertexCount],
                    deltaNormals = new Vector3[selectedMesh.vertexCount],
                    deltaTangents = new Vector3[selectedMesh.vertexCount]
                };
                selectedMesh.GetBlendShapeFrameVertices(index, 0, data.deltaVertices, data.deltaNormals, data.deltaTangents);
                blendShapesData.Add(data);
            }

            BlendMaestroDataAsset exportData = new BlendMaestroDataAsset
            {
                meshName = selectedMesh.name,
                blendShapes = blendShapesData
            };

            string json = JsonUtility.ToJson(exportData, false);
            byte[] compressJson = AssetsComp.Compress(json);
            BlendMaestroDataAssets asset = ScriptableObject.CreateInstance<BlendMaestroDataAssets>();
            asset.VertexData = compressJson;

            string path = EditorUtility.SaveFilePanelInProject("Export", "BlendMaestroDataAssets", "asset", "保存する場所を選択してください");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;

                EditorUtility.DisplayDialog("Complete", "Complete!", "OK");
            }
        }
    }
}