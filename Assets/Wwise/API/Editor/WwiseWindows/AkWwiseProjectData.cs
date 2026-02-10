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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class AkWwiseProjectData
{
	[FormerlySerializedAs("AcousticTextureWwu")] public System.Collections.Generic.List<WwiseTreeObject> AcousticTextureRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	[FormerlySerializedAs("AuxBusWwu")] public System.Collections.Generic.List<WwiseTreeObject> BusRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	[FormerlySerializedAs("BankWwu")] public System.Collections.Generic.List<WwiseTreeObject> BankRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	[FormerlySerializedAs("EventWwu")] public System.Collections.Generic.List<WwiseTreeObject> EventRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	[FormerlySerializedAs("RtpcWwu")] public System.Collections.Generic.List<WwiseTreeObject> GameParameterRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	[FormerlySerializedAs("StateWwu")] public System.Collections.Generic.List<WwiseTreeObject> StateRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	[FormerlySerializedAs("SwitchWwu")] public System.Collections.Generic.List<WwiseTreeObject> SwitchRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	[FormerlySerializedAs("TriggerWwu")] public System.Collections.Generic.List<WwiseTreeObject> TriggerRoot =
		new System.Collections.Generic.List<WwiseTreeObject>();

	////Contains the int id of all items that are expanded in the Wwise picker
	public System.Collections.Generic.List<int> ExpandedFileSystemItemIds = new System.Collections.Generic.List<int>();
	public System.Collections.Generic.List<int> ExpandedWaapiItemIds = new System.Collections.Generic.List<int>();

	public bool AutoSyncSelection;

	public string CurrentPluginConfig;

	public float GetEventMaxAttenuation(uint eventID)
	{
		var Event = GetEventInfo(eventID);
		return Event != null ? Event.maxAttenuation : 0.0f;
	}

	public Event GetEventInfo(uint eventID)
	{
		foreach (var root in EventRoot)
		{
			var found = root.Find(eventID);
			if (found != null)
			{
				return found as Event;
			}
		}

		return null;
	}

	public void Reset()
	{
		EventRoot = new System.Collections.Generic.List<WwiseTreeObject>();
		StateRoot = new System.Collections.Generic.List<WwiseTreeObject>();
		SwitchRoot = new System.Collections.Generic.List<WwiseTreeObject>();
		BankRoot = new System.Collections.Generic.List<WwiseTreeObject>();
		BusRoot = new System.Collections.Generic.List<WwiseTreeObject>();
		GameParameterRoot = new System.Collections.Generic.List<WwiseTreeObject>();
		TriggerRoot = new System.Collections.Generic.List<WwiseTreeObject>();
		AcousticTextureRoot = new System.Collections.Generic.List<WwiseTreeObject>();
	}

	[System.Serializable]
	public class ByteArrayWrapper
	{
		public byte[] bytes;

		public ByteArrayWrapper(byte[] byteArray)
		{
			
			bytes = byteArray;
		}

		public static implicit operator ByteArrayWrapper(System.Guid guid) => new ByteArrayWrapper(guid.ToByteArray());

		public static implicit operator System.Guid(ByteArrayWrapper bytes) =>  new System.Guid(bytes.bytes);
	}

	private static System.Guid GetGuid(byte[] bytes)
	{
		try
		{
			return new System.Guid(bytes);
		}
		catch
		{
			return System.Guid.Empty;
		}
	}

	[System.Serializable]
	public class AkBaseInformation : System.IComparable
	{
		[UnityEngine.SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("Name")]
		private string name;

		public string Name
		{
			get { return name; }

			set
			{
				name = value;
				id = AkUtilities.ShortIDGenerator.Compute(value);
			}
		}
		
		private System.Guid guid;

		public System.Guid Guid
		{
			get { return guid; }
			set { guid = value; }
		}

		[UnityEngine.SerializeField]
		[UnityEngine.Serialization.FormerlySerializedAs("ID")]
		private uint id;

		public uint Id
		{
			get { return id; }

			set { id = value; }
		}

		[UnityEngine.HideInInspector]
		public System.Collections.Generic.List<PathElement> PathAndIcons = new System.Collections.Generic.List<PathElement>();

		int System.IComparable.CompareTo(object other)
		{
			if (other == null)
				return 1;

			var otherAkInformation = other as AkBaseInformation;
			if (otherAkInformation == null)
				throw new System.ArgumentException("Object is not of type AkBaseInformation");

			return Name.CompareTo(otherAkInformation.Name);
		}

		private class _CompareByGuid : System.Collections.Generic.IComparer<AkBaseInformation>
		{
			int System.Collections.Generic.IComparer<AkBaseInformation>.Compare(AkBaseInformation a, AkBaseInformation b)
			{
				if (a == null)
					return b == null ? 0 : -1;

				return a.Guid.CompareTo(b.Guid);
			}
		}

		public static System.Collections.Generic.IComparer<AkBaseInformation> CompareByGuid = new _CompareByGuid();
	}

	[System.Serializable]
	public class WwiseTreeObject : AkBaseInformation
	{
		public string Path;
		public List<WwiseTreeObject> Children;
		public WwiseObjectType Type = WwiseObjectType.Folder;

		public WwiseTreeObject Find(uint shortId)
		{
			if (Id == shortId)
			{
				return this;
			}

			if (Children == null)
			{
				return null;
			}
			foreach(var Child in Children)
			{
				var found = Child.Find(shortId);
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}
	}

	[System.Serializable]
	public class Event : WwiseTreeObject
	{
		public float maxAttenuation;
		public float maxDuration = -1;
		public float minDuration = -1;
	}

	[System.Serializable]
	public class PathElement
	{
		public string ElementName;
		public WwiseObjectType ObjectType;
		public string Path;

		[UnityEngine.SerializeField]
		[UnityEngine.HideInInspector]
		private byte[] guid = null;

		public System.Guid ObjectGuid
		{
			get { return GetGuid(guid); }
			set { guid = value.ToByteArray(); }
		}

		public PathElement(string Name, WwiseObjectType objType, System.Guid guid)
		{
			ElementName = Name;
			ObjectType = objType;
			ObjectGuid = guid;
		}

		public static string GetProjectPathString(System.Collections.Generic.List<PathElement> pathElements, int index)
		{
			string path = "";
			for (int i = 0; i<=index && i < pathElements.Count; i++)
			{
				path += $"/{pathElements[i].ElementName}";

			}
			return path;
		}
	}
}
