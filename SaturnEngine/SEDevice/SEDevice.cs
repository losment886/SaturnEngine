using SaturnEngine.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaturnEngine.SEDevice
{

    public abstract class SEDevice : SEBase
    {
        public enum SEDeviceType
        {
            Keyboard,
            Mouse,
            Gamepad,
            TouchScreen,
            VRController,
            Other
        }
        public SEDeviceType Type { get; internal set; }
        public bool IsConnected { get; internal set; }
        public bool Enabled { get; internal set; }
        public abstract void Connect();
        public abstract void Disconnect();
    }
}
