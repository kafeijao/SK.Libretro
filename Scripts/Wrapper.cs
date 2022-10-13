/* MIT License

 * Copyright (c) 2022 Skurdt
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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SK.Libretro
{
    internal sealed class Wrapper
    {
        public static readonly retro_log_level LogLevel = retro_log_level.RETRO_LOG_WARN;

        public static string MainDirectory        { get; private set; } = null;
        public static string CoresDirectory       { get; private set; } = null;
        public static string CoreOptionsDirectory { get; private set; } = null;
        public static string SystemDirectory      { get; private set; } = null;
        public static string CoreAssetsDirectory  { get; private set; } = null;
        public static string SavesDirectory       { get; private set; } = null;
        public static string StatesDirectory      { get; private set; } = null;
        public static string TempDirectory        { get; private set; } = null;

        public readonly WrapperSettings Settings;
        public readonly EnvironmentVariables EnvironmentVariables;
        public readonly Core Core;
        public readonly Game Game;
        public readonly Environment Environment;
        public Graphics Graphics { get; private set; }
        public readonly Audio Audio;
        public readonly Input Input;
        public readonly Serialization Serialization;

        public readonly retro_log_printf_t LogPrintfCallback;

        public bool RewindEnabled = false;
        public bool PerformRewind = false;

        public OpenGLHelperWindow OpenGLHelperWindow;
        public retro_hw_render_callback HwRenderInterface;
        public retro_frame_time_callback FrameTimeInterface;
        public retro_frame_time_callback_t FrameTimeInterfaceCallback;
        public DiskInterface Disk;
        public PerfInterface Perf;
        public LedInterface Led;
        public MemoryMap Memory;

        public bool UpdateVariables = false;

        //private const int REWIND_FRAMES_INTERVAL = 10;

        private readonly List<IntPtr> _unsafeStrings = new();

        private long _frameTimeLast      = 0;
        //private uint _totalFrameCount    = 0;

        public unsafe Wrapper(WrapperSettings settings)
        {
            Settings = settings;

            if (MainDirectory is null)
            {
                MainDirectory        = FileSystem.GetOrCreateDirectory(!string.IsNullOrWhiteSpace(settings.RootDirectory) ? settings.RootDirectory : "libretro");
                CoresDirectory       = FileSystem.GetOrCreateDirectory($"{MainDirectory}/cores");
                CoreOptionsDirectory = FileSystem.GetOrCreateDirectory($"{MainDirectory}/core_options");
                SystemDirectory      = FileSystem.GetOrCreateDirectory($"{MainDirectory}/system");
                CoreAssetsDirectory  = FileSystem.GetOrCreateDirectory($"{MainDirectory}/core_assets");
                SavesDirectory       = FileSystem.GetOrCreateDirectory($"{MainDirectory}/saves");
                StatesDirectory      = FileSystem.GetOrCreateDirectory($"{MainDirectory}/states");
                TempDirectory        = FileSystem.GetOrCreateDirectory($"{MainDirectory}/temp");
            }

            Core = new(this);
            Game = new(this);

            EnvironmentVariables = new();
            Environment          = new(this);
            Graphics             = new();
            Audio                = new(this);
            Input                = new();
            Serialization        = new(this);

            LogPrintfCallback = LogInterface.RetroLogPrintf;
        }

        public bool StartContent(string coreName, string gameDirectory, string gameName)
        {
            if (string.IsNullOrWhiteSpace(coreName))
                return false;

            if (!Core.Start(coreName))
            {
                StopContent();
                return false;
            }

            if (FrameTimeInterface.callback.IsNotNull())
                FrameTimeInterfaceCallback = Marshal.GetDelegateForFunctionPointer<retro_frame_time_callback_t>(FrameTimeInterface.callback);

            if (!Game.Start(gameDirectory, gameName))
            {
                StopContent();
                return false;
            }

            FrameTimeRestart();

            ulong size = Core.retro_serialize_size();
            if (size > 0)
                Serialization.SetStateSize(size);

            return true;
        }

        public void StopContent()
        {
            Input.DeInit();
            Audio.DeInit();
            Graphics?.Dispose();

            Game.Stop();
            Core.Stop();

            OpenGLHelperWindow?.Dispose();

            PointerUtilities.Free(_unsafeStrings);
        }

        public void InitHardwareContext() =>
            HwRenderInterface.context_reset.GetDelegate<retro_hw_context_reset_t>()
                                           .Invoke();

        public void RunFrame()
        {
            if (!Game.Running || !Core.Initialized)
                return;

            if (EnvironmentVariables.HwAccelerated)
                GLFW.PollEvents();

            //_totalFrameCount++;

            FrameTimeUpdate();

            //if (RewindEnabled)
            //{
            //    if (PerformRewind)
            //        Serialization.RewindLoadState();
            //    else if (_totalFrameCount % REWIND_FRAMES_INTERVAL == 0)
            //        Serialization.RewindSaveState();
            //}

            Core.retro_run();
        }

        public void InitGraphics(GraphicsFrameHandlerBase graphicsFrameHandler, bool enabled) =>
            Graphics.Init(graphicsFrameHandler, enabled);

        public IntPtr GetUnsafeString(string source)
        {
            IntPtr ptr = source.AsAllocatedPtr();
            _unsafeStrings.Add(ptr);
            return ptr;
        }

        private void FrameTimeRestart() => _frameTimeLast = System.Diagnostics.Stopwatch.GetTimestamp();

        private void FrameTimeUpdate()
        {
            if (FrameTimeInterfaceCallback == null)
                return;

            long current = System.Diagnostics.Stopwatch.GetTimestamp();
            long delta   = current - _frameTimeLast;

            if (_frameTimeLast <= 0)
                delta = FrameTimeInterface.reference;
            _frameTimeLast = current;
            FrameTimeInterfaceCallback(delta * 1000);
        }
    }
}
