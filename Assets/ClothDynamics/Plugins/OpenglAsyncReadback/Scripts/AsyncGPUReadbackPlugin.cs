using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;

namespace ClothDynamics
{
    /// <summary>
    /// Remember rendertexture's native pointer.
    /// 
    /// It's cost to call GetNativeTexturePtr() for Unity, it will cause sync between render thread and main thread.
    /// </summary>
    internal static class RenderTextureRegistery
    {
        static Dictionary<Texture, IntPtr> ptrs = new Dictionary<Texture, IntPtr>();
        static Dictionary<ComputeBuffer, IntPtr> cbPtrs = new Dictionary<ComputeBuffer, IntPtr>();
        static public IntPtr GetFor(Texture rt)
        {
            if (ptrs.ContainsKey(rt))
            {
                return ptrs[rt];
            }
            else
            {
                var ptr = rt.GetNativeTexturePtr();
                ptrs.Add(rt, ptr);
                return ptr;
            }
        }

        static public IntPtr GetFor(ComputeBuffer rt)
        {
            if (cbPtrs.ContainsKey(rt))
            {
                return cbPtrs[rt];
            }
            else
            {
                var ptr = rt.GetNativeBufferPtr();
                cbPtrs.Add(rt, ptr);
                return ptr;
            }
        }

        static public void ClearDeadRefs()
        {    //Clear disposed pointers.
            foreach (var item in cbPtrs)
            {
                if (item.Key == null)
                    cbPtrs.Remove(item.Key);
            }

            foreach (var item in ptrs)
            {
                if (item.Key == null)
                {
                    ptrs.Remove(item.Key);
                }
            }
        }
    }


    public static class RuntimeInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore && AsyncReadbackUpdater.instance == null)
            {
                var go = new GameObject("__OpenGL Async Readback Updater__");
                go.hideFlags = HideFlags.HideAndDontSave;
                GameObject.DontDestroyOnLoad(go);
                var updater = go.AddComponent<AsyncReadbackUpdater>();
            }
        }
    }

    /// <summary>
    /// Helper struct that wraps unity async readback and our opengl readback together, to hide difference
    /// </summary>
    public struct UniversalAsyncGPUReadbackRequest
    {

        /// <summary>
        /// Request readback of a texture.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="mipmapIndex"></param>
        /// <returns></returns>
        public static UniversalAsyncGPUReadbackRequest Request(Texture src, int mipmapIndex = 0)
        {
            if (SystemInfo.supportsAsyncGPUReadback)
            {

                return new UniversalAsyncGPUReadbackRequest()
                {
                    isPlugin = false,
                    uDisposd = false,
                    uRequest = AsyncGPUReadback.Request(src, mipIndex: mipmapIndex),
                };
            }
            else
            {
#if CD_UNSAFE_CODE
                return new UniversalAsyncGPUReadbackRequest() {
                    isPlugin = true,
                    oRequest = OpenGLAsyncReadbackRequest.CreateTextureRequest(RenderTextureRegistery.GetFor(src).ToInt32(), mipmapIndex)
                };  
#else
                Debug.Log("<color=blue>CD: </color><color=orange> GPU Readback needs CD_UNSAFE_CODE defined, please add that to the Unity \"Scripting Define Symbols\" and activate \"Allow unsafe code\" !</color>");
                return new UniversalAsyncGPUReadbackRequest() { isPlugin = true };
#endif
            }
        }

        public static UniversalAsyncGPUReadbackRequest Request(ComputeBuffer computeBuffer)
        {
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                return new UniversalAsyncGPUReadbackRequest()
                {
                    isPlugin = false,
                    uInited = true,
                    uDisposd = false,
                    uRequest = AsyncGPUReadback.Request(computeBuffer),
                };
            }
            else
            {
#if CD_UNSAFE_CODE
                return new UniversalAsyncGPUReadbackRequest() {
                    isPlugin = true,
                    oRequest = OpenGLAsyncReadbackRequest.CreateComputeBufferRequest((int)computeBuffer.GetNativeBufferPtr(), computeBuffer.stride * computeBuffer.count),
                };
#else
                Debug.Log("<color=blue>CD: </color><color=orange> GPU Readback needs CD_UNSAFE_CODE defined, please add that to the Unity \"Scripting Define Symbols\" and activate \"Allow unsafe code\" !</color>");
                return new UniversalAsyncGPUReadbackRequest() { isPlugin = true };
#endif
            }
        }

        public static UniversalAsyncGPUReadbackRequest OpenGLRequestTexture(int texture, int mipmapIndex)
        {
            return new UniversalAsyncGPUReadbackRequest()
            {
                isPlugin = true,
#if CD_UNSAFE_CODE
                oRequest = OpenGLAsyncReadbackRequest.CreateTextureRequest((int)texture, mipmapIndex)
#endif
            };
        }

        public static UniversalAsyncGPUReadbackRequest OpenGLRequestComputeBuffer(int computeBuffer, int size)
        {
            return new UniversalAsyncGPUReadbackRequest()
            {
                isPlugin = true,
#if CD_UNSAFE_CODE
                oRequest = OpenGLAsyncReadbackRequest.CreateComputeBufferRequest((int)computeBuffer, size)
#endif
            };
        }

        [Obsolete]
        public void Update()
        {

        }

        public bool done
        {
            get
            {
                if (isPlugin)
                {
#if CD_UNSAFE_CODE
                    return oRequest.done;
#else
                    return true;
#endif
                }
                else
                {
                    return uRequest.done;
                }
            }
        }

        public bool hasError
        {
            get
            {
                if (isPlugin)
                {
#if CD_UNSAFE_CODE
                    return oRequest.hasError;
#else
                    return false;
#endif
                }
                else
                {
                    return uRequest.hasError;
                }
            }
        }

        /// <summary>
        /// Get data of a readback request.  
        /// The data is allocated as temp, so it only stay alive for one frame.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public NativeArray<T> GetData<T>() where T : struct
        {
            if (isPlugin)
            {
#if CD_UNSAFE_CODE
                return oRequest.GetRawData<T>();
#else
                Debug.Log("<color=blue>CD: </color><color=orange> GPU Readback needs CD_UNSAFE_CODE defined, please add that to the Unity \"Scripting Define Symbols\" and activate \"Allow unsafe code\" !</color>");
                return new NativeArray<T>(4, Allocator.Temp);
#endif
            }
            else
            {
                return uRequest.GetData<T>();
            }
        }

        public bool valid
        {
            get
            {
                return isPlugin ? oRequest.Valid() : (!uDisposd && uInited);
            }
        }

        public bool isPlugin { get; private set; }

        //fields for unity request.
        private bool uInited;
        private bool uDisposd;
        private AsyncGPUReadbackRequest uRequest;

        //fields for opengl request.
        private OpenGLAsyncReadbackRequest oRequest;

    }

    internal struct OpenGLAsyncReadbackRequest
    {
        public static bool IsAvailable()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore;  //Not tested on es3 yet.
        }
        /// <summary>
        /// Identify native task object handling the request.
        /// </summary>
        private int nativeTaskHandle;

        /// <summary>
        /// Check if the request is done
        /// </summary>
        public bool done
        {
            get
            {
                // AssertRequestValid();
#if CD_UNSAFE_CODE
                return TaskDone(nativeTaskHandle);
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// Check if the request has an error
        /// </summary>
        public bool hasError
        {
            get
            {
                //  AssertRequestValid();
#if CD_UNSAFE_CODE
                return TaskError(nativeTaskHandle);
#else
                return false;
#endif
            }
        }

        public static OpenGLAsyncReadbackRequest CreateTextureRequest(int textureOpenGLName, int mipmapLevel)
        {
            var result = new OpenGLAsyncReadbackRequest();
            result.nativeTaskHandle = RequestTextureMainThread(textureOpenGLName, mipmapLevel);
            GL.IssuePluginEvent(GetKickstartFunctionPtr(), result.nativeTaskHandle);
            return result;
        }

        public static OpenGLAsyncReadbackRequest CreateComputeBufferRequest(int computeBufferOpenGLName, int size)
        {
            var result = new OpenGLAsyncReadbackRequest();
            result.nativeTaskHandle = RequestComputeBufferMainThread(computeBufferOpenGLName, size);
            GL.IssuePluginEvent(GetKickstartFunctionPtr(), result.nativeTaskHandle);
            return result;
        }

        public bool Valid()
        {
#if CD_UNSAFE_CODE
            return TaskExists(this.nativeTaskHandle);
#else
            return false;
#endif
        }

        private void AssertRequestValid()
        {
            if (!Valid())
            {
                throw new UnityException("The request is not valid!");
            }
        }

#if CD_UNSAFE_CODE
        public unsafe NativeArray<T> GetRawData<T>() where T:struct
		{
            AssertRequestValid();
            if (!done) {
                throw new InvalidOperationException("The request is not done yet!");
            }
			// Get data from cpp plugin
			void* ptr = null;
			int length = 0;
			GetData(this.nativeTaskHandle, ref ptr, ref length);

            //Copy data from plugin native memory to unity-controlled native memory.
            var resultNativeArray = new NativeArray<T>(length / UnsafeUtility.SizeOf<T>(), Allocator.Temp);
            UnsafeUtility.MemMove(resultNativeArray.GetUnsafePtr(), ptr, length);
            //Though there exists an api named NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray.
            //It's only for internal use. The document on docs.unity3d.com is a lie.
            
            return resultNativeArray;
		}
#endif
        internal static void Update()
        {
            UpdateMainThread();
            GL.IssuePluginEvent(GetUpdateRenderThreadFunctionPtr(), 0);
        }

        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern bool CheckCompatible();
        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern int RequestTextureMainThread(int texture, int miplevel);
        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern int RequestComputeBufferMainThread(int bufferID, int bufferSize);
        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern IntPtr GetKickstartFunctionPtr();
        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern IntPtr UpdateMainThread();
        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern IntPtr GetUpdateRenderThreadFunctionPtr();
        [DllImport("AsyncGPUReadbackPlugin")]
#if CD_UNSAFE_CODE
		private static extern unsafe void GetData(int event_id, ref void* buffer, ref int length);
        [DllImport("AsyncGPUReadbackPlugin")]
#endif
        private static extern bool TaskError(int event_id);
        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern bool TaskExists(int event_id);
        [DllImport("AsyncGPUReadbackPlugin")]
        private static extern bool TaskDone(int event_id);
    }
}