using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AK.Wwise.Unity.Logging;

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

[UnityEditor.InitializeOnLoad]
public class AkWwiseJSONBuilder : UnityEditor.AssetPostprocessor
{
	private static bool isSubscribedToInvokePopulate = false;
	private static readonly System.DateTime s_LastParsed = System.DateTime.MinValue;

	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
	{
		if (UnityEditor.AssetDatabase.IsAssetImportWorkerProcess())
		{
			return;
		}

		if (!isSubscribedToInvokePopulate)
		{
			WwiseProjectDatabase.SoundBankDirectoryUpdated += InvokePopulate;
			isSubscribedToInvokePopulate = true;
		}
		if (didDomainReload)
		{
			UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
		}
	}

	private static void PlayModeChanged(UnityEditor.PlayModeStateChange mode)
	{
		if (mode == UnityEditor.PlayModeStateChange.EnteredEditMode)
		{
			AkWwiseProjectInfo.Populate();
		}
	}

	public static void InvokePopulate()
	{
		Populate();
		WwiseProjectDatabase.SoundBankDirectoryUpdated -= InvokePopulate;
		isSubscribedToInvokePopulate = false;
	}

	public static bool Populate()
	{

		if (UnityEditor.EditorApplication.isCompiling)
		{
			return false;
		}

		try
		{
			var bChanged = false;
			WwiseSoundBankRefArray soundBankRefArray = new WwiseSoundBankRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetSoundBankCount(); i++)
			{
				var soundBankRef = soundBankRefArray[i];
				bChanged = SerialiseSoundBank(soundBankRef) || bChanged;
			}
			
			WwiseEventRefArray eventRefArray = new WwiseEventRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetEventCount(); i++)
			{
				var eventRef = eventRefArray[i];
				bChanged = SerialiseEventData(eventRef) || bChanged;
			}
			
			WwiseSwitchGroupRefArray switchGroupRefArray = new WwiseSwitchGroupRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetSwitchGroupCount(); i++)
			{
				var switchGroupRef = switchGroupRefArray[i];
				bChanged = SerialiseSwitchGroupData(switchGroupRef) || bChanged;
			}
			
			WwiseSwitchRefArray switchRefArray = new WwiseSwitchRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetSwitchCount(); i++)
			{
				var switchRef = switchRefArray[i];
				bChanged = SerialiseSwitchData(switchRef) || bChanged;
			}
			
			WwiseStateGroupRefArray stateGroupRefArray = new WwiseStateGroupRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetStateGroupCount(); i++)
			{
				var stateGroupRef = stateGroupRefArray[i];
				bChanged = SerialiseStateGroupData(stateGroupRef) || bChanged;
			}
			
			WwiseStateRefArray stateRefArray = new WwiseStateRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetStateCount(); i++)
			{
				var stateRef = stateRefArray[i];
				bChanged = SerialiseStateData(stateRef) || bChanged;
			}
			
			WwiseAcousticTextureRefArray acousticTextureRefArray = new WwiseAcousticTextureRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetAcousticTextureCount(); i++)
			{
				var acousticTextureRef = acousticTextureRefArray[i];
				bChanged = SerialiseAcousticTextureData(acousticTextureRef) || bChanged;
			}
			
			WwiseGameParameterRefArray gameParameterRefArray = new WwiseGameParameterRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetGameParameterCount(); i++)
			{
				var gameParameterRef = gameParameterRefArray[i];
				bChanged = SerialiseGameParameterData(gameParameterRef) || bChanged;
			}
			
			WwiseTriggerRefArray triggerRefArray = new WwiseTriggerRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetTriggerCount(); i++)
			{
				var triggerRef = triggerRefArray[i];
				bChanged = SerialiseTriggerData(triggerRef) || bChanged;
			}
			
			WwiseBusRefArray busRefArray = new WwiseBusRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetBusCount(); i++)
			{
				var busRef = busRefArray[i];
				bChanged = SerialiseBusData(busRef) || bChanged;
			}
			
			WwiseAuxBusRefArray auxBusRefArray = new WwiseAuxBusRefArray();
			for (var i = 0; i < WwiseProjectDatabase.GetAuxBusCount(); i++)
			{
				var auxBusRef = auxBusRefArray[i];
				bChanged = SerialiseAuxBusData(auxBusRef) || bChanged;
			}

			return bChanged;
		}
		catch (System.Exception e)
		{
			WwiseLogger.Log("Exception occured while parsing SoundbanksInfo.json: " + e.ToString());
			return false;
		}
	}

	private static bool SerialiseSoundBank(WwiseSoundBankRef soundBankRef)
	{
		var bChanged = false;

		if (!soundBankRef.IsUserBank || soundBankRef.IsInitBank)
		{
			var eventCount = soundBankRef.EventsCount;
			for (var i = 0; i < eventCount; i++)
			{
				var events = soundBankRef.Events[i];
				bChanged = SerialiseEventData(events) || bChanged;
			}
			return bChanged;
		}
		AkWwiseProjectData.WwiseTreeObject wwiseSoundBank = new AkWwiseProjectData.WwiseTreeObject();
		wwiseSoundBank.Name = soundBankRef.Name;
		wwiseSoundBank.Path = soundBankRef.ObjectPath;
		wwiseSoundBank.Guid = soundBankRef.Guid;
		wwiseSoundBank.Type = WwiseObjectType.Soundbank;
		var WorkUnit = BuildFolderHierarchy(soundBankRef.ObjectPath, AkWwiseProjectInfo.GetData().BankRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseSoundBank.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseSoundBank);
		}
		return bChanged;
	}

	private static List<AkWwiseProjectData.WwiseTreeObject> BuildFolderHierarchyRecursive(string[] Folders, List<AkWwiseProjectData.WwiseTreeObject> WorkUnits)
	{
		if (Folders.Length == 0)
		{
			return WorkUnits;
		}
		string FolderName = Folders[0];
		if (FolderName.Length == 0)
		{
			return WorkUnits;
		}

		for (int i = 0; i < WorkUnits.Count; i++)
		{
			if (WorkUnits[i].Name == FolderName)
			{
				Folders = Folders.Skip(1).ToArray();
				return BuildFolderHierarchyRecursive(Folders, WorkUnits[i].Children);
			}
		}

		var CurrentWorkUnits = WorkUnits;
		for (int i = 0; i < Folders.Length; i++)
		{
			AkWwiseProjectData.WwiseTreeObject newWorkUnit = new AkWwiseProjectData.WwiseTreeObject();
			newWorkUnit.Name = Folders[i];
			newWorkUnit.Children = new List<AkWwiseProjectData.WwiseTreeObject>();
			CurrentWorkUnits.Add(newWorkUnit);
			CurrentWorkUnits = CurrentWorkUnits[CurrentWorkUnits.Count - 1].Children;
		}
		return CurrentWorkUnits;
	}
	private static List<AkWwiseProjectData.WwiseTreeObject> BuildFolderHierarchy(string Path, List<AkWwiseProjectData.WwiseTreeObject> WorkUnits)
	{
		var Folders = Path.Split("\\");
		if (Folders.Length < 2)
		{
			return WorkUnits;
		}
		Folders = Folders.Skip(2).ToArray();
		Folders = Folders.Take(Folders.Length - 1).ToArray();
		return BuildFolderHierarchyRecursive(Folders, WorkUnits);
	}

	private static float GetFloatFromString(string s)
	{
		if (string.Compare(s, "Infinite") == 0)
		{
			return UnityEngine.Mathf.Infinity;
		}
		else
		{
			System.Globalization.CultureInfo CultInfo = System.Globalization.CultureInfo.CurrentCulture.Clone() as System.Globalization.CultureInfo;
			CultInfo.NumberFormat.NumberDecimalSeparator = ".";
			CultInfo.NumberFormat.CurrencyDecimalSeparator = ".";
			float Result;
			if(float.TryParse(s, System.Globalization.NumberStyles.Float, CultInfo, out Result))
			{
				return Result;
			}
			else
			{
				WwiseLogger.Log("Could not parse float number " + s);
				return 0.0f;
			}
		}
	}

	private static bool SerialiseEventData(WwiseEventRef eventRef)
	{
		float maxAttenuation = eventRef.MaxAttenuation;
		var minDuration = eventRef.MinDuration;
		var maxDuration = eventRef.MaxDuration;
		var name = eventRef.Name;
		
		var bChanged = false;

		AkWwiseProjectData.Event wwiseEvent = new AkWwiseProjectData.Event();
		wwiseEvent.maxAttenuation = maxAttenuation;
		wwiseEvent.minDuration = minDuration;
		wwiseEvent.maxDuration = maxDuration;
		wwiseEvent.Name = name;
		wwiseEvent.Guid = eventRef.Guid;
		wwiseEvent.Path = eventRef.Path;
		wwiseEvent.Type = WwiseObjectType.Event;
		var WorkUnit = BuildFolderHierarchy(eventRef.Path, AkWwiseProjectInfo.GetData().EventRoot);
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseEvent.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseEvent);
		}

		return bChanged;
	}
	
	private static bool SerialiseSwitchGroupData(WwiseSwitchGroupRef switchRef)
	{
		var name = switchRef.Name;
		var bChanged = false;
		AkWwiseProjectData.WwiseTreeObject wwiseSwitchGroup = new AkWwiseProjectData.WwiseTreeObject();
		wwiseSwitchGroup.Name = name;
		wwiseSwitchGroup.Path = switchRef.Path;
		wwiseSwitchGroup.Guid = switchRef.Guid;
		wwiseSwitchGroup.Type = WwiseObjectType.SwitchGroup;
		wwiseSwitchGroup.Children = new List<AkWwiseProjectData.WwiseTreeObject>();
		var workUnit = BuildFolderHierarchy(switchRef.Path, AkWwiseProjectInfo.GetData().SwitchRoot);
		bool bIsDuplicate = false;
		foreach (var Info in workUnit)
		{
			if (Info.Guid == wwiseSwitchGroup.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			workUnit.Add(wwiseSwitchGroup);
		}
		return bChanged;
	}

	private static bool SerialiseSwitchData(WwiseSwitchRef switchRef)
	{
		var name = switchRef.Name;
		
		var bChanged = false;
		
		AkWwiseProjectData.WwiseTreeObject wwiseSwitch = new AkWwiseProjectData.WwiseTreeObject();
		wwiseSwitch.Name = name;
		wwiseSwitch.Path = switchRef.Path;
		wwiseSwitch.Guid = switchRef.Guid;
		wwiseSwitch.Type = WwiseObjectType.Switch;
		var WorkUnit = BuildFolderHierarchy(switchRef.Path, AkWwiseProjectInfo.GetData().SwitchRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseSwitch.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseSwitch);
		}
		return bChanged;
	}
	
	private static bool SerialiseStateGroupData(WwiseStateGroupRef switchRef)
	{
		var name = switchRef.Name;
		var bChanged = false;
		AkWwiseProjectData.WwiseTreeObject wwiseStateGroup = new AkWwiseProjectData.WwiseTreeObject();
		wwiseStateGroup.Name = name;
		wwiseStateGroup.Guid = switchRef.Guid;
		wwiseStateGroup.Path = switchRef.Path;
		wwiseStateGroup.Type = WwiseObjectType.StateGroup;
		wwiseStateGroup.Children = new List<AkWwiseProjectData.WwiseTreeObject>();
		var WorkUnit = BuildFolderHierarchy(switchRef.Path, AkWwiseProjectInfo.GetData().StateRoot);
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseStateGroup.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseStateGroup);
		}
		return bChanged;
	}
	
	private static bool SerialiseStateData(WwiseStateRef stateRef)
	{
		var name = stateRef.Name;
		
		var bChanged = false;
		
		AkWwiseProjectData.WwiseTreeObject wwiseState = new AkWwiseProjectData.WwiseTreeObject();
		wwiseState.Name = name;
		wwiseState.Path = stateRef.Path;
		wwiseState.Guid = stateRef.Guid;
		wwiseState.Type = WwiseObjectType.State;
		var WorkUnit = BuildFolderHierarchy(stateRef.Path, AkWwiseProjectInfo.GetData().StateRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseState.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseState);
		}
		return bChanged;
	}
	
	private static bool SerialiseAcousticTextureData(WwiseAcousticTextureRef acousticTextureRef)
	{
		var name = acousticTextureRef.Name;
		
		var bChanged = false;
		
		AkWwiseProjectData.WwiseTreeObject wwiseAcousticTexture = new AkWwiseProjectData.WwiseTreeObject();
		wwiseAcousticTexture.Name = name;
		wwiseAcousticTexture.Path = acousticTextureRef.Path;
		wwiseAcousticTexture.Guid = acousticTextureRef.Guid;
		wwiseAcousticTexture.Type = WwiseObjectType.AcousticTexture;
		var WorkUnit = BuildFolderHierarchy(acousticTextureRef.Path, AkWwiseProjectInfo.GetData().AcousticTextureRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseAcousticTexture.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseAcousticTexture);
		}
		return bChanged;
	}

	private static bool SerialiseGameParameterData(WwiseGameParameterRef GameParameterRef)
	{
		var name = GameParameterRef.Name;
		
		var bChanged = false;
		
		AkWwiseProjectData.WwiseTreeObject wwiseGameParameter = new AkWwiseProjectData.WwiseTreeObject();
		wwiseGameParameter.Name = name;
		wwiseGameParameter.Path = GameParameterRef.Path;
		wwiseGameParameter.Guid = GameParameterRef.Guid;
		wwiseGameParameter.Type = WwiseObjectType.GameParameter;
		var WorkUnit = BuildFolderHierarchy(GameParameterRef.Path, AkWwiseProjectInfo.GetData().GameParameterRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseGameParameter.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseGameParameter);
		}
		return bChanged;
	}
	
	private static bool SerialiseTriggerData(WwiseTriggerRef TriggerRef)
	{
		var name = TriggerRef.Name;
		
		var bChanged = false;
		
		AkWwiseProjectData.WwiseTreeObject wwiseTrigger = new AkWwiseProjectData.WwiseTreeObject();
		wwiseTrigger.Name = name;
		wwiseTrigger.Path = TriggerRef.Path;
		wwiseTrigger.Guid = TriggerRef.Guid;
		wwiseTrigger.Type = WwiseObjectType.Trigger;
		var WorkUnit = BuildFolderHierarchy(TriggerRef.Path, AkWwiseProjectInfo.GetData().TriggerRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseTrigger.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseTrigger);
		}
		return bChanged;
	}
	
	private static bool SerialiseBusData(WwiseBusRef BusRef)
	{
		var name = BusRef.Name;
		
		var bChanged = false;
		
		AkWwiseProjectData.WwiseTreeObject wwiseBus = new AkWwiseProjectData.WwiseTreeObject();
		wwiseBus.Name = name;
		wwiseBus.Path = BusRef.Path;
		wwiseBus.Guid = BusRef.Guid;
		wwiseBus.Type = WwiseObjectType.Bus;
		wwiseBus.Children = new List<AkWwiseProjectData.WwiseTreeObject>();
		var WorkUnit = BuildFolderHierarchy(BusRef.Path, AkWwiseProjectInfo.GetData().BusRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseBus.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseBus);
		}
		return bChanged;
	}
	
	private static bool SerialiseAuxBusData(WwiseAuxBusRef AuxBusRef)
	{
		var name = AuxBusRef.Name;
		
		var bChanged = false;
		
		AkWwiseProjectData.WwiseTreeObject wwiseAuxBus = new AkWwiseProjectData.WwiseTreeObject();
		wwiseAuxBus.Name = name;
		wwiseAuxBus.Path = AuxBusRef.Path;
		wwiseAuxBus.Guid = AuxBusRef.Guid;
		wwiseAuxBus.Type = WwiseObjectType.AuxBus;
		wwiseAuxBus.Children = new List<AkWwiseProjectData.WwiseTreeObject>();
		var WorkUnit = BuildFolderHierarchy(AuxBusRef.Path, AkWwiseProjectInfo.GetData().BusRoot);
		if (WorkUnit == null)
		{
			return false;
		}
		bool bIsDuplicate = false;
		foreach (var Info in WorkUnit)
		{
			if (Info.Guid == wwiseAuxBus.Guid)
			{
				bIsDuplicate = true;
				break;
			}
		}
		if(!bIsDuplicate)
		{
			WorkUnit.Add(wwiseAuxBus);
		}
		return bChanged;
	}
}
#endif