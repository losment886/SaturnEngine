using SaturnEngine.Platform;
using SaturnEngine.SEInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SaturnEngine.Platform.WinAPI;

namespace SaturnEngine.SEDevice
{
    public class SEKeyboard_WinHook : SEDevice
    {
        public SEKeyboard_WinHook()
        {

        }
        public override void Connect()
        {
            MainThreadQueue.Add(() => 
            {
                WinAPI.InputWin.Keyboard.KeyboardProcess += WinKeyboardProcess;
                if (WinAPI.InputWin.Keyboard.InstallHook())
                {
                    IsConnected = true;
                }
            })?.WaitForInvoke(2000);//wait 2 seconds for the hook to be installed
        }

        public override void Disconnect()
        {
            if (IsConnected) 
            {
                WinAPI.InputWin.Keyboard.UninstallHook();
                IsConnected = false;
            }
        }
        int WinKeyboardProcess(int wParam, InputWin.Keyboard.KeyboardEvent me)
        {
            return 0;
        }
    }
}
