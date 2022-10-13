﻿/* MIT License

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
    internal sealed class Input
    {
        public const int MAX_USERS_SUPPORTED = 2;

        public const int MAX_USERS              = 16;
        public const int FIRST_CUSTOM_BIND      = 16;
        public const int FIRST_LIGHTGUN_BIND    = (int)CustomBinds.ANALOG_BIND_LIST_END;
        public const int FIRST_MISC_CUSTOM_BIND = (int)CustomBinds.LIGHTGUN_BIND_LIST_END;
        public const int FIRST_META_KEY         = (int)CustomBinds.CUSTOM_BIND_LIST_END;

        public enum CustomBinds : uint
        {
            // Analogs (RETRO_DEVICE_ANALOG)
            ANALOG_LEFT_X_PLUS = FIRST_CUSTOM_BIND,
            ANALOG_LEFT_X_MINUS,
            ANALOG_LEFT_Y_PLUS,
            ANALOG_LEFT_Y_MINUS,
            ANALOG_RIGHT_X_PLUS,
            ANALOG_RIGHT_X_MINUS,
            ANALOG_RIGHT_Y_PLUS,
            ANALOG_RIGHT_Y_MINUS,
            ANALOG_BIND_LIST_END,

            // Lightgun
            LIGHTGUN_TRIGGER = FIRST_LIGHTGUN_BIND,
            LIGHTGUN_RELOAD,
            LIGHTGUN_AUX_A,
            LIGHTGUN_AUX_B,
            LIGHTGUN_AUX_C,
            LIGHTGUN_START,
            LIGHTGUN_SELECT,
            LIGHTGUN_DPAD_UP,
            LIGHTGUN_DPAD_DOWN,
            LIGHTGUN_DPAD_LEFT,
            LIGHTGUN_DPAD_RIGHT,
            LIGHTGUN_BIND_LIST_END,

            // Turbo
            TURBO_ENABLE = FIRST_MISC_CUSTOM_BIND,

            CUSTOM_BIND_LIST_END,

            // Command binds. Not related to game input, only usable for port 0.
            FAST_FORWARD_KEY = FIRST_META_KEY,
            FAST_FORWARD_HOLD_KEY,
            SLOWMOTION_KEY,
            SLOWMOTION_HOLD_KEY,
            LOAD_STATE_KEY,
            SAVE_STATE_KEY,
            FULLSCREEN_TOGGLE_KEY,
            QUIT_KEY,
            STATE_SLOT_PLUS,
            STATE_SLOT_MINUS,
            REWIND,
            BSV_RECORD_TOGGLE,
            PAUSE_TOGGLE,
            FRAMEADVANCE,
            RESET,
            SHADER_NEXT,
            SHADER_PREV,
            CHEAT_INDEX_PLUS,
            CHEAT_INDEX_MINUS,
            CHEAT_TOGGLE,
            SCREENSHOT,
            MUTE,
            OSK,
            FPS_TOGGLE,
            SEND_DEBUG_INFO,
            NETPLAY_HOST_TOGGLE,
            NETPLAY_GAME_WATCH,
            ENABLE_HOTKEY,
            VOLUME_UP,
            VOLUME_DOWN,
            OVERLAY_NEXT,
            DISK_EJECT_TOGGLE,
            DISK_NEXT,
            DISK_PREV,
            GRAB_MOUSE_TOGGLE,
            GAME_FOCUS_TOGGLE,
            UI_COMPANION_TOGGLE,

            MENU_TOGGLE,

            RECORDING_TOGGLE,
            STREAMING_TOGGLE,

            AI_SERVICE,

            BIND_LIST_END,
            BIND_LIST_END_NULL
        };

        public readonly retro_input_poll_t PollCallback;
        public readonly retro_input_state_t StateCallback;

        public readonly ControllersMap DeviceMap = new();

        public readonly retro_rumble_interface RumbleInterface = new()
        {
            set_rumble_state = (uint port, retro_rumble_effect effect, ushort strength) =>
            {
                Logger.Instance.LogDebug($"[Rumble] Port: {port} Effect: {effect} Strength: {strength}");
                return true;
            }
        };

        public bool HasInputDescriptors { get; private set; }

        public retro_keyboard_callback KeyboardCallback;

        public bool Enabled { get; set; }

        private readonly List<retro_input_descriptor> _inputDescriptors = new();
        private readonly List<retro_controller_info> _controllerInfo = new();
        private readonly List<retro_controller_description> _controllerDescriptions = new();
        private readonly string[,] _buttonDescriptions = new string[MAX_USERS, FIRST_META_KEY];

        private IInputProcessor _processor;

        public Input()
        {
            PollCallback  = PollCallbackCall;
            StateCallback = StateCallbackCall;
        }

        public void Init(IInputProcessor inputProcessor) =>
            _processor = inputProcessor;

        public void DeInit() =>
            _processor = null;

        public void PollCallbackCall()
        {
        }

        public short StateCallbackCall(uint port, RETRO_DEVICE device, uint index, uint id)
        {
            if (_processor == null || !Enabled)
                return 0;

            //device &= RETRO_DEVICE_MASK;
            return device switch
            {
                RETRO_DEVICE.JOYPAD   => ProcessJoypadDevice(port, (RETRO_DEVICE_ID_JOYPAD)id),
                RETRO_DEVICE.MOUSE    => ProcessMouseDevice(port, (RETRO_DEVICE_ID_MOUSE)id),
                RETRO_DEVICE.KEYBOARD => ProcessKeyboardDevice(port, (retro_key)id),
                RETRO_DEVICE.LIGHTGUN => ProcessLightgunDevice(port, (RETRO_DEVICE_ID_LIGHTGUN)id),
                RETRO_DEVICE.POINTER  => ProcessPointerDevice(port, (RETRO_DEVICE_ID_POINTER)id),
                RETRO_DEVICE.ANALOG   => ProcessAnalogDevice(port, (RETRO_DEVICE_INDEX_ANALOG)index, (RETRO_DEVICE_ID_ANALOG)id),
                _ => 0
            };
        }

        private short ProcessJoypadDevice(uint port, RETRO_DEVICE_ID_JOYPAD id) =>
            id is RETRO_DEVICE_ID_JOYPAD.MASK ? _processor.JoypadButtons((int)port) : _processor.JoypadButton((int)port, id);

        private short ProcessMouseDevice(uint port, RETRO_DEVICE_ID_MOUSE id) =>
            id switch
            {
                RETRO_DEVICE_ID_MOUSE.X => _processor.MouseX((int)port),
                RETRO_DEVICE_ID_MOUSE.Y => _processor.MouseY((int)port),

                RETRO_DEVICE_ID_MOUSE.WHEELUP
                or RETRO_DEVICE_ID_MOUSE.WHEELDOWN => _processor.MouseWheel((int)port),

                RETRO_DEVICE_ID_MOUSE.LEFT
                or RETRO_DEVICE_ID_MOUSE.RIGHT
                or RETRO_DEVICE_ID_MOUSE.MIDDLE
                or RETRO_DEVICE_ID_MOUSE.BUTTON_4
                or RETRO_DEVICE_ID_MOUSE.BUTTON_5 => _processor.MouseButton((int)port, id),

                _ => 0
            };

        private short ProcessKeyboardDevice(uint port, retro_key id) =>
            id < retro_key.RETROK_OEM_102 ? _processor.KeyboardKey((int)port, id) : (short)0;

        private short ProcessLightgunDevice(uint port, RETRO_DEVICE_ID_LIGHTGUN id) =>
            id switch
            {
                RETRO_DEVICE_ID_LIGHTGUN.X
                or RETRO_DEVICE_ID_LIGHTGUN.SCREEN_X => _processor.LightgunX((int)port),

                RETRO_DEVICE_ID_LIGHTGUN.Y
                or RETRO_DEVICE_ID_LIGHTGUN.SCREEN_Y => _processor.LightgunY((int)port),

                RETRO_DEVICE_ID_LIGHTGUN.IS_OFFSCREEN => BoolToShort(_processor.LightgunIsOffscreen((int)port)),

                RETRO_DEVICE_ID_LIGHTGUN.TRIGGER
                or RETRO_DEVICE_ID_LIGHTGUN.RELOAD
                or RETRO_DEVICE_ID_LIGHTGUN.AUX_A
                or RETRO_DEVICE_ID_LIGHTGUN.AUX_B
                or RETRO_DEVICE_ID_LIGHTGUN.START
                or RETRO_DEVICE_ID_LIGHTGUN.SELECT
                or RETRO_DEVICE_ID_LIGHTGUN.AUX_C
                or RETRO_DEVICE_ID_LIGHTGUN.DPAD_UP
                or RETRO_DEVICE_ID_LIGHTGUN.DPAD_DOWN
                or RETRO_DEVICE_ID_LIGHTGUN.DPAD_LEFT
                or RETRO_DEVICE_ID_LIGHTGUN.DPAD_RIGHT => _processor.LightgunButton((int)port, id),

                _ => 0
            };

        private short ProcessPointerDevice(uint port, RETRO_DEVICE_ID_POINTER id) =>
            id switch
            {
                RETRO_DEVICE_ID_POINTER.X => _processor.PointerX((int)port),
                RETRO_DEVICE_ID_POINTER.Y => _processor.PointerY((int)port),
                RETRO_DEVICE_ID_POINTER.PRESSED => _processor.PointerPressed((int)port),
                RETRO_DEVICE_ID_POINTER.COUNT => _processor.PointerCount((int)port),
                _ => 0
            };

        private short ProcessAnalogDevice(uint port, RETRO_DEVICE_INDEX_ANALOG index, RETRO_DEVICE_ID_ANALOG id) =>
            index switch
            {
                RETRO_DEVICE_INDEX_ANALOG.LEFT => id switch
                {
                    RETRO_DEVICE_ID_ANALOG.X => _processor.AnalogLeftX((int)port),
                    RETRO_DEVICE_ID_ANALOG.Y => _processor.AnalogLeftY((int)port),
                    _ => 0,
                },
                RETRO_DEVICE_INDEX_ANALOG.RIGHT => id switch
                {
                    RETRO_DEVICE_ID_ANALOG.X => _processor.AnalogRightX((int)port),
                    RETRO_DEVICE_ID_ANALOG.Y => _processor.AnalogRightY((int)port),
                    _ => 0,
                },
                _ => 0
            };

        private static short BoolToShort(bool boolValue) =>
            (short)(boolValue ? 1 : 0);

        public void SetInputDescriptors(ref IntPtr data)
        {
            _inputDescriptors.Clear();

            retro_input_descriptor descriptor = data.ToStructure<retro_input_descriptor>();
            while (descriptor is not null && !descriptor.desc.IsNull())
            {
                _inputDescriptors.Add(descriptor);

                if (descriptor.device is RETRO_DEVICE.JOYPAD)
                {
                    string desc = descriptor.desc.AsString();
                    _buttonDescriptions[descriptor.port, descriptor.id] = desc;
                }
                else if (descriptor.device is RETRO_DEVICE.ANALOG)
                {
                    RETRO_DEVICE_ID_ANALOG id = (RETRO_DEVICE_ID_ANALOG)descriptor.id;
                    switch (id)
                    {
                        case RETRO_DEVICE_ID_ANALOG.X:
                        {
                            RETRO_DEVICE_INDEX_ANALOG index = (RETRO_DEVICE_INDEX_ANALOG)descriptor.index;
                            switch (index)
                            {
                                case RETRO_DEVICE_INDEX_ANALOG.LEFT:
                                {
                                    string desc = descriptor.desc.AsString();
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_LEFT_X_PLUS] = desc;
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_LEFT_X_MINUS] = desc;
                                }
                                break;
                                case RETRO_DEVICE_INDEX_ANALOG.RIGHT:
                                {
                                    string desc = descriptor.desc.AsString();
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_RIGHT_X_PLUS] = desc;
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_RIGHT_X_MINUS] = desc;
                                }
                                break;
                            }
                        }
                        break;
                        case RETRO_DEVICE_ID_ANALOG.Y:
                        {
                            RETRO_DEVICE_INDEX_ANALOG index = (RETRO_DEVICE_INDEX_ANALOG)descriptor.index;
                            switch (index)
                            {
                                case RETRO_DEVICE_INDEX_ANALOG.LEFT:
                                {
                                    string desc = descriptor.desc.AsString();
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_LEFT_Y_PLUS] = desc;
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_LEFT_Y_MINUS] = desc;
                                }
                                break;
                                case RETRO_DEVICE_INDEX_ANALOG.RIGHT:
                                {
                                    string desc = descriptor.desc.AsString();
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_RIGHT_Y_PLUS] = desc;
                                    _buttonDescriptions[descriptor.port, (int)Input.CustomBinds.ANALOG_RIGHT_Y_MINUS] = desc;
                                }
                                break;
                            }
                        }
                        break;
                    }
                }

                data += Marshal.SizeOf(descriptor);
                data.ToStructure(descriptor);
            }

            HasInputDescriptors = _inputDescriptors.Count > 0;
        }

        public void SetControllerInfo(ref IntPtr data)
        {
            _controllerInfo.Clear();
            _controllerDescriptions.Clear();

            retro_controller_info controllerInfo = data.ToStructure<retro_controller_info>();
            while (!controllerInfo.types.IsNull())
            {
                _controllerInfo.Add(controllerInfo);

                for (int deviceIndex = 0; deviceIndex < controllerInfo.num_types; ++deviceIndex)
                {
                    retro_controller_description controllerDescription = controllerInfo.types.ToStructure<retro_controller_description>();
                    _controllerDescriptions.Add(controllerDescription);

                    controllerInfo.types += Marshal.SizeOf(controllerDescription);
                    controllerInfo.types.ToStructure(controllerDescription);
                }

                data += Marshal.SizeOf(controllerInfo);
                data.ToStructure(controllerInfo);
            }
        }
    }
}
