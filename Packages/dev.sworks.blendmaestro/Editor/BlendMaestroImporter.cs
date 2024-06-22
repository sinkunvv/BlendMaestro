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
        private string prefabVariantPath = "";
        private string prefabVariantName = "";
        private GameObject newAvater;
        private bool modified = false;

        private List<SkinnedMeshRenderer> avaterMeshRendererList = new List<SkinnedMeshRenderer>();
        private List<string> avaterMeshNames = new List<string>();
        private List<SkinnedMeshRenderer> variantMeshRendererList = new List<SkinnedMeshRenderer>();

        [MenuItem("Tools/BlendMaestro/Importer")]
        public static void ShowWindow()
        {
            GetWindow<BlendMaestroImporter>("BlendMaestro-Importer");
        }


        private void OnGUI()
        {
            EditorGUILayout.LabelField("FBXファイルを選択してください:", EditorStyles.boldLabel);
            avater = (GameObject)EditorGUILayout.ObjectField(avater, typeof(GameObject), true);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("JSONファイルを選択してください:", EditorStyles.boldLabel);
            blendMaestroDataAssets = (BlendMaestroDataAssets)EditorGUILayout.ObjectField(blendMaestroDataAssets, typeof(BlendMaestroDataAssets), true);
            EditorGUILayout.Space();

            if (GUILayout.Button("ブレンドシェイプをインポート"))
            {
                ImportBlendShapesFromJson();
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
            Object prefab = PrefabUtility.GetCorrespondingObjectFromSource(avater);
            if (prefab != null)
            {
                prefabVariant = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                prefabVariant.name = prefabVariant.name + "_Modified";
                prefabVariantPath = UnityEditor.AssetDatabase.GetAssetPath(prefab).Replace(prefab.name, prefabVariantName);
                // PrefabUtility.SaveAsPrefabAsset(prefabVariant, prefabVariantPath);
                getVariantMeshList();
            }
        }

        private void ImportBlendShapesFromJson()
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