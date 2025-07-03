using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DickHelper.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DickHelper.ViewModels
{
    public partial class SyncViewModel : ObservableObject
    {
        [RelayCommand]
        public void RefreshPeers()
        {
            if (_p2pService != null)
            {
                DiscoveredPeers.Clear();
                foreach (var peer in _p2pService.GetKnownPeers())
                {
                    DiscoveredPeers.Add(peer);
                }
            }
        }
        private P2PSyncService? _p2pService;

        [ObservableProperty]
        private string _syncStatus = "P2P服务未启动";

        [ObservableProperty]
        private string _selectedPeer = "";

        public ObservableCollection<string> DiscoveredPeers { get; } = new();

        [RelayCommand]
        private async Task StartP2PServiceAsync()
        {
            if (_p2pService == null)
            {
                _p2pService = new P2PSyncService();
                _p2pService.StatusChanged += status => SyncStatus = status;
                _p2pService.PeerDiscovered += peer =>
                {
                    if (!DiscoveredPeers.Contains(peer))
                    {
                        DiscoveredPeers.Add(peer);
                    }
                };
                await _p2pService.StartAsync();
            }
        }

        [RelayCommand]
        private void StopP2PService()
        {
            _p2pService?.Stop();
            _p2pService = null;
            DiscoveredPeers.Clear();
            SyncStatus = "P2P服务已停止";
        }

        [RelayCommand]
        private async Task SyncWithPeerAsync()
        {
            if (_p2pService != null && !string.IsNullOrWhiteSpace(SelectedPeer))
            {
                await _p2pService.SyncWithPeerAsync(SelectedPeer);
            }
        }

        [RelayCommand]
        private async Task SyncWithAllPeersAsync()
        {
            if (_p2pService != null)
            {
                var peers = _p2pService.GetKnownPeers();
                foreach (var peer in peers)
                {
                    await _p2pService.SyncWithPeerAsync(peer);
                }
            }
        }
    }
}
