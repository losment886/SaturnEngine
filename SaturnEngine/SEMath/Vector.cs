namespace SaturnEngine.SEMath
{
    using SaturnEngine.Asset;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics;

    [StructLayout(LayoutKind.Sequential)]
    public struct Vector2D
    {
        double x, y;
        public double X { get { return x; } set { x = value; } }
        public double Y { get { return y; } set { y = value; } }

        public static readonly Vector2D Empty = new Vector2D();
        public Vector2D(double X = 0, double Y = 0)
        {
            this.X = X;
            this.Y = Y;
        }
        public Vector2D()
        {
            x = 0; y = 0;
        }

        public Vector256<double> GetVector256()
        {
            return Vector256.Create(x, y, 0, 0);
        }

        public static bool operator ==(Vector2D left, Vector2D right)
        {
            return (left.x == right.x && left.y == right.y);
        }
        public static bool operator !=(Vector2D left, Vector2D right)
        {
            return !(left == right);
        }
        public static Vector2D operator +(Vector2D left, Vector2D right)
        {
            return new Vector2D(left.X + right.X, left.Y + right.Y);
        }
        public static Vector2D operator +(Vector2D left, double right)
        {
            return new Vector2D(left.X + right, left.Y + right);
        }
        public static Vector2D operator +(double right, Vector2D left)
        {
            return new Vector2D(left.X + right, left.Y + right);
        }
        public static Vector2D operator -(Vector2D left, Vector2D right)
        {
            return new Vector2D(left.X - right.X, left.Y - right.Y);
        }
        public static Vector2D operator -(Vector2D left, double right)
        {
            return new Vector2D(left.X - right, left.Y - right);
        }
        public static Vector2D operator -(double right, Vector2D left)
        {
            return new Vector2D(left.X - right, left.Y - right);
        }
        public static Vector2D operator *(Vector2D left, Vector2D right)
        {
            return new Vector2D(left.X * right.X, left.Y * right.Y);
        }
        public static Vector2D operator *(Vector2D left, double right)
        {
            return new Vector2D(left.X * right, left.Y * right);
        }
        public static Vector2D operator *(double right, Vector2D left)
        {
            return new Vector2D(left.X * right, left.Y * right);
        }
        public static Vector2D operator /(Vector2D left, Vector2D right)
        {
            return new Vector2D(left.X / right.X, left.Y / right.Y);
        }
        public static Vector2D operator /(Vector2D left, double right)
        {
            return new Vector2D(left.X / right, left.Y / right);
        }
        public static Vector2D operator /(double right, Vector2D left)
        {
            return new Vector2D(left.X / right, left.Y / right);
        }

        public (double xx, double yy) GetValue()
        {
            return (x, y);
        }
        public override string ToString()
        {
            return $"[{x},{y}]";
        }

        public override bool Equals(object? obj)
        {
            return obj is Vector2D other && this == other;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public double GetLength()
        {
            return Math.Sqrt(x * x + y * y);
        }
        public static double GetLength(Vector2D v)
        {
            return Math.Sqrt(v.x * v.x + v.y * v.y);
        }

        public Vector2D GetNormalizedVector2D()
        {
            double length = GetLength();
            return new Vector2D(x / length, y / length);
        }

        public void TurnToNormalizedVector2D()
        {
            double length = GetLength();
            x /= length;
            y /= length;
        }

        public static Vector2D GetNormalizedVector2D(Vector2D v)
        {
            double length = v.GetLength();
            return new Vector2D(v.x / length, v.y / length);
        }

        public double GetDistance(Vector2D other)
        {
            double dx = x - other.x;
            double dy = y - other.y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double GetDistance(Vector2D a, Vector2D b)
        {
            return (b - a).GetLength();
        }

        public double DotProduct(Vector2D other)
        {
            return x * other.x + y * other.y;
        }

        public static double DotProduct(Vector2D a, Vector2D b)
        {
            return a.x * b.x + a.y * b.y;
        }

        public double Projecture(Vector2D other)
        {
            Vector2D normalized = GetNormalizedVector2D();
            return normalized.DotProduct(other);
        }

        public static double Projecture(Vector2D a, Vector2D b)
        {
            Vector2D normalized = a.GetNormalizedVector2D();
            return normalized.DotProduct(b);
        }

        public double GetAngle(Vector2D other)
        {
            double dot = DotProduct(other);
            double lengths = GetLength() * other.GetLength();
            return Math.Acos(dot / lengths) * (180 / Math.PI);
        }

        public static double GetAngle(Vector2D a, Vector2D b)
        {
            double dot = DotProduct(a, b);
            double lengths = a.GetLength() * b.GetLength();
            return Math.Acos(dot / lengths) * (180 / Math.PI);
        }

        public double CrossProduct(Vector2D other)
        {
            return x * other.y - y * other.x;
        }

        public static double CrossProduct(Vector2D a, Vector2D b)
        {
            return a.x * b.y - a.y * b.x;
        }
        public Vector2D RotateAround(Vector2D center, double radians)
        {
            Vector2D translated = this - center;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            Vector2D rotated = new Vector2D(
                translated.x * cos - translated.y * sin,
                translated.x * sin + translated.y * cos
            );
            return rotated + center;
        }
        public void ThisRotateAround(Vector2D center, double radians)
        {
            Vector2D translated = this - center;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            Vector2D rotated = new Vector2D(
                translated.x * cos - translated.y * sin,
                translated.x * sin + translated.y * cos
            );
            this = rotated + center;
        }
        public static Vector2D RotateAround(Vector2D v, Vector2D center, double radians)
        {
            Vector2D translated = v - center;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            Vector2D rotated = new Vector2D(
                translated.x * cos - translated.y * sin,
                translated.x * sin + translated.y * cos
            );
            return rotated + center;
        }
        public Vector2D Rotate(double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Vector2D(
                x * cos - y * sin,
                x * sin + y * cos
            );
        }

        public void ThisRotate(double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double newX = x * cos - y * sin;
            double newY = x * sin + y * cos;
            x = newX;
            y = newY;
        }

        public static Vector2D Rotate(Vector2D v, double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Vector2D(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }

        public Vector2D Zoom(double k)
        {
            return new Vector2D(x * k, y * k);
        }

        public void ThisZoom(double k)
        {
            x *= k;
            y *= k;
        }

        public static Vector2D Zoom(Vector2D v, double k)
        {
            return new Vector2D(v.x * k, v.y * k);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3D

    {
        public static implicit operator Vector3D(SEColor s)
        {
            Vector3D v = new Vector3D(s.R, s.G, s.B);
            return v;
        }
        double x, y, z;
        public double X { get { return x; } set { x = value; } }
        public double Y { get { return y; } set { y = value; } }
        public double Z { get { return z; } set { z = value; } }

        public static readonly Vector3D Empty = new Vector3D();
        public Vector3D(double X = 0, double Y = 0, double Z = 0)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }
        public Vector3D()
        {
            x = 0; y = 0; z = 0;
        }

        public Vector3D(PolarPoint pp)
        {
            this = pp.GetVector3D();
        }

        public Vector256<double> GetVector256()
        {

            return Vector256.Create(x, y, z, 0);
        }
        public static bool operator ==(Vector3D left, Vector3D right)
        {
            return (left.x == right.x && left.y == right.y && left.z == right.z);
        }
        public static bool operator !=(Vector3D left, Vector3D right)
        {
            return !(left == right);
        }
        public static Vector3D operator +(Vector3D left, Vector3D right)
        {
            return new Vector3D(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }
        public static Vector3D operator +(Vector3D left, double right)
        {
            return new Vector3D(left.X + right, left.Y + right, left.Z + right);
        }
        public static Vector3D operator +(double right, Vector3D left)
        {
            return new Vector3D(left.X + right, left.Y + right, left.Z + right);
        }
        public static Vector3D operator -(Vector3D left, Vector3D right)
        {
            return new Vector3D(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }
        public static Vector3D operator -(Vector3D left, double right)
        {
            return new Vector3D(left.X - right, left.Y - right, left.Z - right);
        }
        public static Vector3D operator -(double right, Vector3D left)
        {
            return new Vector3D(left.X - right, left.Y - right, left.Z - right);
        }
        public static Vector3D operator *(Vector3D left, Vector3D right)
        {
            return new Vector3D(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        }
        public static Vector3D operator *(Vector3D left, double right)
        {
            return new Vector3D(left.X * right, left.Y * right, left.Z * right);
        }
        public static Vector3D operator *(double right, Vector3D left)
        {
            return new Vector3D(left.X * right, left.Y * right, left.Z * right);
        }
        public static Vector3D operator /(Vector3D left, Vector3D right)
        {
            return new Vector3D(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
        }
        public static Vector3D operator /(Vector3D left, double right)
        {
            return new Vector3D(left.X / right, left.Y / right, left.Z / right);
        }
        public static Vector3D operator /(double right, Vector3D left)
        {
            return new Vector3D(left.X / right, left.Y / right, left.Z / right);
        }
        public (double xx, double yy, double zz) GetValue()
        {
            return (x, y, z);
        }
        public override string ToString()
        {
            return $"[{x},{y},{z}]";
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public PolarPoint GetPolarPoint()
        {
            double r = 0;
            double a = 0;
            double u = 0;
            r = System.Math.Sqrt(x * x + y * y + z * z);
            if (x >= 0)
            {
                if (y >= 0)
                {
                    if (z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(x / z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(x / -z));
                    }
                }
                else
                {
                    if (z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(x / z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(x / -z));
                    }
                }
            }
            else
            {
                if (y >= 0)
                {
                    if (z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(-x / z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(-x / -z));
                    }
                }
                else
                {
                    if (z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(-x / z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(-x / -z));
                    }
                }
            }
            return new PolarPoint(a, u, r);
        }

        public static PolarPoint GetPolarPoint(Vector3D v)
        {
            double r = 0;
            double a = 0;
            double u = 0;
            r = System.Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            if (v.x >= 0)
            {
                if (v.y >= 0)
                {
                    if (v.z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(v.y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(v.x / v.z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(v.y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(v.x / -v.z));
                    }
                }
                else
                {
                    if (v.z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-v.y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(v.x / v.z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-v.y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(v.x / -v.z));
                    }
                }
            }
            else
            {
                if (v.y >= 0)
                {
                    if (v.z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(v.y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(-v.x / v.z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(v.y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(-v.x / -v.z));
                    }
                }
                else
                {
                    if (v.z >= 0)
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-v.y / r));
                        a = Helper.GetAngleByRadians(System.Math.Atan(-v.x / v.z));
                    }
                    else
                    {
                        u = Helper.GetAngleByRadians(System.Math.Asin(-v.y / r));
                        a = -Helper.GetAngleByRadians(System.Math.Atan(-v.x / -v.z));
                    }
                }
            }
            return new PolarPoint(a, u, r);
        }

        public double GetLength()
        {
            double length;
            double Val = x * x + y * y + z * z;
            length = System.Math.Sqrt(Val);

            return length;
        }
        public static double GetLength(Vector3D v3)
        {
            double length;
            double Val = v3.x * v3.x + v3.y * v3.y + v3.z * v3.z;
            length = System.Math.Sqrt(Val);
            return length;
        }
        /// <summary>
        /// 归一化矢量
        /// </summary>
        /// <returns>返回以This为基类的单位矢量</returns>
        public Vector3D GetNormalizedVector3D()
        {
            Vector3D v = this;
            double Leng = GetLength();
            Vector3D vc = v / Leng;
            return vc;
        }
        /// <summary>
        /// 转换为以This为基类的单位矢量
        /// </summary>
        public void TurnToNormalizedVector3D()
        {
            Vector3D v = this;
            double Leng = GetLength();
            Vector3D vc = v / Leng;
            x = vc.x;
            y = vc.y;
            z = vc.z;
        }
        /// <summary>
        /// 归一化矢量
        /// </summary>
        /// <param name="v3">矢量</param>
        /// <returns>返回以v3为基类的单位矢量</returns>
        public static Vector3D GetNormalizedVector3D(Vector3D v3)
        {
            Vector3D v = v3;
            double Leng = v3.GetLength();
            Vector3D vc = v / Leng;
            return vc;
        }
        /// <summary>
        /// 获取This与v3的距离
        /// </summary>
        /// <param name="v3">预测距B点</param>
        /// <returns>This与v3的距离</returns>
        public double GetDistance(Vector3D v3)
        {
            Vector3D v3s = v3 - this;
            double Distance = v3s.GetLength();
            return Distance;
        }
        /// <summary>
        /// 获取A与B的距离
        /// </summary>
        /// <param name="A">预测距A点</param>
        /// <param name="B">预测距B点</param>
        /// <returns>A与B的距离</returns>
        public static double GetDistance(Vector3D A, Vector3D B)
        {
            double Distance = (B - A).GetLength();
            return Distance;
        }
        /// <summary>
        /// 获取This与B的点积
        /// </summary>
        /// <param name="B">矢量</param>
        /// <returns>This与B的点积</returns>
        public double DotProduct(Vector3D B)
        {
            Vector3D A = this;
            double Value = A.x * B.x + A.y * B.y + A.z * B.z;
            return Value;
        }
        /// <summary>
        /// 获取A与B的点积
        /// </summary>
        /// <param name="A">矢量</param>
        /// <param name="B">矢量</param>
        /// <returns>A与B的点积</returns>
        public static double DotProduct(Vector3D A, Vector3D B)
        {
            double Value = A.x * B.x + A.y * B.y + A.z * B.z;
            return Value;
        }
        /// <summary>
        /// 投影至This所在的单位矢量
        /// </summary>
        /// <param name="B"></param>
        /// <returns>于This中B的长度</returns>
        public double Projecture(Vector3D B)
        {
            Vector3D v3v = GetNormalizedVector3D();
            double Val = v3v.DotProduct(B);
            return Val;
        }
        /// <summary>
        /// 投影至A所在的单位矢量
        /// </summary>
        /// <param name="A">单位矢量</param>
        /// <param name="B">矢量</param>
        /// <returns>于A中B的长度</returns>
        public static double Projecture(Vector3D A, Vector3D B)
        {
            Vector3D v3v = A.GetNormalizedVector3D();
            double Val = v3v.DotProduct(B);
            return Val;
        }
        /// <summary>
        /// 求This与B的夹角角度
        /// </summary>
        /// <param name="B">矢量</param>
        /// <returns>角度</returns>
        public double GetAngle(Vector3D B)
        {
            double Angle;
            double Ve = DotProduct(B) / (GetLength() * B.GetLength());
            Angle = System.Math.Acos(Ve) * 180 / double.Pi;
            return Angle;
        }
        /// <summary>
        /// 求A与B的夹角角度
        /// </summary>
        /// <param name="A">矢量</param>
        /// <param name="B">矢量</param>
        /// <returns>角度</returns>
        public static double GetAngle(Vector3D A, Vector3D B)
        {

            double Angle;
            double Ve = A.DotProduct(B) / A.GetLength() * B.GetLength();
            Angle = System.Math.Acos(Ve) * 180 / double.Pi;
            return Angle;
        }
        /// <summary>
        /// 求This与B的叉积
        /// </summary>
        /// <param name="B">矢量</param>
        /// <returns>This与B的叉积</returns>
        public Vector3D CrossProduct(Vector3D B)
        {
            return new Vector3D(y * B.Z - z * B.Y, z * B.X - x * B.Z, x * B.Y - y * B.X);
        }
        /// <summary>
        /// 求A与B的叉积
        /// </summary>
        /// <param name="A">矢量</param>
        /// <param name="B">矢量</param>
        /// <returns>A与B的叉积</returns>
        public static Vector3D CrossProduct(Vector3D A, Vector3D B)
        {
            return new Vector3D(A.y * B.Z - A.z * B.Y, A.z * B.X - A.x * B.Z, A.x * B.Y - A.y * B.X);
        }

        /// <summary>
        /// 绕X轴旋转
        /// </summary>
        /// <param name="v">矢量</param>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public static Vector3D RotateByX(Vector3D v, double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            return new Vector3D(v.x, v.y * c - v.z * s, v.y * s + v.z * c);
        }

        /// <summary>
        /// 绕X轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public Vector3D RotateByX(double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            return new Vector3D(x, y * c - s, y * s + z * c);
        }

        /// <summary>
        /// 绕X轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        public void ThisRotateByX(double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            double dy = y * c - z * s;
            double dz = y * s + z * c;
            z = dz;
            y = dy;
        }




        /// <summary>
        /// 绕Y轴旋转
        /// </summary>
        /// <param name="v">矢量</param>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public static Vector3D RotateByY(Vector3D v, double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            return new Vector3D(v.x * c + v.z * s, v.y, v.z * c - v.x * s);
        }

        /// <summary>
        /// 绕Y轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public Vector3D RotateByY(double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            return new Vector3D(x * c + z * s, y, z * c - x * s);
        }

        /// <summary>
        /// 绕Y轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        public void ThisRotateByY(double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            double dx = x * c + z * s;
            double dz = z * c - x * s;
            x = dx;
            z = dz;
        }


        /// <summary>
        /// 绕Z轴旋转
        /// </summary>
        /// <param name="v">矢量</param>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public static Vector3D RotateByZ(Vector3D v, double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            return new Vector3D(v.x * c - v.y * s, v.y * c + v.x * s, v.z);
        }

        /// <summary>
        /// 绕Y轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public Vector3D RotateByZ(double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            return new Vector3D(x * c - y * s, y * c + x * s, z);
        }

        /// <summary>
        /// 绕Y轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        public void ThisRotateByZ(double d)
        {
            double c = System.Math.Cos(d);
            double s = System.Math.Sin(d);
            double dx = x * c - y * s;
            double dy = y * c + x * s;
            x = dx;
            y = dy;
        }



        /// <summary>
        /// 绕指定轴旋转
        /// </summary>
        /// <param name="v">矢量</param>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public static Vector3D Rotate(Vector3D v, double d, Vector3D n)
        {
            double cd = System.Math.Cos(d);
            double sd = System.Math.Sin(d);
            /*
            double[,] m = new double[4,4];

            m[1, 1] = n.x * n.x * (1 - cd) + cd;
            m[1, 2] = n.x * n.y * (1 - cd) - n.z * sd;
            m[1, 3] = n.x * n.z * (1 - cd) + n.y * sd;

            m[2, 1] = n.y * n.x * (1 - cd) + n.z * sd;
            m[2, 2] = n.y * n.y * (1 - cd) + cd;
            m[2, 3] = n.y * n.z * (1 - cd) - n.x * sd;

            m[2, 1] = n.z * n.x * (1 - cd) - n.y * sd;
            m[2, 2] = n.z * n.y * (1 - cd) + n.x * sd;
            m[2, 3] = n.z * n.z * (1 - cd) + cd;
            */
            double vx = v.x * (n.x * n.x * (1 - cd) + cd) + v.y * (n.x * n.y * (1 - cd) - n.z * sd) + v.z * (n.x * n.z * (1 - cd) + n.y * sd);
            double vy = v.x * (n.y * n.x * (1 - cd) + n.z * sd) + v.y * (n.y * n.y * (1 - cd) + cd) + v.z * (n.y * n.z * (1 - cd) - n.x * sd);
            double vz = v.x * (n.z * n.x * (1 - cd) - n.y * sd) + v.y * (n.z * n.y * (1 - cd) + n.x * sd) + v.z * (n.z * n.z * (1 - cd) + cd);
            return new Vector3D(vx, vy, vz);
        }

        /// <summary>
        /// 绕指定轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        /// <returns>矢量</returns>
        public Vector3D Rotate(double d, Vector3D n)
        {
            double cd = System.Math.Cos(d);
            double sd = System.Math.Sin(d);
            /*
            double[,] m = new double[4,4];

            m[1, 1] = n.x * n.x * (1 - cd) + cd;
            m[1, 2] = n.x * n.y * (1 - cd) - n.z * sd;
            m[1, 3] = n.x * n.z * (1 - cd) + n.y * sd;

            m[2, 1] = n.y * n.x * (1 - cd) + n.z * sd;
            m[2, 2] = n.y * n.y * (1 - cd) + cd;
            m[2, 3] = n.y * n.z * (1 - cd) - n.x * sd;

            m[2, 1] = n.z * n.x * (1 - cd) - n.y * sd;
            m[2, 2] = n.z * n.y * (1 - cd) + n.x * sd;
            m[2, 3] = n.z * n.z * (1 - cd) + cd;
            */
            double vx = x * (n.x * n.x * (1 - cd) + cd) + y * (n.x * n.y * (1 - cd) - n.z * sd) + z * (n.x * n.z * (1 - cd) + n.y * sd);
            double vy = x * (n.y * n.x * (1 - cd) + n.z * sd) + y * (n.y * n.y * (1 - cd) + cd) + z * (n.y * n.z * (1 - cd) - n.x * sd);
            double vz = x * (n.z * n.x * (1 - cd) - n.y * sd) + y * (n.z * n.y * (1 - cd) + n.x * sd) + z * (n.z * n.z * (1 - cd) + cd);
            return new Vector3D(vx, vy, vz);
        }

        /// <summary>
        /// 绕指定轴旋转
        /// </summary>
        /// <param name="d">弧度（可用Helper中的转换函数进行角度与弧度的转换）</param>
        public void ThisRotate(double d, Vector3D n)
        {
            double cd = System.Math.Cos(d);
            double sd = System.Math.Sin(d);
            /*
            double[,] m = new double[4,4];

            m[1, 1] = n.x * n.x * (1 - cd) + cd;
            m[1, 2] = n.x * n.y * (1 - cd) - n.z * sd;
            m[1, 3] = n.x * n.z * (1 - cd) + n.y * sd;

            m[2, 1] = n.y * n.x * (1 - cd) + n.z * sd;
            m[2, 2] = n.y * n.y * (1 - cd) + cd;
            m[2, 3] = n.y * n.z * (1 - cd) - n.x * sd;

            m[2, 1] = n.z * n.x * (1 - cd) - n.y * sd;
            m[2, 2] = n.z * n.y * (1 - cd) + n.x * sd;
            m[2, 3] = n.z * n.z * (1 - cd) + cd;
            */
            double vx = x * (n.x * n.x * (1 - cd) + cd) + y * (n.x * n.y * (1 - cd) - n.z * sd) + z * (n.x * n.z * (1 - cd) + n.y * sd);
            double vy = x * (n.y * n.x * (1 - cd) + n.z * sd) + y * (n.y * n.y * (1 - cd) + cd) + z * (n.y * n.z * (1 - cd) - n.x * sd);
            double vz = x * (n.z * n.x * (1 - cd) - n.y * sd) + y * (n.z * n.y * (1 - cd) + n.x * sd) + z * (n.z * n.z * (1 - cd) + cd);
            x = vx;
            y = vy;
            z = vz;
        }
        /// <summary>
        /// 绕任意两点构成的轴旋转，可以不经过原点
        /// </summary>
        /// <param name="point1">点1</param>
        /// <param name="point2">点2</param>
        /// <param name="v">基础矢量</param>
        /// <param name="d">弧度（逆时针）</param>
        /// <returns></returns>
        public static Vector3D RotateAroundTowPoint(Vector3D point1, Vector3D point2, Vector3D v, double d)
        {
            Vector3D n = (point2 - point1).GetNormalizedVector3D();
            Vector3D tv = v - point1;
            Vector3D rv = Rotate(tv, d, n);
            return rv + point1;
        }

        /// <summary>
        /// 绕任意两点构成的轴旋转，可以不经过原点
        /// </summary>
        /// <param name="point1">点1</param>
        /// <param name="point2">点2</param>
        /// <param name="d">弧度（逆时针）</param>
        /// <returns></returns>
        public Vector3D RotateAroundTowPoint(Vector3D point1, Vector3D point2, double d)
        {
            Vector3D n = (point2 - point1).GetNormalizedVector3D();
            Vector3D tv = this - point1;
            Vector3D rv = Rotate(tv, d, n);
            return rv + point1;
        }
        /// <summary>
        /// 绕任意两点构成的轴旋转，并赋值，可以不经过原点
        /// </summary>
        /// <param name="point1">点1</param>
        /// <param name="point2">点2</param>
        /// <param name="d">弧度（逆时针）</param>
        /// <returns></returns>
        public void ThisRotateAroundTowPoint(Vector3D point1, Vector3D point2, double d)
        {
            Vector3D n = (point2 - point1).GetNormalizedVector3D();
            Vector3D tv = this - point1;
            Vector3D rv = Rotate(tv, d, n);
            this = rv + point1;
        }

        /// <summary>
        /// 沿主轴缩放
        /// </summary>
        /// <param name="v">矢量</param>
        /// <param name="k">比例因子</param>
        /// <returns>矢量</returns>
        public static Vector3D Zoom(Vector3D v, double k)
        {
            return new Vector3D(v.x * k, v.y * k, v.z * k);
        }

        /// <summary>
        /// 沿主轴缩放
        /// </summary>
        /// <param name="k">比例因子</param>
        /// <returns>矢量</returns>
        public Vector3D Zoom(double k)
        {
            return new Vector3D(x * k, y * k, z * k);
        }

        /// <summary>
        /// 沿主轴缩放
        /// </summary>
        /// <param name="k">比例因子</param>
        /// <returns>矢量</returns>
        public void ThisZoom(double k)
        {
            x *= k;
            y *= k;
            z *= k;
        }


        /// <summary>
        /// 沿给定轴缩放
        /// </summary>
        /// <param name="v">矢量</param>
        /// <param name="k">比例因子</param>
        /// <param name="n">指定轴</param>
        /// <returns>矢量</returns>
        public static Vector3D Zoom(Vector3D v, double k, Vector3D n)
        {

            double nx = v.x * (n.x * n.x * (k - 1) + 1) + v.y * (n.y * n.x * (k - 1)) + v.z * (n.z * n.x * (k - 1));
            double ny = v.x * (n.x * n.y * (k - 1)) + v.y * (n.y * n.y * (k - 1) + 1) + v.z * (n.z * n.y * (k - 1));
            double nz = v.x * (n.x * n.z * (k - 1)) + v.y * (n.y * n.z * (k - 1)) + v.z * (n.z * n.z * (k - 1) + 1);

            return new Vector3D(nx, ny, nz);
        }

        /// <summary>
        /// 沿给定轴缩放
        /// </summary>
        /// <param name="k">比例因子</param>
        /// <param name="n">指定轴</param>
        /// <returns>矢量</returns>
        public Vector3D Zoom(double k, Vector3D n)
        {

            double nx = x * (n.x * n.x * (k - 1) + 1) + y * (n.y * n.x * (k - 1)) + z * (n.z * n.x * (k - 1));
            double ny = x * (n.x * n.y * (k - 1)) + y * (n.y * n.y * (k - 1) + 1) + z * (n.z * n.y * (k - 1));
            double nz = x * (n.x * n.z * (k - 1)) + y * (n.y * n.z * (k - 1)) + z * (n.z * n.z * (k - 1) + 1);

            return new Vector3D(nx, ny, nz);
        }

        /// <summary>
        /// 沿给定轴缩放
        /// </summary>
        /// <param name="k">比例因子</param>
        /// <param name="n">指定轴</param>
        public void ThisZoom(double k, Vector3D n)
        {

            double nx = x * (n.x * n.x * (k - 1) + 1) + y * (n.y * n.x * (k - 1)) + z * (n.z * n.x * (k - 1));
            double ny = x * (n.x * n.y * (k - 1)) + y * (n.y * n.y * (k - 1) + 1) + z * (n.z * n.y * (k - 1));
            double nz = x * (n.x * n.z * (k - 1)) + y * (n.y * n.z * (k - 1)) + z * (n.z * n.z * (k - 1) + 1);

            x = nx;
            y = ny;
            z = nz;
        }

        /// <summary>
        /// 正交投影
        /// </summary>
        /// <param name="v"></param>
        /// <param name="n">平面矢量（注：n与要投影的面垂直，v投影到垂直于n的平面上）</param>
        /// <returns>矢量</returns>
        public static Vector3D Projection(Vector3D v, Vector3D n)
        {
            double nx = v.x * (1 - n.x * n.x) + v.y * (-n.x * n.y) + v.z * (-n.x * n.z);
            double ny = v.x * (-n.x * n.y) + v.y * (1 - n.y * n.y) + v.z * (-n.z * n.y);
            double nz = v.x * (-n.x * n.z) + v.y * (-n.z * n.y) + v.z * (1 - n.z * n.z);

            return new Vector3D(nx, ny, nz);
        }


        /// <summary>
        /// 正交投影
        /// </summary>
        /// <param name="n">平面矢量（注：n与要投影的面垂直，v投影到垂直于n的平面上）</param>
        /// <returns>矢量</returns>
        public Vector3D Projection(Vector3D n)
        {
            double nx = x * (1 - n.x * n.x) + y * (-n.x * n.y) + z * (-n.x * n.z);
            double ny = x * (-n.x * n.y) + y * (1 - n.y * n.y) + z * (-n.z * n.y);
            double nz = x * (-n.x * n.z) + y * (-n.z * n.y) + z * (1 - n.z * n.z);

            return new Vector3D(nx, ny, nz);
        }


        /// <summary>
        /// 正交投影
        /// </summary>
        /// <param name="n">平面矢量（注：n与要投影的面垂直，v投影到垂直于n的平面上）</param>
        public void ThisProjection(Vector3D n)
        {
            double nx = x * (1 - n.x * n.x) + y * (-n.x * n.y) + z * (-n.x * n.z);
            double ny = x * (-n.x * n.y) + y * (1 - n.y * n.y) + z * (-n.z * n.y);
            double nz = x * (-n.x * n.z) + y * (-n.z * n.y) + z * (1 - n.z * n.z);

            x = nx;
            y = ny;
            z = nz;
        }
    }
}
