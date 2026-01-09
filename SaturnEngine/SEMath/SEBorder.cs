namespace SaturnEngine.SEMath
{
    public struct SEBorder
    {
        public double Left;
        public double Right;
        public double Top;
        public double Bottom;
        public SEBorder(double left = 0, double right = 0, double top = 0, double bottom = 0)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }
}
