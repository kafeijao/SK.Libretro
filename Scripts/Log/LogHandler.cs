﻿/* MIT License

 * Copyright (c) 2021-2022 Skurdt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. */

using SK.Libretro.Header;
using System;
using System.Runtime.InteropServices;

namespace SK.Libretro
{
    internal class LogHandler : ILogger
    {
        protected LogLevel _level = LogLevel.Warning;

        private readonly ILogProcessor _logProcessor;
        private readonly retro_log_printf_t _logPrintf;

        public LogHandler(ILogProcessor processor)
        {
            _logProcessor = processor ?? new NullLogProcessor();
            _logPrintf    = LogPrintf;
        }

        public void SetLogLevel(LogLevel level) => _level = level;

        public void LogDebug(string message, string caller = null)
        {
            (string prefix, string callerFormatted) = GetFormattedPrefixAndCaller(LogLevel.Debug, caller);
            _logProcessor.LogDebug($"{prefix} {callerFormatted}{message}");
        }

        public void LogInfo(string message, string caller = null)
        {
            (string prefix, string callerFormatted) = GetFormattedPrefixAndCaller(LogLevel.Info, caller);
            _logProcessor.LogInfo($"{prefix} {callerFormatted}{message}");
        }

        public void LogWarning(string message, string caller = null)
        {
            (string prefix, string callerFormatted) = GetFormattedPrefixAndCaller(LogLevel.Warning, caller);
            _logProcessor.LogWarning($"{prefix} {callerFormatted}{message}");
        }

        public void LogError(string message, string caller = null)
        {
            (string prefix, string callerFormatted) = GetFormattedPrefixAndCaller(LogLevel.Error, caller);
            _logProcessor.LogError($"{prefix} {callerFormatted}{message}");
        }

        public void LogException(Exception exception, string caller = null) => _logProcessor.LogException(exception);

        public bool GetLogInterface(IntPtr data)
        {
            if (data.IsNull())
                return false;

            retro_log_callback logCallback = data.ToStructure<retro_log_callback>();
            logCallback.log = _logPrintf.GetFunctionPointer();
            Marshal.StructureToPtr(logCallback, data, false);
            return true;
        }

        protected virtual void LogPrintf(retro_log_level level,
                                         string format,
                                         IntPtr arg1,
                                         IntPtr arg2,
                                         IntPtr arg3,
                                         IntPtr arg4,
                                         IntPtr arg5,
                                         IntPtr arg6,
                                         IntPtr arg7,
                                         IntPtr arg8,
                                         IntPtr arg9,
                                         IntPtr arg10,
                                         IntPtr arg11,
                                         IntPtr arg12)
        {
            LogLevel logLevel = level.ToLogLevel();
            if (logLevel >= _level)
                LogMessage(logLevel, format);
        }

        protected void LogMessage(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    LogDebug(message);
                    break;
                case LogLevel.Info:
                    LogInfo(message);
                    break;
                case LogLevel.Warning:
                    LogWarning(message);
                    break;
                case LogLevel.Error:
                    LogError(message);
                    break;
            }
        }

        private (string prefixFormatted, string callerFormatted) GetFormattedPrefixAndCaller(LogLevel logLevel, string caller)
        {
            string color = logLevel switch
            {
                LogLevel.Info         => "yellow",
                LogLevel.Warning      => "orange",
                LogLevel.Error
                or LogLevel.Exception => "red",
                LogLevel.Debug
                or _                  => "white"
            };
            string prefix = _logProcessor.SupportsColorTags ? $"[<color={color}>{LogLevel.Debug.ToStringUpperFast()}</color>]" : $"[{LogLevel.Debug.ToStringUpperFast()}]";
            caller = !string.IsNullOrWhiteSpace(caller) ? (_logProcessor.SupportsColorTags ? $"<color=lightblue>[{caller}]</color> " : $"[{caller}] ") : "";
            return (prefix, caller);
        }
    }
}