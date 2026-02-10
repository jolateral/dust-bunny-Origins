/*******************************************************************************
The content of this file includes portions of the proprietary AUDIOKINETIC Wwise
Technology released in source code form as part of the game integration package.
The content of this file may not be used without valid licenses to the
AUDIOKINETIC Wwise Technology.
Note that the use of the game engine is subject to the Unity(R) Terms of
Service at https://unity3d.com/legal/terms-of-service
 
License Usage
 
Licensees holding valid licenses to the AUDIOKINETIC Wwise Technology may use
this file in accordance with the end user license agreement provided with the
software or, alternatively, in accordance with the terms contained
in a written agreement between you and Audiokinetic Inc.
Copyright (c) 2025 Audiokinetic Inc.
*******************************************************************************/

#if !(UNITY_DASHBOARD_WIDGET || UNITY_WEBPLAYER || UNITY_WII || UNITY_WIIU || UNITY_NACL || UNITY_FLASH || UNITY_BLACKBERRY) // Disable under unsupported platforms.
#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using System.Linq;
using AK.Wwise.Unity.Logging;

#region GUI
#if UNITY_2018_3_OR_NEWER
	class SettingsProvider : UnityEditor.SettingsProvider
#else
	class EditorWindow : UnityEditor.EditorWindow
#endif
	{
		class Styles
		{
			public static string WwiseProject = "Wwise Project";
			public static UnityEngine.GUIContent WwiseProjectPath = new UnityEngine.GUIContent("Wwise Project Path", "Location of the Wwise project associated with this game. It is recommended to put it in the Unity Project root folder, outside the Assets folder.");

			public static string WwiseApplicationPath = "Wwise Application Path";
			public static UnityEngine.GUIContent WwiseInstallationPath = new UnityEngine.GUIContent("Wwise Application Path", "Location of the Wwise application. This is required to generate the SoundBanks in Unity.");

			public static string AssetManagement = "Asset Management";
			public static UnityEngine.GUIContent SoundbankPath = new UnityEngine.GUIContent("SoundBanks Path", "Location of the SoundBanks relative to (and within) the StreamingAssets folder.");
			public static UnityEngine.GUIContent CopySoundBanksAsPreBuildStep = new UnityEngine.GUIContent("Copy SoundBanks at pre-Build step", "Copies the SoundBanks in the appropriate location for building and deployment. It is recommended to leave this box checked.");
			public static UnityEngine.GUIContent GenerateSoundBanksAsPreBuildStep = new UnityEngine.GUIContent("Generate SoundBanks at pre-Build step", "Generates the SoundBanks before copying them during pre-Build step. It is recommended to leave this box unchecked if SoundBanks are generated on a specific build machine.");

#if AK_WWISE_ADDRESSABLES && UNITY_ADDRESSABLES
			public static UnityEngine.GUIContent GeneratedSoundbankPath = new UnityEngine.GUIContent("Generated SoundBanks Path", "Destination folder for generated SoundBanks. Changing this will update your Wwise project settings");
#endif

			public static string GlobalSettings = "Global Settings";
			public static UnityEngine.GUIContent CreateWwiseGlobal = new UnityEngine.GUIContent("Create WwiseGlobal GameObject", "The WwiseGlobal object is a GameObject that contains the Initializing and Terminating scripts for the Wwise Sound Engine. In the Editor workflow, it is added to every scene, so that it can be properly previewed in the Editor. In the game, only one instance is created, in the first scene, and it is persisted throughout the game. It is recommended to leave this box checked.");
			public static UnityEngine.GUIContent CreateWwiseListener = new UnityEngine.GUIContent("Add Listener to Main Camera", "In order for positioning to work, the AkAudioListener script needs to be attached to the main camera in every scene. If you wish for your listener to be attached to another GameObject, uncheck this box.");
			public static UnityEngine.GUIContent ObjectReferenceAutoCleanup = new UnityEngine.GUIContent("Auto-delete WwiseObjectReferences", "Components that reference Wwise objects such as Events, Banks, and Busses track these references using WwiseObjectReference assets that are created in the Wwise/ScriptableObjects folder. If this option is checked and a Wwise Object has been removed from the Wwise Project, when parsing the Wwise project structure, the corresponding asset in the Wwise/ScriptableObjects folder will be deleted.");
			public static UnityEngine.GUIContent LoadSoundEngineInEditMode = new UnityEngine.GUIContent("Load Sound Engine in Edit Mode", "Load the Sound Engine in Edit Mode. Disable this setting to verify the Sound Engine is properly enabled in-game.");

			public static string InEditorWarnings = "In Editor Warnings";
			public static UnityEngine.GUIContent ShowSpatialAudioWarningMsg = new UnityEngine.GUIContent("Show Spatial Audio Warnings", "Warnings will be displayed on Wwise components that are not configured for Spatial Audio to function properly. It is recommended to leave this box checked.");

			public static string WaapiSection = "Wwise Authoring API (WAAPI)";
			public static UnityEngine.GUIContent UseWaapi = new UnityEngine.GUIContent("Connect to Wwise");
			public static UnityEngine.GUIContent WaapiIP = new UnityEngine.GUIContent("WAAPI IP address");
			public static UnityEngine.GUIContent WaapiPort = new UnityEngine.GUIContent("WAAPI port");
			public static UnityEngine.GUIContent AutosyncSelection = new UnityEngine.GUIContent("Autosync Wwise Browser Selection");

			public static string TranslatorSection = "Wwise Error Message Translator";
			public static UnityEngine.GUIContent XMLTranslatorTimeout = new UnityEngine.GUIContent("XML Translator Timeout", "Maximum time (ms) taken to convert numeric ID in errors through SoundBankInfo.xml. Set to 0 to disable. Change will be applied next time play mode is entered.");
			public static UnityEngine.GUIContent WaapiTranslatorTimeout = new UnityEngine.GUIContent("WAAPI Translator Timeout", "Maximum time (ms) taken to convert numeric ID in errors through WAAPI. Set to 0 to disable. Change will be applied next time play mode is entered.");
			public static UnityEngine.GUIContent AkLoggerLevel = new UnityEngine.GUIContent("AkLogger Level", "Log Verbosity.");

			private static UnityEngine.GUIStyle version;
			public static UnityEngine.GUIStyle Version
			{
				get
				{
					if (version != null)
						return version;

					version = new UnityEngine.GUIStyle(UnityEditor.EditorStyles.whiteLargeLabel);
					if (!UnityEngine.Application.HasProLicense())
					{
						version.active.textColor =
							version.focused.textColor =
							version.hover.textColor =
							version.normal.textColor = UnityEngine.Color.black;
					}
					return version;
				}
			}

			private static UnityEngine.GUIStyle textField;
			public static UnityEngine.GUIStyle TextField
			{
				get
				{
					if (textField == null)
						textField = new UnityEngine.GUIStyle("textfield");
					return textField;
				}
			}
		}

		private static bool Ellipsis()
		{
			return UnityEngine.GUILayout.Button("...", UnityEngine.GUILayout.Width(30));
		}
		
		private bool IsFolderWwiseApplicationPath(string path)
		{
#if UNITY_EDITOR_OSX
			return path.Contains("Wwise.app");
#else
			string fullPath = Path.GetFullPath(Path.Combine(path, "Authoring\\x64\\Release\\bin\\Wwise.exe"));
			return File.Exists(fullPath);
#endif
		}

#if UNITY_2018_3_OR_NEWER
		private SettingsProvider(string path) : base(path, UnityEditor.SettingsScope.Project) { }

		public override void OnDeactivate()
		{
			base.OnDeactivate();
			if(AkWwiseEditorSettings.Instance.LoadSoundEngineInEditMode && !AkUnitySoundEngine.IsInitialized())
			{
				AkUnitySoundEngineInitialization.Instance.InitializeSoundEngine();
			}
			else if (!AkWwiseEditorSettings.Instance.LoadSoundEngineInEditMode && AkUnitySoundEngine.IsInitialized())
			{
				AkUnitySoundEngineInitialization.Instance.TerminateSoundEngine();
			}
		}
			

		public override void OnGUI(string searchContext)
#else
		[UnityEditor.MenuItem("Edit/Wwise Settings...", false, (int)AkWwiseWindowOrder.WwiseSettings)]
		public static void Init()
		{
			// Get existing open window or if none, make a new one:
			var window = GetWindow(typeof(EditorWindow));
			window.position = new UnityEngine.Rect(100, 100, 850, 360);
			window.titleContent = new UnityEngine.GUIContent("Wwise Settings");
		}

		private void OnGUI()
#endif
		{
			bool changed = false;

			var labelWidth = UnityEditor.EditorGUIUtility.labelWidth;
			UnityEditor.EditorGUIUtility.labelWidth += 100;

			var settings = AkWwiseEditorSettings.Instance;

			UnityEngine.GUILayout.Label(string.Format("Wwise v{0} Settings.", AkUnitySoundEngine.WwiseVersion), Styles.Version);
			UnityEngine.GUILayout.Label(Styles.WwiseProject, UnityEditor.EditorStyles.boldLabel);

			using (new UnityEngine.GUILayout.HorizontalScope("box"))
			{
				UnityEditor.EditorGUILayout.PrefixLabel(Styles.WwiseProjectPath);
				UnityEditor.EditorGUILayout.SelectableLabel(settings.WwiseProjectPath, Styles.TextField, UnityEngine.GUILayout.Height(17));

				if (Ellipsis())
				{
					var OpenInPath = System.IO.Path.GetDirectoryName(AkUtilities.GetFullPath(UnityEngine.Application.dataPath, settings.WwiseProjectPath));
					var WwiseProjectPathNew = UnityEditor.EditorUtility.OpenFilePanel("Select your Wwise Project", OpenInPath, "wproj");
					if (WwiseProjectPathNew.Length != 0)
					{
						if (WwiseProjectPathNew.EndsWith(".wproj") == false)
						{
							UnityEditor.EditorUtility.DisplayDialog("Error", "Please select a valid .wproj file", "Ok");
						}
						else
						{
							settings.WwiseProjectPath = AkUtilities.MakeRelativePath(UnityEngine.Application.dataPath, WwiseProjectPathNew);
							changed = true;
						}
					}
				}
			}

			UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);
			UnityEngine.GUILayout.Label(Styles.WwiseApplicationPath, UnityEditor.EditorStyles.boldLabel);

			using (new UnityEngine.GUILayout.HorizontalScope("box"))
			{
				UnityEditor.EditorGUILayout.PrefixLabel(Styles.WwiseInstallationPath);
				UnityEditor.EditorGUILayout.SelectableLabel(settings.WwiseInstallationPath, Styles.TextField, UnityEngine.GUILayout.Height(17));

				if (Ellipsis())
				{
#if UNITY_EDITOR_OSX
					var path = UnityEditor.EditorUtility.OpenFilePanel("Select your Wwise application.", "/Applications/", "");
#else
					var path = UnityEditor.EditorUtility.OpenFolderPanel("Select your Wwise application.", System.Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "");
#endif
					if (path != "" && !IsFolderWwiseApplicationPath(path))
					{
						EditorUtility.DisplayDialog("Wwise Application Path could not be set", $"{path} did not contain a Wwise Authoring application.", "OK");
					}
					else if (path.Length != 0)
					{
						settings.WwiseInstallationPath = System.IO.Path.GetFullPath(path);
						changed = true;
					}
				}
			}

			UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);
			UnityEngine.GUILayout.Label(Styles.AssetManagement, UnityEditor.EditorStyles.boldLabel);


#if AK_WWISE_ADDRESSABLES && UNITY_ADDRESSABLES
			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				using (new UnityEngine.GUILayout.HorizontalScope())
				{
					UnityEditor.EditorGUILayout.PrefixLabel(Styles.GeneratedSoundbankPath);
					UnityEditor.EditorGUILayout.SelectableLabel(settings.RootOutputPath, Styles.TextField, UnityEngine.GUILayout.Height(17));

					if (Ellipsis())
					{
						var FullPath = AkUtilities.GetFullPath(UnityEngine.Application.dataPath, settings.RootOutputPath);
						var OpenInPath = System.IO.Path.GetDirectoryName(FullPath);
						var path = UnityEditor.EditorUtility.OpenFolderPanel("Select your generated SoundBanks destination folder", OpenInPath, FullPath.Substring(OpenInPath.Length + 1));
						if (path.Length != 0)
						{
							if (!path.Contains(UnityEngine.Application.dataPath))
							{
								UnityEditor.EditorUtility.DisplayDialog("Error", "The SoundBanks destination folder must be located within the Unity project 'Assets' folder.", "Ok");
							}
							else if (path == UnityEngine.Application.dataPath)
							{
								UnityEditor.EditorUtility.DisplayDialog("Error", "The SoundBanks destination folder cannot be the 'Assets' folder.", "Ok");
							}
							else
							{
								var newPath = AkUtilities.MakeRelativePath(UnityEngine.Application.dataPath, path);
								var previousPath = settings.RootOutputPath;
								if (previousPath != newPath)
								{
									settings.RootOutputPath = newPath;
									var projectPath = AkUtilities.GetFullPath(UnityEngine.Application.dataPath, settings.WwiseProjectPath);
									var relPath = AkUtilities.MakeRelativePath(System.IO.Path.GetDirectoryName(projectPath), path);

									AkUtilities.SetSoundbanksDestinationFoldersInWproj(projectPath, relPath);
									var fullPreviousPath = AkUtilities.GetFullPath(UnityEngine.Application.dataPath, previousPath);
									var appDataPath = UnityEngine.Application.dataPath.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);

									if (!string.IsNullOrEmpty(previousPath) && System.IO.Directory.Exists(fullPreviousPath)) 
									{
										UnityEditor.AssetDatabase.Refresh();
										if (fullPreviousPath.Contains(appDataPath))
										{
											var destination = System.IO.Path.Combine("Assets", newPath);
											AkUtilities.MoveAssetsFromDirectory(fullPreviousPath, destination, true);
										}
										else
										{
											AkUtilities.DirectoryCopy(fullPreviousPath, path, true);
										}
										UnityEditor.AssetDatabase.Refresh();
									}
									changed = true;
								}
							}
						}
					}
				}
			}
#else

			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				using (new UnityEngine.GUILayout.HorizontalScope())
				{
					UnityEditor.EditorGUILayout.PrefixLabel(Styles.SoundbankPath);
					UnityEditor.EditorGUILayout.SelectableLabel(settings.WwiseStreamingAssetsPath, Styles.TextField, UnityEngine.GUILayout.Height(17));

					if (Ellipsis())
					{
						var FullPath = AkUtilities.GetFullPath(UnityEngine.Application.streamingAssetsPath, settings.WwiseStreamingAssetsPath);
						var OpenInPath = System.IO.Path.GetDirectoryName(FullPath);
						var path = UnityEditor.EditorUtility.OpenFolderPanel("Select your SoundBanks destination folder", OpenInPath, FullPath.Substring(OpenInPath.Length + 1));
						if (path.Length != 0)
						{
							var stremingAssetsIndex = UnityEngine.Application.dataPath.Split('/').Length;
							var folders = path.Split('/');

							if (folders.Length - 1 < stremingAssetsIndex || !string.Equals(folders[stremingAssetsIndex], "StreamingAssets", System.StringComparison.OrdinalIgnoreCase))
							{
								UnityEditor.EditorUtility.DisplayDialog("Error", "The SoundBank destination folder must be located within the Unity project 'StreamingAssets' folder.", "Ok");
							}
							else
							{
								var previousPath = settings.WwiseStreamingAssetsPath;
								var newPath = AkUtilities.MakeRelativePath(UnityEngine.Application.streamingAssetsPath, path);

								if (previousPath != newPath)
								{
									settings.WwiseStreamingAssetsPath = newPath;
									changed = true;
								}
							}
						}
					}
				}

				UnityEditor.EditorGUI.BeginChangeCheck();
				settings.CopySoundBanksAsPreBuildStep = UnityEditor.EditorGUILayout.Toggle(Styles.CopySoundBanksAsPreBuildStep, settings.CopySoundBanksAsPreBuildStep);
				UnityEngine.GUI.enabled = settings.CopySoundBanksAsPreBuildStep;
				settings.GenerateSoundBanksAsPreBuildStep = UnityEditor.EditorGUILayout.Toggle(Styles.GenerateSoundBanksAsPreBuildStep, settings.GenerateSoundBanksAsPreBuildStep);
				UnityEngine.GUI.enabled = true;
				if (UnityEditor.EditorGUI.EndChangeCheck())
					changed = true;
			}
#endif

			UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);
			UnityEngine.GUILayout.Label(Styles.GlobalSettings, UnityEditor.EditorStyles.boldLabel);

			UnityEditor.EditorGUI.BeginChangeCheck();
			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				settings.CreateWwiseGlobal = UnityEditor.EditorGUILayout.Toggle(Styles.CreateWwiseGlobal, settings.CreateWwiseGlobal);
				settings.CreateWwiseListener = UnityEditor.EditorGUILayout.Toggle(Styles.CreateWwiseListener, settings.CreateWwiseListener);
				settings.ObjectReferenceAutoCleanup = UnityEditor.EditorGUILayout.Toggle(Styles.ObjectReferenceAutoCleanup, settings.ObjectReferenceAutoCleanup);
				settings.LoadSoundEngineInEditMode = UnityEditor.EditorGUILayout.Toggle(Styles.LoadSoundEngineInEditMode, settings.LoadSoundEngineInEditMode);
			}

			UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);
			UnityEngine.GUILayout.Label(Styles.InEditorWarnings, UnityEditor.EditorStyles.boldLabel);

			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				settings.ShowSpatialAudioWarningMsg = UnityEditor.EditorGUILayout.Toggle(Styles.ShowSpatialAudioWarningMsg, settings.ShowSpatialAudioWarningMsg);
			}

			UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);
			UnityEngine.GUILayout.Label(Styles.WaapiSection, UnityEditor.EditorStyles.boldLabel);
			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				settings.UseWaapi = UnityEditor.EditorGUILayout.Toggle(Styles.UseWaapi, settings.UseWaapi);
				settings.WaapiPort = UnityEditor.EditorGUILayout.TextField(Styles.WaapiPort, settings.WaapiPort);
				settings.WaapiIP = UnityEditor.EditorGUILayout.TextField(Styles.WaapiIP, settings.WaapiIP);
				settings.AutoSyncWaapi = UnityEditor.EditorGUILayout.Toggle(Styles.AutosyncSelection, settings.AutoSyncWaapi);
			}

			UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);
			UnityEngine.GUILayout.Label(Styles.TranslatorSection, UnityEditor.EditorStyles.boldLabel);
			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				settings.XMLTranslatorTimeout = UnityEditor.EditorGUILayout.TextField(Styles.XMLTranslatorTimeout, settings.XMLTranslatorTimeout);
				settings.WaapiTranslatorTimeout = UnityEditor.EditorGUILayout.TextField(Styles.WaapiTranslatorTimeout, settings.WaapiTranslatorTimeout);
			}

			if (UnityEditor.EditorGUI.EndChangeCheck())
				changed = true;

			UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);

			UnityEditor.EditorGUIUtility.labelWidth = labelWidth;

			if (changed)
			{
				settings.SaveSettings();
			}
		}
#endregion
	}
#endif // UNITY_EDITOR
#endif // #if ! (UNITY_DASHBOARD_WIDGET || UNITY_WEBPLAYER || UNITY_WII || UNITY_WIIU || UNITY_NACL || UNITY_FLASH || UNITY_BLACKBERRY) // Disable under unsupported platforms.
