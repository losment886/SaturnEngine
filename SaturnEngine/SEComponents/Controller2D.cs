using SaturnEngine.Asset;
using SaturnEngine.SEMath;

namespace SaturnEngine.SEComponents
{
    public class Controller2D : SEComponent
    {
        public double MoveSpeed { get; set; } = 1.0;
        public double JumpSpeed { get; set; } = 1.0;
        public Vector2D Gravity { get; set; } = new Vector2D(0, -9.8);
        public bool IsGrounded { get; set; } = false;//是否在地面上
        public Controller2D()
        {
            CType = SEComponentType.Controller2D;
        }

    }
}
