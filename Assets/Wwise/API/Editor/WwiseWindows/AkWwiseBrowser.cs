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

using System;
using UnityEditor;
using UnityEngine;
using AK.Wwise.Unity.Logging;

#if UNITY_EDITOR
public class AkWwiseBrowser : UnityEditor.EditorWindow
{
	[UnityEngine.SerializeField] UnityEditor.IMGUI.Controls.TreeViewState m_treeViewState;

	public static AkWwiseTreeView m_treeView;
	UnityEditor.IMGUI.Controls.SearchField m_SearchField;
	private Rect m_filterMenuRect;

	private static bool s_canRefresh = true;
	private static bool s_controlKeyDown = false;
	private static bool s_altKeyDown = false;
	private static bool s_shiftKeyDown = false;
	private bool isConnectedToWaapi = false;
	
	private static GUIStyle s_expansionButtonStyle;
	private static Texture2D s_textureCollapseAllIcon;
	private static Texture2D s_textureExpandAllIcon;

	[UnityEditor.MenuItem("Window/Wwise Browser", false, (int)AkWwiseWindowOrder.WwisePicker)]
	public static void InitBrowserWindow()
	{
		AkWwiseBrowser window = (AkWwiseBrowser)EditorWindow.GetWindow(typeof(AkWwiseBrowser));

		if (window == null)
		{
			window = GetWindow<AkWwiseBrowser>("Wwise Browser", true,typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow"));
		}

		Texture2D originalIcon = EditorGUIUtility.Load("Assets/Wwise/API/Editor/WwiseWindows/BrowserIcon.png") as Texture2D;

		if (originalIcon != null)
		{
			int newWidth = 32;
			int newHeight = 32;
        
			Texture2D scaledIcon = ScaleTexture(originalIcon, newWidth, newHeight); 

			window.titleContent = new GUIContent("Wwise Browser", scaledIcon);
		}
		else
		{
			window.titleContent = new GUIContent("Wwise Browser");
		}
    
		window.Show();
	}
	
	private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
	{
		RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
		RenderTexture.active = rt;
		Graphics.Blit(source, rt);
		Texture2D result = new Texture2D(targetWidth, targetHeight);
		result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
		result.Apply();
		RenderTexture.active = null; 
		RenderTexture.ReleaseTemporary(rt);
		return result;
	}

	public void OnEnable()
	{
		if (m_treeViewState == null)
		{
			m_treeViewState = new UnityEditor.IMGUI.Controls.TreeViewState();
		}

		var multiColumnHeaderState = AkWwiseTreeView.CreateDefaultMultiColumnHeaderState();
		var multiColumnHeader = new UnityEditor.IMGUI.Controls.MultiColumnHeader(multiColumnHeaderState);
		m_treeView = new AkWwiseTreeView(m_treeViewState, multiColumnHeader, AkWwiseProjectInfo.GetTreeData());
		m_treeView.SetDoubleClickFunction(OnDoubleClick);

		m_treeView.dirtyDelegate = RequestRepaint;

		if (m_treeView.dataSource.Data.ItemDict.Count == 0)
		{
			Refresh();
			RequestRepaint();
		}
		
		m_SearchField = new UnityEditor.IMGUI.Controls.SearchField();
		m_SearchField.downOrUpArrowKeyPressed += m_treeView.SetFocusAndEnsureSelectedItem;
		m_SearchField.SetFocus();

		LoadIcons();
		AkWwiseEditorSettings.OnWaapiSettingsChanged += RefreshWaapi;
	}

    public void LoadIcons()
	{
		var tempBrowserPath = "Assets/Wwise/API/Editor/WwiseWindows/BrowserIcons/";
		s_textureCollapseAllIcon = AkWwisePickerIcons.GetTexture(tempBrowserPath + "GraphNode_CollapseAll.png");
		s_textureExpandAllIcon = AkWwisePickerIcons.GetTexture(tempBrowserPath + "GraphNode_ExpandAll.png");
	}

	public void OnDisable()
	{
		m_treeView.SaveExpansionStatus();
	}

	private void RefreshWaapi()
	{
		Refresh();
	}

	public static void Refresh(bool ignoreIfWaapi = false)
	{
		if (m_treeView != null)
		{
			m_treeView.dataSource.FetchData();
		};
	}
	
	private void OnDoubleClick(AkWwiseTreeViewItem item)
	{
		if (item == null)
		{
			return;
		}

		if (item.objectType == WwiseObjectType.Event)
		{
			PlayPauseItem(item);
			return;
		}

		if (item.hasChildren)
		{
			m_treeView.SetExpanded(item.id, !m_treeView.IsExpanded(item.id));
		}
	}

	private void SetItemAndChildrenExpansion(AkWwiseTreeViewItem item, bool expanded)
	{
		m_treeView.SetExpanded(item.id, expanded);

		foreach (AkWwiseTreeViewItem child in item.children)
		{
			SetItemAndChildrenExpansion(child, expanded);
		}
	}

	private void PlayPauseItem(AkWwiseTreeViewItem item)
	{
		if (m_treeView != null && m_treeView.CheckWaapi())
		{
			AkWaapiUtilities.TogglePlayEvent(item.objectType, item.objectGuid);
		}
	}

	private bool isDirty;
	public void RequestRepaint()
	{
		isDirty = true;
	}

	void Update()
	{
		if (isDirty)
		{
			Repaint();
			m_treeView.Reload();
			isDirty = false;
		}

		if (AkWwiseEditorSettings.Instance.UseWaapi)
		{
			AkWwiseProjectInfo.WaapiBrowserData.Update();
		}
	}

	private void GenerateSoundbank(bool currentPlatformOnly = false)
	{
		System.Collections.Generic.List<string> platform = new System.Collections.Generic.List<string>();
		if (currentPlatformOnly)
		{
			platform.Add(AkBasePathGetter.GetPlatformName());
			platform.Add(AkBasePathGetter.GetTargetPlatformName(EditorUserBuildSettings.activeBuildTarget));
		}
		if (AkWaapiUtilities.IsConnected())
		{
			AkWaapiUtilities.GenerateSoundbank(platform.ToArray());
		}
		else
		{
			GenerateSoundbanks(platform);
		}
	}
	
	void OnLostFocus()
	{
		s_altKeyDown = false;
		s_controlKeyDown = false;
		s_shiftKeyDown = false;
	}

	public void OnGUI()
	{
		if (isConnectedToWaapi != AkWaapiUtilities.IsConnected())
		{
			isConnectedToWaapi = AkWaapiUtilities.IsConnected();
			Refresh();
		}
		const int buttonWidth = 150;
		const int iconButtonWidth = 30;
		using (new UnityEngine.GUILayout.HorizontalScope("box"))
		{
			Event e = Event.current;

			if(e.isKey)
			{
				bool isKeyDown = e.type == EventType.KeyDown;
				if (e.keyCode == KeyCode.LeftAlt || e.keyCode == KeyCode.RightAlt)
				{
					s_altKeyDown = isKeyDown;
				}
				else if (e.keyCode == KeyCode.LeftControl || e.keyCode == KeyCode.RightControl)
				{
					s_controlKeyDown = isKeyDown;
				} 
				else if (e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift)
				{
					s_shiftKeyDown = isKeyDown;
				}
				else if (e.keyCode == KeyCode.F5)
				{
					if(isKeyDown)
					{
						if(s_canRefresh)
						{
							Refresh();
							s_canRefresh = false;
							Event.current.Use();
						}
					}
					else
					{
						s_canRefresh = true;
					}
				}
				else if (isKeyDown)
				{
					if (e.keyCode == KeyCode.F7)
					{
						if (s_controlKeyDown && s_altKeyDown)
						{
							GenerateSoundbank();
                        } 
						else if (s_controlKeyDown && s_shiftKeyDown)
						{
							GenerateSoundbank(true);
						}
                        Event.current.Use();
                    }
                }
            }

			var buttonPressed = GUILayout.Button("Filters", new GUIStyle(GUI.skin.button));
			if (Event.current.type == EventType.Repaint)
			{
				m_filterMenuRect = GUILayoutUtility.GetLastRect();
				m_filterMenuRect.x += m_filterMenuRect.width - 25;
			}
			if (buttonPressed)
			{
				var screenRect = GUIUtility.GUIToScreenRect(m_filterMenuRect);
				AkWwiseBrowserFilter.Init(screenRect);
			}

			if (s_expansionButtonStyle == null)
			{
				s_expansionButtonStyle = new GUIStyle(GUI.skin.button);
			s_expansionButtonStyle.padding = new RectOffset(2, 2, 2, 2);
			}

			float expansionIconSize = 20.0f;
			GUIContent expandAllButton = new GUIContent(s_textureExpandAllIcon);
			expandAllButton.tooltip = "Expand All";
			if (GUILayout.Button(expandAllButton, s_expansionButtonStyle, GUILayout.Height(expansionIconSize), GUILayout.Width(expansionIconSize)))
			{
				SetItemAndChildrenExpansion(m_treeView.dataSource.ProjectRoot, true);
			}
			GUIContent collapseAllButton = new GUIContent(s_textureCollapseAllIcon);
			collapseAllButton.tooltip = "Collapse All";
			if (GUILayout.Button(collapseAllButton, s_expansionButtonStyle, GUILayout.Height(expansionIconSize), GUILayout.Width(expansionIconSize)))
			{
				SetItemAndChildrenExpansion(m_treeView.dataSource.ProjectRoot, false);
			}

			var search_width = System.Math.Max(position.width / 3, buttonWidth * 2);

            m_treeView.StoredSearchString = m_SearchField.OnGUI(UnityEngine.GUILayoutUtility.GetRect(search_width, 30), m_treeView.StoredSearchString);
			UnityEngine.GUILayout.FlexibleSpace();

			var RefreshContent = new GUIContent(EditorGUIUtility.IconContent("Refresh"));
			RefreshContent.tooltip = "Refresh the Wwise Browser (F5)";
			if (UnityEngine.GUILayout.Button(RefreshContent, UnityEngine.GUILayout.Width(iconButtonWidth)))
			{
				Refresh();
			}

			var GenerateContent = new GUIContent("Generate SoundBanks", "Generate all : Ctrl+Alt+F7\nGenerate for the current platform : Ctrl+Shift+F7");
			if (UnityEngine.GUILayout.Button(GenerateContent, UnityEngine.GUILayout.Width(buttonWidth), UnityEngine.GUILayout.Height(20)))
			{
				GenerateSoundbank();
			}
		}

		UnityEngine.GUILayout.Space(UnityEditor.EditorGUIUtility.standardVerticalSpacing);


		UnityEngine.GUILayout.FlexibleSpace();
		UnityEngine.Rect lastRect = UnityEngine.GUILayoutUtility.GetLastRect();
		
		if (m_treeView.dataSource.isRefreshing)
		{
			GUILayout.Label("... LOADING WAAPI DATA ...", EditorStyles.boldLabel);
		}
		else
		{
			m_treeView.OnGUI(new UnityEngine.Rect(lastRect.x, lastRect.y, position.width, lastRect.height));
		}

		using (new UnityEngine.GUILayout.HorizontalScope("box"))
		{
			bool bConnected;
			string tooltip = AkWaapiUtilities.GetStatusString(out bConnected);
			
			var ConnectionContent = new GUIContent(EditorGUIUtility.IconContent(bConnected ? "Linked" : "Unlinked"));
			ConnectionContent.tooltip = tooltip;
			UnityEngine.GUILayout.Label(ConnectionContent, UnityEngine.GUILayout.Width(iconButtonWidth));
			UnityEngine.GUILayout.Label("Root Output Path: " + AkWwiseEditorSettings.GetRootOutputPath());

			var HelpContent = new GUIContent(EditorGUIUtility.IconContent("_Help"));
			HelpContent.tooltip = "Open the Wwise Unity Integration Documentation";
			if (UnityEngine.GUILayout.Button(HelpContent, UnityEngine.GUILayout.Width(iconButtonWidth)))
			{
				Application.OpenURL("https://www.audiokinetic.com/library/edge/?source=Unity&id=index.html");
			}
			
			var ProjectSettingsContent = new GUIContent(EditorGUIUtility.IconContent("_Popup"));
			ProjectSettingsContent.tooltip = $"Open the Wwise Unity Project Settings{Environment.NewLine}" + $"Integration Version : {AkUnitySoundEngine.IntegrationVersion}{Environment.NewLine}" + $"SoundEngine Version : {AkUnitySoundEngine.WwiseVersion}";
			if (UnityEngine.GUILayout.Button(ProjectSettingsContent, UnityEngine.GUILayout.Width(iconButtonWidth)))
			{
				OpenProjectSettings();
			}
		}
	}

	private void GenerateSoundbanks(System.Collections.Generic.List<string> platforms = null)
	{
        if (AkUtilities.IsSoundbankGenerationAvailable(AkWwiseEditorSettings.Instance.WwiseInstallationPath) && !AkUtilities.GeneratingSoundBanks)
        {
            AkUtilities.GenerateSoundbanks(AkWwiseEditorSettings.Instance.WwiseInstallationPath, AkWwiseEditorSettings.WwiseProjectAbsolutePath, platforms);
        }
        else if (!AkUtilities.GeneratingSoundBanks)
        {
            WwiseLogger.Error("Access to Wwise is required to generate the SoundBanks. Please go to Edit > Project Settings... and set the Wwise Application Path found in the Wwise Integration view.");
        }
    }

    void OpenProjectSettings()
	{
		SettingsService.OpenProjectSettings("Project/Wwise Integration");
	}

	static public void SelectInWwiseBrowser(System.Guid guid)
	{
		InitBrowserWindow();
		m_treeView.SelectItem(guid);
	}

	[UnityEditor.MenuItem("CONTEXT/AkBank/Select in Wwise Browser")]
	[UnityEditor.MenuItem("CONTEXT/AkAmbient/Select in Wwise Browser")]
	[UnityEditor.MenuItem("CONTEXT/AkEvent/Select in Wwise Browser")]
	[UnityEditor.MenuItem("CONTEXT/AkState/Select in Wwise Browser")]
	[UnityEditor.MenuItem("CONTEXT/AkSwitch/Select in Wwise Browser")]
	[UnityEditor.MenuItem("CONTEXT/AkWwiseTrigger/Select in Wwise Browser")]
	static void SelectItemInWwiseBrowser(UnityEditor.MenuCommand command)
	{
		AkTriggerHandler component = (AkTriggerHandler)command.context;
		try
		{
			var data = component.GetType().GetField("data");
			var guid = (data.GetValue(component) as AK.Wwise.BaseType).ObjectReference.Guid;
			SelectInWwiseBrowser(guid);
		}
		catch { }
	}

	[UnityEditor.MenuItem("CONTEXT/AkRoom/Select Aux Bus in Wwise Browser")]
	static void SelectAkRoomAuxBusInWwiseBrowser(UnityEditor.MenuCommand command)
	{
		AkRoom component = (AkRoom)command.context;
		SelectInWwiseBrowser(component.reverbAuxBus.ObjectReference.Guid);
	}

	[UnityEditor.MenuItem("CONTEXT/AkRoom/Select Event in Wwise Browser")]
	static void SelectAkRoomEventInWwiseBrowser(UnityEditor.MenuCommand command)
	{
		AkRoom component = (AkRoom)command.context;
		SelectInWwiseBrowser(component.roomToneEvent.ObjectReference.Guid);
	}

	[UnityEditor.MenuItem("CONTEXT/AkSurfaceReflector/Select in Wwise Browser")]
	static void SelectReflectorTextureItemInWwiseBrowser(UnityEditor.MenuCommand command)
	{
		AkSurfaceReflector component = (AkSurfaceReflector)command.context;
		if (component.AcousticTextures.Length >0 && component.AcousticTextures[0].ObjectReference !=null)
		{
			SelectInWwiseBrowser(component.AcousticTextures[0].ObjectReference.Guid);
		}
	}

	[UnityEditor.MenuItem("CONTEXT/AkEnvironment/Select in Wwise Browser")]
	static void SelectEnvironmentItemInWwiseBrowser(UnityEditor.MenuCommand command)
	{
		AkEnvironment component = (AkEnvironment)command.context;
		SelectInWwiseBrowser(component.data.ObjectReference.Guid);
	}

	[UnityEditor.MenuItem("CONTEXT/AkEarlyReflections/Select in Wwise Browser")]
	static void SelectReflectionsItemInWwiseBrowser(UnityEditor.MenuCommand command)
	{
		AkEarlyReflections component = (AkEarlyReflections)command.context;
		SelectInWwiseBrowser(component.reflectionsAuxBus.ObjectReference.Guid);
	}
}
#endif