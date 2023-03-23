// <copyright file="UnityXRHelper.cs" company="Nicolas Gnyra">
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

using System;
using DynamicOpenVR.IO;
using HarmonyLib;
using UnityEngine.XR;

namespace DynamicOpenVR.BeatSaber.HarmonyPatches
{
    [HarmonyPatch(typeof(UnityXRHelper), nameof(UnityXRHelper.TriggerHapticPulse))]
    internal static class UnityXRHelper_TriggerHapticPulse
    {
        public static bool Prefix(XRNode node, float duration, float strength, float frequency)
        {
            HapticVibrationOutput output = node switch
            {
                XRNode.LeftHand => Plugin.beatSaberActions.leftSliceHaptics,
                XRNode.RightHand => Plugin.beatSaberActions.rightSliceHaptics,
                _ => throw new InvalidOperationException($"Unexpected node '{node}'"),
            };

            output.TriggerHapticVibration(duration, strength, frequency);

            return false;
        }
    }
}
