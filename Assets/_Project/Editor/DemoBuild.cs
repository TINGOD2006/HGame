using System;
using System.IO;
using HGame.Unity;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace HGame.Editor
{
    public static class DemoBuild
    {
        private const string BuildDirectory = "Builds/HGameDemo-0.2.1";
        private const string VerificationDirectory = "verification/0.2.1";
        [MenuItem("HGame/Prepare Demo")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");
            Directory.CreateDirectory("Assets/_Project/Resources/Fonts");
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Resources/Art/Heroine" }))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)continue;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;importer.isReadable=true;importer.maxTextureSize=2048;
                importer.filterMode=path.Contains("walk-sheet")?FilterMode.Point:FilterMode.Bilinear;importer.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Resources/Art/Jrpg" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var tmpPath = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_FontAsset).Assembly).resolvedPath;
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
                AssetDatabase.ImportPackage(Path.Combine(tmpPath, "Package Resources/TMP Essential Resources.unitypackage"), false);
            foreach (var path in new[] { "Assets/_Project/Resources/Art/Kenney/button.png", "Assets/_Project/Resources/Art/Kenney/save.png", "Assets/_Project/Resources/Art/Kenney/gear.png" })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (path.EndsWith("button.png")) importer.spriteBorder = new Vector4(8, 8, 8, 8);
                importer.SaveAndReimport();
            }
            var portraitImporter = AssetImporter.GetAtPath("Assets/_Project/Resources/Art/heroine-reference.png") as TextureImporter;
            if (portraitImporter != null)
            {
                portraitImporter.filterMode = FilterMode.Trilinear;
                portraitImporter.textureCompression = TextureImporterCompression.Uncompressed;
                portraitImporter.maxTextureSize = 2048; portraitImporter.mipmapEnabled = true; portraitImporter.SaveAndReimport();
            }
            const string fontPath = "Assets/_Project/Resources/Fonts/DemoFont.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/NotoSansCJKtc-Regular.otf");
                if (font == null) throw new InvalidOperationException("Noto 繁中字型匯入失敗。");
                var asset = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                asset.name = "DemoFont"; AssetDatabase.CreateAsset(asset, fontPath);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (var texture in asset.atlasTextures) { texture.name = "DemoFont Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            if (pipeline == null) throw new InvalidOperationException("URP 2D 管線缺失。");
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.backgroundColor = UiKit.Background;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.transform.position = new Vector3(0, 0, -10);
            new GameObject("DemoBootstrap", typeof(DemoBootstrap));
            EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/Demo.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/_Project/Scenes/Demo.unity", true) };
            PlayerSettings.companyName = "TINGOD"; PlayerSettings.productName = "HGame System Demo";
            PlayerSettings.bundleVersion = "0.2.1"; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true; PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            DefinitionLoader.Load();
            Debug.Log("HGAME_PREPARE_PASS");
        }

        [MenuItem("HGame/Build Windows Demo")]
        public static void BuildWindows()
        {
            try
            {
                Prepare(); Directory.CreateDirectory(BuildDirectory); Directory.CreateDirectory(VerificationDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/_Project/Scenes/Demo.unity" }, target = BuildTarget.StandaloneWindows64,
                    locationPathName = BuildDirectory + "/HGameDemo.exe", options = BuildOptions.CompressWithLz4
                });
                File.WriteAllText(VerificationDirectory + "/build-report.json", JsonUtility.ToJson(new BuildSummaryRecord {
                    Result = report.summary.result.ToString(), Errors = report.summary.totalErrors,
                    Warnings = report.summary.totalWarnings, Bytes = report.summary.totalSize,
                    Seconds = report.summary.totalTime.TotalSeconds, Output = report.summary.outputPath
                }, true));
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                CopyLicense("Assets/_Project/Fonts/OFL.txt", "OFL-Noto.txt");
                CopyLicense("Assets/_Project/Art/Kenney/kenney_ui-pack-License.txt", "Kenney-UI-CC0.txt");
                CopyLicense("Assets/_Project/Art/Kenney/kenney_game-icons-license.txt", "Kenney-Icons-CC0.txt");
                CopyLicense("Assets/_Project/Resources/Art/Jrpg/town-License.txt", "Kenney-TinyTown-CC0.txt");
                CopyLicense("Assets/_Project/Resources/Art/Jrpg/dungeon-License.txt", "Kenney-TinyDungeon-CC0.txt");
                CopyLicense("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", "OFL-LiberationSans.txt");
                var uguiPath = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_FontAsset).Assembly).resolvedPath;
                CopyLicense(Path.Combine(uguiPath, "LICENSE.md"), "Unity-UGUI-License.txt");
                Debug.Log("HGAME_BUILD_PASS " + report.summary.outputPath);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void CopyLicense(string source, string name)
        {
            Directory.CreateDirectory(BuildDirectory + "/ThirdParty");
            File.Copy(source, BuildDirectory + "/ThirdParty/" + name, true);
        }
        [Serializable] private class BuildSummaryRecord
        { public string Result, Output; public int Errors, Warnings; public ulong Bytes; public double Seconds; }
    }
}
