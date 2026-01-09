using System.Numerics;

namespace SaturnEngine.SEMath
{
    public struct SEComplex
    {
        double x;
        double y;

        public double X { get { return x; } set { x = value; } }
        public double Y { get { return y; } set { y = value; } }
        public static implicit operator SEComplex(Complex c)
        {
            return new SEComplex(c.Real, c.Imaginary);
        }
        public static implicit operator Complex(SEComplex c)
        {
            return new Complex(c.x, c.y);
        }
        public static implicit operator SEComplex(Vector2D v)
        {
            return new SEComplex(v.X, v.Y);
        }
        public static implicit operator Vector2D(SEComplex c)
        {
            return new Vector2D(c.x, c.y);
        }
        public static implicit operator SEComplex((double, double) t)
        {
            return new SEComplex(t.Item1, t.Item2);
        }
        public SEComplex(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public SEComplex()
        {
            x = 0;
            y = 0;
        }

        public Complex GetComplex()
        {
            return new Complex(x, y);
        }
    }
}
