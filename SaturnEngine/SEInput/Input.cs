using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaturnEngine.SEInput
{
    public enum InputDeviceType
    {
        Keyboard,
        Mouse,
        XBoxController,
        Gamepad,
        Touch,
        VRController,
        Other
    }
    public struct InputDeviceInfo
    {
        public InputDeviceType DeviceType;
        public string DeviceName;
        public ulong DeviceSTC;
        public bool IsConnected;
    }
    /// <summary>
    /// 整合键盘，鼠标，手柄，触摸等输入设备的输入系统，提供统一的接口供游戏使用。
    /// </summary>
    public class Input
    {
        /// <summary>
        /// 当硬件链接式触发，返回true则处理硬件输入事件，返回false则忽略硬件输入事件。
        /// </summary>
        /// <param name="deviceInfo">硬件信息</param>
        /// <returns>布尔值</returns>
        public delegate bool DeviceConnected(InputDeviceInfo deviceInfo);

        /// <summary>
        /// 硬件断开时触发
        /// </summary>
        /// <param name="deviceInfo">硬件信息</param>
        public delegate void DeviceDisconnected(InputDeviceInfo deviceInfo);

        public static event DeviceConnected? OnDeviceConnected;
        public static event DeviceDisconnected? OnDeviceDisconnected;


    }
}
