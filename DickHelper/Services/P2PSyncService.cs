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
            {
                // 简单协议：收到"GET"则返回本地数据，收到"POST"后接收数据并合并
                var cmdBuffer = new byte[4];
                await stream.ReadAsync(cmdBuffer, 0, 4);
                int cmdLen = BitConverter.ToInt32(cmdBuffer, 0);
                var cmdBytes = new byte[cmdLen];
                await stream.ReadAsync(cmdBytes, 0, cmdLen);
                var cmd = Encoding.UTF8.GetString(cmdBytes);
                if (cmd == "GET")
                {
                    var json = JsonSerializer.Serialize(HistoryViewModel.Instance.Records);
                    var jsonBytes = Encoding.UTF8.GetBytes(json);
                    var lenBytes = BitConverter.GetBytes(jsonBytes.Length);
                    await stream.WriteAsync(lenBytes, 0, 4);
                    await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                }
                else if (cmd == "POST")
                {
                    // 先读长度
                    var lenBuffer = new byte[4];
                    await stream.ReadAsync(lenBuffer, 0, 4);
                    int jsonLen = BitConverter.ToInt32(lenBuffer, 0);
                    var jsonBytes = new byte[jsonLen];
                    int read = 0;
                    while (read < jsonLen)
                    {
                        int r = await stream.ReadAsync(jsonBytes, read, jsonLen - read);
                        if (r == 0) break;
                        read += r;
                    }
                    var json = Encoding.UTF8.GetString(jsonBytes);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var incoming = JsonSerializer.Deserialize<List<HistoryRecord>>(json);
                        if (incoming != null)
                            MergeRecords(incoming);
                    }
                    // 返回OK
                    var okBytes = Encoding.UTF8.GetBytes("OK");
                    var okLen = BitConverter.GetBytes(okBytes.Length);
                    await stream.WriteAsync(okLen, 0, 4);
                    await stream.WriteAsync(okBytes, 0, okBytes.Length);
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

        private void MergeRecords(List<HistoryRecord> incoming)
        {
            var local = HistoryViewModel.Instance.Records;
            var comparer = new HistoryRecordComparer();

            bool changed = false;
            foreach (var record in incoming)
            {
                if (!local.Contains(record, comparer))
                {
                    local.Add(record);
                    changed = true;
                }
            }
            if (changed)
            {
                // 合并后立即保存并通知界面刷新
                HistoryViewModel.Instance.SaveHistoryAsync();
                HistoryViewModel.Instance.NotifyRecordsChanged();
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
                    // 发送GET命令
                    var cmdBytes = Encoding.UTF8.GetBytes("GET");
                    var cmdLen = BitConverter.GetBytes(cmdBytes.Length);
                    await stream.WriteAsync(cmdLen, 0, 4);
                    await stream.WriteAsync(cmdBytes, 0, cmdBytes.Length);
                    // 读取对方数据长度
                    var lenBuffer = new byte[4];
                    await stream.ReadAsync(lenBuffer, 0, 4);
                    int jsonLen = BitConverter.ToInt32(lenBuffer, 0);
                    var jsonBytes = new byte[jsonLen];
                    int read = 0;
                    while (read < jsonLen)
                    {
                        int r = await stream.ReadAsync(jsonBytes, read, jsonLen - read);
                        if (r == 0) break;
                        read += r;
                    }
                    var json = Encoding.UTF8.GetString(jsonBytes);
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
                    // 发送POST命令
                    var cmdBytes = Encoding.UTF8.GetBytes("POST");
                    var cmdLen = BitConverter.GetBytes(cmdBytes.Length);
                    await stream.WriteAsync(cmdLen, 0, 4);
                    await stream.WriteAsync(cmdBytes, 0, cmdBytes.Length);
                    // 发送本地数据
                    var localJson = JsonSerializer.Serialize(HistoryViewModel.Instance.Records.ToArray());
                    var localBytes = Encoding.UTF8.GetBytes(localJson);
                    var localLen = BitConverter.GetBytes(localBytes.Length);
                    await stream.WriteAsync(localLen, 0, 4);
                    await stream.WriteAsync(localBytes, 0, localBytes.Length);
                    // 等待OK
                    var okLenBuf = new byte[4];
                    await stream.ReadAsync(okLenBuf, 0, 4);
                    int okLen = BitConverter.ToInt32(okLenBuf, 0);
                    var okBuf = new byte[okLen];
                    int okRead = 0;
                    while (okRead < okLen)
                    {
                        int r = await stream.ReadAsync(okBuf, okRead, okLen - okRead);
                        if (r == 0) break;
                        okRead += r;
                    }
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
