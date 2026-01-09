using System.Runtime.InteropServices;

namespace SaturnEngine.SEMath
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PolarPoint
    {
        //angle between z - x, from +x to +z to -x(>0)|(<0)+x to -z to -x, [-180 , 180] 
        double a;
        //angle between y - z, from -y to +y , [-90, 90]  
        double u;
        //length
        double r;

        public double R
        {
            get { return r; }
            set
            {
                if (value < 0)
                {
                    r = -value;
                    A = a >= 0 ? a - 180 : a + 180;
                    U = -u;
                }
                else
                {
                    r = value;
                }
            }
        }

        public double A
        {
            get { return a; }
            set
            {
                if (value > 180)
                {
                    a = -180 + value % 180;

                }
                else if (value < -180)
                {
                    a = 180 + value % 180;
                }
                else a = value;
            }
        }

        public double U
        {
            get { return u; }
            set
            {
                if (value > 90)
                {
                    u = 90 - value % 90;
                    A = a >= 0 ? a - 180 : a + 180;
                }
                else if (value < -90)
                {
                    u = 90 + value % 90;
                    A = a >= 0 ? a - 180 : a + 180;
                }
                else u = value;
            }
        }

        public PolarPoint()
        {
            a = 0;
            u = 0;
            r = 0;
        }

        public PolarPoint(double a, double u, double r)
        {
            A = a;
            U = u;
            R = r;
        }
        public PolarPoint(Vector3D v)
        {
            this = v.GetPolarPoint();
        }

        public Vector3D GetVector3D()
        {
            double x = 0;
            double y = 0;
            double z = 0;

            double cs = 0;
            if (u >= 0)
            {
                y = r / (System.Math.Sin(Helper.GetRadiansByAngle(u)));
                cs = r / (System.Math.Cos(Helper.GetRadiansByAngle(u)));
            }
            else
            {
                y = -r / (System.Math.Sin(Helper.GetRadiansByAngle(u)));
                cs = r / (System.Math.Cos(Helper.GetRadiansByAngle(u)));
            }
            if (a <= -90)
            {
                double v = a + 180;
                z = -cs * (System.Math.Sin(Helper.GetRadiansByAngle(v)));
                x = -cs * (System.Math.Cos(Helper.GetRadiansByAngle(v)));

            }
            else if (a <= 0)
            {
                double v = a + 90;
                z = -cs * (System.Math.Sin(Helper.GetRadiansByAngle(v)));
                x = cs * (System.Math.Cos(Helper.GetRadiansByAngle(v)));

            }
            else if (a <= 90)
            {
                z = cs * (System.Math.Sin(Helper.GetRadiansByAngle(a)));
                x = cs * (System.Math.Cos(Helper.GetRadiansByAngle(a)));

            }
            else if (a <= 180)
            {
                double v = a - 90;
                z = cs * (System.Math.Sin(Helper.GetRadiansByAngle(v)));
                x = cs * (System.Math.Cos(Helper.GetRadiansByAngle(v)));

            }
            return new Vector3D(x, y, z);
        }
    }
}
