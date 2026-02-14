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

using System.IO;
using System.Threading.Tasks;
using AK.Wwise.Unity.Logging;

public partial class WwiseProjectDatabase
{
    public static bool IsInitialized = false;

    public static bool ProjectInfoExists = false;

    public static void LogProjectDatabaseNotFound(string errorMessage)
    {
        int index = errorMessage.IndexOf(' '); // Find the index of the first space
        string libraryName = index != -1 ? errorMessage.Substring(0, index) : errorMessage;
        string directory = "Assets/Wwise/ProjectDatabase/Runtime/Plugins";
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        
        bool x86Architecture = System.IntPtr.Size == 4;
        directory += "/Windows/" + (x86Architecture ? "x86" : "x86_64");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        directory += "/Mac/DSP";
#endif
        
        WwiseLogger.Error($"{libraryName} could not be found. Please check the parent folder {directory}. If the {Path.GetExtension(libraryName)} is missing, try 1. Modifying the Wwise Project or 2. Copying the {libraryName} directly from the SDK\\platform_architecture\\Profile\\bin folder of your Wwise installation into {directory}.");
    }

    public static void LogProjectDatabaseDLLException(System.Exception e)
    {
        if (e is System.DllNotFoundException)
        {
            LogProjectDatabaseNotFound(e.Message);
        }
        else
        {
            WwiseLogger.Error($"The project database dll encountered the following error: {e.Message}" );
        }
    }
    public static void InitCheckUp(string inDirectoryPath)
    {
        var jsonFilename = Path.Combine(inDirectoryPath, "ProjectInfo.json");
        ProjectInfoExists = File.Exists(jsonFilename);
    }
    
    public static void PostInitCallback()
    {
        SoundBankDirectoryUpdated?.InvokeUnitySafe();
    }
    public static event System.Action SoundBankDirectoryUpdated;
    public static async Task<bool> InitAsync(string inDirectoryPath, string inDirectoryPlatformName)
    {
        IsInitialized = false;
        InitCheckUp(inDirectoryPath);
        if (!ProjectInfoExists)
            return false;
        
        try
        {
            await Task.Run(() => WwiseProjectDataBasePINVOKE.Init(inDirectoryPath, inDirectoryPlatformName));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }

        IsInitialized = true;
        return ProjectInfoExists;
    }
    
    public static void Init(string inDirectoryPath, string inDirectoryPlatformName, string language = null)
    {
        IsInitialized = false;
        InitCheckUp(inDirectoryPath);
        if (!ProjectInfoExists)
            return;
        
        try
        {
            WwiseProjectDataBasePINVOKE.Init(inDirectoryPath, inDirectoryPlatformName);

            if (!string.IsNullOrEmpty(language))
            {
                WwiseProjectDataBasePINVOKE.SetCurrentLanguage(language);
            }
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
        IsInitialized = true;
    }
    
    public static void SetCurrentPlatform(string inDirectoryPlatformName)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.SetCurrentPlatform(inDirectoryPlatformName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void SetCurrentLanguage(string inLanguageName)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.SetCurrentLanguage(inLanguageName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static string StringFromIntPtrString(System.IntPtr ptr)
    {
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr);
    }
    
    public static int GetGuidInt(global::System.IntPtr Guid, ref uint A, ref uint B, ref uint C, ref uint D)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetGuidInt(Guid, ref A, ref B, ref C, ref D);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * SoundBanks
     */

    public static global::System.IntPtr GetSoundBankRefString(string soundBankName, string soundBankType)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSoundBankRefString(soundBankName, soundBankType);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static global::System.IntPtr GetAllSoundBanksRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllSoundBanksRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetSoundBankCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSoundBankCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static global::System.IntPtr GetSoundBankRefIndex(global::System.IntPtr soundBankArrayRef, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSoundBankRefIndex(soundBankArrayRef, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteSoundBanksArrayRef(global::System.IntPtr soundBankArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteSoundBanksArrayRef(soundBankArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetSoundBankName(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSoundBankName(soundBankRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetSoundBankPath(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSoundBankPath(soundBankRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetSoundBankObjectPath(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSoundBankObjectPath(soundBankRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetSoundBankLanguage(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSoundBankLanguage(soundBankRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static uint GetSoundBankLanguageId(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSoundBankLanguageId(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetSoundBankGuid(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetSoundBankGuid(soundBankRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static uint GetSoundBankShortId(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
           return WwiseProjectDataBasePINVOKE.GetSoundBankShortId(soundBankRefPtr);            
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static bool IsUserBank(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
           return WwiseProjectDataBasePINVOKE.IsUserBank(soundBankRefPtr); 
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static bool IsInitBank(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
           return WwiseProjectDataBasePINVOKE.IsInitBank(soundBankRefPtr); 
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static bool IsSoundBankValid(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.IsSoundBankValid(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static global::System.IntPtr GetSoundBankMedia(global::System.IntPtr soundBankRefPtr, int index)
    {
        try
        {
                  return WwiseProjectDataBasePINVOKE.GetSoundBankMedia(soundBankRefPtr, index);  
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetSoundBankMediasCount(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSoundBankMediasCount(soundBankRefPtr); 
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static global::System.IntPtr GetSoundBankEvent(global::System.IntPtr soundBankRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSoundBankEvent(soundBankRefPtr, index); 
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetSoundBankEventsCount(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSoundBankEventsCount(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteSoundBankRef(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteSoundBankRef(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * Medias
    */
    
    public static string GetMediaName(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetMediaName(mediaRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetMediaPath(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetMediaPath(mediaRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static uint GetMediaShortId(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetMediaShortId(mediaRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetMediaLanguage(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetMediaLanguage(mediaRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static bool GetMediaIsStreaming(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetMediaIsStreaming(mediaRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static uint GetMediaLocation(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetMediaLocation(mediaRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetMediaCachePath(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetMediaCachePath(mediaRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static void DeleteMediaRef(global::System.IntPtr mediaRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteMediaRef(mediaRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * Events
    */
    
    public static global::System.IntPtr GetAllEventsRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllEventsRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static global::System.IntPtr GetEventRefString(string soundBankName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventRefString(soundBankName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetEventName(global::System.IntPtr eventRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetEventName(eventRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetEventPath(global::System.IntPtr eventRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetEventPath(eventRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    static string AdjustGuid(string NumString)
    {
        if (NumString.Length < 8)
        {
            string Add = "";
            for (int i = 0; i < 8 - NumString.Length; i++)
            {
                Add += "0";
            }

            NumString = NumString.Insert(0, Add);
        }

        return NumString;
    }
    public static System.Guid GenerateGuidFromInts(uint A, uint B, uint C, uint D)
    {
        string AString = AdjustGuid(A.ToString("X"));
        string BString = AdjustGuid(B.ToString("X"));
        string CString = AdjustGuid(C.ToString("X"));
        string DString = AdjustGuid(D.ToString("X"));
        string outString = AString;
        outString += "-";
        outString += BString.Substring(0, 4);
        outString += "-";
        outString += BString.Substring(4);
        outString += "-";
        outString += CString.Substring(0, 4);
        outString += "-";
        outString += CString.Substring(4);
        outString += DString;
        return System.Guid.Parse(outString);
    }
    public static System.Guid GetEventGuid(global::System.IntPtr eventRefPtr)
    {
        try
        { 
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            GetGuidInt(WwiseProjectDataBasePINVOKE.GetEventGuid(eventRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }

    }
    public static uint GetEventShortId(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventShortId(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static float GetEventMaxAttenuation(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventMaxAttenuation(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static float GetEventMinDuration(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventMinDuration(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static float GetEventMaxDuration(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventMaxDuration(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static global::System.IntPtr GetEventMedia(global::System.IntPtr soundBankRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventMedia(soundBankRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }    
    public static uint GetEventMediasCount(global::System.IntPtr soundBankRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventMediasCount(soundBankRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static void DeleteEventRef(global::System.IntPtr eventRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteEventRef(eventRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteEventArrayRef(global::System.IntPtr soundBankArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteEventArrayRef(soundBankArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetEventCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEventCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetEvent(global::System.IntPtr eventArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetEvent(eventArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * Platform
    */
    
    public static global::System.IntPtr GetPlatformRef(string soundBankName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetPlatformRef(soundBankName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetPlatformName(global::System.IntPtr platformRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetPlatformName(platformRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetPlatformGuid(global::System.IntPtr platformRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            GetGuidInt(WwiseProjectDataBasePINVOKE.GetPlatformGuid(platformRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);            
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }

    }    
    public static void DeletePlatformRef(global::System.IntPtr platformRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeletePlatformRef(platformRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /**
     * Plugin
     */
    
    public static global::System.IntPtr GetAllPluginRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllPluginRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetPluginCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetPluginCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static global::System.IntPtr GetPluginRefIndex(global::System.IntPtr soundBankArrayRef, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetPluginRefIndex(soundBankArrayRef, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static uint GetPluginId(global::System.IntPtr pluginRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetPluginId(pluginRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static string GetPluginName(global::System.IntPtr pluginRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetPluginName(pluginRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetPluginDLL(global::System.IntPtr pluginRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetPluginDLL(pluginRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetPluginStaticLib(global::System.IntPtr pluginRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetPluginStaticLib(pluginRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static void DeletePluginRef(global::System.IntPtr pluginRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeletePluginRef(pluginRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeletePluginArrayRef(global::System.IntPtr pluginArrayRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeletePluginArrayRef(pluginArrayRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * Switch Group
     */
    
    public static global::System.IntPtr GetSwitchGroupRefString(string switchName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitchGroupRefString(switchName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllSwitchGroupRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllSwitchGroupRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetSwitchGroup(global::System.IntPtr switchArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitchGroup(switchArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetSwitchGroupName(global::System.IntPtr switchRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSwitchGroupName(switchRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetSwitchGroupPath(global::System.IntPtr switchRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSwitchGroupPath(switchRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetSwitchGroupGuid(global::System.IntPtr switchRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetSwitchGuid(switchRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetSwitchGroupShortId(global::System.IntPtr switchRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitchGroupShortId(switchRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteSwitchGroupRef(global::System.IntPtr switchRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteSwitchGroupRef(switchRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteSwitchGroupArrayRef(global::System.IntPtr switchArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteSwitchGroupArrayRef(switchArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetSwitchGroupCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitchGroupCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * Switch
     */
    
    public static global::System.IntPtr GetSwitchRefString(string switchName)
    {
        try
        {
           return WwiseProjectDataBasePINVOKE.GetSwitchRefString(switchName); 
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllSwitchRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllSwitchRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetSwitch(global::System.IntPtr switchArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitch(switchArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetSwitchName(global::System.IntPtr switchRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSwitchName(switchRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetSwitchPath(global::System.IntPtr switchRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetSwitchPath(switchRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetSwitchGuid(global::System.IntPtr switchRefPtr)
    {
        try
        { 
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetSwitchGuid(switchRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }

    }

    public static uint GetSwitchShortId(global::System.IntPtr switchRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitchShortId(switchRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetSwitchGroupId(global::System.IntPtr switchRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitchGroupId(switchRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteSwitchRef(global::System.IntPtr switchRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteSwitchRef(switchRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteSwitchArrayRef(global::System.IntPtr switchArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteSwitchArrayRef(switchArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetSwitchCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetSwitchCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * State Group
     */
    
    public static global::System.IntPtr GetStateGroupRefString(string StateName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateGroupRefString(StateName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllStateGroupRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllStateGroupRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetStateGroup(global::System.IntPtr StateArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateGroup(StateArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetStateGroupName(global::System.IntPtr StateRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetStateGroupName(StateRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetStateGroupPath(global::System.IntPtr StateRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetStateGroupPath(StateRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetStateGroupGuid(global::System.IntPtr StateRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetStateGroupGuid(StateRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }

    }

    public static uint GetStateGroupShortId(global::System.IntPtr StateRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateGroupShortId(StateRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteStateGroupRef(global::System.IntPtr StateRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteStateGroupRef(StateRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteStateGroupArrayRef(global::System.IntPtr StateArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteStateGroupArrayRef(StateArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetStateGroupCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateGroupCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * State
     */
    
    public static global::System.IntPtr GetStateRefString(string StateName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateRefString(StateName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllStateRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllStateRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetState(global::System.IntPtr StateArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetState(StateArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetStateName(global::System.IntPtr StateRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetStateName(StateRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetStatePath(global::System.IntPtr StateRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetStatePath(StateRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetStateGuid(global::System.IntPtr StateRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetStateGuid(StateRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetStateShortId(global::System.IntPtr StateRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateShortId(StateRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetStateGroupId(global::System.IntPtr StateRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateGroupId(StateRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteStateRef(global::System.IntPtr StateRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteStateRef(StateRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteStateArrayRef(global::System.IntPtr StateArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteStateArrayRef(StateArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetStateCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetStateCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * AcousticTexture
     */
    
    public static global::System.IntPtr GetAcousticTextureRefString(string AcousticTextureName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAcousticTextureRefString(AcousticTextureName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllAcousticTextureRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllAcousticTextureRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAcousticTexture(global::System.IntPtr AcousticTextureArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAcousticTexture(AcousticTextureArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetAcousticTextureName(global::System.IntPtr AcousticTextureRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetAcousticTextureName(AcousticTextureRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetAcousticTexturePath(global::System.IntPtr AcousticTextureRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetAcousticTexturePath(AcousticTextureRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetAcousticTextureGuid(global::System.IntPtr AcousticTextureRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetAcousticTextureGuid(AcousticTextureRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);    
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetAcousticTextureShortId(global::System.IntPtr AcousticTextureRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAcousticTextureShortId(AcousticTextureRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteAcousticTextureRef(global::System.IntPtr AcousticTextureRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteAcousticTextureRef(AcousticTextureRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteAcousticTextureArrayRef(global::System.IntPtr AcousticTextureArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteAcousticTextureArrayRef(AcousticTextureArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetAcousticTextureCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAcousticTextureCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * GameParameter
     */
    
    public static global::System.IntPtr GetGameParameterRefString(string GameParameterName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetGameParameterRefString(GameParameterName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllGameParameterRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllGameParameterRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetGameParameter(global::System.IntPtr GameParameterArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetGameParameter(GameParameterArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetGameParameterName(global::System.IntPtr GameParameterRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetGameParameterName(GameParameterRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetGameParameterPath(global::System.IntPtr GameParameterRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetGameParameterPath(GameParameterRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetGameParameterGuid(global::System.IntPtr GameParameterRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetGameParameterGuid(GameParameterRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);    
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetGameParameterShortId(global::System.IntPtr GameParameterRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetGameParameterShortId(GameParameterRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteGameParameterRef(global::System.IntPtr GameParameterRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteGameParameterRef(GameParameterRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteGameParameterArrayRef(global::System.IntPtr GameParameterArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteGameParameterArrayRef(GameParameterArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetGameParameterCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetGameParameterCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * Trigger
     */
    
    public static global::System.IntPtr GetTriggerRefString(string TriggerName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetTriggerRefString(TriggerName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllTriggerRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllTriggerRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetTrigger(global::System.IntPtr TriggerArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetTrigger(TriggerArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetTriggerName(global::System.IntPtr TriggerRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetTriggerName(TriggerRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetTriggerPath(global::System.IntPtr TriggerRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetTriggerPath(TriggerRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetTriggerGuid(global::System.IntPtr TriggerRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetTriggerGuid(TriggerRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);        
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetTriggerShortId(global::System.IntPtr TriggerRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetTriggerShortId(TriggerRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteTriggerRef(global::System.IntPtr TriggerRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteTriggerRef(TriggerRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteTriggerArrayRef(global::System.IntPtr TriggerArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteTriggerArrayRef(TriggerArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetTriggerCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetTriggerCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * Bus
     */
    
    public static global::System.IntPtr GetBusRefString(string BusName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetBusRefString(BusName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllBusRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllBusRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetBus(global::System.IntPtr BusArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetBus(BusArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetBusName(global::System.IntPtr BusRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetBusName(BusRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetBusPath(global::System.IntPtr BusRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetBusPath(BusRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetBusGuid(global::System.IntPtr BusRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetBusGuid(BusRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);        
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetBusShortId(global::System.IntPtr BusRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetBusShortId(BusRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteBusRef(global::System.IntPtr BusRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteBusRef(BusRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteBusArrayRef(global::System.IntPtr BusArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteBusArrayRef(BusArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetBusCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetBusCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    /*
     * AuxBus
     */
    
    public static global::System.IntPtr GetAuxBusRefString(string AuxBusName)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAuxBusRefString(AuxBusName);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAllAuxBusRef()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAllAuxBusRef();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static global::System.IntPtr GetAuxBus(global::System.IntPtr AuxBusArrayRefPtr, int index)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAuxBus(AuxBusArrayRefPtr, index);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetAuxBusName(global::System.IntPtr AuxBusRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetAuxBusName(AuxBusRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static string GetAuxBusPath(global::System.IntPtr AuxBusRefPtr)
    {
        try
        {
            return WwiseProjectDatabase.StringFromIntPtrString(WwiseProjectDataBasePINVOKE.GetAuxBusPath(AuxBusRefPtr));
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    public static System.Guid GetAuxBusGuid(global::System.IntPtr AuxBusRefPtr)
    {
        try
        {
            uint A = 0;
            uint B = 0;
            uint C = 0;
            uint D = 0; 
            WwiseProjectDatabase.GetGuidInt(WwiseProjectDataBasePINVOKE.GetAuxBusGuid(AuxBusRefPtr), ref A, ref B, ref C, ref D);
            return GenerateGuidFromInts(A, B, C, D);        
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static uint GetAuxBusShortId(global::System.IntPtr AuxBusRefPtr)
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAuxBusShortId(AuxBusRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }

    public static void DeleteAuxBusRef(global::System.IntPtr AuxBusRefPtr)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteAuxBusRef(AuxBusRefPtr);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static void DeleteAuxBusArrayRef(global::System.IntPtr AuxBusArrayRef)
    {
        try
        {
            WwiseProjectDataBasePINVOKE.DeleteAuxBusArrayRef(AuxBusArrayRef);
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
    
    public static uint GetAuxBusCount()
    {
        try
        {
            return WwiseProjectDataBasePINVOKE.GetAuxBusCount();
        }
        catch (System.Exception e)
        {
            LogProjectDatabaseDLLException(e);
            throw;
        }
    }
}