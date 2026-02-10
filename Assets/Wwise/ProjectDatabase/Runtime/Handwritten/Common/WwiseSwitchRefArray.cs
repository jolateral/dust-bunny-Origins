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
public class WwiseSwitchRefArray
{
    private global::System.IntPtr ownerPtr;
    protected bool bDeletesManually;

    internal WwiseSwitchRefArray(global::System.IntPtr cPtr, bool cMemoryOwn)
    {
        bDeletesManually = cMemoryOwn;
        ownerPtr = cPtr;
    }
    
    ~WwiseSwitchRefArray()
    {
        Dispose(false);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        lock (this)
        {
            if (ownerPtr != global::System.IntPtr.Zero)
            {
                if (bDeletesManually)
                {
                    bDeletesManually = false;
                    WwiseProjectDatabase.DeleteSwitchArrayRef(ownerPtr);
                }

                ownerPtr = global::System.IntPtr.Zero;
            }

            global::System.GC.SuppressFinalize(this);
        }
    }
    
    public WwiseSwitchRefArray() : this(WwiseProjectDatabase.GetAllSwitchRef(), true)
    {
    }

    public WwiseSwitchRef this[int index]
    {
        get { return new WwiseSwitchRef(WwiseProjectDatabase.GetSwitch(ownerPtr, index), false); }
    }
}

public class WwiseSwitchGroupRefArray
{
    private global::System.IntPtr ownerPtr;
    protected bool bDeletesManually;

    internal WwiseSwitchGroupRefArray(global::System.IntPtr cPtr, bool cMemoryOwn)
    {
        bDeletesManually = cMemoryOwn;
        ownerPtr = cPtr;
    }
    
    ~WwiseSwitchGroupRefArray()
    {
        Dispose(false);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        lock (this)
        {
            if (ownerPtr != global::System.IntPtr.Zero)
            {
                if (bDeletesManually)
                {
                    bDeletesManually = false;
                    WwiseProjectDatabase.DeleteSwitchArrayRef(ownerPtr);
                }

                ownerPtr = global::System.IntPtr.Zero;
            }

            global::System.GC.SuppressFinalize(this);
        }
    }
    
    public WwiseSwitchGroupRefArray() : this(WwiseProjectDatabase.GetAllSwitchGroupRef(), true)
    {
    }

    public WwiseSwitchGroupRef this[int index]
    {
        get { return new WwiseSwitchGroupRef(WwiseProjectDatabase.GetSwitchGroup(ownerPtr, index), false); }
    }
}
#endif