using SaturnEngine.Global;
using SaturnEngine.Performance;
using SaturnEngine.Security;

namespace SaturnEngine.Base
{
    /// <summary>
    /// Saturn Engine的基础类
    /// </summary>
    public abstract class SEBase
    {

        UUID uid;
        string name;
        string description;
        ulong stc;

        /// <summary>
        /// 全局唯一ID
        /// </summary>
        /// <remarks>
        /// 速度稍慢，但保证唯一性。
        /// </remarks>
        public UUID Uuid { get { return uid; } }
        /// <summary>
        /// 名字
        /// </summary>
        public string Name { get { return name; } }
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get { return description; } }
        /// <summary>
        /// NAME的STC码，速度比UUID快
        /// </summary>
        /// <remarks>
        /// 重名情况下值会相同，建议在不清楚名字时使用UUID。
        /// </remarks>
        public ulong STC { get { return stc; } }

        public SEBase()
        {
            uid = new UUID();
            name = "UnknowName";
            description = "NULL";
            stc = STCCode.GetSTC(name);
            GVariables.SEObjects.Add(this);
        }

        public SEBase(string name = "UnknowName", string description = "NULL")
        {
            uid = new UUID();
            this.name = name;
            this.description = description;
            stc = STCCode.GetSTC(name);
        }

        public SEBase(Type t, string description = "NULL")
        {
            uid = new UUID();
            name = t.Name;
            this.description = description;
            stc = STCCode.GetSTC(name);
        }

        ~SEBase()
        {
            uid.Delete();
            description = null;
            name = null;
            GVariables.SEObjects.Remove(this);
        }


        public double GetCurrentTime()
        {
            //Console.WriteLine(SEMonitor.StopwatchGlobal.Elapsed.TotalSeconds);
            return SEMonitor.StopwatchGlobal.Elapsed.TotalSeconds;
        }
    }
}
