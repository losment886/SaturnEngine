namespace SaturnEngine.SEMath
{
    public enum RotateOption
    {
        ByGivedVector = 0,
        ByX = 1,
        ByY = 2,
        ByZ = 3,

    }
    public class Transform
    {
        public static readonly Vector3D VectorX = new Vector3D(1, 0, 0);
        public static readonly Vector3D VectorY = new Vector3D(0, 1, 0);
        public static readonly Vector3D VectorZ = new Vector3D(0, 0, 1);

        public Vector3D BaseVector;
        public Vector3D AngleVector;
        public Vector3D CacheVector;

        public Transform()
        {
            BaseVector = new Vector3D();
            AngleVector = new Vector3D();
            CacheVector = new Vector3D();
        }

        public void Rotate(double d, RotateOption ro = RotateOption.ByGivedVector, Vector3D? v = null)
        {
            switch (ro)
            {
                case RotateOption.ByGivedVector:
                    CacheVector.ThisRotate(d, v.Value);
                    AngleVector = new Vector3D(CacheVector.GetAngle(VectorX), CacheVector.GetAngle(VectorY), CacheVector.GetAngle(VectorZ));
                    break;
            }
        }
    }
}
