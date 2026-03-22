using SaturnEngine.Base;
using SaturnEngine.Performance;
using static SaturnEngine.Management.Event.DelegateQueue;
namespace SaturnEngine.Management.Event
{
    public class DelegateQueue : SEBase
    {
        
        public class DelegatePackage
        {
            public Action T;
            public bool Invoked;
            public DelegatePackage(Action t)
            {
                T = t;
                Invoked = false;
            }
            public bool WaitForInvoke(ulong timeout = 1000)
            {
                
                while (!Invoked)
                {
                    //Thread.Sleep(1);
                    Dispatcher.Sleep(1);
                    timeout -= 1;
                    if (timeout <= 0)
                        break;
                }
                return Invoked;
            }
        }
        public bool IsEmpty { get => tasks.Count == 0; }
        public bool IsInvoking { get; set; }
        List<DelegatePackage> tasks;
        List<DelegatePackage> taskscac;
        public DelegateQueue(string nm)
            : base(nm, nm + "'s DelegateQueue")
        {
            tasks = new List<DelegatePackage>();
            taskscac = new List<DelegatePackage>();
        }
        public void Clean()
        {
            tasks.Clear();
        }
        public DelegatePackage? Add(Action a)
        {
            try
            {
                DelegatePackage dp;
                if (!IsInvoking)
                {
                    dp = new DelegatePackage(a);
                    tasks.Add(dp);
                }
                else
                {
                    //throw new InvalidOperationException("Cannot add new tasks while invoking.");
                    dp = new DelegatePackage(a);
                    taskscac.Add(dp);
                }   
                return dp;
            }
            catch (Exception ex)
            {
                // + Name + " when adding Event!!!"
                SELogger.Error(new Str([new Str("Error in ", new Str.StrStyle(Asset.SEColor.Red)), new Str(Name, new Str.StrStyle(Asset.SEColor.Blue)), new Str(" when adding Event!!!", new Str.StrStyle(Asset.SEColor.Red)), ex.ToString()]));
            }
            return null;
        }
        public void InvokeAll()
        {
            IsInvoking = true;
            while (tasks.Count > 0)
            {
                try
                {
                    var t = tasks[0];
                    tasks.RemoveAt(0);
                    t?.T?.Invoke();
                    t.Invoked = true;
                }
                catch (Exception ex)
                {
                    // + Name + " when invoking Event!!!"
                    SELogger.Error(new Str([new Str("Error in ", new Str.StrStyle(Asset.SEColor.Red)), new Str(Name, new Str.StrStyle(Asset.SEColor.Blue)), new Str(" when invoking Event!!!", new Str.StrStyle(Asset.SEColor.Red)), ex.ToString()]));
                }
            }
            IsInvoking = false;
        }
        public void ProcessEvent()
        {
            while (taskscac.Count > 0)
            {
                try
                {
                    tasks.Add(taskscac[0]);
                    taskscac.RemoveAt(0);
                }
                catch (Exception ex)
                {
                    // + Name + " when process Event!!!"
                    SELogger.Error(new Str([new Str("Error in ", new Str.StrStyle(Asset.SEColor.Red)), new Str(Name, new Str.StrStyle(Asset.SEColor.Blue)), new Str(" when process Event!!!", new Str.StrStyle(Asset.SEColor.Red)), ex.ToString()]));
                }
            }
        }
    }
}