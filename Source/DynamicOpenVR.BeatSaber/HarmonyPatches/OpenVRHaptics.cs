// <copyright file="OpenVRHaptics.cs" company="Nicolas Gnyra">
// DynamicOpenVR.BeatSaber - An implementation of DynamicOpenVR as a Beat Saber plugin.
// Copyright © 2019-2023 Nicolas Gnyra
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

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.XR;

namespace DynamicOpenVR.BeatSaber.HarmonyPatches
{
    [HarmonyPatch]
    internal class OpenVrHaptics_TriggerHapticPulse
    {
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(XRNode node, float duration, float strength, float frequency)
        {
            if (node == XRNode.LeftHand)
            {
                Plugin.beatSaberActions.leftSliceHaptics.TriggerHapticVibration(duration, strength, frequency);
                return false;
            }

            if (node == XRNode.RightHand)
            {
                Plugin.beatSaberActions.rightSliceHaptics.TriggerHapticVibration(duration, strength, frequency);
                return false;
            }

            return true;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SimpleOpenVrOpenVrHaptics), nameof(SimpleOpenVrOpenVrHaptics.TriggerHapticPulse));
            yield return AccessTools.Method(typeof(ThreadedOpenVrOpenVrHaptics), nameof(ThreadedOpenVrOpenVrHaptics.TriggerHapticPulse));
        }
    }
}
