using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Line-delimited JSON connection to the Lobotomy Corporation Client (127.0.0.1). A background thread connects,
    /// reconnects and reads; messages are handed to the main thread through a queue.
    /// </summary>
    public sealed class Bridge
    {
        private readonly int _port;
        private readonly Queue<Dictionary<string, object>> _incoming = new Queue<Dictionary<string, object>>();
        private readonly object _lock = new object();
        private readonly object _writeLock = new object();
        private Thread _thread;
        private volatile bool _running;
        private TcpClient _client;
        private NetworkStream _stream;
        private volatile bool _connected;
        private volatile bool _justConnected;

        public Bridge(int port)
        {
            _port = port;
        }

        public bool Connected { get { return _connected; } }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "Archipelago bridge" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            CloseConnection();
        }

        /// <summary>True once after every new connection (used to send the Hello message from the main thread).</summary>
        public bool ConsumeJustConnected()
        {
            if (!_justConnected) return false;
            _justConnected = false;
            return true;
        }

        public bool TryDequeue(out Dictionary<string, object> message)
        {
            lock (_lock)
            {
                if (_incoming.Count > 0)
                {
                    message = _incoming.Dequeue();
                    return true;
                }
            }
            message = null;
            return false;
        }

        public bool Send(Dictionary<string, object> message)
        {
            NetworkStream stream = _stream;
            if (!_connected || stream == null) return false;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(Json.Serialize(message) + "\n");
                lock (_writeLock)
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("Bridge send failed: " + e.Message);
                CloseConnection();
                return false;
            }
        }

        private void Run()
        {
            while (_running)
            {
                try
                {
                    var client = new TcpClient();
                    client.Connect("127.0.0.1", _port);
                    client.NoDelay = true;
                    _client = client;
                    _stream = client.GetStream();
                    _connected = true;
                    _justConnected = true;
                    Log.Info("Connected to the Lobotomy Corporation Client on port " + _port);
                    ReadLoop(_stream);
                }
                catch (SocketException)
                {
                    // Client not running yet.
                }
                catch (IOException)
                {
                    // Connection dropped.
                }
                catch (Exception e)
                {
                    Log.Error("Bridge error", e);
                }
                if (_connected) Log.Info("Disconnected from the Lobotomy Corporation Client");
                CloseConnection();
                for (int i = 0; i < 30 && _running; i++) Thread.Sleep(100);
            }
        }

        private void ReadLoop(NetworkStream stream)
        {
            var buffer = new byte[8192];
            var pending = new List<byte>();
            while (_running)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return;
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] != (byte)'\n')
                    {
                        pending.Add(buffer[i]);
                        continue;
                    }
                    string line = Encoding.UTF8.GetString(pending.ToArray()).Trim();
                    pending.Clear();
                    if (line.Length == 0) continue;
                    try
                    {
                        var message = Json.Parse(line) as Dictionary<string, object>;
                        if (message != null)
                        {
                            lock (_lock) _incoming.Enqueue(message);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn("Invalid message from client: " + e.Message);
                    }
                }
            }
        }

        private void CloseConnection()
        {
            _connected = false;
            try
            {
                if (_stream != null) _stream.Close();
            }
            catch (Exception)
            {
                // ignored
            }
            try
            {
                if (_client != null) _client.Close();
            }
            catch (Exception)
            {
                // ignored
            }
            _stream = null;
            _client = null;
        }
    }
}
