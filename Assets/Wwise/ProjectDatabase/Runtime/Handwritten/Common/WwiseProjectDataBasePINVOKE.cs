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
#if UNITY_EDITOR
using System.Runtime.InteropServices;
public class WwiseProjectDataBasePINVOKE
{
    /*
     * Utility
    */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Init(string InDirectoryPath, string InDirectoryPlatformName);

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetCurrentPlatform(string InDirectoryPlatformName);

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetCurrentLanguage(string InLanguageName);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetGuidInt(global::System.IntPtr InGuid, ref uint A, ref uint B, ref uint C, ref uint D);
    
    /*
     * SoundBank Ref
    */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllSoundBanksRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSoundBankCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSoundBankRefIndex(global::System.IntPtr soundBankRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteSoundBanksArrayRef(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSoundBankRefString(string soundBankName, string soundBankType);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSoundBankName(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSoundBankPath(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSoundBankObjectPath(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSoundBankLanguage(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSoundBankLanguageId(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSoundBankGuid(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSoundBankShortId(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool IsUserBank(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool IsInitBank(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool IsSoundBankValid(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSoundBankMedia(global::System.IntPtr soundBankRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSoundBankMediasCount(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSoundBankEvent(global::System.IntPtr soundBankRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSoundBankEventsCount(global::System.IntPtr soundBankRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteSoundBankRef(global::System.IntPtr soundBankRefPtr);
    
    /*
     * Media Ref
    */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetMediaName(global::System.IntPtr mediaRef);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetMediaPath(global::System.IntPtr mediaRef);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetMediaShortId(global::System.IntPtr mediaRef);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetMediaLanguage(global::System.IntPtr mediaRef);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool GetMediaIsStreaming(global::System.IntPtr mediaRef);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetMediaLocation(global::System.IntPtr mediaRef);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetMediaCachePath(global::System.IntPtr mediaRef);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteMediaRef(global::System.IntPtr mediaRefPtr);
    
    /*
     * Event Ref
    */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllEventsRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetEventCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetEventRefString(string InString);

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetEventName(global::System.IntPtr eventRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetEventPath(global::System.IntPtr eventRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetEventGuid(global::System.IntPtr eventRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetEventShortId(global::System.IntPtr eventRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetEventMaxAttenuation(global::System.IntPtr eventRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetEventMinDuration(global::System.IntPtr eventRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetEventMaxDuration(global::System.IntPtr eventRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetEventMedia(global::System.IntPtr soundBankRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetEventMediasCount(global::System.IntPtr eventRefPtr);
   
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteEventRef(global::System.IntPtr mediaRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteEventArrayRef(global::System.IntPtr mediaRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetEvent(global::System.IntPtr eventArrayRefPtr, int index);
    
    /*
     * Platform Ref
    */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetPlatformRef(string InString);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetPlatformName(global::System.IntPtr platformRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetPlatformGuid(global::System.IntPtr platformRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeletePlatformRef(global::System.IntPtr platformRefPtr);
    
    /*
    * Plugin Ref
    */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllPluginRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetPluginCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetPluginRefIndex(global::System.IntPtr soundBankRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetPluginId(global::System.IntPtr pluginRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetPluginName(global::System.IntPtr pluginRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetPluginDLL(global::System.IntPtr pluginRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetPluginStaticLib(global::System.IntPtr pluginRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeletePluginRef(global::System.IntPtr pluginRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeletePluginArrayRef(global::System.IntPtr pluginArrayRefPtr);
    
    
    /*
     * Switch Group Ref
     */

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllSwitchGroupRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSwitchGroupCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteSwitchGroupRef(global::System.IntPtr switchGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteSwitchGroupArrayRef(global::System.IntPtr switchGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSwitchGroup(global::System.IntPtr switchArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSwitchGroupName(global::System.IntPtr switchGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSwitchGroupPath(global::System.IntPtr switchGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSwitchGroupGuid(global::System.IntPtr switchGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSwitchGroupShortId(global::System.IntPtr switchGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSwitchGroupRefString(string InString);
    
    /*
     * Switch Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllSwitchRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSwitchCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteSwitchRef(global::System.IntPtr switchRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteSwitchArrayRef(global::System.IntPtr switchRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSwitch(global::System.IntPtr switchArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSwitchName(global::System.IntPtr switchRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSwitchPath(global::System.IntPtr switchRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetSwitchGuid(global::System.IntPtr switchRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSwitchShortId(global::System.IntPtr switchRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetSwitchGroupId(global::System.IntPtr switchRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetSwitchRefString(string InString);

    
    /*
     * State Group Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllStateGroupRef();
        
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetStateGroupCount();

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteStateGroupRef(global::System.IntPtr StateGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteStateGroupArrayRef(global::System.IntPtr StateGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetStateGroup(global::System.IntPtr StateArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetStateGroupName(global::System.IntPtr StateGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetStateGroupPath(global::System.IntPtr StateGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetStateGroupGuid(global::System.IntPtr StateGroupRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetStateGroupShortId(global::System.IntPtr StateGroupRefPtr);

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetStateGroupRefString(string InString);
    
    /*
     * State Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllStateRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetStateCount();

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteStateRef(global::System.IntPtr StateRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteStateArrayRef(global::System.IntPtr StateRefPtr);
        
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetState(global::System.IntPtr StateArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetStateName(global::System.IntPtr StateRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetStatePath(global::System.IntPtr StateRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetStateGuid(global::System.IntPtr StateRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetStateShortId(global::System.IntPtr StateRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetStateGroupId(global::System.IntPtr StateRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetStateRefString(string InString);
    /*
     * AcousticTexture Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllAcousticTextureRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetAcousticTextureCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteAcousticTextureRef(global::System.IntPtr AcousticTextureRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteAcousticTextureArrayRef(global::System.IntPtr AcousticTextureRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAcousticTexture(global::System.IntPtr AcousticTextureArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetAcousticTextureName(global::System.IntPtr AcousticTextureRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetAcousticTexturePath(global::System.IntPtr AcousticTextureRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetAcousticTextureGuid(global::System.IntPtr AcousticTextureRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetAcousticTextureShortId(global::System.IntPtr AcousticTextureRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAcousticTextureRefString(string InString);
    
    /*
     * GameParameter Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllGameParameterRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetGameParameterCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteGameParameterRef(global::System.IntPtr GameParameterRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteGameParameterArrayRef(global::System.IntPtr GameParameterRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetGameParameter(global::System.IntPtr GameParameterArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetGameParameterName(global::System.IntPtr GameParameterRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetGameParameterPath(global::System.IntPtr GameParameterRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetGameParameterGuid(global::System.IntPtr GameParameterRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetGameParameterShortId(global::System.IntPtr GameParameterRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetGameParameterRefString(string InString);
    
    /*
     * Trigger Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllTriggerRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetTriggerCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteTriggerRef(global::System.IntPtr TriggerRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteTriggerArrayRef(global::System.IntPtr TriggerRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetTrigger(global::System.IntPtr TriggerArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetTriggerName(global::System.IntPtr TriggerRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetTriggerPath(global::System.IntPtr TriggerRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetTriggerGuid(global::System.IntPtr TriggerRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetTriggerShortId(global::System.IntPtr TriggerRefPtr);

    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetTriggerRefString(string InString);
    
    /*
     * Bus Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllBusRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetBusCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteBusRef(global::System.IntPtr BusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteBusArrayRef(global::System.IntPtr BusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetBus(global::System.IntPtr BusArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetBusName(global::System.IntPtr BusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetBusPath(global::System.IntPtr BusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetBusGuid(global::System.IntPtr BusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetBusShortId(global::System.IntPtr BusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetBusRefString(string InString);
    
    /*
     * AuxBus Ref
     */
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAllAuxBusRef();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetAuxBusCount();
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteAuxBusRef(global::System.IntPtr AuxBusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DeleteAuxBusArrayRef(global::System.IntPtr AuxBusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAuxBus(global::System.IntPtr AuxBusArrayRefPtr, int index);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetAuxBusName(global::System.IntPtr AuxBusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetAuxBusPath(global::System.IntPtr AuxBusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern System.IntPtr GetAuxBusGuid(global::System.IntPtr AuxBusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetAuxBusShortId(global::System.IntPtr AuxBusRefPtr);
    
    [DllImport("WwiseProjectDatabase.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern global::System.IntPtr GetAuxBusRefString(string InString);
}
#endif
