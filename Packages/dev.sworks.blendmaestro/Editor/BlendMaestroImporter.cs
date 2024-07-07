using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using dev.sworks.blendmaestro.runtime.model;
using dev.sworks.blendmaestro.runtime.utils;

namespace dev.sworks.blendmaestro.Editor
{
    public class BlendMaestroImporter : EditorWindow
    {
        private BlendMaestroDataAssets blendMaestroDataAssets;
        private GameObject avater;
        private GameObject prefabVariant;
        private GameObject newAvater;
        private bool modified = false;

        private List<SkinnedMeshRenderer> avaterMeshRendererList = new List<SkinnedMeshRenderer>();
        private List<string> avaterMeshNames = new List<string>();
        private List<SkinnedMeshRenderer> variantMeshRendererList = new List<SkinnedMeshRenderer>();
        private static readonly Vector2 initialSize = new Vector2(400, 300);

        [MenuItem("Tools/BlendMaestro/Importer")]
        [MenuItem("GameObject/BlendMaestro/Importer")]

        public static void ShowWindow(MenuCommand menuCommand)
        {
            BlendMaestroImporter window = GetWindow<BlendMaestroImporter>("BlendMaestro-Importer");
            window.avater = menuCommand.context as GameObject;
            window.minSize = initialSize;
            window.maxSize = initialSize;
        }
        
        // メニューアイテムの有効/無効を切り替える条件を設定
        [MenuItem("GameObject/BlendMaestro/Importer", true)]
        private static bool ValidateShowWindow()
        {
            return Selection.activeObject is GameObject;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("アバターを選択してください:", EditorStyles.boldLabel);
            avater = (GameObject)EditorGUILayout.ObjectField(avater, typeof(GameObject), true);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("BlendMaestroDataAssetsを選択してください:", EditorStyles.boldLabel);
            blendMaestroDataAssets = (BlendMaestroDataAssets)EditorGUILayout.ObjectField(blendMaestroDataAssets, typeof(BlendMaestroDataAssets), true);
            EditorGUILayout.Space();

            if (GUILayout.Button("Import"))
            {
                ImportBlendShapesFromAssets();
            }

            // Prefab更新チェック
            if (avater != null)
            {
                if (newAvater != avater)
                {
                    newAvater = avater;
                    getAvaterMeshList();
                }
            }
        }

        private void getAvaterMeshList()
        {
            avaterMeshNames.Clear();
            avaterMeshRendererList.Clear();
            var skinnedMeshRenderer = avater.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var smr in skinnedMeshRenderer)
            {
                avaterMeshRendererList.Add(smr);
                avaterMeshNames.Add(smr.sharedMesh.name);
            }

        }
        private void getVariantMeshList()
        {
            variantMeshRendererList.Clear();
            var skinnedMeshRenderer = prefabVariant.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var smr in skinnedMeshRenderer)
            {
                variantMeshRendererList.Add(smr);
            }
        }

        private void createPrefabVariant()
        {
            // Object prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(avater);
            if (avater != null)
            {
                prefabVariant = Instantiate(avater);
                // prefabVariant = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                prefabVariant.name = avater.name + "_Modified";
                // prefabVariant.name = prefab.name + "_Modified";
                getVariantMeshList();
            }
        }

        private void ImportBlendShapesFromAssets()
        {
            if (blendMaestroDataAssets == null)
            {
                EditorUtility.DisplayDialog("ERROR", "シェイプキーデータを指定してください", "OK");
                return;
            }

            // json復号
            string json = AssetsComp.Decompress(blendMaestroDataAssets.VertexData);
            BlendMaestroDataAsset importData = JsonUtility.FromJson<BlendMaestroDataAsset>(json);
            if (importData == null)
            {
                EditorUtility.DisplayDialog("ERROR", "シェイプキーデータの復元に失敗しました\nデータ制作者に問い合わせてください。", "OK");
                return;
            }

            modified = false;
            // 変更チェック
            foreach (var mesh in avaterMeshRendererList)
            {
                Debug.Log(mesh);
                if (mesh.sharedMesh.name == importData.meshName)
                {
                    modified = true;
                    createPrefabVariant();
                    break;
                }
            }

            // _Modifiedに変更を適応する
            if (modified)
            {
                foreach (var mesh in variantMeshRendererList)
                {
                    if (mesh.sharedMesh.name == importData.meshName)
                    {
                        Mesh newMesh = Instantiate(mesh.sharedMesh);
                        newMesh.name = mesh.sharedMesh.name;

                        foreach (var blandshape in importData.blendShapes)
                        {
                            int existingIndex = mesh.sharedMesh.GetBlendShapeIndex(blandshape.name);
                            if (mesh.sharedMesh.GetBlendShapeIndex(blandshape.name) != -1)
                            {
                                // 上書きするために既存のブレンドシェイプを削除
                                newMesh = removeBlendShape(newMesh, blandshape.name);
                            }

                            newMesh.AddBlendShapeFrame(
                                blandshape.name,
                                100.0f,  // Weight of the blend shape
                                blandshape.deltaVertices,
                                blandshape.deltaNormals,
                                blandshape.deltaTangents
                            );

                        }
                        mesh.sharedMesh = newMesh;
                    }
                }
                EditorUtility.DisplayDialog("完了", "インポートが完了しました", "OK");
                Debug.Log("ブレンドシェイプデータがインポートされました");
            }
        }


        private Mesh removeBlendShape(Mesh mesh, string name)
        {
            int blendShapeIndex = mesh.GetBlendShapeIndex(name);

            if (blendShapeIndex < 0)
            {
                EditorUtility.DisplayDialog("ERROR", "シェイプキーデータの上書きに失敗しました\nデータ制作者に問い合わせてください。", "OK");
                return mesh;
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
            return newMesh;
        }
    }

}