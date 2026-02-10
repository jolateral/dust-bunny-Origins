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
using System.Collections.ObjectModel;
using UnityEditor.IMGUI.Controls;
using AK.Wwise.Unity.Logging;

public enum BrowserFilter
{
	None,
	SoundBanksUpToDate = 1 << 0,
	NewInWwise = 1 << 1,
	DeletedInWwise = 1 << 2,
	RenamedInWwise = 1 << 3,
	MovedInWwise = 1 << 4,
	SoundBankNeedsUpdate = 1 << 5,
	Event = 1 << 6,
	Bus = 1 << 7,
	SoundBank = 1 << 8,
	Switch = 1 << 9,
	State = 1 << 10,
	Trigger = 1 << 11,
	AcousticTexture = 1 << 12,
	GameParameter = 1 << 13
}

public class WwiseTreeStringKey
{
	string name;
	WwiseObjectType objectType;

	public WwiseTreeStringKey(string name, WwiseObjectType objectType)
	{
		this.name = name;
		this.objectType = objectType;
	}

	public override int GetHashCode()
	{
		return name.GetHashCode() | objectType.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj is WwiseTreeStringKey)
		{
			WwiseTreeStringKey other = (WwiseTreeStringKey)obj;
			return other.objectType == objectType && other.name == name;
		}
		return false;
	}
}

public abstract class AkWwiseTreeDataSource
{ 
	public class TreeItems
	{
		public Dictionary<System.Guid, AkWwiseTreeViewItem> ItemDict;
		public Dictionary<WwiseTreeStringKey, AkWwiseTreeViewItem> ItemNameDict;


		public TreeItems()
		{
			ItemDict = new Dictionary<System.Guid, AkWwiseTreeViewItem>();
			ItemNameDict = new Dictionary<WwiseTreeStringKey, AkWwiseTreeViewItem>();
		}

		public void Clear()
		{
			ItemDict.Clear();
			ItemNameDict.Clear();
		}
		public void Add(AkWwiseTreeViewItem item)
		{
			try
			{
				ItemDict[item.objectGuid] = item;
				if (item.name != null)
				{
					ItemNameDict[new WwiseTreeStringKey(item.name, item.objectType)] = item;	
				}
			}
			catch (System.ArgumentException e)
			{
				WwiseLogger.Error(e.Message);
			}
		}
		
		public void Remove(AkWwiseTreeViewItem item)
		{
			try
			{
				ItemDict.Remove(item.objectGuid);
				ItemNameDict.Remove(new WwiseTreeStringKey(item.name, item.objectType));
			}
			catch (System.ArgumentException e)
			{
				WwiseLogger.Error(e.Message);
			}
		}
	}
		
	static public ReadOnlyDictionary<WwiseObjectType, string> FolderNames = new ReadOnlyDictionary<WwiseObjectType, string>(new Dictionary<WwiseObjectType, string>()
	{
		{ WwiseObjectType.AuxBus,  "Busses" },
		{ WwiseObjectType.Event,  "Events" },
		{ WwiseObjectType.State, "States"},
		{ WwiseObjectType.StateGroup, "States"},
		{ WwiseObjectType.Soundbank, "SoundBanks"},
		{ WwiseObjectType.Switch, "Switches"},
		{ WwiseObjectType.SwitchGroup, "Switches"},
		{ WwiseObjectType.AcousticTexture, "Virtual Acoustics" },
		{ WwiseObjectType.Trigger, "Triggers" },
		{ WwiseObjectType.GameParameter, "Game Parameters" },
	});

	public TreeItems Data;

	public AkWwiseTreeViewItem ProjectRoot { get; protected set; }
	public Dictionary<WwiseObjectType, AkWwiseTreeViewItem> wwiseObjectFolders;

	public AkWwiseTreeViewItem SearchRoot { get; protected set; }

	public TreeItems SearchData;

	protected AkWwiseTreeView TreeView { get; set; }

	public event System.Action modelChanged;

	public virtual AkWwiseTreeViewItem CreateProjectRootItem()
	{
		return new AkWwiseTreeViewItem(System.IO.Path.GetFileNameWithoutExtension(AkWwiseEditorSettings.Instance.WwiseProjectPath),
			-1, GenerateUniqueID(), System.Guid.Empty, WwiseObjectType.Project);
	}

	protected int m_MaxID;
	public int GenerateUniqueID()
	{
		return ++m_MaxID;
	}

	public AkWwiseTreeDataSource()
	{
		Data = new TreeItems();
		wwiseObjectFolders = new Dictionary<WwiseObjectType, AkWwiseTreeViewItem>();
		ProjectRoot = CreateProjectRootItem();
	}
	
	public TreeViewItem FindByIdRecursive(TreeViewItem item, int id)
	{
		if (item.id == id)
		{
			return item;
		}
		foreach (var element in item.children)
		{
			var found = FindByIdRecursive(element, id);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	public AkWwiseTreeViewItem FindById(int id)
	{
		if (m_MaxID < id)
		{
			return null;
		}

		foreach (var element in ProjectRoot.children)
		{
			var found = FindByIdRecursive(element, id);
			if (found != null)
			{
				return found as AkWwiseTreeViewItem;
			}
		}
		return Data.ItemDict.Values.FirstOrDefault(element => element.id == id);
	}
	
	public TreeViewItem FindByNameRecursive(TreeViewItem item, string name, WwiseObjectType objectType)
	{
		if ( (item as AkWwiseTreeViewItem).objectType == objectType && item.displayName == name)
		{
			return item;
		}
		foreach (var element in item.children)
		{
			var found = FindByNameRecursive(element, name, objectType);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}
	
	public AkWwiseTreeViewItem FindByName(string name, WwiseObjectType objectType)
	{
		var key = new WwiseTreeStringKey(name, objectType);
		if (Data.ItemNameDict.ContainsKey(key))
		{
			return Data.ItemNameDict[key];
		}
		return null;
	}

	public AkWwiseTreeViewItem FindByGuid(System.Guid guid)
	{
		return TreeUtility.FindByGuid(Data, guid);
	}

	public AkWwiseTreeViewItem FindInSearchResults(System.Guid guid)
	{
		return TreeUtility.FindByGuid(SearchData, guid);
	}

	public IEnumerable<System.Guid> GetGuidsFromIds(IEnumerable<int> ids)
	{
		if (Data.ItemDict.Count == 0) return new List<System.Guid>();
		return Data.ItemDict.Values.Where(el => ids.Contains(el.id)).Select(el => el.objectGuid);
	}

	public IEnumerable<int> GetIdsFromGuids(IEnumerable<System.Guid> guids)
	{
		if (Data.ItemDict.Count == 0) return new List<int>();
		return Data.ItemDict.Values.Where(el => guids.Contains(el.objectGuid)).Select(el => el.id);
	}

	public IList<int> GetAncestors(int id)
	{
		var parents = new List<int>();
		TreeViewItem el = FindById(id);
		if (el != null)
		{
			while (el.parent != null)
			{
				parents.Add(el.parent.id);
				el = el.parent;
			}
		}
		return parents;
	}

	public IList<int> GetDescendantsThatHaveChildren(int id)
	{
		AkWwiseTreeViewItem searchFromThis = FindById(id);
		if (searchFromThis != null)
		{
			return GetParentsBelowStackBased(searchFromThis);
		}
		return new List<int>();
	}

	public bool isRefreshing = false;

	IList<int> GetParentsBelowStackBased(AkWwiseTreeViewItem searchFromThis)
	{
		Stack<AkWwiseTreeViewItem> stack = new Stack<AkWwiseTreeViewItem>();
		stack.Push(searchFromThis);

		var parentsBelow = new List<int>();
		while (stack.Count > 0)
		{
			AkWwiseTreeViewItem current = stack.Pop();
			if (current.hasChildren)
			{
				parentsBelow.Add(current.id);
				foreach (AkWwiseTreeViewItem el in current.children)
				{
					stack.Push(el);
				}
			}
		}
		return parentsBelow;
	}

	abstract public AkWwiseTreeViewItem GetComponentDataRoot(WwiseObjectType objectType);

	protected void Changed()
	{
		modelChanged?.Invoke();
	}

	public bool IsExpanded(TreeViewState state, int id)
	{
		if (ProjectRoot != null && id == ProjectRoot.id)
		{
			return true;
		}
		return state.expandedIDs.BinarySearch(id) >= 0;
	}

	public abstract void FetchData();

	public static int GetDepthFromPath(string path)
	{
		return path.Split('\\').Length - 1;
	}

	public virtual void ScheduleRebuild()
	{
	}

	public abstract void SaveExpansionStatus(List<int> itemIds);
	public abstract List<int> LoadExpansionSatus();

	public string currentSearchString;
	public bool isSearching = false;
	public abstract AkWwiseTreeViewItem GetSearchResults();
	public abstract void UpdateSearchResults(string searchString, WwiseObjectType objectType, BrowserFilter Filters);
	public virtual void SelectItem(System.Guid itemGuid)
	{
		bool success = TreeView.ExpandItem(itemGuid, true);
		if (!success)
		{
			UnityEditor.EditorApplication.delayCall += () => { SelectItem(itemGuid); };
		}
		
	}
	
	public virtual void SetWwiseTreeView(AkWwiseTreeView treeView)
	{
		TreeView = treeView;
	}

	public virtual void LoadComponentData(WwiseObjectType objectType) { }
	public virtual void ItemSelected(AkWwiseTreeViewItem itemID) { }
	public virtual void SelectObjectInAuthoring(System.Guid objectGuid) { }
	public abstract void SetExpanded(IEnumerable<System.Guid> ids);
}

#region Utility Functions
public static class TreeUtility
{
	public static void CopyTree(AkWwiseTreeViewItem sourceRoot, AkWwiseTreeViewItem destRoot)
	{
		for (int i = 0; i < sourceRoot.children.Count(); i++)
		{
			var currItem = sourceRoot.children[i];
			var newItem = new AkWwiseTreeViewItem(currItem as AkWwiseTreeViewItem);
			CopyTree(currItem as AkWwiseTreeViewItem, newItem);
			destRoot.AddWwiseItemChild(newItem);
		}
	}

	public static void TreeToList(AkWwiseTreeViewItem root, ref AkWwiseTreeDataSource.TreeItems result)
	{
		if (root == null)
			return;

		if (result == null)
			return;

		result.Clear();

		Stack<AkWwiseTreeViewItem> stack = new Stack<AkWwiseTreeViewItem>();
		stack.Push(root);

		while (stack.Count > 0)
		{
			AkWwiseTreeViewItem current = stack.Pop();
			result.Add(current);

			if (current.children != null && current.children.Count > 0)
			{
				for (int i = current.children.Count - 1; i >= 0; i--)
				{
					if (current.children[i] != null)
					{
						stack.Push((AkWwiseTreeViewItem)current.children[i]);
					}
				}
			}
		}
	}

	public static void SortTreeIfNecessary(AkWwiseTreeViewItem rootElement)
	{
		if (rootElement.hasChildren)
		{
			if (!rootElement.isSorted)
			{
				rootElement.SortChildren();
			}
			foreach (AkWwiseTreeViewItem child in rootElement.children)
			{
				SortTreeIfNecessary(child);
			}
		}
	}

	public static AkWwiseTreeViewItem FindByGuid(IEnumerable<AkWwiseTreeViewItem> data, System.Guid guid)
	{
		return data.FirstOrDefault(element => element.objectGuid == guid);
	}


	public static AkWwiseTreeViewItem FindByGuid(AkWwiseTreeDataSource.TreeItems data, System.Guid guid)
	{
		if (!data.ItemDict.ContainsKey(guid))
		{
			return null;
		}
		return data.ItemDict[guid];
	}
}
#endregion
#endif