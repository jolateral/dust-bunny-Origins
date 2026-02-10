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

using UnityEditor;
using UnityEngine;

public class AkWwiseBrowserFilter : EditorWindow
{
	private BrowserFilter Filters;

	class BrowserFilterInfo
	{
		public bool Checked;
		public string Label;
		public BrowserFilter Filter;

		public BrowserFilterInfo(string in_Label, BrowserFilter in_Filter)
		{
			Checked = false;
			Label = in_Label;
			Filter = in_Filter;
		}
	}
	BrowserFilterInfo [] statusInfos = new BrowserFilterInfo[6];
	BrowserFilterInfo [] typeInfos = new BrowserFilterInfo[8];

	private bool allStatusSelected = false;
	private bool allTypeSelected = false;
	
	private bool m_close = false;

	void InitBrowserFilterInfos()
	{
		statusInfos[0] = new BrowserFilterInfo("New in Wwise", BrowserFilter.NewInWwise);
		statusInfos[1] = new BrowserFilterInfo("Deleted in Wwise", BrowserFilter.DeletedInWwise);
		statusInfos[2] = new BrowserFilterInfo("Renamed in Wwise", BrowserFilter.RenamedInWwise);
		statusInfos[3] = new BrowserFilterInfo("Moved in Wwise", BrowserFilter.MovedInWwise);
		statusInfos[4] = new BrowserFilterInfo("SoundBanks need Update", BrowserFilter.SoundBankNeedsUpdate);
		statusInfos[5] = new BrowserFilterInfo("SoundBanks Up to Date", BrowserFilter.SoundBanksUpToDate);
		
		typeInfos[0] = new BrowserFilterInfo("Event", BrowserFilter.Event);
		typeInfos[1] = new BrowserFilterInfo("Bus", BrowserFilter.Bus);
		typeInfos[2] = new BrowserFilterInfo("Switch", BrowserFilter.Switch);
		typeInfos[3] = new BrowserFilterInfo("State", BrowserFilter.State);
		typeInfos[4] = new BrowserFilterInfo("Game Parameter", BrowserFilter.GameParameter);
		typeInfos[5] = new BrowserFilterInfo("SoundBank", BrowserFilter.SoundBank);
		typeInfos[6] = new BrowserFilterInfo("Trigger", BrowserFilter.Trigger);
		typeInfos[7] = new BrowserFilterInfo("Acoustic Texture", BrowserFilter.AcousticTexture);
	}

	AkWwiseBrowserFilter()
	{
		InitBrowserFilterInfos();
		SetCheckboxes();
	}

	public static void Init(Rect position)
	{
		EditorWindow window = CreateInstance<AkWwiseBrowserFilter>();
		Vector2 windowSize = new Vector2(225, 390);
		window.minSize = windowSize;
		window.maxSize = windowSize;
		window.ShowAsDropDown(position, windowSize);
	}

	void AllSelectedCheck()
	{
		allStatusSelected = true;
		foreach (var status in statusInfos)
		{
			if (!status.Checked)
			{
				allStatusSelected = false;
			}
		}

		allTypeSelected = true;
		foreach (var type in typeInfos)
		{
			if (!type.Checked)
			{
				allTypeSelected = false;
			}
		}
		ApplyFilters();
	}
	
	private void Update()
	{
		//Unity sometimes generates an error when the window is closed from the OnGUI function.
		//So We close it here
		if (m_close)
		{
			Close();
		}
	}

	bool DisplayFilter(bool value, string Name)
	{
		const int iconButtonWidth = 20;
		using (new UnityEngine.GUILayout.HorizontalScope())
		{
			EditorGUILayout.Space(5);
			bool prev = value;
			value = EditorGUILayout.Toggle(value, UnityEngine.GUILayout.Width(iconButtonWidth));
			EditorGUILayout.LabelField(Name);
			if (value != prev)
			{
				ApplyFilters();				
			}
		}
		return value;
	}

	void ApplyFilters()
	{
		if (AkWwiseBrowser.m_treeView == null)
		{
			return;
		}
		BrowserFilter newFilter = BrowserFilter.None;
		foreach (var status in statusInfos)
		{
			if (status.Checked)
			{
				newFilter |= status.Filter;
			}
		}
		
		foreach (var type in typeInfos)
		{
			if (type.Checked)
			{
				newFilter |= type.Filter;
			}
		}
		
		if (AkWwiseBrowser.m_treeView.Filters != newFilter)
		{
			AkWwiseBrowser.m_treeView.Filters = newFilter;
			AkWwiseBrowser.m_treeView.FiltersChanged = true;
			AkWwiseBrowser.m_treeView.Repaint();
		}
	}

	void SetCheckboxes()
	{
		if (AkWwiseBrowser.m_treeView == null)
		{
			return;
		}
		BrowserFilter Filter = AkWwiseBrowser.m_treeView.Filters;
		foreach (var status in statusInfos)
		{
			if ((Filter & status.Filter) != 0)
			{
				status.Checked = true;
			}
		}
		
		foreach (var type in typeInfos)
		{
			if ((Filter & type.Filter) != 0)
			{
				type.Checked = true;
			}
		}
	}

	void OnGUI()
	{
		const string SOUNDBANK_STATUS = "SoundBanks Status:";
		const string OBJECT_TYPE = "Object Type:";
		using (new UnityEngine.GUILayout.VerticalScope("box"))
		{
			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				using (new UnityEngine.GUILayout.HorizontalScope("box"))
				{
					EditorGUILayout.LabelField(SOUNDBANK_STATUS);
				}

				foreach (var status in statusInfos)
				{
					status.Checked = DisplayFilter(status.Checked, status.Label);
					AllSelectedCheck();
				}

				var Style = new GUIStyle();
				var content = new GUIContent("SoundBank Not Up to Date");
				content.tooltip = "Checks every filters in SoundBank Status except for \"SoundBanks Up to Date\"";
				Style.normal.textColor = GUI.skin.button.normal.textColor;
				Style.contentOffset = new Vector2(32, 5);
				if (GUILayout.Button(content, Style, GUILayout.Height(20)))
				{
					allStatusSelected = true;
					foreach (var status in statusInfos)
					{
						if (status.Filter != BrowserFilter.SoundBanksUpToDate)
						{
							status.Checked = true;
						}
					}
				}		
			}

			using (new UnityEngine.GUILayout.VerticalScope("box"))
			{
				using (new UnityEngine.GUILayout.HorizontalScope("box"))
				{
					EditorGUILayout.LabelField(OBJECT_TYPE);
				}
				foreach (var type in typeInfos)
				{
					type.Checked = DisplayFilter(type.Checked, type.Label);
					AllSelectedCheck();
				}
			}
			using (new UnityEngine.GUILayout.HorizontalScope("box"))
			{
				if (UnityEngine.GUILayout.Button("Reset"))
				{
					foreach (var status in statusInfos)
					{
						status.Checked = false;
					}
					foreach (var type in typeInfos)
					{
						type.Checked = false;
					}
				}

				if (UnityEngine.GUILayout.Button("Close"))
				{
					m_close = true;					
				}

			}
		}
	}
}