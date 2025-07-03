using DickHelper.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DickHelper.Services
{
    public class P2PSyncService
    {
        private readonly int _httpPort;
        private readonly int _udpPort;
        private TcpListener? _tcpListener;
        private UdpClient? _udpClient;
        private bool _isRunning;
        private readonly HashSet<string> _knownPeers = new();
        private readonly object _peersLock = new();

        public event Action<string>? PeerDiscovered;
        public event Action<string>? StatusChanged;

        public P2PSyncService(int httpPort = 50505, int udpPort = 50506)
        {
            _httpPort = httpPort;
            _udpPort = udpPort;
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                await StartSocketServerAsync();
                await StartUdpDiscoveryAsync();
                _ = Task.Run(BroadcastPresenceLoop);
                StatusChanged?.Invoke("P2P服务已启动");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"启动失败: {ex.Message}");
                try
                {
                    Stop();
                }
                catch (Exception) { }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _tcpListener?.Stop();
            _udpClient?.Close();
            StatusChanged?.Invoke("P2P服务已停止");
        }


        private Task StartSocketServerAsync()
        {
            _tcpListener = new TcpListener(IPAddress.Any, _httpPort);
            _tcpListener.Start();
            _ = Task.Run(SocketListenLoop);
            return Task.CompletedTask;
        }

        private async Task SocketListenLoop()
        {
            while (_isRunning && _tcpListener != null)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleSocketClient(client));
                }
                catch { }
            }
        }

        private async Task HandleSocketClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true) { AutoFlush = true })
            {
                // 简单协议：收到"GET"则返回本地数据，收到"POST"后接收数据并合并
                var cmd = await reader.ReadLineAsync();
                if (cmd == "GET")
                {
                    var json = JsonSerializer.Serialize(HistoryViewModel.Instance.Records);
                    await writer.WriteLineAsync(json);
                }
                else if (cmd == "POST")
                {
                    var json = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var incoming = JsonSerializer.Deserialize<List<HistoryRecord>>(json);
                        if (incoming != null)
                            MergeRecords(incoming);
                    }
                    await writer.WriteLineAsync("OK");
                }
            }
        }

        private Task StartUdpDiscoveryAsync()
        {
            _udpClient = new UdpClient(_udpPort);
            _ = Task.Run(UdpListenLoop);
            return Task.CompletedTask;
        }



        private static readonly HashSet<string> _localAddresses = GetLocalAddresses();

        private async Task UdpListenLoop()
        {
            while (_isRunning && _udpClient != null)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    if (message.StartsWith("DICKHELPER_PEER:"))
                    {
                        var peerInfo = message.Substring(16);
                        var peerEndpoint = result.RemoteEndPoint.Address.ToString();
                        // 排除本机
                        if (_localAddresses.Contains(peerEndpoint) || peerEndpoint == "127.0.0.1" || peerEndpoint == "::1")
                            continue;
                        lock (_peersLock)
                        {
                            if (_knownPeers.Add(peerEndpoint))
                            {
                                PeerDiscovered?.Invoke(peerEndpoint);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static HashSet<string> GetLocalAddresses()
        {
            var set = new HashSet<string>();
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    set.Add(ua.Address.ToString());
                }
            }
            set.Add("127.0.0.1");
            set.Add("::1");
            return set;
        }

        private async Task BroadcastPresenceLoop()
        {
            using var broadcastClient = new UdpClient();
            broadcastClient.EnableBroadcast = true;
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _udpPort);

            while (_isRunning)
            {
                try
                {
                    var message = $"DICKHELPER_PEER:{_httpPort}";
                    var data = Encoding.UTF8.GetBytes(message);
                    await broadcastClient.SendAsync(data, data.Length, broadcastEndpoint);
                    await Task.Delay(5000); // 每5秒广播一次
                }
                catch { }
            }
        }

        private async Task HandleHttpRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            try
            {
                if (req.HttpMethod == "GET")
                {
                    // 返回本地历史数据
                    var records = HistoryViewModel.Instance.Records;
                    var json = JsonSerializer.Serialize(records);
                    var buffer = Encoding.UTF8.GetBytes(json);
                    resp.ContentType = "application/json";
                    resp.ContentLength64 = buffer.Length;
                    await resp.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else if (req.HttpMethod == "POST")
                {
                    // 接收并合并历史数据
                    using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();
                    var incoming = JsonSerializer.Deserialize<List<HistoryRecord>>(body);
                    if (incoming != null)
                    {
                        MergeRecords(incoming);
                    }
                    resp.StatusCode = 200;
                }
                else
                {
                    resp.StatusCode = 405;
                }
            }
            catch
            {
                resp.StatusCode = 500;
            }
            finally
            {
                resp.Close();
            }
        }

        private void MergeRecords(List<HistoryRecord> incoming)
        {
            var local = HistoryViewModel.Instance.Records;
            var comparer = new HistoryRecordComparer();

            foreach (var record in incoming)
            {
                if (!local.Contains(record, comparer))
                {
                    local.Add(record);
                }
            }
        }

        public async Task SyncWithPeerAsync(string peerAddress)
        {
            try
            {
                // 1. 获取对方数据
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(peerAddress, _httpPort);
                    using var stream = client.GetStream();
                    using var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true) { AutoFlush = true };
                    using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);
                    await writer.WriteLineAsync("GET");
                    var json = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(json))
                    {
                        var incoming = JsonSerializer.Deserialize<HistoryRecord[]>(json);
                        if (incoming != null)
                            MergeRecords(incoming.ToList());
                    }
                }
                // 2. 发送本地数据给对方
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(peerAddress, _httpPort);
                    using var stream = client.GetStream();
                    using var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true) { AutoFlush = true };
                    using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);
                    await writer.WriteLineAsync("POST");
                    var localJson = JsonSerializer.Serialize(HistoryViewModel.Instance.Records.ToArray());
                    await writer.WriteLineAsync(localJson);
                    await reader.ReadLineAsync(); // 等待OK
                }
                StatusChanged?.Invoke($"与 {peerAddress} 同步成功");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"与 {peerAddress} 同步失败: {ex.Message}");
            }
        }

        public string[] GetKnownPeers()
        {
            lock (_peersLock)
            {
                return _knownPeers.ToArray();
            }
        }
    }

    // 简单比较器，按时间和Detail内容判断是否为同一条记录
    public class HistoryRecordComparer : IEqualityComparer<HistoryRecord>
    {
        public bool Equals(HistoryRecord? x, HistoryRecord? y)
        {
            if (x == null || y == null) return false;
            return x.Date == y.Date && x.Duration == y.Duration &&
                   (x.Detail?.Remark ?? "") == (y.Detail?.Remark ?? "");
        }

        public int GetHashCode(HistoryRecord obj)
        {
            return HashCode.Combine(obj.Date, obj.Duration, obj.Detail?.Remark);
        }
    }
}
