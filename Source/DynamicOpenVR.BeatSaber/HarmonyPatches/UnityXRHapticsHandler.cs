// <copyright file="UnityXRHapticsHandler.cs" company="Nicolas Gnyra">
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DynamicOpenVR.IO;
using HarmonyLib;
using UnityEngine.XR;

namespace DynamicOpenVR.BeatSaber.HarmonyPatches
{
    internal static class UnityXRHapticsHandler
    {
        private static readonly MethodInfo kTargetMethod = AccessTools.Method(typeof(InputDevice), nameof(InputDevice.SendHapticImpulse));
        private static readonly MethodInfo kOverrideMethod = AccessTools.Method(typeof(UnityXRHapticsHandler), nameof(Handle));

        private static bool Handle(InputDevice instance, uint channel, float amplitude, float duration)
        {
            if (!instance.isValid || !instance.characteristics.HasFlag(InputDeviceCharacteristics.Controller) || !instance.characteristics.HasFlag(InputDeviceCharacteristics.HeldInHand))
            {
                return false;
            }

            HapticVibrationOutput output;

            if (instance.characteristics.HasFlag(InputDeviceCharacteristics.Left))
            {
                output = Plugin.beatSaberActions.leftHandHaptics;
            }
            else if (instance.characteristics.HasFlag(InputDeviceCharacteristics.Right))
            {
                output = Plugin.beatSaberActions.rightHandHaptics;
            }
            else
            {
                return true;
            }

            output.TriggerHapticVibration(duration, amplitude);

            return true;
        }

        [HarmonyPatch]
        internal static class KnucklesUnityXRHapticsHandler_HapticsCoroutine
        {
            private static readonly FieldInfo kTargetField = AccessTools.Field(AccessTools.TypeByName("KnucklesUnityXRHapticsHandler+<HapticsCoroutine>d__9"), "<device>5__2");
            private static readonly CodeMatch[] kCodeMatches = new[]
            {
                new CodeMatch(i => i.opcode == OpCodes.Ldflda && ((FieldInfo)i.operand) == kTargetField),
                new CodeMatch(i => i.opcode == OpCodes.Ldc_I4_0),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_1),
                new CodeMatch(i => i.opcode == OpCodes.Ldfld),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_1),
                new CodeMatch(i => i.opcode == OpCodes.Ldfld),
                new CodeMatch(i => i.opcode == OpCodes.Call && ((MethodInfo)i.operand) == kTargetMethod),
            };

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher cm = new CodeMatcher(instructions, generator).MatchForward(false, kCodeMatches);

                if (cm.IsInvalid)
                {
                    throw new InvalidOperationException("InputDevice.SendHapticImpulse parameters and call not found");
                }

                cm.RemoveInstruction();
                cm.Insert(new CodeInstruction(OpCodes.Ldfld, kTargetField));

                cm.MatchForward(false, kCodeMatches.Last());

                cm.RemoveInstruction();
                cm.Insert(new CodeInstruction(OpCodes.Call, kOverrideMethod));

                return cm.InstructionEnumeration();
            }

            // since the IEnumerator class is compiler-generated with numbers that may vary between versions added to the end, we look for the type based on the method's name
            public static MethodBase TargetMethod() => AccessTools.Method(typeof(KnucklesUnityXRHapticsHandler).GetNestedTypes(BindingFlags.NonPublic).First(t => t.Name.StartsWith("<HapticsCoroutine>")), "MoveNext");
        }

        [HarmonyPatch(typeof(DefaultUnityXRHapticsHandler), "TriggerHapticPulse")]
        internal static class DefaultUnityXRHapticsHandler_TriggerHapticPulse
        {
            private static readonly CodeMatch[] kCodeMatches = new[]
            {
                new CodeMatch(i => i.opcode == OpCodes.Ldloca_S && ((LocalVariableInfo)i.operand).LocalType == typeof(InputDevice)),
                new CodeMatch(i => i.opcode == OpCodes.Ldc_I4_0),
                new CodeMatch(i => i.opcode == OpCodes.Ldarg_1),
                new CodeMatch(i => i.opcode == OpCodes.Ldarg_2),
                new CodeMatch(i => i.opcode == OpCodes.Call && ((MethodInfo)i.operand) == kTargetMethod),
            };

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher cm = new CodeMatcher(instructions, generator).MatchForward(false, kCodeMatches);

                if (cm.IsInvalid)
                {
                    throw new InvalidOperationException("InputDevice.SendHapticImpulse parameters and call not found");
                }

                cm.RemoveInstruction();
                cm.Insert(new CodeInstruction(OpCodes.Ldloc_0));

                cm.MatchForward(false, kCodeMatches.Last());

                cm.RemoveInstruction();
                cm.Insert(new CodeInstruction(OpCodes.Call, kOverrideMethod));

                return cm.InstructionEnumeration();
            }
        }
    }
}
