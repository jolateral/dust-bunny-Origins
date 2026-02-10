#if UNITY_EDITOR
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

using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using AK.Wwise.Unity.Logging;

public class AkWwiseTreeView : TreeView
{

	public enum PickerMode
	{
		FullPicker,
		ComponentPicker
	}

	public class AkWwiseTreeViewCellInfo
	{
		public AkWwiseTreeViewCellInfo(UnityEngine.Rect inCellRect, AkWwiseTreeViewItem inItem, ObjectColumns inColumn)
		{
			CellRect = inCellRect; 
			Item = inItem;
			Column = inColumn;
		}
		public UnityEngine.Rect CellRect;
		public AkWwiseTreeViewItem Item;
		public ObjectColumns Column;
	}

	private PickerMode m_pickerMode;
	private WwiseObjectType componentObjectType;
	public BrowserFilter Filters = BrowserFilter.None;

	AkWwisePickerIcons icons;
	protected AkWwiseTreeDataSource m_dataSource;
	public AkWwiseTreeDataSource dataSource { get { return m_dataSource; } }
	readonly IList<AkWwiseTreeViewItem> m_Rows = new List<AkWwiseTreeViewItem>(100);

	public event System.Action treeChanged;
	public static event System.Action<List<MultiColumnHeaderState.Column>> wwiseBrowserColumnDelegate;
	public static event System.Action<AkWwiseTreeViewCellInfo> wwiseBrowserCellDelegate;

	private static Dictionary<WwiseObjectType, UnityEditor.MonoScript> DragDropMonoScriptMap;
	private static Dictionary<System.Type, WwiseObjectType> ScriptTypeMap
		= new Dictionary<System.Type, WwiseObjectType>{
			{ typeof(AkAmbient), WwiseObjectType.Event },
			{ typeof(AkBank), WwiseObjectType.Soundbank },
			{ typeof(AkEnvironment), WwiseObjectType.AuxBus },
			{ typeof(AkState), WwiseObjectType.State },
			{ typeof(AkSurfaceReflector), WwiseObjectType.AcousticTexture },
			{ typeof(AkWwiseTrigger), WwiseObjectType.Trigger },
			{ typeof(AkSwitch), WwiseObjectType.Switch },
		};


	public AkWwiseTreeView(TreeViewState treeViewState,
		MultiColumnHeader multiColumnHeader, AkWwiseTreeDataSource data)
		: base(treeViewState, multiColumnHeader)
	{
		m_pickerMode = PickerMode.FullPicker;
		Initialize(data);
		Reload();
	}

	public AkWwiseTreeView(TreeViewState treeViewState,
		AkWwiseTreeDataSource data, WwiseObjectType componentType)
	: base(treeViewState)

	{
		m_pickerMode = PickerMode.ComponentPicker;
		componentObjectType = componentType;
		Initialize(data);
		data.LoadComponentData(componentObjectType);
		Reload();
	}

	private void Initialize(AkWwiseTreeDataSource data)
	{
		m_dataSource = data;
		m_dataSource.SetWwiseTreeView(this);
		m_dataSource.modelChanged += ModelChanged;
		this.LoadExpansionStatus();

		icons = new AkWwisePickerIcons();
		icons.LoadIcons();

		DragDropEnabled = true;
		extraSpaceBeforeIconAndLabel = AkWwisePickerIcons.kIconWidth;
		StoredSearchString = "";

		if (DragDropMonoScriptMap == null)
		{
			DragDropMonoScriptMap = new Dictionary<WwiseObjectType, UnityEditor.MonoScript>();

			var scripts = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.MonoScript>();
			foreach (var script in scripts)
			{
				WwiseObjectType wwiseObjectType;
				var type = script.GetClass();
				if (type != null && ScriptTypeMap.TryGetValue(type, out wwiseObjectType))
					DragDropMonoScriptMap[wwiseObjectType] = script;
			}
		}

		UnityEditor.EditorApplication.playModeStateChanged += (UnityEditor.PlayModeStateChange playMode) =>
		{
			if (playMode == UnityEditor.PlayModeStateChange.ExitingEditMode)
				SaveExpansionStatus();
		};
		UnityEditor.EditorApplication.quitting += SaveExpansionStatus;
	}

	private bool bSearchStringChanged;
	private bool bFiltersChanged;
	public string m_storedSearchString;

	public bool FiltersChanged
	{
		get { return bFiltersChanged; }

		set
		{
			if (bFiltersChanged != value)
			{
				bFiltersChanged = value;
				SaveExpansionStatus();
			}
		}
	}
	public string StoredSearchString
	{
		get { return m_storedSearchString; }
		set
		{
			if (m_storedSearchString != value)
			{
				if (value != string.Empty)
				{
					bSearchStringChanged = true;
					SaveExpansionStatus();
				}
				else
				{
					LoadExpansionStatus();
				}
			}
			m_storedSearchString = value;
			searchString = value;
		}
	}

	public void SaveExpansionStatus()
	{
		//Don't save exansion state when searching
		if (m_storedSearchString != string.Empty) return;
		dataSource.SaveExpansionStatus(new List<int>(state.expandedIDs));
	}

	public void LoadExpansionStatus()
	{
		state.expandedIDs = dataSource.LoadExpansionSatus();
	}

	protected override void ExpandedStateChanged()
	{
		if (this.m_storedSearchString == string.Empty)
		{
			this.m_dataSource.ScheduleRebuild();
		}
	}

	void ModelChanged()
	{
		treeChanged?.Invoke();
		m_dataSource.UpdateSearchResults(searchString, componentObjectType, Filters);
		SetDirty();
	}

	public delegate void DirtyDelegate();
	public DirtyDelegate dirtyDelegate;
	public void SetDirty()
	{
		dirtyDelegate?.Invoke();
	}

	public override void OnGUI(UnityEngine.Rect rect)
	{
		if (bSearchStringChanged || bFiltersChanged)
		{
			if (!m_dataSource.isSearching)
			{
				m_dataSource.UpdateSearchResults(searchString, componentObjectType, Filters);
				bSearchStringChanged = false;
				bFiltersChanged = false;
				Reload();
			}
		}

		base.OnGUI(rect);
	}

	protected override TreeViewItem BuildRoot()
	{ 
		return m_dataSource.CreateProjectRootItem();
	}

	public void RebuildRows()
	{
		BuildRows(new AkWwiseTreeViewItem());
	}

	protected override IList<TreeViewItem> BuildRows(
		TreeViewItem root)
	{
		m_Rows.Clear();

		var dataRoot = m_dataSource.ProjectRoot;

		if (m_pickerMode == PickerMode.ComponentPicker)
		{
			dataRoot = m_dataSource.GetComponentDataRoot(componentObjectType);
		}

		if ((!string.IsNullOrEmpty(searchString)) || Filters != BrowserFilter.None)
		{
			dataRoot = m_dataSource.GetSearchResults();
		}
		TreeUtility.SortTreeIfNecessary(dataRoot);
		AddChildrenRecursive(dataRoot, m_Rows);
		searchString = "";
		return m_Rows.Cast<TreeViewItem>().ToList();
	}


	private bool TestExpanded(AkWwiseTreeViewItem node)
	{
		if (node.children.Count > 0)
		{
			if (node.depth ==-1)
			{
				return true;
			}
			return IsExpanded(node.id);
		}
		return false;
	}

	void AddChildrenRecursive(AkWwiseTreeViewItem parent, IList<AkWwiseTreeViewItem> newRows)
	{
		if (parent == null)
		{
			return;
		}

		foreach (AkWwiseTreeViewItem child in parent.children)
		{
			newRows.Add(child);

			if (child.children.Count > 0)
			{
				if (TestExpanded(child))
				{
					AddChildrenRecursive(child, newRows);
				}
			}
		}
	}

	protected override IList<int> GetAncestors(int id)
	{
		return m_dataSource.GetAncestors(id);
	}

	protected override IList<int> GetDescendantsThatHaveChildren(int id)
	{
		return m_dataSource.GetDescendantsThatHaveChildren(id);
	}

	public AkWwiseTreeViewItem GetItemByGuid(System.Guid guid)
	{
		return TreeUtility.FindByGuid(m_Rows, guid);
	}

	public void SelectItem(System.Guid guid)
	{
		var item = m_dataSource.FindByGuid(guid);
		if (item == null)
		{
			m_dataSource.SelectItem(guid);
		}
		else
		{
			HighlightItem(item, true);
		}
	}

	public bool ExpandItem(System.Guid guid, bool select)
	{
		var item = GetItemByGuid(guid);
		if (item != null)
		{
			HighlightItem(item, select);
			return true;
		}

		item = m_dataSource.FindByGuid(guid);
		if (item != null)
		{
			AkWwiseTreeViewItem parent = item;
			while (parent.parent is AkWwiseTreeViewItem nextParent)
			{
				if (nextParent.objectType == WwiseObjectType.Project)
				{
					break;
				}
				parent = nextParent;
			}
			SetExpandedRecursive(parent.id, true);
			return !select;
		}
		return false;
	}


	public void HighlightItem(AkWwiseTreeViewItem item, bool select)
	{
		if (item != null)
		{
			FrameItem(item.id);
			if (select)
			{
				SetSelection(new List<int>() { item.id });
			}
			SetDirty();
		}
	}

	#region Mulicolumn 
	public enum ObjectColumns
	{
		Name,
		Status,
		AddressableGroup
	}

	public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
	{
		List<MultiColumnHeaderState.Column> columns = new List<MultiColumnHeaderState.Column>
		{
				new()
				{
					headerContent = new UnityEngine.GUIContent("Name"),
					headerTextAlignment = UnityEngine.TextAlignment.Left,
					sortedAscending = true,
					sortingArrowAlignment = UnityEngine.TextAlignment.Center,
					width = 300,
					minWidth = 100,
					autoResize = true,
					allowToggleVisibility = false,
					canSort = false
				},
				new()
				{
					headerContent = new UnityEngine.GUIContent("Status"),
					headerTextAlignment = UnityEngine.TextAlignment.Left,
					sortedAscending = true,
					sortingArrowAlignment = UnityEngine.TextAlignment.Center,
					width = 300,
					minWidth = 100,
					autoResize = true,
					allowToggleVisibility = false,
					canSort = false
				},
			};
		
		wwiseBrowserColumnDelegate?.Invoke(columns);
		
		var state = new MultiColumnHeaderState(columns.ToArray());
		return state;
	}


	public static MultiColumnHeaderState CreateDebug()
	{
		List<MultiColumnHeaderState.Column> columns = new List<MultiColumnHeaderState.Column>
		{
				new()
				{
					headerContent = new UnityEngine.GUIContent("Name"),
					headerTextAlignment = UnityEngine.TextAlignment.Left,
					sortedAscending = true,
					sortingArrowAlignment = UnityEngine.TextAlignment.Center,
					width = 300,
					minWidth = 200,
					autoResize = true,
					allowToggleVisibility = false
				},
				new()
				{
					headerContent = new UnityEngine.GUIContent("Guid"),
					headerTextAlignment = UnityEngine.TextAlignment.Right,
					sortedAscending = true,
					sortingArrowAlignment = UnityEngine.TextAlignment.Left,
					width = 200,
					minWidth = 60,
					autoResize = true
				},
				new()
				{
					headerContent = new UnityEngine.GUIContent("depth"),
					headerTextAlignment = UnityEngine.TextAlignment.Right,
					sortedAscending = true,
					sortingArrowAlignment = UnityEngine.TextAlignment.Left,
					width = 200,
					minWidth = 60,
					autoResize = true
				},
			};
		wwiseBrowserColumnDelegate?.Invoke(columns);

		var state = new MultiColumnHeaderState(columns.ToArray());
		return state;
	}

	#endregion

	#region Search 

	protected void SearchStringChanged(string lastSearch, string newSearch)
	{

	}

	#endregion

	#region Drawing
	protected override void AfterRowsGUI()
	{
		base.AfterRowsGUI();
		this.searchString = StoredSearchString;
	}

	//check here to see if multicolumn or not
	protected override void RowGUI(RowGUIArgs args)
	{
		var evt = UnityEngine.Event.current;
		var item = (AkWwiseTreeViewItem)args.item;
		if (m_pickerMode == PickerMode.ComponentPicker)
		{
			CellGUI(args.rowRect, item, ObjectColumns.Name, ref args);
		}
		else
		{
			for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
			{
				CellGUI(args.GetCellRect(i), item, (ObjectColumns)args.GetColumn(i), ref args);
			}
		}
	}

	void CellGUI(UnityEngine.Rect cellRect, AkWwiseTreeViewItem item, ObjectColumns column, ref RowGUIArgs args)
	{
		wwiseBrowserCellDelegate?.Invoke(new AkWwiseTreeViewCellInfo(cellRect, item, column));
		// Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
		CenterRectUsingSingleLineHeight(ref cellRect);

		switch (column)
		{
			case ObjectColumns.Name:
				{
					UnityEngine.Rect iconRect = new UnityEngine.Rect(cellRect);
					iconRect.x += GetContentIndent(item);
					iconRect.width = AkWwisePickerIcons.kIconWidth;
					UnityEngine.GUI.DrawTexture(iconRect, icons.GetIcon(item.objectType), UnityEngine.ScaleMode.ScaleToFit);
					//// Default icon and label
					args.rowRect = cellRect;
					base.RowGUI(args);
				}
				break;
			case ObjectColumns.Status:
				{
					if (item.IsUpToDate)
					{
						UnityEngine.GUI.Label(cellRect, item.status);						
					}
					else
					{
						UnityEngine.GUI.Label(cellRect, item.status, AkWwiseTreeViewItem.OutOfDateStyle);
					}

				}
				break;
		}
	}

	public void SetExpandedUpwardsRecursive(TreeViewItem item)
	{
		if (item == null)
		{
			return;
		}
		SetExpanded(item.id, true);
		SetExpandedUpwardsRecursive(item.parent);
	}

	public AkWwiseTreeViewItem Find(int id)
	{
		var result = this.m_Rows.FirstOrDefault(element => element.id == id);
		return result as AkWwiseTreeViewItem;
	}

	#endregion

	#region click and drag/drop
	protected override bool CanMultiSelect(TreeViewItem item)
	{
		return true;
	}

	protected override void SelectionChanged(IList<int> selectedIds)
	{
		dataSource.ItemSelected(Find(selectedIds.Last()));
		base.SelectionChanged(selectedIds);
	}

	public bool CheckWaapi()
	{
		return AkWwiseEditorSettings.Instance.UseWaapi && AkWaapiUtilities.IsConnected();
	}

	protected override void ContextClickedItem(int id)
	{
		List<int> selectedIDs = GetSelection().ToList();

		List<AkWwiseTreeViewItem> selectedItems = selectedIDs
			.Select(this.Find)
			.Where(item => item != null)
			.ToList();

		UnityEditor.GenericMenu menu = new UnityEditor.GenericMenu();
		if (CheckWaapi())
		{
			List<AkWwiseTreeViewItem> soundBankItems = selectedItems
				.Where(item => item.objectType == WwiseObjectType.Soundbank)
				.ToList();

			List<AkWwiseTreeViewItem> nonSoundBankItems = selectedItems
				.Where(item => item.objectType != WwiseObjectType.Soundbank)
				.ToList();

			List<AkWwiseTreeViewItem> playableItems = selectedItems
				.Where(item => CanPlay(item))
				.ToList();

			if (playableItems.Any())
			{
				menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Play \u2215 Stop _SPACE"), false,
					() => 
					{
						playableItems.ForEach(item => 
						{
							AkWaapiUtilities.TogglePlayEvent(item.objectType, item.objectGuid);
						});
					});
			}
			else
			{
				menu.AddDisabledItem(UnityEditor.EditorGUIUtility.TrTextContent("Play \u2215 Stop _Space"));
			}

			menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Stop All"), false,
					() => AkWaapiUtilities.StopAllTransports());

			menu.AddSeparator("");

			CreateExpansionOptions(menu, selectedItems);

			menu.AddSeparator("");

			bool shouldOpenNewExplorerTab = soundBankItems.Count() + nonSoundBankItems.Count() > 1;
			
			if (soundBankItems.Any())
			{
				menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Open Folder (SoundBanks)/WorkUnit #O"), false,
					() => soundBankItems.ForEach(item => 
					{
						AkWaapiUtilities.OpenWorkUnitInExplorer(item.objectGuid, shouldOpenNewExplorerTab);
					}));

				menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Open Folder (SoundBanks)/SoundBank "), false,
					() => soundBankItems.ForEach(item => 
					{
						AkWaapiUtilities.OpenSoundBankInExplorer(item.objectGuid);
					}));
			}

			if(nonSoundBankItems.Any())
			{
				menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Open Containing Folder #O"), false,
					() => nonSoundBankItems.ForEach(item => 
					{
						AkWaapiUtilities.OpenWorkUnitInExplorer(item.objectGuid, shouldOpenNewExplorerTab);
					}));
			}
		}
		else
		{
			menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Wwise Connection Settings"), false,
				OpenSettings);
			menu.AddSeparator("");

			menu.AddDisabledItem(UnityEditor.EditorGUIUtility.TrTextContent("Play \u2215 Stop"));
			menu.AddDisabledItem(UnityEditor.EditorGUIUtility.TrTextContent("Stop all"));

			menu.AddSeparator("");

			CreateExpansionOptions(menu, selectedItems);

			menu.AddSeparator("");
			menu.AddDisabledItem(UnityEditor.EditorGUIUtility.TrTextContent("Open Containing Folder"));
		}

		//This is the only operation that does not support multiple selection.
		var item = Find(id);
		menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Find References in Scene #R"), false,
			 () => FindReferencesInScene(item));

		menu.ShowAsContext();
	}

	private void CreateExpansionOptions(UnityEditor.GenericMenu menu, List<AkWwiseTreeViewItem> selectedItems)
	{
		List<AkWwiseTreeViewItem> expandableItems = selectedItems
			.Where((item) => CanExpandItem(item))
			.ToList();

		List<AkWwiseTreeViewItem> collapsableItems = selectedItems
			.Where((item) => CanCollapseItem(item))
			.ToList();

		if (expandableItems.Any())
		{
			menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Expand Selected"), false,
				() => expandableItems.ForEach(item => SetExpandedRecursive(item.id, true)));
		}
		else
		{
			menu.AddDisabledItem(UnityEditor.EditorGUIUtility.TrTextContent("Expand Selected"));
		}

		if(collapsableItems.Any())
		{
			menu.AddItem(UnityEditor.EditorGUIUtility.TrTextContent("Collapse Selected"), false,
				() => collapsableItems.ForEach(item => SetExpandedRecursive(item.id, false)));
		}
		else
		{
			menu.AddDisabledItem(UnityEditor.EditorGUIUtility.TrTextContent("Collapse Selected"));
		}
	}

	protected void OpenSettings()
	{
		UnityEditor.SettingsService.OpenProjectSettings("Project/Wwise Integration");
	}

	protected override void KeyEvent()
	{
		var selected = GetSelection();
		if (selected.Count == 0)
		{
			return;
		}

		if (UnityEngine.Event.current.type == UnityEngine.EventType.KeyDown)
		{
			foreach (var selection in selected)
			{
				var item = Find(selection);

				switch (UnityEngine.Event.current.keyCode)
				{
					case UnityEngine.KeyCode.KeypadEnter:
						DoubleClickedItem(item.id);
						UnityEngine.Event.current.Use();
						break;
					case UnityEngine.KeyCode.Space:
						if (CanPlay(item))
							AkWaapiUtilities.TogglePlayEvent(item.objectType, item.objectGuid);
						UnityEngine.Event.current.Use();
						break;
					case UnityEngine.KeyCode.O:
						if (UnityEngine.Event.current.shift)
						{
							if (CanOpen(item))
								AkWaapiUtilities.OpenWorkUnitInExplorer(item.objectGuid);
							UnityEngine.Event.current.Use();
						}

						break;
					case UnityEngine.KeyCode.F:
						if (UnityEngine.Event.current.shift)
						{
							if (CanSelect(item))
								m_dataSource.SelectObjectInAuthoring(item.objectGuid);
							UnityEngine.Event.current.Use();
						}

						break;
					case UnityEngine.KeyCode.R:
						if (UnityEngine.Event.current.shift)
						{
							FindReferencesInScene(item);
							UnityEngine.Event.current.Use();
						}

						break;
				}
			}
		}
}

	internal static void FindReferencesInScene(AkWwiseTreeViewItem item)
	{
		var reference = WwiseObjectReference.FindWwiseObject(item.objectType, item.objectGuid);
		var path = UnityEditor.AssetDatabase.GetAssetPath(reference);

		if (path.IndexOf(' ') != -1)
			path = '"' + path + '"';

		if (path == string.Empty)
		{
			WwiseLogger.Log($"No references to {item.displayName} in scene.");
			return;
		}

#if !UNITY_2019_1_OR_NEWER
		//drop "Assets" part of path
		path = string.Join("/", path.Split('/').Skip(1));
#endif

		var searchFilter = "ref:" + path;

		System.Type type = typeof(UnityEditor.SearchableEditorWindow);
		System.Reflection.FieldInfo info = type.GetField("searchableWindows",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		var searchableWindows = info.GetValue(null) as List<UnityEditor.SearchableEditorWindow>;

		foreach (UnityEditor.SearchableEditorWindow sw in searchableWindows)
		{
			info = type.GetField("m_HierarchyType",
				System.Reflection.BindingFlags.NonPublic);
			if (sw.GetType().ToString() == "UnityEditor.SceneHierarchyWindow")
			{
				if (sw.GetType().ToString() == "UnityEditor.SceneHierarchyWindow")
				{
					System.Reflection.MethodInfo setSearchFilter = typeof(UnityEditor.SearchableEditorWindow).GetMethod(
						"SetSearchFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					object[] parameters = new object[] { searchFilter, 0, false, false };

					setSearchFilter.Invoke(sw, parameters);
					sw.Repaint();
				}
			}
		}
	}

	protected bool CanPlay(TreeViewItem item)
	{
		if (!CheckWaapi()) return false;

		var wwiseItem = (AkWwiseTreeViewItem)item;
		if (wwiseItem.objectType == WwiseObjectType.Event) return true;

		return false;
	}

	protected bool CanExpandItem(TreeViewItem item)
	{
		return item.children.Count > 0 && !IsExpanded(item.id);
	}

	protected bool CanCollapseItem(TreeViewItem item)
	{
		return item.children.Count > 0 && IsExpanded(item.id);
	}

	protected bool CanSelect(TreeViewItem item)
	{
		if (!CheckWaapi()) return false;
		return true;
	}

	protected bool CanOpen(TreeViewItem item)
	{
		if (!CheckWaapi()) return false;
		return true;
	}

	const int MAX_NAME_LENGTH = 1024;
	bool ValidateNameChange(AkWwiseTreeViewItem item, string newName)
	{
		if (item == null)
		{
			WwiseLogger.Warning("Tree item no longer exists");
			return false;
		}

		if (newName.Trim() == System.String.Empty)
		{
			WwiseLogger.Warning("Names cannot be left blank");
			return false;
		}

		if (newName.Trim().Length >= MAX_NAME_LENGTH)
		{
			WwiseLogger.Warning($"Names must be less than {MAX_NAME_LENGTH} characters long.");
			return false;
		}

		// If the new name is the same as the old name, consider this to be unchanged
		if (item.displayName == newName)
		{
			return false;
		}

		if (newName.Contains('/') || newName.Contains('\\'))
		{
			WwiseLogger.Warning("Item names cannot contain / or \\.");
			return false;
		}

		// Validate that an item with this name doesn't exist already
		if (item.parent.children.Find((i) => i.displayName == newName) != null)
		{
			WwiseLogger.Warning("An item with this name already exists at this level");
			return false;
		}

		return true;
	}

	public delegate void DoubleClickFunctionDelegate(AkWwiseTreeViewItem element);

	private DoubleClickFunctionDelegate doubleClickExternalFunction;
	public void SetDoubleClickFunction(DoubleClickFunctionDelegate f)
	{
		doubleClickExternalFunction = f;
	}

	protected override void DoubleClickedItem(int id)
	{
		base.DoubleClickedItem(id);
		var doubleClickedElement = m_dataSource.FindById(id);
		doubleClickExternalFunction?.Invoke(doubleClickedElement);
	}

	public bool DragDropEnabled;
	protected override bool CanStartDrag(CanStartDragArgs args)
	{
		return DragDropEnabled;
	}

	protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
	{
		UnityEditor.DragAndDrop.PrepareStartDrag();

		var draggedRows = GetRows().Where(item => args.draggedItemIDs.Contains(item.id)).ToList();
		var draggedItem = draggedRows[0] as AkWwiseTreeViewItem;
		if (draggedItem.objectGuid == System.Guid.Empty ||
			draggedItem.objectType == WwiseObjectType.Bus ||
			draggedItem.objectType == WwiseObjectType.PhysicalFolder ||
			draggedItem.objectType == WwiseObjectType.Folder ||
			draggedItem.objectType == WwiseObjectType.WorkUnit ||
			draggedItem.objectType == WwiseObjectType.Project ||
			draggedItem.objectType == WwiseObjectType.StateGroup ||
			draggedItem.objectType == WwiseObjectType.SwitchGroup)
			return;


		var reference = WwiseObjectReference.FindOrCreateWwiseObject(draggedItem.objectType, draggedItem.name, draggedItem.objectGuid);
		if (!reference)
			return;

		var groupReference = reference as WwiseGroupValueObjectReference;
		if (groupReference)
		{
			var parent = draggedItem.parent as AkWwiseTreeViewItem;
			groupReference.SetupGroupObjectReference(parent.name, parent.objectGuid);
		}

		UnityEditor.MonoScript script;
		if (DragDropMonoScriptMap.TryGetValue(reference.WwiseObjectType, out script))
		{
			UnityEngine.GUIUtility.hotControl = 0;
			UnityEditor.DragAndDrop.PrepareStartDrag();
			UnityEditor.DragAndDrop.objectReferences = new UnityEngine.Object[] { script };
			AkWwiseTypes.DragAndDropObjectReference = reference;
			UnityEditor.DragAndDrop.StartDrag("Dragging an AkObject");
		}
	}

	public void SetDataSource(AkWwiseTreeDataSource datasource)
	{
		if (m_dataSource != null)
		{
			m_dataSource.modelChanged -= this.ModelChanged;
			m_dataSource.SetWwiseTreeView(null);
		}
		m_dataSource = datasource;
		m_dataSource.modelChanged += this.ModelChanged;
		m_dataSource.SetWwiseTreeView(this);
		m_dataSource.FetchData();
	}

#endregion
}


#region Icons
public class AkWwisePickerIcons
{
	public const float kIconWidth = 18f;

	private UnityEngine.Texture2D m_textureWwiseAcousticTextureIcon;
	private UnityEngine.Texture2D m_textureWwiseAuxBusIcon;
	private UnityEngine.Texture2D m_textureWwiseBusIcon;
	private UnityEngine.Texture2D m_textureWwiseEventIcon;
	private UnityEngine.Texture2D m_textureWwiseFolderIcon;
	private UnityEngine.Texture2D m_textureWwiseGameParameterIcon;
	private UnityEngine.Texture2D m_textureWwisePhysicalFolderIcon;
	private UnityEngine.Texture2D m_textureWwiseProjectIcon;
	private UnityEngine.Texture2D m_textureWwiseSoundbankIcon;
	private UnityEngine.Texture2D m_textureWwiseStateIcon;
	private UnityEngine.Texture2D m_textureWwiseStateGroupIcon;
	private UnityEngine.Texture2D m_textureWwiseSwitchIcon;
	private UnityEngine.Texture2D m_textureWwiseSwitchGroupIcon;
	private UnityEngine.Texture2D m_textureWwiseWorkUnitIcon;
	private UnityEngine.Texture2D m_textureWwiseTriggerIcon;

	public static UnityEngine.Texture2D GetTexture(string texturePath)
	{
		try
		{
			return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(texturePath);
		}
		catch (System.Exception ex)
		{
			WwiseLogger.Error(string.Format("Failed to find local texture: {0}", ex));
			return null;
		}
	}

	public void LoadIcons()
	{
		var tempWwisePath = "Assets/Wwise/API/Editor/WwiseWindows/TreeViewIcons/";

		m_textureWwiseAcousticTextureIcon = GetTexture(tempWwisePath + "acoustictexture_nor.png");
		m_textureWwiseAuxBusIcon = GetTexture(tempWwisePath + "auxbus_nor.png");
		m_textureWwiseBusIcon = GetTexture(tempWwisePath + "bus_nor.png");
		m_textureWwiseEventIcon = GetTexture(tempWwisePath + "event_nor.png");
		m_textureWwiseFolderIcon = GetTexture(tempWwisePath + "folder_nor.png");
		m_textureWwiseGameParameterIcon = GetTexture(tempWwisePath + "gameparameter_nor.png");
		m_textureWwisePhysicalFolderIcon = GetTexture(tempWwisePath + "physical_folder_nor.png");
		m_textureWwiseProjectIcon = GetTexture(tempWwisePath + "wproj.png");
		m_textureWwiseSoundbankIcon = GetTexture(tempWwisePath + "soundbank_nor.png");
		m_textureWwiseStateIcon = GetTexture(tempWwisePath + "state_nor.png");
		m_textureWwiseStateGroupIcon = GetTexture(tempWwisePath + "stategroup_nor.png");
		m_textureWwiseSwitchIcon = GetTexture(tempWwisePath + "switch_nor.png");
		m_textureWwiseSwitchGroupIcon = GetTexture(tempWwisePath + "switchgroup_nor.png");
		m_textureWwiseWorkUnitIcon = GetTexture(tempWwisePath + "workunit_nor.png");
		m_textureWwiseTriggerIcon = GetTexture(tempWwisePath + "trigger_nor.png");
	}

	public UnityEngine.Texture2D GetIcon(WwiseObjectType type)
	{
		switch (type)
		{
			case WwiseObjectType.AcousticTexture:
				return m_textureWwiseAcousticTextureIcon;
			case WwiseObjectType.AuxBus:
				return m_textureWwiseAuxBusIcon;
			case WwiseObjectType.Bus:
				return m_textureWwiseBusIcon;
			case WwiseObjectType.Event:
				return m_textureWwiseEventIcon;
			case WwiseObjectType.Folder:
				return m_textureWwiseFolderIcon;
			case WwiseObjectType.GameParameter:
				return m_textureWwiseGameParameterIcon;
			case WwiseObjectType.PhysicalFolder:
				return m_textureWwisePhysicalFolderIcon;
			case WwiseObjectType.Project:
				return m_textureWwiseProjectIcon;
			case WwiseObjectType.Soundbank:
				return m_textureWwiseSoundbankIcon;
			case WwiseObjectType.State:
				return m_textureWwiseStateIcon;
			case WwiseObjectType.StateGroup:
				return m_textureWwiseStateGroupIcon;
			case WwiseObjectType.Switch:
				return m_textureWwiseSwitchIcon;
			case WwiseObjectType.SwitchGroup:
				return m_textureWwiseSwitchGroupIcon;
			case WwiseObjectType.WorkUnit:
				return m_textureWwiseFolderIcon;
			case WwiseObjectType.Trigger:
				return m_textureWwiseTriggerIcon;
			default:
				return m_textureWwisePhysicalFolderIcon;
		}
	}
}
#endregion
#endif
