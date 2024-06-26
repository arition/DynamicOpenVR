// <copyright file="HapticVibrationOutput.cs" company="Nicolas Gnyra">
// DynamicOpenVR - Unity scripts to allow dynamic creation of OpenVR actions at runtime.
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

namespace DynamicOpenVR.IO
{
    public class HapticVibrationOutput : OVRAction
    {
        public HapticVibrationOutput(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Triggers a haptic vibration action.
        /// </summary>
        /// <param name="durationSeconds">How long to trigger the haptic event for.</param>
        /// <param name="magnitude">The magnitude of the haptic event. This value must be between 0.0 and 1.0.</param>
        /// <param name="frequency">The frequency in cycles per second of the haptic event.</param>
        public void TriggerHapticVibration(float durationSeconds, float magnitude, float frequency = 150f)
        {
            OpenVRFacade.TriggerHapticVibrationAction(handle, 0, durationSeconds, frequency, magnitude);
        }
    }
}
