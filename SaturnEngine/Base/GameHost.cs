using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.SEGraphics;

namespace SaturnEngine.Base
{
    /// <summary>
    /// 帮助加载GAME，以及高度封装引擎代码
    /// </summary>
    public class GameHost : SEBase
    {
        public SEWindow SW { get; private set; } = null!;
        public Game G { get; private set; } = null!;
        public GameHost()
        {
            Platform.Global.EngineInit();
        }
        /// <summary>
        /// 加载GAME，一个GAMEHOST只能由一个GAME对象
        /// </summary>
        /// <param name="game"></param>
        public void LoadGame(Game game)
        {
            G = game;
            GVariables.ThisGameHost = this;
            GVariables.ThisGame = G;

            if(GVariables.CurrentWindowHostType == WindowHostType.SDL)
            {
                SW = new SEWindowSDL();
                GVariables.MainWindow = SW;
            }
            else if(GVariables.CurrentWindowHostType == WindowHostType.Avalonia)
            {
                SW = new SEWindowAvalonia();
                GVariables.MainWindow = SW;
            }
            else
            {
                SW = new SEWindowOpenGL();
                GVariables.MainWindow = SW;
            }
            SW.Initialize();
            G.Initialize();
            SW.UpdateLoop = G;

            SW.CreateWindow();

        }
        public void SetWindowStyle(WindowStyle style)
        {
            if (SW == null)
            {
                throw new InvalidOperationException("窗体并未初始化，请在设置样式前加载一个Game对象".GetInCurrLang());
            }
            //SW.Style = style;
            SW.SetTitle(style.Title);
            SW.SetICONFromImage(style.ICON);
            SW.SetCursorFromImage(style.Cursor, (int)style.PointSet.X, (int)style.PointSet.Y);
        }
        public void Start()
        {
            if (G == null)
            {
                throw new InvalidOperationException("Game对象并未加载，请在启动GameHost前加载一个Game对象".GetInCurrLang());
            }

            SW.RunWindow();
        }
    }
}
