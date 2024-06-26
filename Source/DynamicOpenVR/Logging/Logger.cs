﻿// <copyright file="Logger.cs" company="Nicolas Gnyra">
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

namespace DynamicOpenVR.Logging
{
    public static class Logger
    {
        public static ILogHandler handler { get; set; } = new UnityDebugLogHandler();

        internal static void Trace(object message) => handler.Trace(message);

        internal static void Debug(object message) => handler.Debug(message);

        internal static void Info(object message) => handler.Info(message);

        internal static void Notice(object message) => handler.Notice(message);

        internal static void Warn(object message) => handler.Warn(message);

        internal static void Error(object message) => handler.Error(message);

        internal static void Critical(object message) => handler.Critical(message);
    }
}
