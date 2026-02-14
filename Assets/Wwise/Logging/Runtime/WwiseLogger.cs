#if !(UNITY_QNX) // Disable under unsupported platforms.
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

namespace AK.Wwise.Unity.Logging
{
    public class WwiseLogger
    {
        private const string WwiseUnityMessagePrefix = "WwiseUnity: ";

        private static WwiseLogger _msInstance;

        private WwiseLogger()
        {
            if (_msInstance == null)
            {
                _msInstance = this;
            }
        }

        /// <summary>
        /// Get the AkLogger singleton
        /// </summary>
        public static WwiseLogger Instance
        {
            get { return _msInstance ??= new WwiseLogger(); }
        }

        private LogLevel logLevel
        {
            get => WwiseLoggerSettings.Instance.LogLevel;
        }

        ~WwiseLogger()
        {
            if (_msInstance == this)
            {
                _msInstance = null;
            }
        }

        /// <summary>
        /// Log a WwiseUnity error message.
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Error(string message)
        {
            Log(LogLevel.Error, message);
        }

        /// <summary>
        /// Log a WwiseUnity warning message.
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        /// <summary>
        /// Log a WwiseUnity message.
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Log(string message)
        {
            Log(LogLevel.Log, message);
        }

        /// <summary>
        /// Log a WwiseUnity message.
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Verbose(string message)
        {
            Log(LogLevel.Verbose, message);
        }
        
        /// <summary>
        /// Log a WwiseUnity very verbose message.
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void VeryVerbose(string message)
        {
            Log(LogLevel.VeryVerbose, message);
        }

        /// <summary>
        /// Log a formatted WwiseUnity error message.
        /// </summary>
        /// <param name="format">Formatting string</param>
        /// <param name="args">Formatting arguments</param>
        public static void ErrorFormat(string format, params object[] args)
        {
            LogFormat(LogLevel.Error, format, args);
        }

        /// <summary>
        /// Log a formatted WwiseUnity warning message.
        /// </summary>
        /// <param name="format">Formatting string</param>
        /// <param name="args">Formatting arguments</param>
        public static void WarningFormat(string format, params object[] args)
        {
            LogFormat(LogLevel.Warning, format, args);
        }

        /// <summary>
        /// Log a formatted WwiseUnity message.
        /// </summary>
        /// <param name="format">Formatting string</param>
        /// <param name="args">Formatting arguments</param>
        public static void LogFormat(string format, params object[] args)
        {
            LogFormat(LogLevel.Log, format, args);
        }
        
        /// <summary>
        /// Log a formatted WwiseUnity verbose message.
        /// </summary>
        /// <param name="format">Formatting string</param>
        /// <param name="args">Formatting arguments</param>
        public static void VerboseFormat(string format, params object[] args)
        {
            LogFormat(LogLevel.Verbose, format, args);
        }
        
        /// <summary>
        /// Log a formatted WwiseUnity very verbose message.
        /// </summary>
        /// <param name="format">Formatting string</param>
        /// <param name="args">Formatting arguments</param>
        public static void VeryVerboseFormat(string format, params object[] args)
        {
            LogFormat(LogLevel.VeryVerbose, format, args);
        }

        /// <summary>
        /// Log a WwiseUnity message.
        /// </summary>
        /// <param name="logLevel">Log verbosity</param>
        /// <param name="message">Message to log</param>
        public static void Log(LogLevel logLevel, string message)
        {
#if (DEVELOPMENT_BUILD || UNITY_EDITOR) || !WWISE_SILENCE_LOGS_IN_RELEASE
            if (Instance.logLevel >= logLevel)
            {
                switch (logLevel)
                {
                    case LogLevel.None:
                        break;
                    case LogLevel.Error:
                        UnityEngine.Debug.LogError(WwiseUnityMessagePrefix + "(ERROR) "+ message);
                        break;
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(WwiseUnityMessagePrefix + "(WARNING) "+ message);
                        break;
                    case LogLevel.Log:
                        UnityEngine.Debug.Log(WwiseUnityMessagePrefix + "(LOG) "+ message);
                        break;
                    case LogLevel.Verbose:
                        UnityEngine.Debug.Log(WwiseUnityMessagePrefix + "(VERBOSE) "+ message);
                        break;
                    case LogLevel.VeryVerbose:
                        UnityEngine.Debug.Log(WwiseUnityMessagePrefix + "(VERYVERBOSE) "+ message);
                        break;
                }
            }
#endif
        }

        /// <summary>
        /// Log a formatted WwiseUnity message.
        /// </summary>
        /// <param name="logLevel">Log verbosity</param>
        /// <param name="format">Formatting string</param>
        /// <param name="args">Formatting arguments</param>
        public static void LogFormat(LogLevel logLevel, string format, params object[] args)
        {
#if (DEVELOPMENT_BUILD || UNITY_EDITOR) || !WWISE_SILENCE_LOGS_IN_RELEASE
            if (Instance.logLevel >= logLevel)
            {
                switch (logLevel)
                {
                    case LogLevel.None:
                        break;
                    case LogLevel.Error:
                        UnityEngine.Debug.LogErrorFormat(WwiseUnityMessagePrefix + "(ERROR) "+ format, args);
                        break;
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarningFormat(WwiseUnityMessagePrefix + "(WARNING) "+ format, args);
                        break;
                    case LogLevel.Log:
                        UnityEngine.Debug.LogFormat(WwiseUnityMessagePrefix + "(LOG) "+ format, args);
                        break;
                    case LogLevel.Verbose:
                        UnityEngine.Debug.LogFormat(WwiseUnityMessagePrefix + "(VERBOSE) "+ format, args);
                        break;
                    case LogLevel.VeryVerbose:
                        UnityEngine.Debug.LogFormat(WwiseUnityMessagePrefix + "(VERYVERBOSE) "+ format, args);
                        break;
                }
            }
#endif
        }
    }
}

#endif // #if !(UNITY_QNX) // Disable under unsupported platforms.