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
public class WwiseMediaRef: global::System.IDisposable
{
    private global::System.IntPtr projectDatabasePtr;
    protected bool bDeletesManually;

    internal WwiseMediaRef(global::System.IntPtr cPtr, bool cMemoryOwn)
    {
        bDeletesManually = cMemoryOwn;
        projectDatabasePtr = cPtr;
    }

    internal static global::System.IntPtr getCPtr(WwiseMediaRef obj)
    {
        return (obj == null) ? global::System.IntPtr.Zero : obj.projectDatabasePtr;
    }

    internal virtual void setCPtr(global::System.IntPtr cPtr)
    {
        Dispose();
        projectDatabasePtr = cPtr;
    }

    ~WwiseMediaRef()
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
                    WwiseProjectDatabase.DeleteMediaRef(projectDatabasePtr);
                }

                projectDatabasePtr = global::System.IntPtr.Zero;
            }

            global::System.GC.SuppressFinalize(this);
        }
    }
    
    public WwiseMediaRef(global::System.IntPtr cPtr) : this(cPtr, true)
    {
    }
    public string Name => WwiseProjectDatabase.GetMediaName(projectDatabasePtr);
    public string Path => WwiseProjectDatabase.GetMediaPath(projectDatabasePtr);
    public uint ShortId => WwiseProjectDatabase.GetMediaShortId(projectDatabasePtr);
    public string Language => WwiseProjectDatabase.GetMediaLanguage(projectDatabasePtr);
    public bool IsStreaming => WwiseProjectDatabase.GetMediaIsStreaming(projectDatabasePtr);
    public uint Location => WwiseProjectDatabase.GetMediaLocation(projectDatabasePtr);
    public string CachePath => WwiseProjectDatabase.GetMediaCachePath(projectDatabasePtr);
}
#endif