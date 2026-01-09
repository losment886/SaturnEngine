using System;
using System.Collections.Generic;
using System.Text;

namespace SaturnEngine.SEMath
{
    [Flags]
    public enum SELineType : uint
    {
        None = 0,
        LineSegment = 1,//线段
        Circle = 2,//圆
        Ellipse = 3,//椭圆
        Hyperbola = 4,//双曲线
        Parabola = 5,//抛物线
        Ray = 6,//射线
        Line = 7,//直线
        UnShaped = 8,//不规则线
        Sphere = 9,//球
        Cone = 10,//圆锥
    }
    public struct SELine
    {
        public List<Vector3D> Points;//按先后顺序的点，圆和球则是原点，再加一个r(Vector3D.X)，再加一个任圆上（球上）一点的坐标
        //圆锥则是底面圆心坐标，加R，加h
        //直线与射线是两个点，射线向第二个点衍生
        //抛物线则是ax^2 + bx+c的参数a b c，再加一个任一点的坐标
        //双曲线与椭圆是两个交点坐标，还有其上任意一点
        public bool Limited = false;//是否可以无线延申
        public SELineType Type;


        public SELine()
        {
            Points = new List<Vector3D>();
            Type = SELineType.None;
        }
        public static bool operator ==(SELine a,SELine b)
        {
            return a.Type == b.Type && a.Limited == b.Limited && a.Points.Count == b.Points.Count && a.Points[0] == b.Points[0];
        }
        public static bool operator !=(SELine a, SELine b)
        {
            return !(a == b);
        }
    }
}
