using SaturnEngine.Base;
namespace SaturnEngine.Management.Event
{
    public class DelegateQueue : SEBase
    {
        public bool IsEmpty { get => tasks.Count == 0; }
        public bool IsInvoking { get; set; }
        List<Action> tasks;
        List<Action> taskscac;
        public DelegateQueue(string nm)
            : base(nm, nm + "'s DelegateQueue")
        {
            tasks = new List<Action>();
            taskscac = new List<Action>();
        }
        public void Clean()
        {
            tasks.Clear();
        }
        public void Add(Action a)
        {
            try
            {
                if(!IsInvoking)
                {
                    tasks.Add(a);
                }
                else
                {
                    //throw new InvalidOperationException("Cannot add new tasks while invoking.");
                    taskscac.Add(a);
                }   
            }
            catch (Exception ex)
            {
                // + Name + " when adding Event!!!"
                SELogger.Error(new Str([new Str("Error in ", new Str.StrStyle(Asset.SEColor.Red)), new Str(Name, new Str.StrStyle(Asset.SEColor.Blue)), new Str(" when adding Event!!!", new Str.StrStyle(Asset.SEColor.Red)), ex.ToString()]));
            }
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
                    t?.Invoke();
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