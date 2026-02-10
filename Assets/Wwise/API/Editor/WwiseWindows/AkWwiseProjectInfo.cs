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
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using AK.Wwise.Unity.Logging;

public static class AkWwiseProjectInfo
{
	public static AkWwiseProjectData ProjectData;
	private static AkWwiseTreeWAAPIDataSource _waapiBrowserData = new AkWwiseTreeWAAPIDataSource();
	private static AkWwiseTreeProjectDataSource _projectPickerData = new AkWwiseTreeProjectDataSource();

	public static AkWwiseTreeWAAPIDataSource WaapiBrowserData
	{
		get
		{
			return _waapiBrowserData;
		}
	}

	public static AkWwiseTreeProjectDataSource ProjectPickerData
	{
		get
		{
			return _projectPickerData;
		}
	}
	
	public static AkWwiseTreeDataSource GetCurrentTreeData()
	{
		AkWwiseTreeMergedDataSource treeData = new AkWwiseTreeMergedDataSource();
		treeData.MergeDataSources();
		return treeData;
	}

	public static AkWwiseTreeDataSource GetTreeData()
	{
		GetData().Reset();
		AkWwiseTreeMergedDataSource treeData = new AkWwiseTreeMergedDataSource();
		treeData.FetchData();
		return treeData;
	}

	private static bool WwiseFolderExists()
	{
		return System.IO.Directory.Exists(System.IO.Path.Combine(UnityEngine.Application.dataPath, "Wwise"));
	}

	public static AkWwiseProjectData GetData()
	{
		if (ProjectData == null && WwiseFolderExists())
		{
			try
			{
				CreateWwiseProjectData();
			}
			catch (System.Exception e)
			{
				WwiseLogger.Error("Unable to load Wwise Data: " + e);
			}
		}

		return ProjectData;
	}

	private static void CreateWwiseProjectData()
	{
		ProjectData = new AkWwiseProjectData();
	}

	public static bool Populate()
	{
		var bDirty = false;
		if (AkUtilities.IsWwiseProjectAvailable)
		{
			AkWwiseProjectInfo.GetData().Reset();
			bDirty |= AkWwiseJSONBuilder.Populate();
		}

		if (AkWaapiUtilities.IsConnected())
		{
			_waapiBrowserData.FetchData();
		}

		return bDirty;
	}
}
#endif
