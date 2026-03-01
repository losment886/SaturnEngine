using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SEWirelessDebugger
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("SE Wireless Debugger");

            while (true)
            {
                Console.WriteLine("Enter target host (ip or ip:port). Default port is 52525. Type '/quit' to exit.");
                Console.Write("Host: ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("No host provided.");
                    continue;
                }
                input = input.Trim();
                if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase) || input.Equals("q", StringComparison.OrdinalIgnoreCase))
                    return 0;

                string host = input;
                int port = 52525;
                if (host.Contains(':'))
                {
                    var sp = host.Split(':', 2);
                    host = sp[0];
                    if (!int.TryParse(sp[1], out port)) port = 52525;
                }

                using var client = new TcpClient();
                try
                {
                    Console.WriteLine($"Connecting to {host}:{port}...");
                    // blocking connect with a simple timeout via Begin/End
                    IAsyncResult ar = client.BeginConnect(host, port, null, null);
                    bool success = ar.AsyncWaitHandle.WaitOne(5000);
                    if (!success)
                    {
                        Console.WriteLine("Connection timed out or failed.");
                        try { client.Close(); } catch { }
                        continue;
                    }
                    client.EndConnect(ar);

                    if (!client.Connected)
                    {
                        Console.WriteLine("Connection failed.");
                        try { client.Close(); } catch { }
                        continue;
                    }

                    Console.WriteLine("Connected.");

                    using NetworkStream ns = client.GetStream();
                    using var reader = new StreamReader(ns, Encoding.UTF8);
                    using var writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

                    bool running = true;
                    Thread receiver = new Thread(() =>
                    {
                        try
                        {
                            while (running && client.Connected)
                            {
                                string? line = reader.ReadLine();
                                if (line == null) break;
                                Console.WriteLine($"[Remote] {line}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Receive error: {ex.Message}");
                        }
                        finally
                        {
                            // mark running false so main thread can exit
                            running = false;
                        }
                    }) { IsBackground = true };

                    receiver.Start();

                    Console.WriteLine("Type debug commands (e.g. FUNCTION ARG1 ARG2...) and press Enter. Type '/exit' to disconnect.");
                    while (running)
                    {
                        Console.Write("> ");
                        string? cmd = Console.ReadLine();
                        if (cmd == null) break;
                        cmd = cmd.Trim();
                        if (cmd.Length == 0) continue;
                        if (cmd.Equals("/exit", StringComparison.OrdinalIgnoreCase) || cmd.Equals("q", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                        try
                        {
                            writer.WriteLine(cmd);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Send error: {ex.Message}");
                            break;
                        }
                    }

                    // shutdown
                    running = false;
                    try { client.Close(); } catch { }
                    // give receiver a moment
                    receiver.Join(1000);

                    Console.WriteLine("Disconnected. Returning to host prompt.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    try { client.Close(); } catch { }
                }
            }
        }
    }
}
