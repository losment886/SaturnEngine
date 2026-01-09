namespace SaturnEngine.SEMath
{
    public struct POINTF
    {
        public float x;
        public float y;
        public POINTF(float X, float Y)
        {
            x = X;
            y = Y;
        }
        public static POINTF operator -(POINTF l, POINTF r)
        {
            return new POINTF(l.x - r.x, l.y - r.y);
        }
        public static POINTF operator +(POINTF l, POINTF r)
        {
            return new POINTF(l.x + r.x, l.y + r.y);
        }
        public static POINTF operator *(POINTF l, float r)
        {
            return new POINTF(l.x * r, l.y * r);
        }
        public static POINTF operator *(POINTF l, POINTF r)
        {
            return new POINTF(l.x * r.x, l.y * r.y);
        }
        public static POINTF operator *(POINTF l, POINT r)
        {
            return new POINTF(l.x * r.x, l.y * r.y);
        }
        public static POINTF operator /(POINTF l, float r)
        {
            return new POINTF(l.x / r, l.y / r);
        }
        public static implicit operator POINTF(POINT p) => new POINTF(p.x, p.y);
        public static implicit operator POINT(POINTF p) => new POINT((int)p.x, (int)p.y);
        public override string ToString()
        {
            return $"({x},{y})";
        }
    }
    public struct POINT
    {
        public int x;
        public int y;
        public POINT(int X, int Y)
        {
            x = X;
            y = Y;
        }
        public static POINT operator -(POINT l, POINT r)
        {
            return new POINT(l.x - r.x, l.y - r.y);
        }
        public static POINT operator +(POINT l, POINT r)
        {
            return new POINT(l.x + r.x, l.y + r.y);
        }
        public static POINT operator *(POINT l, int r)
        {
            return new POINT(l.x * r, l.y * r);
        }
        public static POINT operator *(POINT l, POINT r)
        {
            return new POINT(l.x * r.x, l.y * r.y);
        }
        public static POINT operator *(POINT l, POINTF r)
        {
            return new POINT((int)(l.x * r.x), (int)(l.y * r.y));
        }
        public static POINT operator /(POINT l, int r)
        {
            return new POINT(l.x / r, l.y / r);
        }
        public override string ToString()
        {
            return $"({x},{y})";
        }
    }
}
