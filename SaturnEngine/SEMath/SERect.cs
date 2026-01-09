namespace SaturnEngine.SEMath
{
    public struct SERect
    {
        public List<double[]>? r;
        public List<double[]>? c;
        public int Height
        {
            get
            {
                return r?.Count ?? 0;
            }
        }
        public int Width
        {
            get
            {
                return r?[0].Length ?? 0;
            }
        }
        public void Turn()
        {
            c.Clear();
            for (int x = 0; x < Width; x++)
            {
                c.Add(new double[Height]);
                for (int y = 0; y < Height; y++)
                {
                    c[x][y] = r[y][x];
                }
            }
            r = c;
            c = new List<double[]>();
        }
        public SERect(double x1, double y1, double x2, double y2)
        {
            r = new List<double[]>();
            c = new List<double[]>();
            r.Add(new double[2] { x1, y1 });
            r.Add(new double[2] { x2, y2 });
        }
        public SERect(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            r = new List<double[]>();
            c = new List<double[]>();
            r.Add(new double[3] { x1, y1, z1 });
            r.Add(new double[3] { x2, y2, z2 });
        }
        public SERect(double[][] v)
        {
            r = new List<double[]>();
            c = new List<double[]>();
            for (int x = 0; x < v.Length; x++)
            {
                r.Add(v[x]);
            }
        }
        public SERect(Vector3D[] v)
        {
            r = new List<double[]>();
            c = new List<double[]>();
            for (int x = 0; x < v.Length; x++)
            {
                r.Add(new double[3] { v[x].X, v[x].Y, v[x].Z });
            }
        }
        public SERect(Vector2D[] v)
        {
            r = new List<double[]>();
            c = new List<double[]>();
            for (int x = 0; x < v.Length; x++)
            {
                r.Add(new double[2] { v[x].X, v[x].Y });
            }
        }
        public double[] this[int index]
        {
            get
            {
                return r?[index] ?? [];
            }
            set
            {
#if NET10_0
                r?[index] = value;
#else
                if(r != null)
                {
                    r[index] = value;
                }
#endif
            }
        }
        public override string ToString()
        {
            string s = "[";
            for (int i = 0; i < r?.Count; i++)
            {
                s += "[";
                for (int j = 0; j < r[i].Length; j++)
                {
                    s += r[i][j];
                    if (j < r[i].Length - 1)
                    {
                        s += ",";
                    }
                }
                s += "]";
                if (i < r.Count - 1)
                {
                    s += ",";
                }
            }
            s += "]";
            return s;
        }

        public SERect Clone()
        {
            SERect sr = new SERect();
            sr.r = new List<double[]>();
            sr.c = new List<double[]>();
            for (int i = 0; i < r.Count; i++)
            {
                double[] dr = new double[r[i].Length];
                for (int j = 0; j < r[i].Length; j++)
                {
                    dr[j] = r[i][j];
                }
                sr.r.Add(dr);
            }
            return sr;
        }

        public static SERect operator +(SERect a, SERect b)
        {
            if (a.Height != b.Height || a.Width != b.Width)
            {
                throw new Exception("Cannot add two SERect with different size.");
            }
            SERect sr = new SERect();
            sr.r = new List<double[]>();
            sr.c = new List<double[]>();
            for (int i = 0; i < a.Height; i++)
            {
                double[] dr = new double[a.Width];
                for (int j = 0; j < a.Width; j++)
                {
                    dr[j] = a[i][j] + b[i][j];
                }
                sr.r.Add(dr);
            }
            return sr;
        }
        public static SERect operator -(SERect a, SERect b)
        {
            if (a.Height != b.Height || a.Width != b.Width)
            {
                throw new Exception("Cannot subtract two SERect with different size.");
            }
            SERect sr = new SERect();
            sr.r = new List<double[]>();
            sr.c = new List<double[]>();
            for (int i = 0; i < a.Height; i++)
            {
                double[] dr = new double[a.Width];
                for (int j = 0; j < a.Width; j++)
                {
                    dr[j] = a[i][j] - b[i][j];
                }
                sr.r.Add(dr);
            }
            return sr;
        }
        public static SERect operator *(SERect a, double b)
        {
            SERect sr = new SERect();
            sr.r = new List<double[]>();
            sr.c = new List<double[]>();
            for (int i = 0; i < a.Height; i++)
            {
                double[] dr = new double[a.Width];
                for (int j = 0; j < a.Width; j++)
                {
                    dr[j] = a[i][j] * b;
                }
                sr.r.Add(dr);
            }
            return sr;
        }
        public static SERect operator /(SERect a, double b)
        {
            SERect sr = new SERect();
            sr.r = new List<double[]>();
            sr.c = new List<double[]>();
            for (int i = 0; i < a.Height; i++)
            {
                double[] dr = new double[a.Width];
                for (int j = 0; j < a.Width; j++)
                {
                    dr[j] = a[i][j] / b;
                }
                sr.r.Add(dr);
            }
            return sr;
        }

        public static SERect operator *(SERect a, SERect b)
        {
            if (a.Width != b.Height)
            {
                throw new Exception("Cannot multiply two SERect with invalid size.");
            }
            SERect sr = new SERect();
            sr.r = new List<double[]>();
            sr.c = new List<double[]>();
            for (int i = 0; i < a.Height; i++)
            {
                double[] dr = new double[b.Width];
                for (int j = 0; j < b.Width; j++)
                {
                    double v = 0;
                    for (int k = 0; k < a.Width; k++)
                    {
                        v += a[i][k] * b[k][j];
                    }
                    dr[j] = v;
                }
                sr.r.Add(dr);
            }
            return sr;
        }

        public static implicit operator SERect(double[][] v)
        {
            return new SERect(v);
        }
        public static implicit operator SERect(Vector3D[] v)
        {
            return new SERect(v);
        }
        public static implicit operator SERect(Vector2D[] v)
        {
            return new SERect(v);
        }

        public static implicit operator double[][](SERect v)
        {
            double[][] dr = new double[v.Height][];
            for (int i = 0; i < v.Height; i++)
            {
                dr[i] = new double[v.Width];
                for (int j = 0; j < v.Width; j++)
                {
                    dr[i][j] = v[i][j];
                }
            }
            return dr;
        }
    }
}
