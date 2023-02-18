using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace DynamicOpenVR.UntilYouFall
{
    [BepInPlugin("com.nicoco007.until-you-fall.dynamicopenvr", "DynamicOpenVR.UntilYouFall", "1.0.0")]
    public class Plugin : BasePlugin
    {
        public override void Load()
        {
            ClassInjector.RegisterTypeInIl2Cpp<OpenVRActionManager>();
            // ClassInjector.RegisterTypeInIl2Cpp<OpenVREventHandler>();

            try
            {
                OpenVRUtilities.Init(false);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to initialize OpenVR API; DynamicOpenVR will not run");
                Debug.LogError(ex?.ToString());
                return;
            }

            Logging.Logger.handler = new BepInExLoggerWrapper(Log);
            OpenVRActionManager.instance.Initialize("Until You Fall", Path.Combine(Paths.GameRootPath, "actions.json"));
        }

        private class BepInExLoggerWrapper : Logging.ILogHandler
        {
            private readonly ManualLogSource _manualLogSource;

            public BepInExLoggerWrapper(ManualLogSource manualLogSource)
            {
                _manualLogSource = manualLogSource;
            }

            public void Critical(object message)
            {
                _manualLogSource.LogError(message);
            }

            public void Debug(object message)
            {
                _manualLogSource.LogDebug(message);
            }

            public void Error(object message)
            {
                _manualLogSource.LogError(message);
            }

            public void Info(object message)
            {
                _manualLogSource.LogInfo(message);
            }

            public void Notice(object message)
            {
                _manualLogSource.LogInfo(message);
            }

            public void Trace(object message)
            {
                _manualLogSource.LogDebug(message);
            }

            public void Warn(object message)
            {
                _manualLogSource.LogWarning(message);
            }
        }
    }
}
