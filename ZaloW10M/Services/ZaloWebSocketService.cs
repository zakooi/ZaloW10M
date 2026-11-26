using System;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Newtonsoft.Json.Linq;
using ZaloW10M.Core;
using ZaloW10M.Core.Models;

namespace ZaloW10M.Services
{
    public class ZaloWebSocketService
    {
        private MessageWebSocket _webSocket;
        private DataWriter _dataWriter;
        private ThreadPoolTimer _pingTimer;
        private readonly ZaloCredentials _creds;

        public event Action<ZaloMessage> MessageReceived;
        public event Action<string> StatusChanged;

        public ZaloWebSocketService(ZaloCredentials creds)
        {
            _creds = creds;
        }

        public async Task ConnectAsync()
        {
            if (_webSocket != null) return;
            try
            {
                StatusChanged?.Invoke("Connecting to Zalo Gateway...");
                _webSocket = new MessageWebSocket();
                _webSocket.Control.MessageType = SocketMessageType.Binary;
                _webSocket.MessageReceived += WebSocket_MessageReceived;
                _webSocket.Closed += WebSocket_Closed;

                if (!string.IsNullOrEmpty(_creds.Cookie))
                {
                    _webSocket.SetRequestHeader("Cookie", _creds.Cookie);
                    _webSocket.SetRequestHeader("User-Agent", _creds.UserAgent);
                }

                string wsUrl = _creds.WsUrl ?? "wss://chat.zalo.me/ws/";
                await _webSocket.ConnectAsync(new Uri(wsUrl));
                _dataWriter = new DataWriter(_webSocket.OutputStream);

                StatusChanged?.Invoke("Connected");
                _pingTimer = ThreadPoolTimer.CreatePeriodicTimer(async timer =>
                {
                    await SendPingAsync();
                }, TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                Cleanup();
                StatusChanged?.Invoke($"Connection Error: {ex.Message}");
            }
        }

        public async Task SendPingAsync()
        {
            if (_dataWriter == null) return;
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                byte[] pingFrame = ZaloProtocol.BuildPingFrame(now);
                _dataWriter.WriteBytes(pingFrame);
                await _dataWriter.StoreAsync();
            }
            catch { }
        }

        private void WebSocket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (var reader = args.GetDataReader())
                {
                    byte[] buffer = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(buffer);

                    var (version, cmd, subCmd, dataJson) = ZaloProtocol.ParseWebSocketFrame(buffer, _creds.CipherKey);

                    if (cmd == ZaloProtocol.CMD_HANDSHAKE && subCmd == 1)
                    {
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            var jObj = JObject.Parse(dataJson);
                            _creds.CipherKey = jObj["cipherKey"]?.ToString();
                        }
                    }
                    else if (cmd == ZaloProtocol.CMD_USER_MESSAGE || cmd == ZaloProtocol.CMD_GROUP_MESSAGE)
                    {
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            var jObj = JObject.Parse(dataJson);
                            var msg = new ZaloMessage
                            {
                                MsgId = jObj["msgId"]?.ToString() ?? Guid.NewGuid().ToString(),
                                UidFrom = jObj["uidFrom"]?.ToString(),
                                SenderName = jObj["dName"]?.ToString() ?? "Người gửi",
                                Content = jObj["content"]?.ToString() ?? jObj["message"]?.ToString(),
                                Timestamp = jObj["ts"]?.Value<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                IsOutgoing = jObj["uidFrom"]?.ToString() == _creds.UserId
                            };
                            MessageReceived?.Invoke(msg);
                        }
                    }
                }
            }
            catch { }
        }

        private void WebSocket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Cleanup();
            StatusChanged?.Invoke("Disconnected");
        }

        private void Cleanup()
        {
            _pingTimer?.Cancel();
            _pingTimer = null;
            _dataWriter?.Dispose();
            _dataWriter = null;
            _webSocket?.Dispose();
            _webSocket = null;
        }

        public void Disconnect()
        {
            Cleanup();
        }
    }
}
