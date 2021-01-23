// DynamicOpenVR - Unity scripts to allow dynamic creation of OpenVR actions at runtime.
// Copyright � 2019-2021 Nicolas Gnyra

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see http://www.gnu.org/licenses/.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using DynamicOpenVR.Logging;

namespace DynamicOpenVR.IO
{
    public abstract class OVRAction : IDisposable
    {
        private static readonly Regex kNameRegex = new Regex(@"^\/actions\/[a-z0-9_-]+\/(?:in|out)\/[a-z0-9_-]+$", RegexOptions.IgnoreCase);

        public string id => ((uint)GetHashCode()).ToString(); // this might change at some point
        public string name { get; }
        internal ulong handle { get; private set; }

        protected OVRAction(string name)
        {
            if (!kNameRegex.IsMatch(name))
            {
                throw new Exception($"Unexpected action name '{name}'; name should only contain letters, numbers, dashes, and underscores.");
            }

            this.name = name.ToLowerInvariant();
            OpenVRActionManager.instance.RegisterAction(this);
        }

        internal string GetActionSetName()
        {
            return string.Join("/", name.Split('/').Take(3));
        }

        internal void UpdateHandle()
        {
            handle = OpenVRFacade.GetActionHandle(name);

            if (handle <= 0)
            {
                Logger.Error($"Got invalid handle for action '{name}'. Make sure it is defined in the action manifest and try again.");
                OpenVRActionManager.instance.DeregisterAction(this);
            }
        }

        public void Dispose()
        {
            OpenVRActionManager.instance.DeregisterAction(this);
        }
    }
}
