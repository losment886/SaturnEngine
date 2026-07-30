using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.Performance;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using SaturnEngine.Management.Debugger;

namespace SaturnEngine.Management
{
    public struct Str
    {
        public struct StrStyle
        {
            public SEColor Color;
            //public SEFont Font;
            //public double Size;
            public StrStyle()
            {
                Color = new SEColor(1, 1, 1, 1);
                //Font = new SEFont();
                //Size = 12;
            }
            public StrStyle(SEColor color)
            {
                Color = color;
                //Font = new SEFont();
                //Size = 12;
            }

        }

        public struct Pair<T1, T2>
        {
            public T1 V1;
            public T2 V2;
            public Pair(T1 key, T2 value)
            {
                V1 = key;
                V2 = value;
            }
        }
        public string RawString
        {
            get
            {
                string s = "";
                for (int i = 0; i < Sts.Count; i++)
                {
                    s += Sts[i].V1;
                }
                return s;
            }
        }
        public int Length
        {
            get
            {
                int l = 0;
                for (int i = 0; i < Sts.Count; i++)
                {
                    l += Sts[i].V1.Length;
                }
                return l;
            }
        }
        public bool IsEmpty
        {
            get
            {
                return Length == 0;
            }
        }
        public int FromRawStringIndexGetListIndex(int index)
        {

            int nbox = 0;
            int lg = 0;
            for (; nbox < Sts.Count; nbox++)
            {
                lg += Sts[nbox].V1.Length;
                if (index > lg)
                {
                    return nbox - 1;
                }
            }
            return -1;
        }
        public void RemoveBox(int index)
        {
            if (index < 0 || index >= Sts.Count)
                return;
            Sts.RemoveAt(index);
        }
        public List<Pair<string, StrStyle>> Sts;
        public Str()
        {
            Sts = new List<Pair<string, StrStyle>>();
        }
        public Str(string s, StrStyle style)
        {
            Sts = new List<Pair<string, StrStyle>>();

            Sts.Add(new Pair<string, StrStyle>(s, style));
        }
        public Str(Str[] v)
        {
            Sts = new List<Pair<string, StrStyle>>();
            for (int i = 0; i < v.Length; i++)
            {
                for (int j = 0; j < v[i].Sts.Count; j++)
                {
                    Sts.Add(v[i].Sts[j]);
                }
            }

        }

        public void Add(string s, StrStyle style)
        {
            Sts.Add(new Pair<string, StrStyle>(s, style));
        }
        public void Add(Str[] s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                for (int j = 0; j < s[i].Sts.Count; j++)
                {
                    Sts.Add(s[i].Sts[j]);
                }
            }
        }
        public override string ToString()
        {
            return RawString;
        }
        public static implicit operator Str(string s)
        {
            var st = new Str();
            var sty = new StrStyle();
            //sty.Color = new SEColor(1, 1, 1, 1);
            st.Sts.Add(new Pair<string, StrStyle>(s, sty));

            return st;
        }
        public static explicit operator string(Str s)
        {
            return s.ToString();
        }
        public static Str operator +(Str a, Str b)
        {
            for (int i = 0; i < b.Sts.Count; i++)
            {
                a.Sts.Add(b.Sts[i]);
            }
            return a;
        }
        public static Str operator +(Str a, string b)
        {
            var sty = new StrStyle();
            //sty.Color = new SEColor(1, 1, 1, 1);
            a.Sts.Add(new Pair<string, StrStyle>(b, sty));
            return a;
        }
    }
    public enum SENLTcpMethod
    {
        /// <summary>
        /// 作为服务段，监听端口，等待客户端连接。
        /// </summary>
        Host = 0,
        /// <summary>
        /// 作为客户端，连接到服务端的IP和端口。
        /// </summary>
        Client = 1,
        /// <summary>
        /// 同时作为服务端和客户端，既监听端口又连接到其他服务端。两边同时进行数据收发。
        /// </summary>
        Both = 2,
    }
    /// <summary>
    /// 客户端接入时触发，返回true则接受客户端连接，返回false则拒绝客户端连接。可以在这进行客户端校验等操作，但要确保在退出时对方回话完毕，且不可关闭对象。
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    public delegate bool SENLTcpHostHandler(TcpClient client);
    public enum SENLHostType
    {
        None = 0,
        TCP = 1,
        UDP = 2,
        Both = 3,
    }
    public enum SENLUdpMode
    {
        Broadcast = 0,
        Unicast = 1,
    }
    public struct SENLTcpHostConfig
    {
        public string HostName;
        public IPAddress ListenIp;
        public int ListenPort;
        public List<IPAddress>? AllowIp;
        public List<IPAddress>? BanIp;
        public SENLTcpHostHandler? OnClientConnected;
        public static bool AllowDebug;
    }
    public struct SENLUdpConfig
    {
        public SENLUdpMode Mode;
        public string? TargetHost;
        public IPAddress? BroadcastAddress;
        public int Port;

        public SENLUdpConfig()
        {
            Mode = SENLUdpMode.Broadcast;
            TargetHost = null;
            BroadcastAddress = IPAddress.Broadcast;
            Port = 0;
        }
    }
    public struct SENLDebuggerFunctionConfig
    {
        public List<KeyValuePair<string, Action<string[]>>> FunctionList;
    }
    public enum LogLevel
    {
        None = 0,
        Log = 1,
        Warn = 2,
        Error = 4,

        LogAndWarn = Log | Warn,
        LogAndError = Log | Error,
        WarnAndError = Warn | Error,
        All = Log | Warn | Error,
    }
    /// <summary>
    /// 网络日志系统，为SELogger的拓展，有TCP与UDP两种，TCP支持双向通信，可以无线调试，UDP只支持广播日志，不支持调试。
    /// 调试输入类似命令行
    /// FUNCTIONNAME ARG1 ARG2 ARG3 ...
    /// 
    /// </summary>
    public class SENetLogger
    {
        /// <summary>
        /// Debug function list, used for remote debugging. The key is the function name, the value is the function to execute. The function takes an array of strings as arguments. You must register your own functions to the list.
        /// </summary>
        public static List<KeyValuePair<string, Action<string[]>>> SystemFunctionList = new List<KeyValuePair<string, Action<string[]>>>();
        // subscribe to engine close so network logger can shutdown
        public static void Init()
        {
            try
            {
                GVariables.OnEngineClose += EngineClosed;
                SESystemDebugFunction.Adder(ref SystemFunctionList);
            }
            catch { }
        }
        
        public static bool TCPSupport { get; private set; } = true;
        public static bool UDPSupport { get; private set; } = false;
        public static SENLHostType HostType { get; private set; } = SENLHostType.None;
        public static LogLevel LogLevel { get; set; } = LogLevel.All;
        static bool tcprunning = false;
        static bool udprunning = false;
        static SENLTcpMethod cactm; 
        static SENLTcpHostConfig cachc;
        static SENLDebuggerFunctionConfig cacfc;
        static SENLUdpConfig cacuc;
        static SEThread tcpt;
        static CancellationTokenSource? tcpCts;
        static Task? reconnectTask;

        // TCP runtime objects
        static TcpListener? tcplistener;
        static List<TcpClient> tcpClients = new List<TcpClient>();
        static TcpClient? tcpClientOut;
        static object clientLock = new object();

        // Broadcast queue for async sending
        static System.Collections.Concurrent.ConcurrentQueue<string> broadcastQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
        static SemaphoreSlim broadcastSemaphore = new SemaphoreSlim(0);
        static Task? broadcastTask;

        // UDP runtime objects
        static UdpClient? udpClient;
        static IPEndPoint? udpTarget;

        const int DefaultPort = 52525;

        static (string host, int port) ParseHostPort(string hostName, int defaultPort)
        {
            string host = hostName;
            int port = defaultPort;
            if (host.Contains(':'))
            {
                var sp = host.Split(':');
                host = sp[0];
                int.TryParse(sp[1], out port);
            }
            return (host, port);
        }

        static void StopTcp()
        {
            tcprunning = false;
            try { tcpCts?.Cancel(); } catch { }
            try
            {
                tcpt?.Dispose();
                tcpt = null;
            }
            catch { }
            try { reconnectTask?.Wait(1000); } catch { }
            reconnectTask = null;
        }

        /// <summary>
        /// Gracefully shutdown the SENetLogger (stop TCP/UDP, close sockets/clients).
        /// Called when engine closes.
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                // stop threads
                tcprunning = false;
                udprunning = false;

                // cancel tokens
                try { tcpCts?.Cancel(); } catch { }
                tcpCts = null;

                // signal broadcast task to exit
                broadcastSemaphore.Release();
                try { broadcastTask?.Wait(1000); } catch { }
                broadcastTask = null;

                // wait for reconnect task
                try { reconnectTask?.Wait(1000); } catch { }
                reconnectTask = null;

                // stop tcp worker thread
                try { tcpt?.Dispose(); } catch { }
                tcpt = null;

                // stop listener
                try { tcplistener?.Stop(); } catch { }
                tcplistener = null;

                // close outbound client
                lock (clientLock)
                {
                    try { if (tcpClientOut != null) { tcpClientOut.Close(); } } catch { }
                    tcpClientOut = null;
                }

                // close all connected clients
                lock (clientLock)
                {
                    for (int i = tcpClients.Count - 1; i >= 0; i--)
                    {
                        try { tcpClients[i].Close(); } catch { }
                    }
                    tcpClients.Clear();
                }

                // close UDP client
                try { udpClient?.Close(); } catch { }
                udpClient = null;
                udpTarget = null;
            }
            catch { }
        }

        static void EngineClosed()
        {
            try { Shutdown(); } catch { }
        }

        static void RegUdp(SENLUdpConfig? uc)
        {
            try
            {
                if (udpClient != null)
                {
                    try { udpClient.Close(); } catch { }
                    udpClient = null;
                }

                cacuc = uc ?? new SENLUdpConfig();
                int port = cacuc.Port == 0 ? DefaultPort : cacuc.Port;

                if (cacuc.Mode == SENLUdpMode.Broadcast)
                {
                    udpClient = new UdpClient();
                    udpClient.EnableBroadcast = true;
                    var broadcastAddr = cacuc.BroadcastAddress ?? IPAddress.Broadcast;
                    udpTarget = new IPEndPoint(broadcastAddr, port);
                }
                else // Unicast
                {
                    if (!string.IsNullOrEmpty(cacuc.TargetHost))
                    {
                        var (host, targetPort) = ParseHostPort(cacuc.TargetHost, port);
                        udpClient = new UdpClient();
                        // Resolve host to IP
                        try
                        {
                            var addresses = Dns.GetHostAddresses(host);
                            if (addresses.Length > 0)
                            {
                                udpTarget = new IPEndPoint(addresses[0], targetPort);
                            }
                        }
                        catch
                        {
                            // Try parsing as direct IP
                            if (IPAddress.TryParse(host, out var ip))
                            {
                                udpTarget = new IPEndPoint(ip, targetPort);
                            }
                        }
                    }
                }

                if (udpClient != null && udpTarget != null)
                {
                    udprunning = true;
                    UDPSupport = true;
                }
            }
            catch
            {
                udprunning = false;
                UDPSupport = false;
                try { udpClient?.Close(); } catch { }
                udpClient = null;
            }
        }

        static void RegTcp(SENLTcpMethod? tm, SENLTcpHostConfig? hc, SENLDebuggerFunctionConfig? fc)
        {
            StopTcp();
            cactm = tm ?? SENLTcpMethod.Host;
            cachc = hc ?? new SENLTcpHostConfig();
            cacfc = fc ?? new SENLDebuggerFunctionConfig();

            // start tcp worker if needed
            if ((HostType == SENLHostType.TCP || HostType == SENLHostType.Both) && TCPSupport)
            {
                tcprunning = true;
                tcpCts = new CancellationTokenSource();
                try
                {
                    // Start broadcast sender task
                    broadcastTask = Task.Run(BroadcastSenderWorker);

                    // Start reconnect task if in client mode
                    if (cactm == SENLTcpMethod.Client || cactm == SENLTcpMethod.Both)
                    {
                        reconnectTask = Task.Run(() => ReconnectWorker(tcpCts.Token));
                    }

                    tcpt = Dispatcher.CreateThread(new ThreadStart(TcpWorker), ThreadPriority.BelowNormal);
                    tcpt.Start();
                }
                catch
                {
                    tcprunning = false;
                }
            }

        }
        public static void Register(SENLHostType ht, SENLTcpMethod? tm, SENLTcpHostConfig? hc, SENLDebuggerFunctionConfig? fc, SENLUdpConfig? uc = null)
        {
            switch (ht)
            {
                case SENLHostType.TCP:
                    HostType = ht;
                    RegTcp(tm, hc, fc);
                    break;
                case SENLHostType.UDP:
                    HostType = ht;
                    RegUdp(uc);
                    break;
                case SENLHostType.Both:
                    HostType = ht;
                    RegTcp(tm, hc, fc);
                    RegUdp(uc);
                    break;
                default:
                    return;
            }
        }


        static async Task ReconnectWorker(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && tcprunning)
                {
                    bool needReconnect = false;
                    lock (clientLock)
                    {
                        needReconnect = tcpClientOut == null;
                    }

                    if (needReconnect && !string.IsNullOrEmpty(cachc.HostName))
                    {
                        try
                        {
                            var (host, port) = ParseHostPort(cachc.HostName, DefaultPort);
                            var nc = new TcpClient();
                            var connectTask = nc.ConnectAsync(host, port);
                            await Task.WhenAny(connectTask, Task.Delay(2000, ct)).ConfigureAwait(false);

                            if (nc.Connected)
                            {
                                lock (clientLock)
                                {
                                    if (tcpClientOut == null) // double check
                                    {
                                        tcpClientOut = nc;
                                        Task.Run(() => HandleClientReceive(nc));
                                    }
                                    else
                                    {
                                        try { nc.Close(); } catch { }
                                    }
                                }
                            }
                            else
                            {
                                try { nc.Close(); } catch { }
                            }
                        }
                        catch { }
                    }

                    // Wait 2 seconds before next reconnect attempt
                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        static void TcpWorker()
        {
            try
            {
                // start listener if hosting
                if (cactm == SENLTcpMethod.Host || cactm == SENLTcpMethod.Both)
                {
                    IPAddress ip = IPAddress.Any;
                    try { if (cachc.ListenIp != null) ip = cachc.ListenIp; } catch { }
                    tcplistener = new TcpListener(ip, cachc.ListenPort == 0 ? DefaultPort : cachc.ListenPort);
                    tcplistener.Start();
                }

                // if client mode, try initial connection
                if ((cactm == SENLTcpMethod.Client || cactm == SENLTcpMethod.Both) && !string.IsNullOrEmpty(cachc.HostName))
                {
                    try
                    {
                        var (host, port) = ParseHostPort(cachc.HostName, cachc.ListenPort == 0 ? DefaultPort : cachc.ListenPort);
                        var newClient = new TcpClient();
                        var t = newClient.ConnectAsync(host, port);
                        t.Wait(2000);
                        if (!newClient.Connected)
                        {
                            try { newClient.Close(); } catch { }
                        }
                        else
                        {
                            lock (clientLock)
                            {
                                tcpClientOut = newClient;
                            }
                            // start receive loop for outbound connection
                            Task.Run(() => HandleClientReceive(newClient));
                        }
                    }
                    catch { }
                }

                // Accept loop
                while (tcprunning && tcplistener != null)
                {
                    try
                    {
                        var client = tcplistener.AcceptTcpClient();
                        // check allow/ban
                        try
                        {
                            var remote = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                            if (cachc.BanIp != null && cachc.BanIp.Contains(remote))
                            {
                                try { client.Close(); } catch { }
                            }
                            else if (cachc.AllowIp != null && cachc.AllowIp.Count > 0 && !cachc.AllowIp.Contains(remote))
                            {
                                try { client.Close(); } catch { }
                            }
                            else
                            {
                                bool accept = true;
                                try { if (cachc.OnClientConnected != null) accept = cachc.OnClientConnected(client); } catch { }
                                if (!accept) { try { client.Close(); } catch { } }
                                else
                                {
                                    lock (clientLock) { tcpClients.Add(client); }
                                    Task.Run(() => HandleClientReceive(client));
                                }
                            }
                        }
                        catch { try { client.Close(); } catch { } }
                    }
                    catch (SocketException) 
                    { 
                        // Listener stopped, exit loop
                        if (!tcprunning) break;
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                try { tcplistener?.Stop(); } catch { }
            }
        }

        static bool TryDispatchCommand(string line)
        {
            try
            {
                // parse command: FUNCTION ARG1 ARG2 ...
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return false;
                var fname = parts[0];
                var args = parts.Skip(1).ToArray();

                // First check SystemFunctionList
                foreach (var kv in SystemFunctionList)
                {
                    if (string.Equals(kv.Key, fname, StringComparison.OrdinalIgnoreCase))
                    {
                        try { kv.Value(args); } catch { }
                        return true;
                    }
                }

                // If not found in system functions, check user functions
                if (cacfc.FunctionList != null)
                {
                    foreach (var kv in cacfc.FunctionList)
                    {
                        if (string.Equals(kv.Key, fname, StringComparison.OrdinalIgnoreCase))
                        {
                            try { kv.Value(args); } catch { }
                            return true;
                        }
                    }
                }

                return false;
            }
            catch { return false; }
        }

        static async Task HandleClientReceive(TcpClient client)
        {
            try
            {
                using var ns = client.GetStream();
                using var sr = new StreamReader(ns, Encoding.UTF8);
                while (tcprunning && client.Connected)
                {
                    string? line = await sr.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    TryDispatchCommand(line);
                }
            }
            catch { }
            finally
            {
                try 
                { 
                    lock (clientLock) 
                    { 
                        // If this is the outbound client, clear it so reconnect logic can trigger
                        if (ReferenceEquals(client, tcpClientOut))
                        {
                            tcpClientOut = null;
                        }
                        tcpClients.Remove(client); 
                    } 
                } 
                catch { }
                try { client.Close(); } catch { }
            }
        }

        static async Task BroadcastSenderWorker()
        {
            try
            {
                while (tcprunning)
                {
                    await broadcastSemaphore.WaitAsync().ConfigureAwait(false);
                    if (!tcprunning) break;

                    while (broadcastQueue.TryDequeue(out string? message))
                    {
                        if (message == null) continue;
                        byte[] data = Encoding.UTF8.GetBytes(message + "\n");

                        // Get snapshot of clients
                        List<TcpClient> clientsCopy;
                        TcpClient? outClientCopy;
                        lock (clientLock)
                        {
                            clientsCopy = new List<TcpClient>(tcpClients);
                            outClientCopy = tcpClientOut;
                        }

                        // Send to all connected clients (non-blocking)
                        for (int i = clientsCopy.Count - 1; i >= 0; i--)
                        {
                            var c = clientsCopy[i];
                            try
                            {
                                if (c.Connected)
                                {
                                    var ns = c.GetStream();
                                    await ns.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                                }
                                else
                                {
                                    lock (clientLock)
                                    {
                                        c.Close();
                                        tcpClients.Remove(c);
                                    }
                                }
                            }
                            catch
                            {
                                try
                                {
                                    lock (clientLock)
                                    {
                                        c.Close();
                                        tcpClients.Remove(c);
                                    }
                                }
                                catch { }
                            }
                        }

                        // Send to outbound client if exists
                        if (outClientCopy != null)
                        {
                            try
                            {
                                if (outClientCopy.Connected)
                                {
                                    var ns = outClientCopy.GetStream();
                                    await ns.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                                }
                            }
                            catch { }
                        }

                        // Send via UDP if enabled
                        if (udprunning && udpClient != null && udpTarget != null)
                        {
                            try
                            {
                                udpClient.Send(data, data.Length, udpTarget);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        static void Broadcast(string s)
        {
            broadcastQueue.Enqueue(s);
            try { broadcastSemaphore.Release(); } catch { }
        }

        static bool LevelAllows(LogLevel level)
        {
            return (LogLevel & level) != 0;
        }

        public static void Log(Str message, string sender)
        {
            if (!LevelAllows(LogLevel.Log)) return;
            try
            {
                var txt = message.RawString;
                Broadcast($"[{sender}]<LOG({DateTime.Now})>{txt}");
            }
            catch { }
        }
        public static void Warn(Str message, string sender)
        {
            if (!LevelAllows(LogLevel.Warn)) return;
            try
            {
                var txt = message.RawString;
                Broadcast($"[{sender}]<WARN({DateTime.Now})>{txt}");
            }
            catch { }
        }
        public static void Error(Str message, string sender)
        {
            if (!LevelAllows(LogLevel.Error)) return;
            try
            {
                var txt = message.RawString;
                Broadcast($"[{sender}]<ERROR({DateTime.Now})>{txt}");
            }
            catch { }
        }
    }
    public class SELogger
    {
        static object clientLock = new object();
        public static string? Input()
        {
            lock(clientLock)
            return Console.ReadLine();
        }
        public static void Log(Str message) => Log(message, "Saturn Engine");
        public static void Log(Str message,string sender)
        {
            lock(clientLock)
            {
                if (GVariables.LogOnline)
                    SENetLogger.Log(message, sender);
                if (!GVariables.AllowConsoleOutput) return;
                var pr = Console.ForegroundColor;
                Console.Write($"[");
                Console.ForegroundColor = ConsoleColor.Blue;
                //Console.Write(part.V1);
                Console.Write(sender);
                Console.ForegroundColor = pr;
                Console.Write("]<");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("LOG");
                Console.ForegroundColor = pr;
                Console.Write("(");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"{DateTime.Now}");
                Console.ForegroundColor = pr;

                //]<LOG({DateTime.Now})>
                Console.Write($")>");
                for (int i = 0; i < message.Sts.Count; i++)
                {
                    var part = message.Sts[i];
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = part.V2.Color.GetConsoleColor();
                    Console.Write(part.V1);
                    Console.ForegroundColor = prevColor;
                }
                Console.WriteLine();
            }
            
        }
        public static void Warn(Str message) => Warn(message, "Saturn Engine");
        public static void Warn(Str message, string sender )
        {
            lock(clientLock)
            {
                if (GVariables.LogOnline)
                    SENetLogger.Warn(message, sender);
                if (!GVariables.AllowConsoleOutput) return;
                var pr = Console.ForegroundColor;
                Console.Write($"[");
                Console.ForegroundColor = ConsoleColor.Blue;
                //Console.Write(part.V1);
                Console.Write(sender);
                Console.ForegroundColor = pr;
                Console.Write("]<");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("WARN");
                Console.ForegroundColor = pr;
                Console.Write("(");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"{DateTime.Now}");
                Console.ForegroundColor = pr;

                //]<LOG({DateTime.Now})>
                Console.Write($")>");
                for (int i = 0; i < message.Sts.Count; i++)
                {
                    var part = message.Sts[i];
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = part.V2.Color.GetConsoleColor();
                    Console.Write(part.V1);
                    Console.ForegroundColor = prevColor;
                }
                Console.WriteLine();
            }
        }
        public static void Error(Str message) => Error(message, "Saturn Engine");
        public static void Error(Str message, string sender)
        {
            lock(clientLock)
            {
                if (GVariables.LogOnline)
                    SENetLogger.Error(message, sender);
                if (!GVariables.AllowConsoleOutput) return;
                var pr = Console.ForegroundColor;
                Console.Write($"[");
                Console.ForegroundColor = ConsoleColor.Blue;
                //Console.Write(part.V1);
                Console.Write(sender);
                Console.ForegroundColor = pr;
                Console.Write("]<");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("ERROR");
                Console.ForegroundColor = pr;
                Console.Write("(");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"{DateTime.Now}");
                Console.ForegroundColor = pr;

                //]<LOG({DateTime.Now})>
                Console.Write($")>");
                for (int i = 0; i < message.Sts.Count; i++)
                {
                    var part = message.Sts[i];
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = part.V2.Color.GetConsoleColor();
                    Console.Write(part.V1);
                    Console.ForegroundColor = prevColor;
                }
                Console.WriteLine();
            }
        }
    }
}
