﻿// <copyright file="OVRInput.cs" company="Nicolas Gnyra">
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
    public abstract class OVRInput : OVRAction
    {
        protected OVRInput(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Gets a value indicating whether this action is bound to an input source that is present in the system and is in an action set that is active or not.
        /// </summary>
        public abstract bool isActive { get; }

        /// <summary>
        /// Update data from OpenVR. Called every frame.
        /// </summary>
        internal abstract void UpdateData();
    }
}
