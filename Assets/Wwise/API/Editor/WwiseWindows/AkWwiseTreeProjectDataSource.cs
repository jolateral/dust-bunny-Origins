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

public class AkWwiseTreeProjectDataSource : AkWwiseTreeDataSource
{

	public AkWwiseTreeProjectDataSource() : base()
	{
		WwiseProjectDatabase.SoundBankDirectoryUpdated += FetchData;
	}
	
	~AkWwiseTreeProjectDataSource()
	{
		WwiseProjectDatabase.SoundBankDirectoryUpdated -= FetchData;
	}

	public override void FetchData()
	{
		AkWwiseProjectInfo.GetData().Reset();
		AkWwiseJSONBuilder.Populate();
		Data.Clear();
		m_MaxID = 0;
		InitializeMinimal();
		Changed();
	}

	protected void InitializeMinimal()
	{
		ProjectRoot = CreateProjectRootItem();

		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.Event));
		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.AuxBus));
		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.Switch));
		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.State));
		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.GameParameter));
		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.Soundbank));
		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.Trigger));
		ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(WwiseObjectType.AcousticTexture));

		TreeUtility.TreeToList(ProjectRoot, ref Data);
	}

	public override AkWwiseTreeViewItem GetComponentDataRoot(WwiseObjectType objectType)
	{
		if (!wwiseObjectFolders.ContainsKey(objectType))
			ProjectRoot.AddWwiseItemChild(BuildObjectTypeTree(objectType));

		var tempProjectRoot = new AkWwiseTreeViewItem(ProjectRoot);
		tempProjectRoot.AddWwiseItemChild(wwiseObjectFolders[objectType]);
		return tempProjectRoot;
	}

	virtual protected AkWwiseTreeViewItem BuildObjectTypeTree(WwiseObjectType objectType)
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
			childItem.path = children.Path;
			childItem = BuildTree(childItem, children.Children, depth++);
			if (childItem != null)
			{
				treeViewItem.AddWwiseItemChild(childItem);	
			}
		}

		return treeViewItem;
	}

	public AkWwiseTreeViewItem BuildTree(string name,
	List<AkWwiseProjectData.WwiseTreeObject> wwiseTreeObjects)
	{
		var rootFolder = new AkWwiseTreeViewItem(name, 1, GenerateUniqueID(), new System.Guid(), WwiseObjectType.PhysicalFolder);
		return BuildTree(rootFolder, wwiseTreeObjects, 1);
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
					newItem.path = pathElem.ToString();
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
			newItem.path = pathElem.ToString();
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
			var objectRoot = new AkWwiseTreeViewItem(wwiseObjectFolders[objectType]);
			TreeUtility.CopyTree(wwiseObjectFolders[objectType], objectRoot);
			SearchRoot.AddWwiseItemChild(objectRoot);
		}
		else
		{
			TreeUtility.CopyTree(ProjectRoot, SearchRoot);
		}
		FilterTree(SearchRoot, searchString);
	}

	void FilterTree(AkWwiseTreeViewItem treeElement, string searchFilter)
	{
		var ItemsToRemove = new List<AkWwiseTreeViewItem>();
		for (int i = 0; i < treeElement.children.Count(); i++)
		{
			var current = treeElement.children[i] as AkWwiseTreeViewItem;
			FilterTree(current, searchFilter);

			if (current.name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) == -1 && current.children.Count == 0)
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
}
#endif