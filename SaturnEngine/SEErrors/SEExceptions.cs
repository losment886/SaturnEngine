namespace SaturnEngine.SEErrors
{
    public class SEException : Exception
    {
        public SEException() : base($"[SEException=>UnknowException]")
        {
        }
        public SEException(string message) : base($"[SEException=>UnknowException] MESSAGE:{message}")
        {

        }
        public override string ToString()
        {
            return $"[SEException=>{(InnerException != null ? GetType() : "UnknowException")}] MESSAGE:{Message}";
        }
    }

    public class SEResourceVersionOldException : Exception
    {
        public SEResourceVersionOldException() : base($"[SEException=>SEResourceVersionOldException]")
        {
        }
        public SEResourceVersionOldException(string message) : base($"[SEException=>SEResourceVersionOldException] MESSAGE:{message}")
        {

        }
        public override string ToString()
        {
            return $"[SEException=>{(InnerException != null ? GetType() : "SEResourceVersionOldException")}] MESSAGE:{Message}";
        }
    }
}
