﻿// <copyright file="OpenVRHelper.cs" company="Nicolas Gnyra">
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
using IPA.Utilities;
using UnityEngine;
using UnityEngine.XR;

namespace DynamicOpenVR.BeatSaber
{
    internal class OpenVRHelper : UnityXRHelper
    {
        private static readonly PropertyAccessor<UnityXRHelper, bool>.Setter kUnityXRHelperUserPresenceSetter = PropertyAccessor<UnityXRHelper, bool>.GetSetter("userPresence");

        public override Vector2 GetAnyJoystickMaxAxis()
        {
            return new Vector2(
                AbsMax(Plugin.beatSaberActions.leftThumbstick.vector.x, Plugin.beatSaberActions.rightThumbstick.vector.x),
                AbsMax(Plugin.beatSaberActions.leftThumbstick.vector.y, Plugin.beatSaberActions.rightThumbstick.vector.y));
        }

        public override bool GetMenuButton()
        {
            return Plugin.beatSaberActions.leftMenuButton.state || Plugin.beatSaberActions.rightMenuButton.state;
        }

        public override bool GetMenuButtonDown()
        {
            return Plugin.beatSaberActions.leftMenuButton.activeChange || Plugin.beatSaberActions.rightMenuButton.activeChange;
        }

        public override bool GetNodePose(XRNode nodeType, int idx, out Vector3 pos, out Quaternion rot)
        {
            PoseInput poseInput = nodeType switch
            {
                XRNode.LeftHand => Plugin.beatSaberActions.leftHandPose,
                XRNode.RightHand => Plugin.beatSaberActions.rightHandPose,
                _ => null,
            };

            if (poseInput == null)
            {
                pos = Vector3.zero;
                rot = Quaternion.identity;
                return false;
            }

            pos = poseInput.position;
            rot = poseInput.rotation;
            return true;
        }

        public override Vector2 GetThumbstickValue(XRNode node)
        {
            Vector2Input input = node switch
            {
                XRNode.LeftHand => Plugin.beatSaberActions.leftThumbstick,
                XRNode.RightHand => Plugin.beatSaberActions.rightThumbstick,
                _ => throw new InvalidOperationException($"Unexpected node '{node}'"),
            };

            if (input == null)
            {
                return Vector2.zero;
            }

            return input.vector;
        }

        public override float GetTriggerValue(XRNode node)
        {
            VectorInput input = node switch
            {
                XRNode.LeftHand => Plugin.beatSaberActions.leftTrigger,
                XRNode.RightHand => Plugin.beatSaberActions.rightTrigger,
                _ => throw new InvalidOperationException($"Unexpected node '{node}'"),
            };

            if (input == null)
            {
                return 0;
            }

            return input.value;
        }

        public override void StopHaptics(XRNode node)
        {
        }

        public override void TriggerHapticPulse(XRNode node, float duration, float strength, float frequency)
        {
            HapticVibrationOutput output = node switch
            {
                XRNode.LeftHand => Plugin.beatSaberActions.leftSliceHaptics,
                XRNode.RightHand => Plugin.beatSaberActions.rightSliceHaptics,
                _ => throw new InvalidOperationException($"Unexpected node '{node}'"),
            };

            output.TriggerHapticVibration(duration, strength, frequency);
        }

        private void Update()
        {
            BooleanInput headsetOnHead = Plugin.beatSaberActions.headsetOnHead;

            if (headsetOnHead.activeChange || headsetOnHead.inactiveChange)
            {
                UnityXRHelper self = this;
                kUnityXRHelperUserPresenceSetter(ref self, headsetOnHead.state);
            }
        }

        private float AbsMax(float a, float b) => Mathf.Abs(a) > Mathf.Abs(b) ? a : b;
    }
}
