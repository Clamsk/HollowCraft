using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HollowCraft
{
    // All Unity work stays on the main thread. The socket thread only moves JSON.
    internal sealed class BridgeClient : IDisposable
    {
        private readonly ConcurrentQueue<JObject> inbound = new ConcurrentQueue<JObject>();
        private readonly ConcurrentQueue<string> outbound = new ConcurrentQueue<string>();
        private readonly AutoResetEvent writeSignal=new AutoResetEvent(false);
        private JObject latestSnapshot,latestProjectiles;
        private string latestVisual;
        private readonly Thread worker;
        private volatile bool stopped;
        private TcpClient socket;
        public volatile bool Connected;
        internal volatile string Campaign="";
        internal volatile int SceneVisit;
        public string LastError = "Waiting for Minecraft world HollowCraft";
        public BridgeClient()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "HollowCraft bridge" };
            worker.Start();
        }
        public bool TryRead(out JObject message) {
            if(inbound.TryDequeue(out message))return true;
            message=Interlocked.Exchange(ref latestProjectiles,null)??Interlocked.Exchange(ref latestSnapshot,null);
            return message!=null;
        }
        public void Send(object value)
        {
            if(!Connected)return;
            var command=JObject.FromObject(value);string line=command.ToString(Formatting.None);
            if(command["scene"]!=null&&command["campaign"]==null&&!string.IsNullOrEmpty(Campaign)) {command["campaign"]=Campaign;line=command.ToString(Formatting.None);}
            if(command["scene"]!=null&&command["visit"]==null) {command["visit"]=SceneVisit;line=command.ToString(Formatting.None);}
            if((string)command["type"]=="visual")Interlocked.Exchange(ref latestVisual,line);
            else outbound.Enqueue(line); // Commands and acknowledgements are never dropped for a render frame.
            writeSignal.Set();
        }
        private void Run()
        {
            while (!stopped)
            {
                Thread sender=null;var session=new CancellationTokenSource();
                try
                {
                    socket = new TcpClient { NoDelay = true };
                    socket.Connect(IPAddress.Loopback, 31674);
                    using (socket)
                    using (var stream = socket.GetStream())
                    using (var reader = new StreamReader(stream))
                    using (var writer = new StreamWriter(stream) { AutoFlush = true })
                    {
                        writer.WriteLine("{\"type\":\"hello\",\"protocol\":1,\"nativeFallEvents\":true,\"nativeProjectileSteps\":true,\"nativeTntSteps\":true,\"minecraftAudio\":true,\"nativeGui\":true,\"nativeHud\":true,\"nativeEntityRender\":true,\"nativeBlockRender\":true}");
                        string hello = reader.ReadLine();
                        if (hello == null || hello.Length > 65536) throw new IOException("Invalid hello");
                        var reply = JObject.Parse(hello);
                        if ((string)reply["type"] != "hello" || (int?)reply["protocol"] != 1)
                            throw new IOException("Bridge protocol mismatch");
                        Connected = true;
                        inbound.Enqueue(reply);
                        var connection=socket;
                        sender=new Thread(()=> {
                            try {
                                while(!stopped&&!session.IsCancellationRequested) {
                                    while(outbound.TryDequeue(out string line))writer.WriteLine(line);
                                    string visual=Interlocked.Exchange(ref latestVisual,null);if(visual!=null)writer.WriteLine(visual);
                                    writeSignal.WaitOne(25);
                                }
                            } catch(Exception error) {LastError=error.Message;connection.Close();}
                        }) {IsBackground=true,Name="HollowCraft commands"};sender.Start();
                        // A blocking reader drains StreamReader's own buffer immediately. Testing
                        // NetworkStream.DataAvailable left buffered attack replies waiting for new data.
                        while (!stopped)
                        {
                            string line=reader.ReadLine();
                            if(line==null||line.Length>2*1024*1024)throw new IOException("Invalid frame");
                            var message=JObject.Parse(line);string type=(string)message["type"];
                            if(type=="snapshot")Interlocked.Exchange(ref latestSnapshot,message);
                            else if(type=="projectiles")Interlocked.Exchange(ref latestProjectiles,message);
                            else inbound.Enqueue(message);
                        }
                    }
                }
                catch (Exception error) { LastError = error.Message; }
                finally
                {
                    Connected = false;
                    session.Cancel();writeSignal.Set();socket?.Close();sender?.Join(500);session.Dispose();
                    while (outbound.TryDequeue(out _)) { }
                    while (inbound.TryDequeue(out _)) { }
                    Interlocked.Exchange(ref latestVisual,null);Interlocked.Exchange(ref latestSnapshot,null);Interlocked.Exchange(ref latestProjectiles,null);
                }
                if (!stopped) Thread.Sleep(1000);
            }
        }
        public void Dispose() { stopped = true; Connected = false; socket?.Close();writeSignal.Set(); }
    }
}
