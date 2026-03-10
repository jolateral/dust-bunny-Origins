
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClothDynamics
{
	class BlendShapesTexturePostprocessor : AssetPostprocessor
	{
		void OnPreprocessTexture()
		{
			if (assetPath.Contains("_RT_"))
			{
				var format = TextureImporterFormat.RGBAHalf;

				TextureImporter textureImporter = (TextureImporter)assetImporter;
				TextureImporterPlatformSettings tips = new TextureImporterPlatformSettings();
				tips.name = "Standalone";
				tips.overridden = true;
				tips.textureCompression = TextureImporterCompression.Uncompressed;
				tips.format = format;
				textureImporter.SetPlatformTextureSettings(tips);

				tips = new TextureImporterPlatformSettings();
				tips.name = "iOS";
				tips.overridden = true;
				tips.textureCompression = TextureImporterCompression.Uncompressed;
				tips.format = format;
				textureImporter.SetPlatformTextureSettings(tips);

				textureImporter.npotScale = TextureImporterNPOTScale.None;
				textureImporter.filterMode = FilterMode.Point;
				textureImporter.sRGBTexture = false;
				textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
				textureImporter.mipmapEnabled = false;

				// var fileName = Path.GetFileNameWithoutExtension(assetPath).Replace("_N_", "_");
				// AssetImporter.GetAtPath(assetPath).SetAssetBundleNameAndVariant(fileName, "");

			}
		}
	}


	// public class AssetBundleBuilder : MonoBehaviour
	// {
	// static string path = "Assets/StreamingAssets/AssetBundles";


	// static bool IsDirectoryEmpty(string path)
	// {
	// return !Directory.EnumerateFileSystemEntries(path).Where(x => Path.GetExtension(x) != ".meta").Any();
	// }

	// static void BuildAllAssetBundleByName(string dir, AssetBundleBuild[] builds, bool deleteSource = false)
	// {
	// var path = Application.streamingAssetsPath + "/AssetBundles/" + dir + "/";
	// if (!Directory.Exists(path))
	// Directory.CreateDirectory(path);

	// BuildPipeline.BuildAssetBundles(path, builds, BuildAssetBundleOptions.UncompressedAssetBundle, EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? BuildTarget.iOS : BuildTarget.StandaloneWindows64);

	// if (deleteSource)
	// {
	// Debug.Log("Try Delete: " + dir);

	// string[] dirPath = null;

	// if (builds.Length > 0 && builds[0].assetNames.Length > 0)
	// {
	// dirPath = new string[builds[0].assetNames.Length];
	// for (int i = 0; i < builds[0].assetNames.Length; i++)
	// {
	// dirPath[i] = Path.GetDirectoryName(builds[0].assetNames[i]);
	// }
	// }

	// foreach (var asset in builds)
	// foreach (var files in asset.assetNames)
	// FileUtil.DeleteFileOrDirectory(files);

	// AssetDatabase.Refresh();

	// if(dirPath!=null)
	// for (int i = 0; i < dirPath.Length; i++)
	// if (!string.IsNullOrEmpty(dirPath[i]) && dirPath[i].Contains("_RT"))
	// Directory.Delete(dirPath[i]);

	// }
	// }

	// [MenuItem("Build Asset Bundles/Uncompressed")]
	// static void BuildABsUncompressed()
	// {
	// BuildABsUncompressed(false);
	// }


	// [MenuItem("Build Asset Bundles/Uncompressed (Delete Source)")]
	// static void BuildABsUncompressedAndDelete()
	// {
	// BuildABsUncompressed(true);
	// }

	// static void BuildABsUncompressed(bool deleteSource = false)
	// {
	// var dictionary = new Dictionary<string, List<AssetBundleBuild>>();
	// string[] names = AssetDatabase.GetAllAssetBundleNames();
	// for (int i = 0; i < names.Length; i++)
	// {
	// var fileName = names[i];
	// var name = names[i];

	// var splits = fileName.Split('_');
	// fileName = fileName.Substring(0, fileName.Length - splits[splits.Length - 1].Length - 1);

	// List<AssetBundleBuild> val;
	// if (dictionary.TryGetValue(fileName, out val))
	// {
	// dictionary[fileName].Add(new AssetBundleBuild() { assetBundleName = name, assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(name) });
	// }
	// else
	// {
	// List<AssetBundleBuild> list = new List<AssetBundleBuild>();
	// list.Add(new AssetBundleBuild() { assetBundleName = name, assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(name) });
	// dictionary.Add(fileName, list);
	// }
	// }

	// int count = dictionary.Count;
	// for (int i = 0; i < count; i++)
	// {
	// var pair = dictionary.ElementAt(i);
	// BuildAllAssetBundleByName(pair.Key, pair.Value.ToArray(), deleteSource);
	// }

	// AssetDatabase.Refresh();
	// Resources.UnloadUnusedAssets();
	// }

	// [MenuItem("Build Asset Bundles/Remove Unused")]
	// static void RemoveUnused()
	// {
	// string[] names = AssetDatabase.GetAllAssetBundleNames();
	// for (int i = 0; i < names.Length; i++)
	// {
	// var name = names[i];
	// AssetDatabase.RemoveAssetBundleName(name, false);
	// }
	// }

	// //[MenuItem("Build Asset Bundles/Normal")]
	// //static void BuildABsNone()
	// //{
	// //	if (!Directory.Exists(path)) Directory.CreateDirectory(path);
	// //	BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.None, Application.platform == RuntimePlatform.WindowsEditor ? BuildTarget.StandaloneWindows64 : BuildTarget.iOS);
	// //}

	// //[MenuItem("Build Asset Bundles/Strict Mode ")]
	// //static void BuildABsStrict()
	// //{
	// //	if (!Directory.Exists(path)) Directory.CreateDirectory(path);
	// //	BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.StrictMode, Application.platform == RuntimePlatform.WindowsEditor ? BuildTarget.StandaloneWindows64 : BuildTarget.iOS);
	// //}

	// [MenuItem("Build Asset Bundles/ChangeBundleName")]
	// static void ChangeBundleName()
	// {
	// var sel = Selection.assetGUIDs;
	// foreach (var assetGuid in sel)
	// {
	// string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
	// AssetImporter.GetAtPath(assetPath).SetAssetBundleNameAndVariant(Path.GetFileNameWithoutExtension(assetPath).Replace("_N_", "_"), "");
	// }
	// }

	// [MenuItem("Build Asset Bundles/Texture To Float (iPhone)")]
	// static void TextureToFloatiPhone()
	// {
	// TextureToFloat("iPhone");
	// }

	// [MenuItem("Build Asset Bundles/Texture To Half (iPhone)")]
	// static void TextureToHalfiPhone()
	// {
	// TextureToFloat("iPhone", TextureImporterFormat.RGBAHalf);
	// }

	// [MenuItem("Build Asset Bundles/Texture To Float (Standalone)")]
	// static void TextureToFloatStandalone()
	// {
	// TextureToFloat("Standalone");
	// }

	// static void TextureToFloat(string platform = "Standalone", TextureImporterFormat format = TextureImporterFormat.RGBAFloat)
	// {
	// foreach (var obj in Selection.objects)
	// {
	// var tex = obj as Texture;
	// if (tex)
	// {
	// Debug.Log(tex.name);
	// var path = AssetDatabase.GetAssetPath(tex);
	// var i = TextureImporter.GetAtPath(path) as TextureImporter;

	// var settings = new TextureImporterSettings();
	// i.ReadTextureSettings(settings);
	// settings.textureFormat = format;
	// settings.rgbm = TextureImporterRGBMMode.Off;


	// //i.textureFormat = format;

	// TextureImporterPlatformSettings tips = new TextureImporterPlatformSettings();
	// tips.format = format;
	// i.SetPlatformTextureSettings(tips);
	// i.SetTextureSettings(settings);

	// AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

	// }
	// }
	// }
	// }
}