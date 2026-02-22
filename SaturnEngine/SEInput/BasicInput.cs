using SaturnEngine.SEMath;
using Silk.NET.GLFW;
using static SaturnEngine.Platform.WinAPI;

namespace SaturnEngine.SEInput
{
    public enum Keys
    {
        //
        // 摘要:
        //     The bitmask to extract modifiers from a key value.
        Modifiers = -65536,
        //
        // 摘要:
        //     No key pressed.
        None = 0,
        //
        // 摘要:
        //     The left mouse button.
        LButton = 1,
        //
        // 摘要:
        //     The right mouse button.
        RButton = 2,
        //
        // 摘要:
        //     The CANCEL key.
        Cancel = 3,
        //
        // 摘要:
        //     The middle mouse button (three-button mouse).
        MButton = 4,
        //
        // 摘要:
        //     The first x mouse button (five-button mouse).
        XButton1 = 5,
        //
        // 摘要:
        //     The second x mouse button (five-button mouse).
        XButton2 = 6,
        //
        // 摘要:
        //     The BACKSPACE key.
        Back = 8,
        //
        // 摘要:
        //     The TAB key.
        Tab = 9,
        //
        // 摘要:
        //     The LINEFEED key.
        LineFeed = 10,
        //
        // 摘要:
        //     The CLEAR key.
        Clear = 12,
        //
        // 摘要:
        //     The RETURN key.
        Return = 13,
        //
        // 摘要:
        //     The ENTER key.
        Enter = 13,
        //
        // 摘要:
        //     The SHIFT key.
        ShiftKey = 16,
        //
        // 摘要:
        //     The CTRL key.
        ControlKey = 17,
        //
        // 摘要:
        //     The ALT key.
        Menu = 18,
        //
        // 摘要:
        //     The PAUSE key.
        Pause = 19,
        //
        // 摘要:
        //     The CAPS LOCK key.
        Capital = 20,
        //
        // 摘要:
        //     The CAPS LOCK key.
        CapsLock = 20,
        //
        // 摘要:
        //     The IME Kana mode key.
        KanaMode = 21,
        //
        // 摘要:
        //     The IME Hanguel mode key. (maintained for compatibility; use HangulMode)
        HanguelMode = 21,
        //
        // 摘要:
        //     The IME Hangul mode key.
        HangulMode = 21,
        //
        // 摘要:
        //     The IME Junja mode key.
        JunjaMode = 23,
        //
        // 摘要:
        //     The IME final mode key.
        FinalMode = 24,
        //
        // 摘要:
        //     The IME Hanja mode key.
        HanjaMode = 25,
        //
        // 摘要:
        //     The IME Kanji mode key.
        KanjiMode = 25,
        //
        // 摘要:
        //     The ESC key.
        Escape = 27,
        //
        // 摘要:
        //     The IME convert key.
        IMEConvert = 28,
        //
        // 摘要:
        //     The IME nonconvert key.
        IMENonconvert = 29,
        //
        // 摘要:
        //     The IME accept key, replaces System.Windows.Forms.Keys.IMEAceept.
        IMEAccept = 30,
        //
        // 摘要:
        //     The IME accept key. Obsolete, use System.Windows.Forms.Keys.IMEAccept instead.
        IMEAceept = 30,
        //
        // 摘要:
        //     The IME mode change key.
        IMEModeChange = 31,
        //
        // 摘要:
        //     The SPACEBAR key.
        Space = 32,
        //
        // 摘要:
        //     The PAGE UP key.
        Prior = 33,
        //
        // 摘要:
        //     The PAGE UP key.
        PageUp = 33,
        //
        // 摘要:
        //     The PAGE DOWN key.
        Next = 34,
        //
        // 摘要:
        //     The PAGE DOWN key.
        PageDown = 34,
        //
        // 摘要:
        //     The END key.
        End = 35,
        //
        // 摘要:
        //     The HOME key.
        Home = 36,
        //
        // 摘要:
        //     The LEFT ARROW key.
        Left = 37,
        //
        // 摘要:
        //     The UP ARROW key.
        Up = 38,
        //
        // 摘要:
        //     The RIGHT ARROW key.
        Right = 39,
        //
        // 摘要:
        //     The DOWN ARROW key.
        Down = 40,
        //
        // 摘要:
        //     The SELECT key.
        Select = 41,
        //
        // 摘要:
        //     The PRINT key.
        Print = 42,
        //
        // 摘要:
        //     The EXECUTE key.
        Execute = 43,
        //
        // 摘要:
        //     The PRINT SCREEN key.
        Snapshot = 44,
        //
        // 摘要:
        //     The PRINT SCREEN key.
        PrintScreen = 44,
        //
        // 摘要:
        //     The INS key.
        Insert = 45,
        //
        // 摘要:
        //     The DEL key.
        Delete = 46,
        //
        // 摘要:
        //     The HELP key.
        Help = 47,
        //
        // 摘要:
        //     The 0 key.
        D0 = 48,
        //
        // 摘要:
        //     The 1 key.
        D1 = 49,
        //
        // 摘要:
        //     The 2 key.
        D2 = 50,
        //
        // 摘要:
        //     The 3 key.
        D3 = 51,
        //
        // 摘要:
        //     The 4 key.
        D4 = 52,
        //
        // 摘要:
        //     The 5 key.
        D5 = 53,
        //
        // 摘要:
        //     The 6 key.
        D6 = 54,
        //
        // 摘要:
        //     The 7 key.
        D7 = 55,
        //
        // 摘要:
        //     The 8 key.
        D8 = 56,
        //
        // 摘要:
        //     The 9 key.
        D9 = 57,
        /// <summary>
        /// The semicolon key.
        /// </summary>
        Semicolon = 59 /* ; */,

        /// <summary>
        /// The equal key.
        /// </summary>
        Equal = 61 /* = */,
        //
        // 摘要:
        //     The A key.
        A = 65,
        //
        // 摘要:
        //     The B key.
        B = 66,
        //
        // 摘要:
        //     The C key.
        C = 67,
        //
        // 摘要:
        //     The D key.
        D = 68,
        //
        // 摘要:
        //     The E key.
        E = 69,
        //
        // 摘要:
        //     The F key.
        F = 70,
        //
        // 摘要:
        //     The G key.
        G = 71,
        //
        // 摘要:
        //     The H key.
        H = 72,
        //
        // 摘要:
        //     The I key.
        I = 73,
        //
        // 摘要:
        //     The J key.
        J = 74,
        //
        // 摘要:
        //     The K key.
        K = 75,
        //
        // 摘要:
        //     The L key.
        L = 76,
        //
        // 摘要:
        //     The M key.
        M = 77,
        //
        // 摘要:
        //     The N key.
        N = 78,
        //
        // 摘要:
        //     The O key.
        O = 79,
        //
        // 摘要:
        //     The P key.
        P = 80,
        //
        // 摘要:
        //     The Q key.
        Q = 81,
        //
        // 摘要:
        //     The R key.
        R = 82,
        //
        // 摘要:
        //     The S key.
        S = 83,
        //
        // 摘要:
        //     The T key.
        T = 84,
        //
        // 摘要:
        //     The U key.
        U = 85,
        //
        // 摘要:
        //     The V key.
        V = 86,
        //
        // 摘要:
        //     The W key.
        W = 87,
        //
        // 摘要:
        //     The X key.
        X = 88,
        //
        // 摘要:
        //     The Y key.
        Y = 89,
        //
        // 摘要:
        //     The Z key.
        Z = 90,
        //
        // 摘要:
        //     The left Windows logo key (Microsoft Natural Keyboard).
        LWin = 91,
        //
        // 摘要:
        //     The right Windows logo key (Microsoft Natural Keyboard).
        RWin = 92,
        //
        // 摘要:
        //     The application key (Microsoft Natural Keyboard).
        Apps = 93,
        //
        // 摘要:
        //     The computer sleep key.
        Sleep = 95,
        //
        // 摘要:
        //     The 0 key on the numeric keypad.
        NumPad0 = 96,
        //
        // 摘要:
        //     The 1 key on the numeric keypad.
        NumPad1 = 97,
        //
        // 摘要:
        //     The 2 key on the numeric keypad.
        NumPad2 = 98,
        //
        // 摘要:
        //     The 3 key on the numeric keypad.
        NumPad3 = 99,
        //
        // 摘要:
        //     The 4 key on the numeric keypad.
        NumPad4 = 100,
        //
        // 摘要:
        //     The 5 key on the numeric keypad.
        NumPad5 = 101,
        //
        // 摘要:
        //     The 6 key on the numeric keypad.
        NumPad6 = 102,
        //
        // 摘要:
        //     The 7 key on the numeric keypad.
        NumPad7 = 103,
        //
        // 摘要:
        //     The 8 key on the numeric keypad.
        NumPad8 = 104,
        //
        // 摘要:
        //     The 9 key on the numeric keypad.
        NumPad9 = 105,
        //
        // 摘要:
        //     The multiply key.
        Multiply = 106,
        //
        // 摘要:
        //     The add key.
        Add = 107,
        //
        // 摘要:
        //     The separator key.
        Separator = 108,
        //
        // 摘要:
        //     The subtract key.
        Subtract = 109,
        //
        // 摘要:
        //     The decimal key.
        Decimal = 110,
        //
        // 摘要:
        //     The divide key.
        Divide = 111,
        //
        // 摘要:
        //     The F1 key.
        F1 = 112,
        //
        // 摘要:
        //     The F2 key.
        F2 = 113,
        //
        // 摘要:
        //     The F3 key.
        F3 = 114,
        //
        // 摘要:
        //     The F4 key.
        F4 = 115,
        //
        // 摘要:
        //     The F5 key.
        F5 = 116,
        //
        // 摘要:
        //     The F6 key.
        F6 = 117,
        //
        // 摘要:
        //     The F7 key.
        F7 = 118,
        //
        // 摘要:
        //     The F8 key.
        F8 = 119,
        //
        // 摘要:
        //     The F9 key.
        F9 = 120,
        //
        // 摘要:
        //     The F10 key.
        F10 = 121,
        //
        // 摘要:
        //     The F11 key.
        F11 = 122,
        //
        // 摘要:
        //     The F12 key.
        F12 = 123,
        //
        // 摘要:
        //     The F13 key.
        F13 = 124,
        //
        // 摘要:
        //     The F14 key.
        F14 = 125,
        //
        // 摘要:
        //     The F15 key.
        F15 = 126,
        //
        // 摘要:
        //     The F16 key.
        F16 = 127,
        //
        // 摘要:
        //     The F17 key.
        F17 = 128,
        //
        // 摘要:
        //     The F18 key.
        F18 = 129,
        //
        // 摘要:
        //     The F19 key.
        F19 = 130,
        //
        // 摘要:
        //     The F20 key.
        F20 = 131,
        //
        // 摘要:
        //     The F21 key.
        F21 = 132,
        //
        // 摘要:
        //     The F22 key.
        F22 = 133,
        //
        // 摘要:
        //     The F23 key.
        F23 = 134,
        //
        // 摘要:
        //     The F24 key.
        F24 = 135,
        //
        // 摘要:
        //     The NUM LOCK key.
        NumLock = 144,
        //
        // 摘要:
        //     The SCROLL LOCK key.
        Scroll = 145,
        //
        // 摘要:
        //     The left SHIFT key.
        LShiftKey = 160,
        //
        // 摘要:
        //     The right SHIFT key.
        RShiftKey = 161,
        //
        // 摘要:
        //     The left CTRL key.
        LControlKey = 162,
        //
        // 摘要:
        //     The right CTRL key.
        RControlKey = 163,
        //
        // 摘要:
        //     The left ALT key.
        LMenu = 164,
        //
        // 摘要:
        //     The right ALT key.
        RMenu = 165,
        //
        // 摘要:
        //     The browser back key.
        BrowserBack = 166,
        //
        // 摘要:
        //     The browser forward key.
        BrowserForward = 167,
        //
        // 摘要:
        //     The browser refresh key.
        BrowserRefresh = 168,
        //
        // 摘要:
        //     The browser stop key.
        BrowserStop = 169,
        //
        // 摘要:
        //     The browser search key.
        BrowserSearch = 170,
        //
        // 摘要:
        //     The browser favorites key.
        BrowserFavorites = 171,
        //
        // 摘要:
        //     The browser home key.
        BrowserHome = 172,
        //
        // 摘要:
        //     The volume mute key.
        VolumeMute = 173,
        //
        // 摘要:
        //     The volume down key.
        VolumeDown = 174,
        //
        // 摘要:
        //     The volume up key.
        VolumeUp = 175,
        //
        // 摘要:
        //     The media next track key.
        MediaNextTrack = 176,
        //
        // 摘要:
        //     The media previous track key.
        MediaPreviousTrack = 177,
        //
        // 摘要:
        //     The media Stop key.
        MediaStop = 178,
        //
        // 摘要:
        //     The media play pause key.
        MediaPlayPause = 179,
        //
        // 摘要:
        //     The launch mail key.
        LaunchMail = 180,
        //
        // 摘要:
        //     The select media key.
        SelectMedia = 181,
        //
        // 摘要:
        //     The start application one key.
        LaunchApplication1 = 182,
        //
        // 摘要:
        //     The start application two key.
        LaunchApplication2 = 183,
        //
        // 摘要:
        //     The OEM Semicolon key on a US standard keyboard.
        OemSemicolon = 186,
        //
        // 摘要:
        //     The OEM 1 key.
        Oem1 = 186,
        //
        // 摘要:
        //     The OEM plus key on any country/region keyboard.
        Oemplus = 187,
        //
        // 摘要:
        //     The OEM comma key on any country/region keyboard.
        Oemcomma = 188,
        //
        // 摘要:
        //     The OEM minus key on any country/region keyboard.
        OemMinus = 189,
        //
        // 摘要:
        //     The OEM period key on any country/region keyboard.
        OemPeriod = 190,
        //
        // 摘要:
        //     The OEM question mark key on a US standard keyboard.
        OemQuestion = 191,
        //
        // 摘要:
        //     The OEM 2 key.
        Oem2 = 191,
        //
        // 摘要:
        //     The OEM tilde key on a US standard keyboard.
        Oemtilde = 192,
        //
        // 摘要:
        //     The OEM 3 key.
        Oem3 = 192,
        //
        // 摘要:
        //     The OEM open bracket key on a US standard keyboard.
        OemOpenBrackets = 219,
        //
        // 摘要:
        //     The OEM 4 key.
        Oem4 = 219,
        //
        // 摘要:
        //     The OEM pipe key on a US standard keyboard.
        OemPipe = 220,
        //
        // 摘要:
        //     The OEM 5 key.
        Oem5 = 220,
        //
        // 摘要:
        //     The OEM close bracket key on a US standard keyboard.
        OemCloseBrackets = 221,
        //
        // 摘要:
        //     The OEM 6 key.
        Oem6 = 221,
        //
        // 摘要:
        //     The OEM singled/double quote key on a US standard keyboard.
        OemQuotes = 222,
        //
        // 摘要:
        //     The OEM 7 key.
        Oem7 = 222,
        //
        // 摘要:
        //     The OEM 8 key.
        Oem8 = 223,
        //
        // 摘要:
        //     The OEM angle bracket or backslash key on the RT 102 key keyboard.
        OemBackslash = 226,
        //
        // 摘要:
        //     The OEM 102 key.
        Oem102 = 226,
        //
        // 摘要:
        //     The PROCESS KEY key.
        ProcessKey = 229,
        //
        // 摘要:
        //     Used to pass Unicode characters as if they were keystrokes. The Packet key value
        //     is the low word of a 32-bit virtual-key value used for non-keyboard input methods.
        Packet = 231,
        //
        // 摘要:
        //     The ATTN key.
        Attn = 246,
        //
        // 摘要:
        //     The CRSEL key.
        Crsel = 247,
        //
        // 摘要:
        //     The EXSEL key.
        Exsel = 248,
        //
        // 摘要:
        //     The ERASE EOF key.
        EraseEof = 249,
        //
        // 摘要:
        //     The PLAY key.
        Play = 250,
        //
        // 摘要:
        //     The ZOOM key.
        Zoom = 251,
        //
        // 摘要:
        //     A constant reserved for future use.
        NoName = 252,
        //
        // 摘要:
        //     The PA1 key.
        Pa1 = 253,
        //
        // 摘要:
        //     The CLEAR key.
        OemClear = 254,

        JoyStickA = 260,
        JoyStickB = 261,
        JoyStickX = 262,
        JoyStickY = 263,
        JoyStickLB = 264,
        JoyStickRB = 265,
        JoyStickMultiWindow = 266,
        JoyStickMenu = 267,
        JoyStickXBOX = 268,

        JoyStickUp = 270,
        JoyStickUpRight = 271,
        JoyStickRight = 272,
        JoyStickDownRight = 273,
        JoyStickDown = 274,
        JoyStickDownLeft = 275,
        JoyStickLeft = 276,
        JoyStickUpLeft = 277,

        SEMouseMove = 1145,
        SEMouseWheel = 1146,

        SEMouseMoveBackGround = 1147,

    }

    public enum Provider : int
    {
        Unknown = -1,
        None = 0,
        Hook = 1,
        Glfw = 2,
        Sdl = 3
    }
    
    public unsafe class BasicInput
    {
        public static Provider InputType { get; set; }
        public static Keys FromGLFWKeyGetKeys(Silk.NET.GLFW.Keys k)
        {
            switch (k)
            {
                case Silk.NET.GLFW.Keys.A: return Keys.A;
                case Silk.NET.GLFW.Keys.B: return Keys.B;
                case Silk.NET.GLFW.Keys.C: return Keys.C;
                case Silk.NET.GLFW.Keys.D: return Keys.D;
                case Silk.NET.GLFW.Keys.E: return Keys.E;
                case Silk.NET.GLFW.Keys.F: return Keys.F;
                case Silk.NET.GLFW.Keys.G: return Keys.G;
                case Silk.NET.GLFW.Keys.H: return Keys.H;
                case Silk.NET.GLFW.Keys.I: return Keys.I;
                case Silk.NET.GLFW.Keys.J: return Keys.J;
                case Silk.NET.GLFW.Keys.K: return Keys.K;
                case Silk.NET.GLFW.Keys.L: return Keys.L;
                case Silk.NET.GLFW.Keys.M: return Keys.M;
                case Silk.NET.GLFW.Keys.N: return Keys.N;
                case Silk.NET.GLFW.Keys.O: return Keys.O;
                case Silk.NET.GLFW.Keys.P: return Keys.P;
                case Silk.NET.GLFW.Keys.Q: return Keys.Q;
                case Silk.NET.GLFW.Keys.R: return Keys.R;
                case Silk.NET.GLFW.Keys.S: return Keys.S;
                case Silk.NET.GLFW.Keys.T: return Keys.T;
                case Silk.NET.GLFW.Keys.U: return Keys.U;
                case Silk.NET.GLFW.Keys.V: return Keys.V;
                case Silk.NET.GLFW.Keys.W: return Keys.W;
                case Silk.NET.GLFW.Keys.X: return Keys.X;
                case Silk.NET.GLFW.Keys.Y: return Keys.Y;
                case Silk.NET.GLFW.Keys.Z: return Keys.Z;

                case Silk.NET.GLFW.Keys.Number0: return Keys.D0;
                case Silk.NET.GLFW.Keys.Number1: return Keys.D1;
                case Silk.NET.GLFW.Keys.Number2: return Keys.D2;
                case Silk.NET.GLFW.Keys.Number3: return Keys.D3;
                case Silk.NET.GLFW.Keys.Number4: return Keys.D4;
                case Silk.NET.GLFW.Keys.Number5: return Keys.D5;
                case Silk.NET.GLFW.Keys.Number6: return Keys.D6;
                case Silk.NET.GLFW.Keys.Number7: return Keys.D7;
                case Silk.NET.GLFW.Keys.Number8: return Keys.D8;
                case Silk.NET.GLFW.Keys.Number9: return Keys.D9;

                case Silk.NET.GLFW.Keys.Keypad0: return Keys.NumPad0;
                case Silk.NET.GLFW.Keys.Keypad1: return Keys.NumPad1;
                case Silk.NET.GLFW.Keys.Keypad2: return Keys.NumPad2;
                case Silk.NET.GLFW.Keys.Keypad3: return Keys.NumPad3;
                case Silk.NET.GLFW.Keys.Keypad4: return Keys.NumPad4;
                case Silk.NET.GLFW.Keys.Keypad5: return Keys.NumPad5;
                case Silk.NET.GLFW.Keys.Keypad6: return Keys.NumPad6;
                case Silk.NET.GLFW.Keys.Keypad7: return Keys.NumPad7;
                case Silk.NET.GLFW.Keys.Keypad8: return Keys.NumPad8;
                case Silk.NET.GLFW.Keys.Keypad9: return Keys.NumPad9;

                case Silk.NET.GLFW.Keys.F1: return Keys.F1;
                case Silk.NET.GLFW.Keys.F2: return Keys.F2;
                case Silk.NET.GLFW.Keys.F3: return Keys.F3;
                case Silk.NET.GLFW.Keys.F4: return Keys.F4;
                case Silk.NET.GLFW.Keys.F5: return Keys.F5;
                case Silk.NET.GLFW.Keys.F6: return Keys.F6;
                case Silk.NET.GLFW.Keys.F7: return Keys.F7;
                case Silk.NET.GLFW.Keys.F8: return Keys.F8;
                case Silk.NET.GLFW.Keys.F9: return Keys.F9;
                case Silk.NET.GLFW.Keys.F10: return Keys.F10;
                case Silk.NET.GLFW.Keys.F11: return Keys.F11;
                case Silk.NET.GLFW.Keys.F12: return Keys.F12;
                case Silk.NET.GLFW.Keys.F13: return Keys.F13;
                case Silk.NET.GLFW.Keys.F14: return Keys.F14;
                case Silk.NET.GLFW.Keys.F15: return Keys.F15;
                case Silk.NET.GLFW.Keys.F16: return Keys.F16;
                case Silk.NET.GLFW.Keys.F17: return Keys.F17;
                case Silk.NET.GLFW.Keys.F18: return Keys.F18;
                case Silk.NET.GLFW.Keys.F19: return Keys.F19;
                case Silk.NET.GLFW.Keys.F20: return Keys.F20;
                case Silk.NET.GLFW.Keys.F21: return Keys.F21;
                case Silk.NET.GLFW.Keys.F22: return Keys.F22;
                case Silk.NET.GLFW.Keys.F23: return Keys.F23;
                case Silk.NET.GLFW.Keys.F24: return Keys.F24;
                case Silk.NET.GLFW.Keys.F25: return Keys.None;

                case Silk.NET.GLFW.Keys.Escape: return Keys.Escape;
                case Silk.NET.GLFW.Keys.Space: return Keys.Space;
                case Silk.NET.GLFW.Keys.Enter: return Keys.Enter;
                case Silk.NET.GLFW.Keys.Tab: return Keys.Tab;
                case Silk.NET.GLFW.Keys.Backspace: return Keys.Back;
                case Silk.NET.GLFW.Keys.Insert: return Keys.Insert;
                case Silk.NET.GLFW.Keys.Delete: return Keys.Delete;
                case Silk.NET.GLFW.Keys.Right: return Keys.Right;
                case Silk.NET.GLFW.Keys.Left: return Keys.Left;
                case Silk.NET.GLFW.Keys.Down: return Keys.Down;
                case Silk.NET.GLFW.Keys.Up: return Keys.Up;
                case Silk.NET.GLFW.Keys.PageUp: return Keys.PageUp;
                case Silk.NET.GLFW.Keys.PageDown: return Keys.PageDown;
                case Silk.NET.GLFW.Keys.Home: return Keys.Home;
                case Silk.NET.GLFW.Keys.End: return Keys.End;
                case Silk.NET.GLFW.Keys.CapsLock: return Keys.CapsLock;
                case Silk.NET.GLFW.Keys.ScrollLock: return Keys.Scroll;
                case Silk.NET.GLFW.Keys.NumLock: return Keys.NumLock;
                case Silk.NET.GLFW.Keys.PrintScreen: return Keys.PrintScreen;
                case Silk.NET.GLFW.Keys.Pause: return Keys.Pause;

                case Silk.NET.GLFW.Keys.LeftBracket: return Keys.LWin;
                case Silk.NET.GLFW.Keys.RightBracket: return Keys.RWin;
                case Silk.NET.GLFW.Keys.Menu: return Keys.Menu;
                case Silk.NET.GLFW.Keys.ControlLeft: return Keys.LControlKey;
                case Silk.NET.GLFW.Keys.ControlRight: return Keys.RControlKey;
                case Silk.NET.GLFW.Keys.ShiftLeft: return Keys.LShiftKey;
                case Silk.NET.GLFW.Keys.ShiftRight: return Keys.RShiftKey;
                case Silk.NET.GLFW.Keys.AltLeft: return Keys.LMenu;
                case Silk.NET.GLFW.Keys.AltRight: return Keys.RMenu;

                case Silk.NET.GLFW.Keys.Semicolon: return Keys.OemSemicolon;
                case Silk.NET.GLFW.Keys.Equal: return Keys.Oemplus;
                case Silk.NET.GLFW.Keys.Comma: return Keys.Oemcomma;
                case Silk.NET.GLFW.Keys.Minus: return Keys.OemMinus;
                case Silk.NET.GLFW.Keys.Period: return Keys.OemPeriod;
                case Silk.NET.GLFW.Keys.Slash: return Keys.OemQuestion;
                case Silk.NET.GLFW.Keys.GraveAccent: return Keys.Oemtilde;
                //case Silk.NET.GLFW.Keys.LeftBracket: return Keys.OemOpenBrackets;
                //case Silk.NET.GLFW.Keys.RightBracket: return Keys.OemCloseBrackets;
                case Silk.NET.GLFW.Keys.BackSlash: return Keys.OemPipe;
                case Silk.NET.GLFW.Keys.Apostrophe: return Keys.OemQuotes;

                case Silk.NET.GLFW.Keys.KeypadMultiply: return Keys.Multiply;
                case Silk.NET.GLFW.Keys.KeypadAdd: return Keys.Add;
                case Silk.NET.GLFW.Keys.KeypadSubtract: return Keys.Subtract;
                case Silk.NET.GLFW.Keys.KeypadDecimal: return Keys.Decimal;
                case Silk.NET.GLFW.Keys.KeypadDivide: return Keys.Divide;


                default: return Keys.None;
            }
        }
        public static Silk.NET.GLFW.Keys FromKeysGetGLFWKey(Keys k)
        {
            switch (k)
            {
                case Keys.A: return Silk.NET.GLFW.Keys.A;
                case Keys.B: return Silk.NET.GLFW.Keys.B;
                case Keys.C: return Silk.NET.GLFW.Keys.C;
                case Keys.D: return Silk.NET.GLFW.Keys.D;
                case Keys.E: return Silk.NET.GLFW.Keys.E;
                case Keys.F: return Silk.NET.GLFW.Keys.F;
                case Keys.G: return Silk.NET.GLFW.Keys.G;
                case Keys.H: return Silk.NET.GLFW.Keys.H;
                case Keys.I: return Silk.NET.GLFW.Keys.I;
                case Keys.J: return Silk.NET.GLFW.Keys.J;
                case Keys.K: return Silk.NET.GLFW.Keys.K;
                case Keys.L: return Silk.NET.GLFW.Keys.L;
                case Keys.M: return Silk.NET.GLFW.Keys.M;
                case Keys.N: return Silk.NET.GLFW.Keys.N;
                case Keys.O: return Silk.NET.GLFW.Keys.O;
                case Keys.P: return Silk.NET.GLFW.Keys.P;
                case Keys.Q: return Silk.NET.GLFW.Keys.Q;
                case Keys.R: return Silk.NET.GLFW.Keys.R;
                case Keys.S: return Silk.NET.GLFW.Keys.S;
                case Keys.T: return Silk.NET.GLFW.Keys.T;
                case Keys.U: return Silk.NET.GLFW.Keys.U;
                case Keys.V: return Silk.NET.GLFW.Keys.V;
                case Keys.W: return Silk.NET.GLFW.Keys.W;
                case Keys.X: return Silk.NET.GLFW.Keys.X;
                case Keys.Y: return Silk.NET.GLFW.Keys.Y;
                case Keys.Z: return Silk.NET.GLFW.Keys.Z;

                case Keys.D0: return Silk.NET.GLFW.Keys.Number0;
                case Keys.D1: return Silk.NET.GLFW.Keys.Number1;
                case Keys.D2: return Silk.NET.GLFW.Keys.Number2;
                case Keys.D3: return Silk.NET.GLFW.Keys.Number3;
                case Keys.D4: return Silk.NET.GLFW.Keys.Number4;
                case Keys.D5: return Silk.NET.GLFW.Keys.Number5;
                case Keys.D6: return Silk.NET.GLFW.Keys.Number6;
                case Keys.D7: return Silk.NET.GLFW.Keys.Number7;
                case Keys.D8: return Silk.NET.GLFW.Keys.Number8;
                case Keys.D9: return Silk.NET.GLFW.Keys.Number9;

                case Keys.NumPad0: return Silk.NET.GLFW.Keys.Keypad0;
                case Keys.NumPad1: return Silk.NET.GLFW.Keys.Keypad1;
                case Keys.NumPad2: return Silk.NET.GLFW.Keys.Keypad2;
                case Keys.NumPad3: return Silk.NET.GLFW.Keys.Keypad3;
                case Keys.NumPad4: return Silk.NET.GLFW.Keys.Keypad4;
                case Keys.NumPad5: return Silk.NET.GLFW.Keys.Keypad5;
                case Keys.NumPad6: return Silk.NET.GLFW.Keys.Keypad6;
                case Keys.NumPad7: return Silk.NET.GLFW.Keys.Keypad7;
                case Keys.NumPad8: return Silk.NET.GLFW.Keys.Keypad8;
                case Keys.NumPad9: return Silk.NET.GLFW.Keys.Keypad9;

                case Keys.F1: return Silk.NET.GLFW.Keys.F1;
                case Keys.F2: return Silk.NET.GLFW.Keys.F2;
                case Keys.F3: return Silk.NET.GLFW.Keys.F3;
                case Keys.F4: return Silk.NET.GLFW.Keys.F4;
                case Keys.F5: return Silk.NET.GLFW.Keys.F5;
                case Keys.F6: return Silk.NET.GLFW.Keys.F6;
                case Keys.F7: return Silk.NET.GLFW.Keys.F7;
                case Keys.F8: return Silk.NET.GLFW.Keys.F8;
                case Keys.F9: return Silk.NET.GLFW.Keys.F9;
                case Keys.F10: return Silk.NET.GLFW.Keys.F10;
                case Keys.F11: return Silk.NET.GLFW.Keys.F11;
                case Keys.F12: return Silk.NET.GLFW.Keys.F12;
                case Keys.F13: return Silk.NET.GLFW.Keys.F13;
                case Keys.F14: return Silk.NET.GLFW.Keys.F14;
                case Keys.F15: return Silk.NET.GLFW.Keys.F15;
                case Keys.F16: return Silk.NET.GLFW.Keys.F16;
                case Keys.F17: return Silk.NET.GLFW.Keys.F17;
                case Keys.F18: return Silk.NET.GLFW.Keys.F18;
                case Keys.F19: return Silk.NET.GLFW.Keys.F19;
                case Keys.F20: return Silk.NET.GLFW.Keys.F20;
                case Keys.F21: return Silk.NET.GLFW.Keys.F21;
                case Keys.F22: return Silk.NET.GLFW.Keys.F22;
                case Keys.F23: return Silk.NET.GLFW.Keys.F23;
                case Keys.F24: return Silk.NET.GLFW.Keys.F24;

                case Keys.Escape: return Silk.NET.GLFW.Keys.Escape;
                case Keys.Space: return Silk.NET.GLFW.Keys.Space;
                case Keys.Enter: return Silk.NET.GLFW.Keys.Enter;
                case Keys.Tab: return Silk.NET.GLFW.Keys.Tab;
                case Keys.Back: return Silk.NET.GLFW.Keys.Backspace;
                case Keys.Insert: return Silk.NET.GLFW.Keys.Insert;
                case Keys.Delete: return Silk.NET.GLFW.Keys.Delete;
                case Keys.Right: return Silk.NET.GLFW.Keys.Right;
                case Keys.Left: return Silk.NET.GLFW.Keys.Left;
                case Keys.Down: return Silk.NET.GLFW.Keys.Down;
                case Keys.Up: return Silk.NET.GLFW.Keys.Up;
                case Keys.PageUp: return Silk.NET.GLFW.Keys.PageUp;
                case Keys.PageDown: return Silk.NET.GLFW.Keys.PageDown;
                case Keys.Home: return Silk.NET.GLFW.Keys.Home;
                case Keys.End: return Silk.NET.GLFW.Keys.End;
                case Keys.CapsLock: return Silk.NET.GLFW.Keys.CapsLock;
                case Keys.Scroll: return Silk.NET.GLFW.Keys.ScrollLock;
                case Keys.NumLock: return Silk.NET.GLFW.Keys.NumLock;
                case Keys.PrintScreen: return Silk.NET.GLFW.Keys.PrintScreen;
                case Keys.Pause: return Silk.NET.GLFW.Keys.Pause;

                case Keys.LWin: return Silk.NET.GLFW.Keys.LeftBracket;
                case Keys.RWin: return Silk.NET.GLFW.Keys.RightBracket;
                case Keys.Menu: return Silk.NET.GLFW.Keys.Menu;
                case Keys.LControlKey: return Silk.NET.GLFW.Keys.ControlLeft;
                case Keys.RControlKey: return Silk.NET.GLFW.Keys.ControlRight;
                case Keys.LShiftKey: return Silk.NET.GLFW.Keys.ShiftLeft;
                case Keys.RShiftKey: return Silk.NET.GLFW.Keys.ShiftRight;
                case Keys.LMenu: return Silk.NET.GLFW.Keys.AltLeft;
                case Keys.RMenu: return Silk.NET.GLFW.Keys.AltRight;

                case Keys.OemSemicolon: return Silk.NET.GLFW.Keys.Semicolon;
                case Keys.Oemplus: return Silk.NET.GLFW.Keys.Equal;
                case Keys.Oemcomma: return Silk.NET.GLFW.Keys.Comma;
                case Keys.OemMinus: return Silk.NET.GLFW.Keys.Minus;
                case Keys.OemPeriod: return Silk.NET.GLFW.Keys.Period;
                case Keys.OemQuestion: return Silk.NET.GLFW.Keys.Slash;
                case Keys.Oemtilde: return Silk.NET.GLFW.Keys.GraveAccent;
                case Keys.OemOpenBrackets: return Silk.NET.GLFW.Keys.LeftBracket;
                case Keys.OemCloseBrackets: return Silk.NET.GLFW.Keys.RightBracket;
                case Keys.OemPipe: return Silk.NET.GLFW.Keys.BackSlash;
                case Keys.OemQuotes: return Silk.NET.GLFW.Keys.Apostrophe;

                case Keys.Multiply: return Silk.NET.GLFW.Keys.KeypadMultiply;
                case Keys.Add: return Silk.NET.GLFW.Keys.KeypadAdd;
                case Keys.Subtract: return Silk.NET.GLFW.Keys.KeypadSubtract;
                case Keys.Decimal: return Silk.NET.GLFW.Keys.KeypadDecimal;
                case Keys.Divide: return Silk.NET.GLFW.Keys.KeypadDivide;

                case Keys.LButton: return Silk.NET.GLFW.Keys.Unknown; // Mouse buttons are handled separately in GLFW
                case Keys.RButton: return Silk.NET.GLFW.Keys.Unknown;
                case Keys.MButton: return Silk.NET.GLFW.Keys.Unknown;
                case Keys.XButton1: return Silk.NET.GLFW.Keys.Unknown;
                case Keys.XButton2: return Silk.NET.GLFW.Keys.Unknown;

                case Keys.SEMouseMove: return Silk.NET.GLFW.Keys.Unknown;
                case Keys.SEMouseWheel: return Silk.NET.GLFW.Keys.Unknown;

                default: return Silk.NET.GLFW.Keys.Unknown;
            }
        }
        public static POINT WPosi;
        public static POINT WSize;
        /// <summary>
        /// 虚拟光标移动速度（mv = m*speed）
        /// </summary>
        public static POINTF VirtualCursorSpeed { get; private set; } = new POINTF(1, 1);
        /// <summary>
        /// 逻辑光标移动倍率（mv = m*speed）
        /// </summary>
        public static POINTF LogicCursorSpeed { get; private set; } = new POINTF(1, 1);
        /// <summary>
        /// 上一帧鼠标指针坐标
        /// </summary>
        public static POINT LastFrameCursorPosition { get; private set; }
        /// <summary>
        /// 上一帧鼠标指针坐标(后台情况下)
        /// </summary>
        public static POINT LastFrameCursorPositionBackground { get; private set; }
        /// <summary>
        /// 鼠标指针坐标
        /// </summary>
        public static POINT CursorPosition { get; private set; }
        /// <summary>
        /// 鼠标指针逻辑坐标
        /// </summary>
        public static POINT CursorLogicPosition { get { if (WH_LGCCUR) return CursorPosition; else return CursorPosition - WPosi; } }
        /// <summary>
        /// 鼠标指针移动了的向量
        /// </summary>
        public static POINT CursorMovedValue { get; private set; }
        /// <summary>
        /// 鼠标是否移动
        /// </summary>
        public static bool MouseMoved { get; private set; }
        /// <summary>
        /// 鼠标指针坐标(后台情况下)
        /// </summary>
        public static POINT CursorPositionBackground { get; private set; }
        /// <summary>
        /// 鼠标指针逻辑坐标(后台情况下)
        /// </summary>
        public static POINT CursorLogicPositionBackground { get { if (WH_LGCCUR) return CursorPositionBackground; else return CursorPositionBackground - WPosi; } }
        /// <summary>
        /// 鼠标指针移动了的向量(后台情况下)
        /// </summary>
        public static POINT CursorMovedValueBackground { get; private set; }
        /// <summary>
        /// 鼠标是否移动(后台情况下)
        /// </summary>
        public static bool MouseMovedBackground { get; private set; }
        /// <summary>
        /// 上一帧是否有按键输入(处于焦点情况下)
        /// </summary>
        public static bool KeyInputted { get; private set; }
        /// <summary>
        /// 鼠标是否滚动
        /// </summary>
        public static bool MouseWheeled { get { return MouseWheelValue != 0; } }
        /// <summary>
        /// 鼠标滚动的值
        /// </summary>
        public static short MouseWheelValue { get; private set; }
        public static bool JoystickConnected { get; private set; } = false;

        public static Glfw g = Glfw.GetApi();

        public static WindowHandle* ThisWindow;
        /// <summary>
        /// 检测是否按下某个特定的按键
        /// </summary>
        /// <param name="k"></param>
        /// <returns></returns>
        public static bool IfKeyDown(Keys k)
        {

            if (UseWinHook)
                return kpool[(int)k];
            else
                return g.GetKey(ThisWindow, FromKeysGetGLFWKey(k)) != 0;
        }
        /// <summary>
        /// 失焦时获取按键，使用GLFW作为输入管理器时不可用
        /// </summary>
        /// <param name="k"></param>
        /// <returns></returns>
        public static bool BackgroundIfKeyDown(Keys k)
        {
            return bacpool[(int)k];
        }
        //public static bool UseWinHook = true;
        //public static bool UseXInput = true;
        /// <summary>
        /// 要屏蔽的单个按键的集群，在失焦时屏蔽按键不可用
        /// </summary>
        public static List<Keys> KeysToPrevent = new List<Keys>();
        public static bool[] kpool = new bool[300];
        public static bool[] bacpool = new bool[300];//失焦时缓冲池
        public static Stack<KeyValuePair<Keys, object>> cac = new Stack<KeyValuePair<Keys, object>>();
        public static bool Eviin = false;
        /// <summary>
        /// 是否处于焦点，失焦时不可屏蔽按键，且无法获取鼠标输入
        /// </summary>
        public static bool Focued = false;

        public delegate void PKeyInputHandler(Keys key,bool enable);
        /// <summary>
        /// 有按键输入时触发此事件，按键松开也会触发
        /// </summary>
        /// <remarks>此事件不包含鼠标按键，触发仅处于焦点</remarks>
        public static event PKeyInputHandler? OnKeyInput;
        public static void UseGLFWInput()
        {
            UseWinHook = false;
            //ThisWindow = wh;
            g.SetKeyCallback(ThisWindow, (window, key, scancode, action, mods) =>
            {
                Keys k = FromGLFWKeyGetKeys((Silk.NET.GLFW.Keys)key);
                if (Focued)
                {
                    KeyInputted = true;
                    if (action == Silk.NET.GLFW.InputAction.Press)
                    {
                        //down
                        
                        if(cache)
                        {
                            cac.Push(new KeyValuePair<Keys, object>(k, true));
                        }
                        else
                        {
                            kpool[(int)k] = true;
                        }
                        OnKeyInput?.Invoke(k, true);
                    }
                    else if (action == Silk.NET.GLFW.InputAction.Release)
                    {
                        //up
                        if (cache)
                        {
                            cac.Push(new KeyValuePair<Keys, object>(k, false));
                        }
                        else
                        {
                            kpool[(int)k] = false;
                        }
                        OnKeyInput?.Invoke(k, false);
                    }
                }
                else
                {
                    if (action == Silk.NET.GLFW.InputAction.Press)
                    {
                        //down
                        bacpool[(int)k] = true;
                    }
                    else if (action == Silk.NET.GLFW.InputAction.Release)
                    {
                        //up
                        bacpool[(int)k] = false;
                    }
                }
            });
        }
        public static int WinKeyboardProcess(int wParam, InputWin.Keyboard.KeyboardEvent me)
        {
            //Console.WriteLine($"KB:wParam:{wParam}||vkCode:{me.vkCode}||scanCode:{me.scanCode}||flags:{me.flags}||dwextinfo:{me.dwExtraInfo}");
            if (Focued)
            {
                KeyInputted = true;
                if (cache)
                {
                    Eviin = true;
                    if (wParam == InputWin.Keyboard.WM_KEYDOWN || wParam == InputWin.Keyboard.WM_SYSKEYDOWN)
                    {
                        //down
                        //Keys nk = (Keys)me.vkCode;
                        //kpool[me.vkCode] = true;
                        //Console.WriteLine(me.vkCode);
                        //Console.WriteLine(me.vkCode);
                        cac.Push(new KeyValuePair<Keys, object>((Keys)me.vkCode, true));
                        if (KeysToPrevent.Count > 0)
                        {
                            //屏蔽按键，仅屏蔽单个按键
                            if (KeysToPrevent.IndexOf((Keys)me.vkCode) >= 0)
                            {
                                return 1;
                            }
                        }
                        OnKeyInput?.Invoke((Keys)me.vkCode, true);
                    }
                    else if (wParam == InputWin.Keyboard.WM_SYSKEYUP || wParam == InputWin.Keyboard.WM_KEYUP)
                    {
                        //up
                        //kpool[me.vkCode] = false;
                        cac.Push(new KeyValuePair<Keys, object>((Keys)me.vkCode, false));
                        OnKeyInput?.Invoke((Keys)me.vkCode, false); 
                    }
                }
                else
                {
                    if (wParam == InputWin.Keyboard.WM_KEYDOWN || wParam == InputWin.Keyboard.WM_SYSKEYDOWN)
                    {
                        //down
                        //Keys nk = (Keys)me.vkCode;
                        //Console.WriteLine(me.vkCode);
                        kpool[me.vkCode] = true;

                        if (KeysToPrevent.Count > 0)
                        {
                            //屏蔽按键，仅屏蔽单个按键
                            if (KeysToPrevent.IndexOf((Keys)me.vkCode) >= 0)
                            {
                                return 1;
                            }
                        }
                        OnKeyInput?.Invoke((Keys)me.vkCode, true);
                    }
                    else if (wParam == InputWin.Keyboard.WM_SYSKEYUP || wParam == InputWin.Keyboard.WM_KEYUP)
                    {
                        //up
                        kpool[me.vkCode] = false;
                        OnKeyInput?.Invoke((Keys)me.vkCode, false);
                    }
                }
            }
            else
            {
                if (wParam == InputWin.Keyboard.WM_KEYDOWN || wParam == InputWin.Keyboard.WM_SYSKEYDOWN)
                {
                    //down
                    //Keys nk = (Keys)me.vkCode;
                    bacpool[me.vkCode] = true;

                }
                else if (wParam == InputWin.Keyboard.WM_SYSKEYUP || wParam == InputWin.Keyboard.WM_KEYUP)
                {
                    //up
                    bacpool[me.vkCode] = false;
                }
            }
            return 0;
        }
        public static int WinMouseProcess(int wParam, InputWin.Mouse.MouseEvent me)
        {
            //Console.WriteLine($"MS:wParam:{wParam}||pt:{me.pt.x}-{me.pt.y}||time:{me.time}||flags:{me.flags}||dwextinfo:{me.dwExtraInfo}");
            if (Focued)
            {
                if (cache)
                {
                    Eviin = true;
                    if (wParam == InputWin.Mouse.WM_LBUTTONDOWN)
                    {
                        //W_kl[W_posi++] = Keys.LButton;
                        //MouseClicked = true;
                        //kpool[(int)Keys.LButton] = true;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.LButton, true));
                    }
                    else if (wParam == InputWin.Mouse.WM_LBUTTONUP)
                    {
                        //W_kl[W_posi++] = Keys.LButton;
                        //MouseClicked = true;
                        //kpool[(int)Keys.LButton] = false;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.LButton, false));
                    }
                    else if (wParam == InputWin.Mouse.WM_RBUTTONDOWN)
                    {
                        //W_kl[W_posi++] = (Keys.RButton);
                        //MouseClicked = true;
                        //kpool[(int)Keys.RButton] = true;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.RButton, true));
                    }
                    else if (wParam == InputWin.Mouse.WM_RBUTTONUP)
                    {
                        //W_kl[W_posi++] = Keys.LButton;
                        //MouseClicked = true;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.RButton, false));
                    }
                    else if (wParam == InputWin.Mouse.WM_MBUTTONDOWN)
                    {
                        //W_kl[W_posi++] = (Keys.MButton);
                        //MouseClicked = true;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.MButton, true));
                    }
                    else if (wParam == InputWin.Mouse.WM_MBUTTONUP)
                    {
                        //W_kl[W_posi++] = Keys.LButton;
                        //MouseClicked = true;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.MButton, false));
                    }
                    else if (wParam == InputWin.Mouse.WM_XBUTTONDOWN)
                    {
                        short v = (short)(me.mouseData >> 16);
                        if (v == 1)
                        {
                            //W_kl[W_posi++] = (Keys.XButton1);
                            //MouseClicked = true;
                            cac.Push(new KeyValuePair<Keys, object>(Keys.XButton1, true));
                        }
                        else if (v == 2)
                        {
                            //W_kl[W_posi++] = (Keys.XButton2);
                            //MouseClicked = true;
                            cac.Push(new KeyValuePair<Keys, object>(Keys.XButton2, true));
                        }

                    }
                    else if (wParam == InputWin.Mouse.WM_XBUTTONUP)
                    {
                        short v = (short)(me.mouseData >> 16);
                        if (v == 1)
                        {
                            //W_kl[W_posi++] = (Keys.XButton1);
                            cac.Push(new KeyValuePair<Keys, object>(Keys.XButton1, false));
                        }
                        else if (v == 2)
                        {
                            //W_kl[W_posi++] = (Keys.XButton2);
                            cac.Push(new KeyValuePair<Keys, object>(Keys.XButton2, false));
                        }

                    }
                    else if (wParam == InputWin.Mouse.WM_MOUSEMOVE)
                    {
                        //W_mp_o = W_mp_n;
                        //W_mp_n = me.pt;
                        //W_mp_m.x = W_mp_n.x - W_mp_o.x;
                        //W_mp_m.y = W_mp_n.y - W_mp_o.y;
                        //W_mmb = true;
                        //W_mmch = 2;

                        bool otd = false;
                        POINT lgp = me.pt - WPosi;
                        int mx = lgp.x, my = lgp.y;
                        if (WH_LGCCUR)
                        {
                            //cac.Push(new KeyValuePair<Keys, object>(Keys.SEMouseMove, me.pt));
                            POINT cent = (WSize / 2);
                            POINT mvd = me.pt - WPosi - cent;
                            cac.Push(new KeyValuePair<Keys, object>(Keys.SEMouseMove, mvd));
                            g.SetCursorPos(ThisWindow, cent.x, cent.y);
                            //InputWin.SetCursorPos(cent.x, cent.y);
                            return 1;
                        }
                        else if (WH_ENSCUR)
                        {
                            if (mx < 0)
                            {
                                mx = WSize.x;
                                otd = true;
                            }
                            if (my < 0)
                            {
                                my = WSize.y;
                                otd = true;
                            }
                            if (mx > WSize.x)
                            {
                                mx = 0;
                                otd = true;
                            }
                            if (my > WSize.y)
                            {
                                my = 0;
                                otd = true;
                            }
                            if (otd)
                            {
                                g.SetCursorPos(ThisWindow, mx, my);
                                //InputWin.SetCursorPos(mx, my);
                                otd = false;
                                return 1;
                            }

                        }
                        else if (WH_LCKCUR)
                        {

                            if (mx < 0)
                            {
                                mx = 0;
                                otd = true;
                            }
                            if (my < 0)
                            {
                                my = 0;
                                otd = true;
                            }
                            if (mx > WSize.x)
                            {
                                mx = WSize.x;
                                otd = true;
                            }
                            if (my > WSize.y)
                            {
                                my = WSize.y;
                                otd = true;
                            }
                            if (otd)
                            {

                                g.SetCursorPos(ThisWindow, mx, my);
                                //InputWin.SetCursorPos(mx, my);
                                otd = false;
                                return 1;
                            }


                        }
                        else
                        {
                            cac.Push(new KeyValuePair<Keys, object>(Keys.SEMouseMove, me.pt));
                        }
                        //CursorPosition = ;
                        //CursorMovedValue = new POINT(CursorPosition.x - LastFrameCursorPosition.x, CursorPosition.y - LastFrameCursorPosition.y);
                    }
                    else if (wParam == InputWin.Mouse.WM_MOUSEWHEEL)
                    {
                        short v = (short)(me.mouseData >> 16);
                        //W_ml = v;
                        //W_mlb = true;
                        //W_mlch = 2;
                        //MouseWheelValue = v;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.SEMouseWheel, v));
                    }
                }
                else
                {
                    if (wParam == InputWin.Mouse.WM_LBUTTONDOWN)
                    {
                        //W_kl[W_posi++] = Keys.LButton;

                        kpool[(int)Keys.LButton] = true;
                    }
                    else if (wParam == InputWin.Mouse.WM_LBUTTONUP)
                    {
                        //W_kl[W_posi++] = Keys.LButton;
                        //MouseClicked = true;
                        kpool[(int)Keys.LButton] = false;
                    }
                    else if (wParam == InputWin.Mouse.WM_RBUTTONDOWN)
                    {
                        //W_kl[W_posi++] = (Keys.RButton);

                        kpool[(int)Keys.RButton] = true;
                    }
                    else if (wParam == InputWin.Mouse.WM_RBUTTONUP)
                    {
                        //W_kl[W_posi++] = Keys.LButton;
                        //MouseClicked = true;
                        kpool[(int)Keys.RButton] = false;
                    }
                    else if (wParam == InputWin.Mouse.WM_MBUTTONDOWN)
                    {
                        //W_kl[W_posi++] = (Keys.MButton);

                        kpool[(int)Keys.MButton] = true;
                    }
                    else if (wParam == InputWin.Mouse.WM_MBUTTONUP)
                    {
                        //W_kl[W_posi++] = Keys.LButton;
                        //MouseClicked = true;
                        kpool[(int)Keys.MButton] = false;
                    }
                    else if (wParam == InputWin.Mouse.WM_XBUTTONDOWN)
                    {
                        short v = (short)(me.mouseData >> 16);
                        if (v == 1)
                        {
                            //W_kl[W_posi++] = (Keys.XButton1);

                            kpool[(int)Keys.XButton1] = true;
                        }
                        else if (v == 2)
                        {
                            //W_kl[W_posi++] = (Keys.XButton2);

                            kpool[(int)Keys.XButton2] = true;
                        }

                    }
                    else if (wParam == InputWin.Mouse.WM_XBUTTONUP)
                    {
                        short v = (short)(me.mouseData >> 16);
                        if (v == 1)
                        {
                            //W_kl[W_posi++] = (Keys.XButton1);
                            kpool[(int)Keys.XButton1] = false;
                        }
                        else if (v == 2)
                        {
                            //W_kl[W_posi++] = (Keys.XButton2);
                            kpool[(int)Keys.XButton2] = false;
                        }

                    }
                    else if (wParam == InputWin.Mouse.WM_MOUSEMOVE)
                    {
                        //W_mp_o = W_mp_n;
                        //W_mp_n = me.pt;
                        //W_mp_m.x = W_mp_n.x - W_mp_o.x;
                        //W_mp_m.y = W_mp_n.y - W_mp_o.y;
                        //W_mmb = true;
                        //W_mmch = 2;


                        bool otd = false;
                        POINT lgp = me.pt - WPosi;
                        int mx = lgp.x, my = lgp.y;
                        if (WH_LGCCUR)
                        {
                            //cac.Push(new KeyValuePair<Keys, object>(Keys.SEMouseMove, me.pt));
                            POINT cent = (WSize / 2);
                            POINT mvd = me.pt - WPosi - cent;
                            //cac.Push(new KeyValuePair<Keys, object>(Keys.SEMouseMove, mvd));
                            g.SetCursorPos(ThisWindow, cent.x, cent.y);
                            //InputWin.SetCursorPos(cent.x, cent.y);
                            CursorPosition = new POINT(me.pt.x, me.pt.y);
                            CursorMovedValue = CursorPosition * LogicCursorSpeed;
                            return 1;
                        }
                        if (WH_ENSCUR)
                        {
                            if (mx < 0)
                            {
                                mx = WSize.x;
                                otd = true;
                            }
                            if (my < 0)
                            {
                                my = WSize.y;
                                otd = true;
                            }
                            if (mx > WSize.x)
                            {
                                mx = 0;
                                otd = true;
                            }
                            if (my > WSize.y)
                            {
                                my = 0;
                                otd = true;
                            }
                            if (otd)
                            {
                                g.SetCursorPos(ThisWindow, mx, my);
                                //InputWin.SetCursorPos(mx, my);
                                otd = false;
                                return 1;
                            }

                        }
                        else if (WH_LCKCUR)
                        {

                            if (mx < 0)
                            {
                                mx = 0;
                                otd = true;
                            }
                            if (my < 0)
                            {
                                my = 0;
                                otd = true;
                            }
                            if (mx > WSize.x)
                            {
                                mx = WSize.x;
                                otd = true;
                            }
                            if (my > WSize.y)
                            {
                                my = WSize.y;
                                otd = true;
                            }
                            if (otd)
                            {
                                g.SetCursorPos(ThisWindow, mx, my);
                                //InputWin.SetCursorPos(mx, my);
                                otd = false;
                                return 1;
                            }

                            return 1;
                        }
                        else
                        {
                            CursorPosition = new POINT(me.pt.x, me.pt.y);
                            CursorMovedValue = new POINT(CursorPosition.x - LastFrameCursorPosition.x, CursorPosition.y - LastFrameCursorPosition.y);
                        }
                    }
                    else if (wParam == InputWin.Mouse.WM_MOUSEWHEEL)
                    {
                        short v = (short)(me.mouseData >> 16);
                        //W_ml = v;
                        //W_mlb = true;
                        //W_mlch = 2;
                        MouseWheelValue = v;
                    }
                }
            }
            else
            {
                if (cache)
                {
                    Eviin = true;
                    if (wParam == InputWin.Mouse.WM_MOUSEMOVE)
                    {
                        POINT lgp = me.pt - WPosi;
                        int mx = lgp.x, my = lgp.y;
                        cac.Push(new KeyValuePair<Keys, object>(Keys.SEMouseMoveBackGround, me.pt));
                    }
                }
                else
                {
                    if (wParam == InputWin.Mouse.WM_MOUSEMOVE)
                    {
                        POINT lgp = me.pt - WPosi;
                        int mx = lgp.x, my = lgp.y;
                        CursorPositionBackground = new POINT(me.pt.x, me.pt.y);
                        CursorMovedValueBackground = new POINT(CursorPositionBackground.x - LastFrameCursorPositionBackground.x, CursorPositionBackground.y - LastFrameCursorPositionBackground.y);
                    }
                }
            }

            return 0;
        }

        public static bool WH_LCKCUR = false;//困住光标在窗口
        public static bool WH_LGCCUR = false;//居中光标逻辑位移输入
        public static bool WH_VTLCUR = false;//虚拟光标
        public static bool WH_ENSCUR = false;//无止境的光标
        /// <summary>
        /// 是否将输入缓存到下一帧
        /// </summary>
        public static bool cache = false;

        public static ushort JoyStickLeftMotorSpeed = 0;
        public static ushort JoyStickRightMotorSpeed = 0;



        public static void BeforeUpdate()//在每一帧前后会锁状态
        {
            cache = true;
            
            
            if(Focued)
            {
                if (WH_LGCCUR)
                {
                    if (CursorPosition.x != 0 || CursorPosition.y != 0)
                    {
                        MouseMoved = true;
                        CursorMovedValue = new POINT(CursorPosition.x, CursorPosition.y);
                    }
                    else
                    {
                        MouseMoved = false;
                        CursorMovedValue = new POINT(0, 0);
                    }
                }
                else
                {
                    if (CursorPosition.x != LastFrameCursorPosition.x || CursorPosition.y != LastFrameCursorPosition.y)
                    {
                        MouseMoved = true;
                        CursorMovedValue = new POINT(CursorPosition.x - LastFrameCursorPosition.x, CursorPosition.y - LastFrameCursorPosition.y);
                    }
                    else
                    {
                        MouseMoved = false;
                        CursorMovedValue = new POINT(0, 0);
                    }
                }
            }
            else
            {
                if (CursorPositionBackground.x != LastFrameCursorPositionBackground.x || CursorPositionBackground.y != LastFrameCursorPositionBackground.y)
                {
                    MouseMovedBackground = true;
                    CursorMovedValueBackground = new POINT(CursorPositionBackground.x - LastFrameCursorPositionBackground.x, CursorPositionBackground.y - LastFrameCursorPositionBackground.y);
                }
                else
                {
                    MouseMovedBackground = false;
                    CursorMovedValueBackground = new POINT(0, 0);
                }
            }

        }
        static double omx = 0, omy = 0;
        public static void GLFWProcess()
        {
            if (Focued)
            {
                double mx = 0, my = 0;
                //double mxold = 0, myold = 0;

                g.GetCursorPos(ThisWindow, out mx, out my);

                if (g.GetMouseButton(ThisWindow, 0) == 0)
                {
                    kpool[(int)Keys.LButton] = false;
                }
                else
                {
                    kpool[(int)Keys.LButton] = true;
                }
                if (g.GetMouseButton(ThisWindow, 1) == 0)
                {
                    kpool[(int)Keys.RButton] = false;
                }
                else
                {
                    kpool[(int)Keys.RButton] = true;
                }
                if (g.GetMouseButton(ThisWindow, 2) == 0)
                {
                    kpool[(int)Keys.MButton] = false;
                }
                else
                {
                    kpool[(int)Keys.MButton] = true;
                }
                if (g.GetMouseButton(ThisWindow, 3) == 0)
                {
                    kpool[(int)Keys.XButton1] = false;
                }
                else
                {
                    kpool[(int)Keys.XButton1] = true;
                }
                if (g.GetMouseButton(ThisWindow, 4) == 0)
                {
                    kpool[(int)Keys.XButton2] = false;
                }
                else
                {
                    kpool[(int)Keys.XButton2] = true;
                }
                if (WH_LGCCUR)
                {
                    CursorPosition = new POINT((int)(mx - omx), (int)(my - omy));
                    omx = mx;
                    omy = my;
                    CursorMovedValue = CursorPosition * LogicCursorSpeed;
                }
                else
                {
                    CursorPosition = new POINT((int)mx + WPosi.x, (int)my + WPosi.y);
                    if (MouseMoved)
                    {
                        CursorMovedValue = new POINT(CursorPosition.x - LastFrameCursorPosition.x, CursorPosition.y - LastFrameCursorPosition.y);
                    }
                    bool otd = false;
                    if (WH_ENSCUR)
                    {
                        if (mx < 0)
                        {
                            mx = WSize.x;
                            otd = true;
                        }
                        if (my < 0)
                        {
                            my = WSize.y;
                            otd = true;
                        }
                        if (mx > WSize.x)
                        {
                            mx = 0;
                            otd = true;
                        }
                        if (my > WSize.y)
                        {
                            my = 0;
                            otd = true;
                        }
                        if (otd)
                        {
                            g.SetCursorPos(ThisWindow, mx, my);
                            otd = false;
                        }

                    }
                    else if (WH_LCKCUR)
                    {
                        if (mx < 0)
                        {
                            mx = 0;
                            otd = true;
                        }
                        if (my < 0)
                        {
                            my = 0;
                            otd = true;
                        }
                        if (mx > WSize.x)
                        {
                            mx = WSize.x;
                            otd = true;
                        }
                        if (my > WSize.y)
                        {
                            my = WSize.y;
                            otd = true;
                        }
                        if (otd)
                        {
                            g.SetCursorPos(ThisWindow, mx, my);
                            otd = false;
                        }

                    }
                }

            }
            else
            {

            }

        }



        public static void AfterUpdate()//将解锁状态
        {
            LastFrameCursorPosition = CursorPosition;
            LastFrameCursorPositionBackground = CursorPositionBackground;
            if (WH_LGCCUR)
                CursorPosition = new POINT(0, 0);

            MouseWheelValue = 0;
            cache = false;
            KeyInputted = false;
            if (Eviin)
            {
                while (cac.Count > 0)
                {
                    var c = cac.Pop();
                    if (c.Key == Keys.SEMouseMove)
                    {
                        POINT pt = (POINT)c.Value;
                        //CursorPosition = new POINT();
                        CursorPosition = new POINT(pt.x, pt.y);
                        if (WH_LGCCUR)
                            CursorMovedValue = CursorPosition * LogicCursorSpeed;
                        else
                            CursorMovedValue = new POINT(CursorPosition.x - LastFrameCursorPosition.x, CursorPosition.y - LastFrameCursorPosition.y);
                    }
                    else if (c.Key == Keys.SEMouseWheel)
                    {
                        MouseWheelValue = (short)c.Value;
                    }
                    else if (c.Key == Keys.SEMouseMoveBackGround)
                    {
                        POINT pt = (POINT)c.Value;
                        //CursorPosition = new POINT();
                        CursorPositionBackground = new POINT(pt.x, pt.y);

                        CursorMovedValueBackground = new POINT(CursorPositionBackground.x - LastFrameCursorPositionBackground.x, CursorPositionBackground.y - LastFrameCursorPositionBackground.y);
                    }
                    else
                    {
                        kpool[(int)c.Key] = (bool)c.Value;
                    }
                }

            }

        }
    }
}
