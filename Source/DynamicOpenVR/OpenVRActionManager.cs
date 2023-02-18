// <copyright file="OpenVRActionManager.cs" company="Nicolas Gnyra">
// DynamicOpenVR - Unity scripts to allow dynamic creation of OpenVR actions at runtime.
// Copyright � 2019-2021 Nicolas Gnyra
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
using DynamicOpenVR.DefaultBindings;
using DynamicOpenVR.Exceptions;
using DynamicOpenVR.IO;
using DynamicOpenVR.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using Logger = DynamicOpenVR.Logging.Logger;

namespace DynamicOpenVR
{
    public class OpenVRActionManager : MonoBehaviour
    {
        private static OpenVRActionManager _instance;

        private readonly Dictionary<string, OVRAction> _actions = new();
        private readonly HashSet<string> _actionSetNames = new();
        private readonly List<ulong> _actionSetHandles = new();
        private readonly JsonSerializerSettings _jsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private string _actionManifestPath;
        private string _gameName;

        public static OpenVRActionManager instance
        {
            get
            {
                // check for actual null reference since we don't want to create another object if the current one is marked for destruction
                if (_instance is null)
                {
                    Logger.Info($"Creating instance of {nameof(OpenVRActionManager)}");

                    var go = new GameObject(nameof(OpenVRActionManager));
                    DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<OpenVRActionManager>();
                }

                return _instance;
            }
        }

        public string actionManifestPath
        {
            get
            {
                if (!initialized)
                {
                    throw new Exception(nameof(OpenVRActionManager) + " is not initialized");
                }

                return _actionManifestPath;
            }
        }

        public bool initialized { get; private set; }

        public void Initialize(string gameName, string actionManifestPath)
        {
            if (initialized)
            {
                throw new InvalidOperationException("Already initialized");
            }

            Logger.Info($"Initializing {nameof(OpenVRActionManager)}");

            _actionManifestPath = actionManifestPath;
            _gameName = gameName;

            CombineAndWriteManifest();

            OpenVRFacade.SetActionManifestPath(actionManifestPath);

            IEnumerable<string> actionSetNames = _actions.Values.Select(action => action.GetActionSetName()).Distinct();

            foreach (string actionSetName in actionSetNames)
            {
                TryAddActionSet(actionSetName);
            }

            foreach (OVRAction action in _actions.Values.ToList())
            {
                TryUpdateHandle(action);
            }

            initialized = true;
        }

        public void Update()
        {
            if (!initialized)
            {
                return; // do nothing until initialized
            }

            if (_actionSetHandles.Count > 0)
            {
                OpenVRFacade.UpdateActionState(_actionSetHandles);
            }

            foreach (OVRInput input in _actions.Values.OfType<OVRInput>().ToList())
            {
                try
                {
                    input.UpdateData();
                }
                catch (OpenVRInputException ex)
                {
                    Logger.Error($"An unexpected OpenVR error occurred when fetching data for action '{input.name}'. Action has been disabled.");
                    Logger.Error(ex);

                    DeregisterAction(input);
                }
                catch (NullReferenceException ex)
                {
                    Logger.Error($"A null reference exception occurred when fetching data for action '{input.name}'. This is most likely caused by an internal OpenVR issue. Action has been disabled.");
                    Logger.Error(ex);

                    DeregisterAction(input);
                }
                catch (Exception ex)
                {
                    Logger.Error($"An unexpected error occurred when fetching data for action '{input.name}'. Action has been disabled.");
                    Logger.Error(ex);

                    DeregisterAction(input);
                }
            }
        }

        public void RegisterAction(OVRAction action)
        {
            if (_actions.ContainsKey(action.id))
            {
                throw new InvalidOperationException("Action was already registered.");
            }

            Logger.Trace($"Registering action '{action.name}' ({action.id})");

            _actions.Add(action.id, action);

            if (initialized)
            {
                string actionSetName = action.GetActionSetName();

                if (!_actionSetNames.Contains(actionSetName))
                {
                    TryAddActionSet(actionSetName);
                }

                TryUpdateHandle(action);
            }
        }

        public void DeregisterAction(OVRAction action)
        {
            Logger.Trace($"Deregistering action '{action.name}' ({action.id})");

            _actions.Remove(action.id);
        }

        private void CombineAndWriteManifest()
        {
            string actionsFolder = Path.Combine(Directory.GetCurrentDirectory(), "DynamicOpenVR", "Actions");

            if (!Directory.Exists(actionsFolder))
            {
                Logger.Warn("Actions folder does not exist!");
                return;
            }

            Logger.Trace($"Reading actions from '{actionsFolder}'");

            string[] actionFiles = Directory.GetFiles(actionsFolder);
            var actionManifests = new List<ActionManifest>();

            foreach (string actionFile in actionFiles)
            {
                try
                {
                    Logger.Trace($"Reading '{actionFile}'");

                    using (var reader = new StreamReader(actionFile))
                    {
                        string data = reader.ReadToEnd();
                        actionManifests.Add(JsonConvert.DeserializeObject<ActionManifest>(data, _jsonSerializerSettings));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"An error of type {ex.GetType().FullName} occured when trying to parse '{actionFile}': {ex.Message}");
                }
            }

            uint version = 23;

            unchecked
            {
                foreach (string action in actionManifests.SelectMany(am => am.actions).Select(a => a.name).OrderBy(n => n, StringComparer.InvariantCulture))
                {
                    version = version * 17 + (uint)action.GetHashCode();
                }
            }

            version = uint.MaxValue;

            List<ManifestDefaultBinding> defaultBindings = CombineAndWriteBindings(version);

            var manifest = new ActionManifest()
            {
                version = version,
                actions = actionManifests.SelectMany(m => m.actions).ToList(),
                actionSets = actionManifests.SelectMany(m => m.actionSets).ToList(),
                defaultBindings = defaultBindings,
                localization = CombineLocalizations(actionManifests),
            };

            foreach (string actionSetName in manifest.actionSets.Select(a => a.name))
            {
                Logger.Trace($"Found defined action set '{actionSetName}'");
            }

            foreach (string actionName in manifest.actions.Select(a => a.name))
            {
                Logger.Trace($"Found defined action '{actionName}'");
            }

            foreach (string controllerType in manifest.defaultBindings.Select(a => a.controllerType))
            {
                Logger.Trace($"Found default binding for controller '{controllerType}'");
            }

            Logger.Trace($"Writing action manifest to '{_actionManifestPath}'");

            using (var writer = new StreamWriter(_actionManifestPath))
            {
                writer.WriteLine(JsonConvert.SerializeObject(manifest, _jsonSerializerSettings));
            }
        }

        private void TryUpdateHandle(OVRAction action)
        {
            Logger.Trace($"Updating handle for action '{action.name}' ({action.id})");

            try
            {
                action.UpdateHandle();
            }
            catch (OpenVRInputException ex)
            {
                Logger.Error($"An unexpected OpenVR error occurred when fetching handle for action '{action.name}'. Action has been disabled.");
                Logger.Error(ex);

                DeregisterAction(action);
            }
            catch (NullReferenceException ex)
            {
                Logger.Error($"A null reference exception occurred when fetching handle for action '{action.name}'. This is most likely caused by an internal OpenVR issue. Action has been disabled.");
                Logger.Error(ex);

                DeregisterAction(action);
            }
            catch (Exception ex)
            {
                Logger.Error($"An unexpected error occurred when fetching handle for action '{action.name}'. Action has been disabled.");
                Logger.Error(ex);

                DeregisterAction(action);
            }
        }

        private void TryAddActionSet(string actionSetName)
        {
            Logger.Trace($"Registering action set '{actionSetName}'");

            if (_actionSetNames.Contains(actionSetName))
            {
                throw new InvalidOperationException($"Action set '{actionSetName}' has already been registered");
            }

            try
            {
                _actionSetNames.Add(actionSetName);
                _actionSetHandles.Add(OpenVRFacade.GetActionSetHandle(actionSetName));
            }
            catch (OpenVRInputException ex)
            {
                Logger.Error($"An unexpected OpenVR error occurred when fetching handle for action set '{actionSetName}'.");
                Logger.Error(ex);
            }
            catch (NullReferenceException ex)
            {
                Logger.Error($"A null reference exception occured when fetching handle for action set '{actionSetName}'. This is most likely caused by an internal OpenVR issue. Action has been disabled.");
                Logger.Error(ex);
            }
            catch (Exception ex)
            {
                Logger.Error($"An unexpected error occurred when fetching handle for action set '{actionSetName}'.");
                Logger.Error(ex);
            }
        }

        private List<ManifestDefaultBinding> CombineAndWriteBindings(uint manifestVersion)
        {
            string bindingsFolder = Path.Combine(Directory.GetCurrentDirectory(), "DynamicOpenVR", "Bindings");

            if (!Directory.Exists(bindingsFolder))
            {
                Logger.Warn("Bindings folder does not exist!");
                return new List<ManifestDefaultBinding>();
            }

            Logger.Trace($"Reading default bindings from '{bindingsFolder}'");

            string[] bindingFiles = Directory.GetFiles(bindingsFolder);
            var defaultBindings = new List<DefaultBinding>();

            foreach (string bindingFile in bindingFiles)
            {
                try
                {
                    Logger.Trace($"Reading '{bindingFile}'");

                    using (var reader = new StreamReader(bindingFile))
                    {
                        defaultBindings.Add(JsonConvert.DeserializeObject<DefaultBinding>(reader.ReadToEnd(), _jsonSerializerSettings));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"An error of type {ex.GetType().FullName} occured when trying to parse '{bindingFile}': {ex.Message}");
                }
            }

            var combinedBindings = new List<ManifestDefaultBinding>();

            foreach (string controllerType in defaultBindings.Select(b => b.controllerType).Distinct())
            {
                var defaultBinding = new DefaultBinding
                {
                    actionManifestVersion = manifestVersion,
                    name = $"Default {_gameName} Bindings",
                    description = $"Action bindings for {_gameName}.",
                    controllerType = controllerType,
                    category = "steamvr_input",
                    bindings = MergeBindings(defaultBindings.Where(b => b.controllerType == controllerType)),
                };

                string fileName = $"default_bindings_{defaultBinding.controllerType}.json";
                combinedBindings.Add(new ManifestDefaultBinding { controllerType = controllerType, bindingUrl = fileName });

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(_actionManifestPath), fileName)))
                {
                    writer.WriteLine(JsonConvert.SerializeObject(defaultBinding, _jsonSerializerSettings));
                }
            }

            return combinedBindings;
        }

        private Dictionary<string, BindingCollection> MergeBindings(IEnumerable<DefaultBinding> bindingSets)
        {
            var final = new Dictionary<string, BindingCollection>();

            foreach (DefaultBinding bindingSet in bindingSets)
            {
                foreach (KeyValuePair<string, BindingCollection> kvp in bindingSet.bindings)
                {
                    string actionSetName = kvp.Key;
                    BindingCollection bindings = kvp.Value;

                    if (!final.ContainsKey(actionSetName))
                    {
                        final.Add(actionSetName, new BindingCollection());
                    }

                    final[actionSetName].chords.AddRange(bindings.chords);
                    final[actionSetName].haptics.AddRange(bindings.haptics);
                    final[actionSetName].poses.AddRange(bindings.poses);
                    final[actionSetName].skeleton.AddRange(bindings.skeleton);
                    final[actionSetName].sources.AddRange(bindings.sources);
                }
            }

            return final;
        }

        private List<Dictionary<string, string>> CombineLocalizations(IEnumerable<ActionManifest> manifests)
        {
            var combinedLocalizations = new Dictionary<string, Dictionary<string, string>>();

            foreach (ActionManifest manifest in manifests)
            {
                foreach (Dictionary<string, string> language in manifest.localization)
                {
                    if (!language.ContainsKey("language_tag"))
                    {
                        continue;
                    }

                    if (!combinedLocalizations.ContainsKey(language["language_tag"]))
                    {
                        combinedLocalizations.Add(language["language_tag"], new Dictionary<string, string>() { { "language_tag", language["language_tag"] } });
                    }

                    foreach (KeyValuePair<string, string> kvp in language.Where(kvp => kvp.Key != "language_tag"))
                    {
                        if (combinedLocalizations.ContainsKey(kvp.Key))
                        {
                            Logger.Warn($"Duplicate entry '{kvp.Key}'");
                        }
                        else
                        {
                            combinedLocalizations[language["language_tag"]].Add(kvp.Key, kvp.Value);
                        }
                    }
                }
            }

            return combinedLocalizations.Values.ToList();
        }
    }
}
