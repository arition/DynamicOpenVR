// <copyright file="Plugin.cs" company="Nicolas Gnyra">
// DynamicOpenVR.BeatSaber - An implementation of DynamicOpenVR as a Beat Saber plugin.
// Copyright © 2019-2021 Nicolas Gnyra
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see http://www.gnu.org/licenses/.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicOpenVR.BeatSaber.InputCollections;
using DynamicOpenVR.IO;
using DynamicOpenVR.SteamVR;
using DynamicOpenVR.SteamVR.VRManifest;
using HarmonyLib;
using IPA;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using Logger = IPA.Logging.Logger;

namespace DynamicOpenVR.BeatSaber
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    internal class Plugin
    {
        internal static readonly string kSteamPath = SteamUtilities.GetSteamHomeDirectory();
        internal static readonly string kManifestPath = Path.Combine(UnityGame.InstallPath, "beatsaber.vrmanifest");
        internal static readonly string kAppConfigPath = Path.Combine(kSteamPath, "config", "appconfig.json");
        internal static readonly string kGlobalManifestPath = Path.Combine(kSteamPath, "config", "steamapps.vrmanifest");

        private static readonly string kActionManifestPath = Path.Combine(UnityGame.InstallPath, "DynamicOpenVR", "action_manifest.json");

        private static readonly JsonSerializerSettings kJsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly Logger _logger;
        private readonly Harmony _harmonyInstance;

        private AppConfig _updatedAppConfig;

        [Init]
        public Plugin(Logger logger)
        {
            _logger = logger;
            _harmonyInstance = new Harmony("com.nicoco007.dynamicopenvr.beatsaber");

            Logging.Logger.handler = new IPALogHandler(logger);
        }

        public static UnityXRActions unityXRActions { get; private set; }

        public static BeatSaberActions beatSaberActions { get; private set; }

        [OnStart]
        public void OnStart()
        {
            _logger.Info("Starting " + typeof(Plugin).Namespace);

            try
            {
                OpenVRUtilities.Init();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to initialize OpenVR API; DynamicOpenVR will not run");
                _logger.Error(ex);
                return;
            }

            _logger.Info("Successfully initialized OpenVR API");

            // adding the manifest to config is more of a quality of life thing
            try
            {
                AddManifestToSteamConfig();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to update SteamVR manifest.");
                _logger.Error(ex);
            }

            RegisterActionSet();
            ApplyHarmonyPatches();

            OpenVRActionManager.instance.Initialize("Beat Saber", kActionManifestPath);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [OnExit]
        public void OnExit()
        {
            beatSaberActions?.Dispose();
            unityXRActions?.Dispose();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu" && _updatedAppConfig != null)
            {
                SceneContext sceneContext = Resources.FindObjectsOfTypeAll<SceneContext>().First(sc => sc.gameObject.scene.name == "MainMenu");
                sceneContext.OnPostInstall.AddListener(() =>
                {
                    AppConfigConfirmationModal.Create(sceneContext.Container, _updatedAppConfig);
                });
            }
        }

        private void AddManifestToSteamConfig()
        {
            VRApplication beatSaberManifest = ReadBeatSaberManifest(kGlobalManifestPath);

            beatSaberManifest.actionManifestPath = kActionManifestPath;

            var vrManifest = new VRApplicationManifest
            {
                applications = new[] { beatSaberManifest },
            };

            WriteBeatSaberManifest(kManifestPath, vrManifest);

            AppConfig appConfig = ReadAppConfig(kAppConfigPath);
            List<string> manifestPaths = appConfig.manifestPaths;
            var existing = manifestPaths.Where(p => string.Equals(p, kManifestPath, StringComparison.InvariantCultureIgnoreCase)).ToList();
            bool updated = false;

            // only rewrite if path isn't in list already or is not at the top
            if (manifestPaths.IndexOf(existing.FirstOrDefault()) != 0 || existing.Count > 1)
            {
                _logger.Info($"Adding '{kManifestPath}' to app config");

                foreach (string token in existing)
                {
                    manifestPaths.Remove(token);
                }

                manifestPaths.Insert(0, kManifestPath);

                updated = true;
            }
            else
            {
                _logger.Trace("Manifest is already in app config");
            }

            if (!manifestPaths.Any(p => string.Equals(p, kGlobalManifestPath, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.Info($"Adding '{kGlobalManifestPath}' to app config");

                manifestPaths.Add(kGlobalManifestPath);

                updated = true;
            }
            else
            {
                _logger.Info("Global manifest is already in app config");
            }

            if (updated)
            {
                _updatedAppConfig = appConfig;
            }
        }

        private VRApplication ReadBeatSaberManifest(string globalManifestPath)
        {
            if (!File.Exists(globalManifestPath))
            {
                throw new FileNotFoundException($"Could not find file '{globalManifestPath}'");
            }

            VRApplication beatSaberManifest;

            _logger.Trace($"Reading '{globalManifestPath}'");

            using (var reader = new StreamReader(globalManifestPath))
            {
                VRApplicationManifest globalManifest = JsonConvert.DeserializeObject<VRApplicationManifest>(reader.ReadToEnd(), kJsonSerializerSettings);
                beatSaberManifest = globalManifest.applications?.FirstOrDefault(a => a.appKey == "steam.app.620980");
            }

            return beatSaberManifest ?? throw new Exception($"Beat Saber manifest not found in '{globalManifestPath}'");
        }

        private AppConfig ReadAppConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                _logger.Warn($"Could not find file '{configPath}'");
                return new AppConfig();
            }

            _logger.Trace($"Reading '{configPath}'");

            AppConfig appConfig;

            using (var reader = new StreamReader(configPath))
            {
                appConfig = JsonConvert.DeserializeObject<AppConfig>(reader.ReadToEnd(), kJsonSerializerSettings) ?? new AppConfig();
            }

            return appConfig;
        }

        private void WriteBeatSaberManifest(string manifestPath, VRApplicationManifest beatSaberManifest)
        {
            _logger.Info($"Writing manifest to '{manifestPath}'");

            using StreamWriter writer = new(manifestPath);
            writer.Write(JsonConvert.SerializeObject(beatSaberManifest, kJsonSerializerSettings));
        }

        private void RegisterActionSet()
        {
            _logger.Info("Registering actions");

            // Beat Saber inputs
            beatSaberActions = new BeatSaberActions()
            {
                leftTrigger = new VectorInput("/actions/main/in/left_trigger"),
                rightTrigger = new VectorInput("/actions/main/in/right_trigger"),
                leftMenuButton = new BooleanInput("/actions/main/in/left_menu_button"),
                rightMenuButton = new BooleanInput("/actions/main/in/right_menu_button"),
                leftSliceHaptics = new HapticVibrationOutput("/actions/main/out/left_slice_haptics"),
                rightSliceHaptics = new HapticVibrationOutput("/actions/main/out/right_slice_haptics"),
                leftHandPose = new PoseInput("/actions/main/in/left_hand_pose"),
                rightHandPose = new PoseInput("/actions/main/in/right_hand_pose"),
                leftThumbstick = new Vector2Input("/actions/main/in/left_thumbstick"),
                rightThumbstick = new Vector2Input("/actions/main/in/right_thumbstick"),
            };

            // Generic Unity InputDevices stuff
            // mappings are based on https://docs.unity3d.com/Manual/xr_input.html
            unityXRActions = new UnityXRActions
            {
                left = new UnityXRActionsHand
                {
                    pose = new PoseInput("/actions/unity/in/left_pose"),
                    primaryButton = new BooleanInput("/actions/unity/in/left_primary_button"),
                    primaryTouch = new BooleanInput("/actions/unity/in/left_primary_touch"),
                    secondaryButton = new BooleanInput("/actions/unity/in/left_secondary_button"),
                    secondaryTouch = new BooleanInput("/actions/unity/in/left_secondary_touch"),
                    grip = new VectorInput("/actions/unity/in/left_grip"),
                    gripButton = new BooleanInput("/actions/unity/in/left_grip_button"),
                    trigger = new VectorInput("/actions/unity/in/left_trigger"),
                    triggerButton = new BooleanInput("/actions/unity/in/left_trigger_button"),
                    menuButton = new BooleanInput("/actions/unity/in/left_menu_button"),
                    primary2DAxis = new Vector2Input("/actions/unity/in/left_primary_2d_axis"),
                    primary2DAxisClick = new BooleanInput("/actions/unity/in/left_primary_2d_axis_click"),
                    primary2DAxisTouch = new BooleanInput("/actions/unity/in/left_primary_2d_axis_touch"),
                    haptics = new HapticVibrationOutput("/actions/unity/out/left_haptics"),
                },
                right = new UnityXRActionsHand
                {
                    pose = new PoseInput("/actions/unity/in/right_pose"),
                    primaryButton = new BooleanInput("/actions/unity/in/right_primary_button"),
                    primaryTouch = new BooleanInput("/actions/unity/in/right_primary_touch"),
                    secondaryButton = new BooleanInput("/actions/unity/in/right_secondary_button"),
                    secondaryTouch = new BooleanInput("/actions/unity/in/right_secondary_touch"),
                    grip = new VectorInput("/actions/unity/in/right_grip"),
                    gripButton = new BooleanInput("/actions/unity/in/right_grip_button"),
                    trigger = new VectorInput("/actions/unity/in/right_trigger"),
                    triggerButton = new BooleanInput("/actions/unity/in/right_trigger_button"),
                    menuButton = new BooleanInput("/actions/unity/in/right_menu_button"),
                    primary2DAxis = new Vector2Input("/actions/unity/in/right_primary_2d_axis"),
                    primary2DAxisClick = new BooleanInput("/actions/unity/in/right_primary_2d_axis_click"),
                    primary2DAxisTouch = new BooleanInput("/actions/unity/in/right_primary_2d_axis_touch"),
                    haptics = new HapticVibrationOutput("/actions/unity/out/right_haptics"),
                },
            };
        }

        private void ApplyHarmonyPatches()
        {
            _logger.Info("Applying input patches");

            _harmonyInstance.PatchAll();
        }
    }
}
