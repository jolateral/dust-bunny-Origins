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

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using AK.Wwise.Unity.Logging;

public class AkWwiseTreeMergedDataSource : AkWwiseTreeDataSource
{

	private AkWwiseTreeProjectDataSource SoundBanksDataSource;
	private AkWwiseTreeWAAPIDataSource WaapiDataSource;
	
	public AkWwiseTreeMergedDataSource() : base()
	{
		AkWwiseJSONBuilder.Populate();
		SoundBanksDataSource = AkWwiseProjectInfo.ProjectPickerData;
		WaapiDataSource = AkWwiseProjectInfo.WaapiBrowserData;
		WaapiDataSource.ObjectInfoLoaded+=OnWaapiDataLoaded;
		SoundBanksDataSource.modelChanged+=MergeDataSources;
		WaapiDataSource.modelChanged+=MergeDataSources;
	}

	public void OnWaapiDataLoaded()
	{
		MergeDataSources();
		isRefreshing = false;
	}
	
	public AkWwiseTreeMergedDataSource(AkWwiseTreeProjectDataSource projectDataSource, AkWwiseTreeWAAPIDataSource waapiDataSource) : base()
	{
		SoundBanksDataSource = projectDataSource;
		WaapiDataSource = waapiDataSource;
	}

	~AkWwiseTreeMergedDataSource()
	{
		SoundBanksDataSource.modelChanged-=MergeDataSources;
		WaapiDataSource.modelChanged-=MergeDataSources;
		WaapiDataSource.ObjectInfoLoaded-=OnWaapiDataLoaded;
	}

	public override void FetchData()
	{
		if (AkWaapiUtilities.IsConnected())
		{
			isRefreshing = true;
		}
		SoundBanksDataSource.FetchData();
		WaapiDataSource.FetchData();
		Changed();
	}

	private AkWwiseTreeViewItem GetCurrentWaapiParent(AkWwiseTreeViewItem Item, int index, int totalSize)
	{
		AkWwiseTreeViewItem currItem = Item;
		var distanceFromParent = totalSize - 1 - index;
		while (distanceFromParent > 0)
		{
			if (currItem.parent is AkWwiseTreeViewItem item)
			{
				currItem = item;
			}
			else
			{
				return null;
			}
			distanceFromParent--;
		}
		return currItem;
	}

	void AddItem(AkWwiseTreeViewItem Item)
	{
		var parts = Item.path.Split("\\");
		var root = ProjectRoot;
		int counter = 0;
		for (int i = 0; i < parts.Length; i++)
		{
			if (parts[i].Length == 0)
			{
				counter++;
				continue;
			}
			AkWwiseTreeViewItem currentWaapiItem = GetCurrentWaapiParent(Item, i, parts.Length);
			bool found = false;
			foreach (var child in root.children)
			{
				// WG-79742: Even if the child exist, make sure that it's info is up to date, otherwise update it.
				if (child.displayName == currentWaapiItem.displayName)
				{
					if (child is AkWwiseTreeViewItem currItem)
					{
						if (currItem.waapiPath != currentWaapiItem.waapiPath)
						{
							currItem.objectGuid = currentWaapiItem.objectGuid;
							currItem.objectType = currentWaapiItem.objectType;
							currItem.waapiPath = currentWaapiItem.waapiPath;
							currItem.waapiName = currentWaapiItem.name;
						}
					}
				}
				if (child.displayName == parts[counter])
				{
					root = child as AkWwiseTreeViewItem;
					found = true;
					counter++;
					break;
				}
			}

			if (counter > parts.Length)
			{
				return;
			}

			if (!found)
			{
				break;
			}
		}

		for (int currentItemIndex = counter; currentItemIndex < parts.Length; currentItemIndex++)
		{
			var Child = new AkWwiseTreeViewItem(parts[currentItemIndex], counter, 0, Guid.Empty, WwiseObjectType.Folder);
			
			AkWwiseTreeViewItem currentWaapiItem = GetCurrentWaapiParent(Item, currentItemIndex, parts.Length);
			// WG-79742: If the counter is smaller than parts.Length - 1, it means that even the new item parent(s) isn't (aren't) in the Data Source. Make sure to add the WAAPI information for the parents as well.
			Child.objectGuid = currentWaapiItem.objectGuid;
			Child.objectType = currentWaapiItem.objectType;
			Child.path = "";
			Child.waapiPath = currentWaapiItem.waapiPath;
			Child.waapiName = currentWaapiItem.name;
			Child.bNewInWwise = true;
			Child.bNewInWwise = true;
			root.AddWwiseItemChild(Child);
			root = Child;
		}
	}
	
	void MergeWaapiDataSource(AkWwiseTreeViewItem Item)
	{
		var ProjectDBObj = FindByGuid(Item.objectGuid);
		if (ProjectDBObj != null && !ProjectDBObj.IsFolder())
		{
			ProjectDBObj.waapiName = Item.name;
			ProjectDBObj.waapiPath = Item.path;
			
			//An existing item was only moved in WAAPI.
			//Due to the condition below, the parent will be flagged as having children and won't be added to the DataSource.
			//In this special case, we need to directly add the parent to the DataSource
			if (ProjectDBObj.parent != Item.parent)
			{
				if (Item.parent is AkWwiseTreeViewItem item)
				{
					AddItem(item);
				}
			}
			return;
		}

		if (ProjectDBObj == null && !Item.hasChildren)
		{
			ProjectDBObj = FindByName(Item.name, Item.objectType);
			if (ProjectDBObj == null)
			{
				AddItem(Item);	
			}
			else
			{
				ProjectDBObj.waapiName = Item.name;
				ProjectDBObj.waapiPath = Item.waapiPath;
				ProjectDBObj.bDifferentGuid = true;
			}
		}
	}

	void UpdateIds(TreeViewItem Item, ref int Id)
	{
		Item.id = Id;
		foreach (var child in Item.children)
		{
			Id++;
			UpdateIds(child, ref Id);
		}
	}
	void UpdateIds()
	{
		int counter = 1;
		foreach (var child in ProjectRoot.children)
		{
			UpdateIds(child, ref counter);
			counter++;
		}
		m_MaxID = counter;
	}

	public void MergeDataSources()
	{
		ProjectRoot = SoundBanksDataSource.ProjectRoot.Copy();
		TreeUtility.TreeToList(ProjectRoot, ref Data);

		foreach (var WaapiItem in WaapiDataSource.Data.ItemDict.Values)
		{
			MergeWaapiDataSource(WaapiItem);
		}

		// We do not want to sort the root object as the order is forced
		ProjectRoot.isSorted = true;
		foreach (var child in ProjectRoot.children)
		{
		    (child as AkWwiseTreeViewItem).SortChildren();
		}

		UpdateIds();
		TreeUtility.TreeToList(ProjectRoot, ref Data);
		Changed();
	}

	public override AkWwiseTreeViewItem GetComponentDataRoot(WwiseObjectType objectType)
	{
		foreach (var child in ProjectRoot.children)
		{
			if (child.displayName == AkWwiseTreeDataSource.FolderNames[objectType])
			{
				var tempProjectRoot = new AkWwiseTreeViewItem(ProjectRoot);
				tempProjectRoot.AddWwiseItemChild(child as AkWwiseTreeViewItem);
				return tempProjectRoot;
			}
		}

		return null;
	}

	protected AkWwiseTreeViewItem BuildObjectTypeTree(WwiseObjectType objectType)
	{
		var rootElement = new AkWwiseTreeViewItem();
		switch (objectType)
		{
			case WwiseObjectType.AuxBus:
				rootElement = BuildTree(FolderNames[WwiseObjectType.AuxBus], AkWwiseProjectInfo.GetData().BusRoot);
				break;

			case WwiseObjectType.Event:
				rootElement = BuildTree(FolderNames[WwiseObjectType.Event], AkWwiseProjectInfo.GetData().EventRoot);
				break;

			case WwiseObjectType.Soundbank:
				rootElement = BuildTree(FolderNames[WwiseObjectType.Soundbank], AkWwiseProjectInfo.GetData().BankRoot);
				break;

			case WwiseObjectType.State:
				rootElement = BuildTree(FolderNames[WwiseObjectType.State], AkWwiseProjectInfo.GetData().StateRoot);
				break;

			case WwiseObjectType.Switch:
			case WwiseObjectType.SwitchGroup:
				rootElement = BuildTree(FolderNames[WwiseObjectType.Switch], AkWwiseProjectInfo.GetData().SwitchRoot);
				break;

			case WwiseObjectType.GameParameter:
				rootElement = BuildTree(FolderNames[WwiseObjectType.GameParameter], AkWwiseProjectInfo.GetData().GameParameterRoot);
				break;

			case WwiseObjectType.Trigger:
				rootElement = BuildTree(FolderNames[WwiseObjectType.Trigger], AkWwiseProjectInfo.GetData().TriggerRoot);
				break;

			case WwiseObjectType.AcousticTexture:
				rootElement = BuildTree(FolderNames[WwiseObjectType.AcousticTexture], AkWwiseProjectInfo.GetData().AcousticTextureRoot);
				break;
		}
		wwiseObjectFolders[objectType] = rootElement;
		return rootElement;
	}

	AkWwiseTreeViewItem BuildTree(AkWwiseTreeViewItem treeViewItem, List<AkWwiseProjectData.WwiseTreeObject> childrenInfo, int depth)
	{
		if (childrenInfo == null)
		{
			return treeViewItem;
		}
		foreach (var children in childrenInfo)
		{
			var childItem = new AkWwiseTreeViewItem(children.Name, depth, GenerateUniqueID(), children.Guid, children.Type);
			childItem = BuildTree(childItem, children.Children, depth++);
			if (childItem != null)
			{
				treeViewItem.AddWwiseItemChild(childItem);	
			}
		}

		return treeViewItem;
	}

	public AkWwiseTreeViewItem BuildTree(string name,
	List<AkWwiseProjectData.WwiseTreeObject> Events)
	{
		var rootFolder = new AkWwiseTreeViewItem(name, 1, GenerateUniqueID(), System.Guid.NewGuid(), WwiseObjectType.PhysicalFolder);
		return BuildTree(rootFolder, Events, 1);
	}

	private AkWwiseTreeViewItem AddTreeItem(AkWwiseTreeViewItem parentWorkUnit, List<AkWwiseProjectData.PathElement> pathAndIcons)
	{
		var pathDepth = pathAndIcons.Count;
		var treeDepth = pathDepth + 1;
		AkWwiseTreeViewItem newItem;
		AkWwiseProjectData.PathElement pathElem;
		var parent = parentWorkUnit;

		if (pathDepth > parentWorkUnit.depth)
		{
			var unaccountedDepth = pathDepth - parentWorkUnit.depth;
			for (; unaccountedDepth > 0; unaccountedDepth--)
			{
				var pathIndex = pathAndIcons.Count - unaccountedDepth;
				pathElem = pathAndIcons[pathIndex];
				if (pathElem.ObjectGuid == System.Guid.Empty)
				{
					var path = AkWwiseProjectData.PathElement.GetProjectPathString(pathAndIcons, pathIndex);
					newItem = Find(pathElem.ObjectGuid, pathElem.ElementName, path);
				}
				else
				{
					newItem = FindByGuid(pathElem.ObjectGuid);
				}

				if (newItem == null)
				{
					newItem = new AkWwiseTreeViewItem(pathElem.ElementName, treeDepth - unaccountedDepth, GenerateUniqueID(), pathElem.ObjectGuid, pathElem.ObjectType);
					parent.AddWwiseItemChild(newItem);
					Data.Add(newItem);

				}
				parent = newItem;

			}
		}

		pathElem = pathAndIcons.Last();
		newItem = FindByGuid(pathElem.ObjectGuid);

		if (newItem == null)
		{
			newItem = new AkWwiseTreeViewItem(pathElem.ElementName, treeDepth, GenerateUniqueID(), pathElem.ObjectGuid, pathElem.ObjectType);
			parent.AddWwiseItemChild(newItem);
			Data.Add(newItem);
		}
		return newItem;
	}

	public AkWwiseTreeViewItem Find(System.Guid guid, string name, string path)
	{
		if (guid.Equals(System.Guid.Empty))
		{
			var results = Data.ItemDict.Values.ToList().FindAll(element => element.objectGuid == guid && element.name == name);

			foreach (var r in results)
			{
				var itemPath = GetProjectPath(r, "");
				if (itemPath == path)
				{
					return r;
				}
			}
		}
		else
		{
			return Data.ItemDict[guid];
		}
		

		return null;
	}

	public string GetProjectPath(AkWwiseTreeViewItem item, string currentpath)
	{
		currentpath = $"/{item.name}{currentpath}";
		if (item.parent == null || item.parent == ProjectRoot)
		{
			return currentpath;
		}

		return GetProjectPath(item.parent as AkWwiseTreeViewItem, currentpath);
	}


	public override AkWwiseTreeViewItem GetSearchResults()
	{
		return SearchRoot;
	}

	public override void UpdateSearchResults(string searchString, WwiseObjectType objectType, BrowserFilter Filters)
	{
		SearchRoot = new AkWwiseTreeViewItem(ProjectRoot);
		if (objectType != WwiseObjectType.None)
		{
			SearchRoot = new AkWwiseTreeViewItem(ProjectRoot);
			var rootItemOfType = GetComponentDataRoot(objectType);
			if (rootItemOfType != null)
			{
				var objectRoot = new AkWwiseTreeViewItem(rootItemOfType);
				TreeUtility.CopyTree(rootItemOfType, objectRoot);
				SearchRoot.AddWwiseItemChild(objectRoot);
			}
		}
		else
		{
			TreeUtility.CopyTree(ProjectRoot, SearchRoot);
		}
		FilterTree(SearchRoot, searchString, Filters);
	}

	BrowserFilter GetParentFilter(AkWwiseTreeViewItem parent)
	{
		switch (parent.name)
		{
			case "Busses":
				return BrowserFilter.Bus;
			case "Events":
				return BrowserFilter.Event;
			case "Game Parameters":
				return BrowserFilter.GameParameter;
			case "SoundBanks":
				return BrowserFilter.SoundBank;
			case "States":
				return BrowserFilter.State;
			case "Switches":
				return BrowserFilter.Switch;
			case "Triggers":
				return BrowserFilter.Trigger;
			case "Virtual Acoustics":
				return BrowserFilter.AcousticTexture;
		}

		return BrowserFilter.None;
	}

	bool IsFilteredTypes(AkWwiseTreeViewItem treeElement, BrowserFilter Filters)
	{
		if (treeElement.objectType == WwiseObjectType.Project)
		{
			return true;
		}
		
		AkWwiseTreeViewItem topElement = treeElement;
		bool projectTypeFound = false;
		while(!projectTypeFound)
		{
			AkWwiseTreeViewItem parent = (AkWwiseTreeViewItem)topElement.parent;

			if (parent.objectType == WwiseObjectType.Project)
			{
				projectTypeFound = true;
			}
			else
			{
				topElement = parent;
			}
		}
		BrowserFilter Expected = BrowserFilter.Bus | BrowserFilter.Event |
		                         BrowserFilter.SoundBank | BrowserFilter.Switch
		                         | BrowserFilter.State | BrowserFilter.GameParameter |
		                         BrowserFilter.Trigger | BrowserFilter.AcousticTexture;
		if (((ulong)Expected & (ulong)Filters) == 0)
		{
			return true;
		}
		BrowserFilter Filter = BrowserFilter.None;
		switch (treeElement.objectType)
		{
			case WwiseObjectType.None:
				Filter = BrowserFilter.None;
				break;
			case WwiseObjectType.AuxBus:
				Filter = BrowserFilter.Bus;
				break;
			case WwiseObjectType.Bus:
				Filter = BrowserFilter.Bus;
				break;
			case WwiseObjectType.Event:
				Filter = BrowserFilter.Event;
				break;
			case WwiseObjectType.Folder:
			case WwiseObjectType.WorkUnit:
				Filter = GetParentFilter(topElement);
				if (Filter == BrowserFilter.None || !treeElement.hasChildren)
				{
					return false;
				}
				break;
			case WwiseObjectType.PhysicalFolder:
				break;
			case WwiseObjectType.Project:
				break;
			case WwiseObjectType.Soundbank:
				Filter = BrowserFilter.SoundBank;
				break;
			case WwiseObjectType.State:
			case WwiseObjectType.StateGroup: // When filtering for States we also want to include State groups.
				Filter = BrowserFilter.State;
				break;
			case WwiseObjectType.Switch:
			case WwiseObjectType.SwitchGroup: // When filtering for Switches we also want to include Switch groups.
				Filter = BrowserFilter.Switch;
				break;
			case WwiseObjectType.GameParameter:
				Filter = BrowserFilter.GameParameter;
				break;
			case WwiseObjectType.Trigger:
				Filter = BrowserFilter.Trigger;
				break;
			case WwiseObjectType.AcousticTexture:
				Filter = BrowserFilter.AcousticTexture;
				break;
		}

		if (Filter == BrowserFilter.None)
		{
			return true;
		}
		return ((ulong)Filter & (ulong)Filters) != 0;
	}

	bool IsFilteredStatus(AkWwiseTreeViewItem treeElement, BrowserFilter Filters)
	{
		if (treeElement.IsFolder())
		{
			return true;
		}
		BrowserFilter Expected = BrowserFilter.SoundBanksUpToDate | BrowserFilter.NewInWwise |
		                        BrowserFilter.DeletedInWwise | BrowserFilter.RenamedInWwise
		                        | BrowserFilter.MovedInWwise | BrowserFilter.SoundBankNeedsUpdate;
		if (((ulong)Expected & (ulong)Filters) == 0)
		{
			return true;
		}
		BrowserFilter Filter = BrowserFilter.None;
		if (treeElement.IsUpToDate)
		{
			Filter = BrowserFilter.SoundBanksUpToDate;
		}
		else if (treeElement.bNewInWwise)
		{
			Filter = BrowserFilter.NewInWwise;
		}
		else if (treeElement.bDifferentGuid)
		{
			Filter = BrowserFilter.SoundBankNeedsUpdate;
		}
		else if (treeElement.IsDeletedInWwise)
		{
			Filter = BrowserFilter.DeletedInWwise;
		}
		else if (treeElement.IsRenamedInWwise)
		{
			Filter = BrowserFilter.RenamedInWwise;
		}
		else if (treeElement.IsMovedInWwise)
		{
			Filter = BrowserFilter.MovedInWwise;
		}

		if (Filter == BrowserFilter.None)
		{
			return true;
		}
		return ((ulong)Filter & (ulong)Filters) != 0;
	}

	bool ShouldRemove(AkWwiseTreeViewItem treeElement, BrowserFilter Filters)
	{
		if (Filters == BrowserFilter.None)
		{
			return false;
		}

		if (IsFilteredTypes(treeElement, Filters) && IsFilteredStatus(treeElement, Filters))
		{
			return false;
		}
		return true;
	}
	
	void FilterTree(AkWwiseTreeViewItem treeElement, string searchFilter, BrowserFilter Filters)
	{
		var ItemsToRemove = new List<AkWwiseTreeViewItem>();
		for (int i = 0; i < treeElement.children.Count(); i++)
		{
			var current = treeElement.children[i] as AkWwiseTreeViewItem;
			FilterTree(current, searchFilter, Filters);

			if ((current.name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) == -1 && current.children.Count == 0) || ShouldRemove(current, Filters))
			{
				ItemsToRemove.Add(current);
			}
		}

		for (int i = 0; i < ItemsToRemove.Count(); i++)
		{
			treeElement.children.Remove(ItemsToRemove[i]);
		}
	}

	public override void SetExpanded(IEnumerable<System.Guid> ids)
	{
		if (TreeView != null)
		{
			TreeView.state.expandedIDs = GetIdsFromGuids(ids).ToList();
		}
		Changed();
	}

	public override void SaveExpansionStatus(List<int> expandedItems )
	{
		AkWwiseProjectInfo.GetData().ExpandedFileSystemItemIds = expandedItems;
	}

	public override List<int> LoadExpansionSatus()
	{
		return AkWwiseProjectInfo.GetData().ExpandedFileSystemItemIds;
	}

	public override void SetWwiseTreeView(AkWwiseTreeView treeView)
	{
		TreeView = treeView;
		WaapiDataSource.SetWwiseTreeView(treeView);
		SoundBanksDataSource.SetWwiseTreeView(treeView);
	}
	
	public override void ItemSelected(AkWwiseTreeViewItem item)
	{
		// WG-79742: The auto-sync selection feature do not support selecting folders that are part of the minimal structure of a Wwise Project (Just like in Unreal).
		if (item.parent is AkWwiseTreeViewItem parent && parent.objectType == WwiseObjectType.Project)
		{
			return;
		}
		AkWaapiUtilities.GetStatusString(out var bConnected);
		if (AkWwiseEditorSettings.Instance.AutoSyncWaapi && bConnected)
		{
			if (item.IsDeletedInWwise)
			{
				WwiseLogger.Log("Item has been deleted in Wwise. Nothing to sync in Wwise.");
				return;
			}
				
			if (item.bDifferentGuid)
			{
				WwiseLogger.Log("Item has a different GUID in Wwise. Generate SoundBanks to sync selection.");
				return;
			}
			WaapiDataSource.SelectObjectInAuthoring(item.objectGuid);
		}
	}
}
#endif