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
public class WwiseEventRef: global::System.IDisposable
{
    private global::System.IntPtr projectDatabasePtr;
    protected bool bDeletesManually;

    internal WwiseEventRef(global::System.IntPtr cPtr, bool cMemoryOwn)
    {
        bDeletesManually = cMemoryOwn;
        projectDatabasePtr = cPtr;
        Medias = new WwiseEventRefMediaArray(cPtr);
    }

    internal static global::System.IntPtr getCPtr(WwiseEventRef obj)
    {
        return (obj == null) ? global::System.IntPtr.Zero : obj.projectDatabasePtr;
    }

    internal virtual void setCPtr(global::System.IntPtr cPtr)
    {
        Dispose();
        projectDatabasePtr = cPtr;
    }

    ~WwiseEventRef()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        global::System.GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (this)
        {
            if (projectDatabasePtr != global::System.IntPtr.Zero)
            {
                if (bDeletesManually)
                {
                    bDeletesManually = false;
                    WwiseProjectDatabase.DeleteEventRef(projectDatabasePtr);
                }

                projectDatabasePtr = global::System.IntPtr.Zero;
            }

            global::System.GC.SuppressFinalize(this);
        }
    }
    
    public WwiseEventRef(global::System.IntPtr cPtr) : this(cPtr, true)
    {
    }
    public WwiseEventRef(string eventName) : this(WwiseProjectDatabase.GetEventRefString(eventName), true)
    {
    }
    
    public string Name => WwiseProjectDatabase.GetEventName(projectDatabasePtr);
    public string Path => WwiseProjectDatabase.GetEventPath(projectDatabasePtr);
    public System.Guid Guid => WwiseProjectDatabase.GetEventGuid(projectDatabasePtr);

    public uint ShortId =>WwiseProjectDatabase.GetEventShortId(projectDatabasePtr);
    public float MaxAttenuation => WwiseProjectDatabase.GetEventMaxAttenuation(projectDatabasePtr);
    public float MinDuration => WwiseProjectDatabase.GetEventMinDuration(projectDatabasePtr);
    public float MaxDuration => WwiseProjectDatabase.GetEventMaxDuration(projectDatabasePtr);
    public WwiseEventRefMediaArray Medias { get; }
    public uint MediasCount => WwiseProjectDatabase.GetEventMediasCount(projectDatabasePtr);
}
#endif