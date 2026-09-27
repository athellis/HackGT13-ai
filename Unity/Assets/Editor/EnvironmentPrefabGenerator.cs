using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


public static class EnvironmentPrefabGenerator
{
    private const string ENVIRONMENTS_ROOT =
        "Assets/Environments";

    private const string TEMPLATE_PREFAB =
        "Assets/Environments/Beach/Prefabs/ENV_BEACH.prefab";


    [MenuItem(
        "Tools/Immersive Shopping/Build Environment Prefabs"
    )]
    public static void BuildEnvironmentPrefabs()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError(
                "Stop Play mode before generating environments."
            );

            return;
        }


        GameObject template =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                TEMPLATE_PREFAB
            );


        if (template == null)
        {
            Debug.LogError(
                "Could not find template prefab at:\n"
                + TEMPLATE_PREFAB
            );

            return;
        }


        Dictionary<string, GameObject> builtPrefabs =
            new Dictionary<string, GameObject>();


        // Beach already exists and is our template.
        builtPrefabs["beach"] = template;


        string[] environmentFolders =
            AssetDatabase.GetSubFolders(
                ENVIRONMENTS_ROOT
            );


        foreach (string environmentFolder
                 in environmentFolders)
        {
            string folderName =
                Path.GetFileName(
                    environmentFolder
                );


            string environmentId =
                folderName
                    .ToLowerInvariant()
                    .Replace(" ", "_");


            // Beach is already finished.
            if (environmentId == "beach")
            {
                continue;
            }


            string texturesFolder =
                environmentFolder
                + "/Textures";


            if (
                !AssetDatabase.IsValidFolder(
                    texturesFolder
                )
            )
            {
                Debug.LogWarning(
                    "Skipping "
                    + environmentId
                    + ": no Textures folder."
                );

                continue;
            }


            // ----------------------------------------
            // FIND HDR / EXR AUTOMATICALLY
            // ----------------------------------------

            string[] textureGuids =
                AssetDatabase.FindAssets(
                    "t:Texture2D",
                    new[]
                    {
                        texturesFolder
                    }
                );


            string hdriPath = null;


            foreach (string guid in textureGuids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guid
                    );


                string extension =
                    Path.GetExtension(path)
                        .ToLowerInvariant();


                if (
                    extension == ".hdr"
                    || extension == ".exr"
                )
                {
                    hdriPath = path;
                    break;
                }
            }


            if (hdriPath == null)
            {
                Debug.LogWarning(
                    "Skipping "
                    + environmentId
                    + ": no HDR or EXR found."
                );

                continue;
            }


            Debug.Log(
                "Building environment: "
                + environmentId
            );


            // ----------------------------------------
            // FORCE TEXTURE MAX SIZE TO 1K
            // ----------------------------------------

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    hdriPath
                ) as TextureImporter;


            if (importer != null)
            {
                importer.maxTextureSize = 1024;

                importer.mipmapEnabled = true;

                importer.SaveAndReimport();
            }


            Texture2D hdri =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    hdriPath
                );


            // ----------------------------------------
            // CREATE MATERIALS FOLDER
            // ----------------------------------------

            string materialsFolder =
                environmentFolder
                + "/Materials";


            EnsureFolder(
                materialsFolder
            );


            // ----------------------------------------
            // CREATE SKYBOX MATERIAL
            // ----------------------------------------

            string upperId =
                environmentId.ToUpperInvariant();


            string materialPath =
                materialsFolder
                + "/MAT_"
                + upperId
                + "_Sky.mat";


            Material skyboxMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath
                );


            Shader skyboxShader =
                Shader.Find(
                    "Skybox/Panoramic"
                );


            if (skyboxShader == null)
            {
                Debug.LogError(
                    "Unity could not find "
                    + "Skybox/Panoramic shader."
                );

                return;
            }


            if (skyboxMaterial == null)
            {
                skyboxMaterial =
                    new Material(
                        skyboxShader
                    );


                AssetDatabase.CreateAsset(
                    skyboxMaterial,
                    materialPath
                );
            }
            else
            {
                skyboxMaterial.shader =
                    skyboxShader;
            }


            // Different Unity versions expose
            // the panorama texture under one
            // of these property names.

            if (
                skyboxMaterial.HasProperty(
                    "_MainTex"
                )
            )
            {
                skyboxMaterial.SetTexture(
                    "_MainTex",
                    hdri
                );
            }


            if (
                skyboxMaterial.HasProperty(
                    "_Tex"
                )
            )
            {
                skyboxMaterial.SetTexture(
                    "_Tex",
                    hdri
                );
            }


            // Latitude / longitude panoramic layout
            if (
                skyboxMaterial.HasProperty(
                    "_Mapping"
                )
            )
            {
                skyboxMaterial.SetFloat(
                    "_Mapping",
                    1f
                );
            }


            skyboxMaterial.DisableKeyword(
                "_MAPPING_6_FRAMES_LAYOUT"
            );


            // 360 degree image
            if (
                skyboxMaterial.HasProperty(
                    "_ImageType"
                )
            )
            {
                skyboxMaterial.SetFloat(
                    "_ImageType",
                    0f
                );
            }


            if (
                skyboxMaterial.HasProperty(
                    "_Exposure"
                )
            )
            {
                skyboxMaterial.SetFloat(
                    "_Exposure",
                    1f
                );
            }


            EditorUtility.SetDirty(
                skyboxMaterial
            );


            // ----------------------------------------
            // CREATE PREFABS FOLDER
            // ----------------------------------------

            string prefabsFolder =
                environmentFolder
                + "/Prefabs";


            EnsureFolder(
                prefabsFolder
            );


            string prefabPath =
                prefabsFolder
                + "/ENV_"
                + upperId
                + ".prefab";


            // ----------------------------------------
            // COPY BEACH TEMPLATE
            // ----------------------------------------

            GameObject existingPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath
                );


            if (existingPrefab == null)
            {
                bool copied =
                    AssetDatabase.CopyAsset(
                        TEMPLATE_PREFAB,
                        prefabPath
                    );


                if (!copied)
                {
                    Debug.LogError(
                        "Failed to create prefab for "
                        + environmentId
                    );

                    continue;
                }
            }


            // ----------------------------------------
            // EDIT COPIED PREFAB
            // ----------------------------------------

            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath
                );


            prefabRoot.name =
                "ENV_" + upperId;


            EnvironmentData data =
                prefabRoot.GetComponent<
                    EnvironmentData
                >();


            if (data == null)
            {
                data =
                    prefabRoot.AddComponent<
                        EnvironmentData
                    >();
            }


            data.environmentId =
                environmentId;


            data.skyboxMaterial =
                skyboxMaterial;


            EditorUtility.SetDirty(
                data
            );


            PrefabUtility.SaveAsPrefabAsset(
                prefabRoot,
                prefabPath
            );


            PrefabUtility.UnloadPrefabContents(
                prefabRoot
            );


            GameObject finishedPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath
                );


            builtPrefabs[
                environmentId
            ] = finishedPrefab;


            Debug.Log(
                "Created: "
                + prefabPath
            );
        }


        // --------------------------------------------
        // AUTO-REGISTER EVERYTHING
        // WITH ENVIRONMENT MANAGER
        // --------------------------------------------

        EnvironmentManager manager =
            Object.FindFirstObjectByType<
                EnvironmentManager
            >();


        if (manager == null)
        {
            Debug.LogWarning(
                "Prefabs were created, but no "
                + "EnvironmentManager exists "
                + "in the current scene."
            );
        }
        else
        {
            if (manager.environments == null)
            {
                manager.environments =
                    new List<
                        EnvironmentManager.EnvironmentEntry
                    >();
            }


            foreach (
                KeyValuePair<string, GameObject> pair
                in builtPrefabs
            )
            {
                EnvironmentManager.EnvironmentEntry
                    existing =
                        manager.environments
                            .FirstOrDefault(
                                entry =>
                                    entry.environmentId
                                    == pair.Key
                            );


                if (existing != null)
                {
                    existing.prefab =
                        pair.Value;
                }
                else
                {
                    manager.environments.Add(
                        new EnvironmentManager
                            .EnvironmentEntry
                        {
                            environmentId =
                                pair.Key,

                            prefab =
                                pair.Value
                        }
                    );
                }
            }


            EditorUtility.SetDirty(
                manager
            );


            EditorSceneManager.MarkSceneDirty(
                manager.gameObject.scene
            );
        }


        AssetDatabase.SaveAssets();

        AssetDatabase.Refresh();


        Debug.Log(
            "==================================\n"
            + "ENVIRONMENT GENERATION COMPLETE\n"
            + "=================================="
        );
    }


    private static void EnsureFolder(
        string folderPath
    )
    {
        if (
            AssetDatabase.IsValidFolder(
                folderPath
            )
        )
        {
            return;
        }


        string parent =
            Path.GetDirectoryName(
                folderPath
            ).Replace(
                "\\",
                "/"
            );


        string folderName =
            Path.GetFileName(
                folderPath
            );


        AssetDatabase.CreateFolder(
            parent,
            folderName
        );
    }
}