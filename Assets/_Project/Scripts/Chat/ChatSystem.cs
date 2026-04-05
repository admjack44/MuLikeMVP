using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MuLike.Core;
using MuLike.Social;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Chat
{
    /// <summary>
    /// Social chat orchestration on top of ChatClientSystem.
    /// Adds trade channel, profanity filtering, spam reports, @mentions, push-style notifications,
    /// whisper helpers, and optional microphone capture hooks for party VOIP transport.
    /// </summary>
    public sealed class ChatSystem : MonoBehaviour
    {
        [Serializable]
        public struct PushNotificationRequest
        {
            public string title;
            public string body;
            public ChatChannel channel;
        }

        [Serializable]
        public struct PlayerReport
        {
            public string reporter;
            public string target;
            public string reason;
            public long timestampUnixMs;
        }

        [Serializable]
        public struct VoiceFrame
        {
            public int sampleRate;
            public int channelCount;
            public float[] samples;
        }

        [Header("Runtime")]
        [SerializeField] private string[] _blockedWords = { "spamword" };
        [SerializeField, Min(1)] private int _maxMessagesPerWindow = 6;
        [SerializeField, Min(0.5f)] private float _spamWindowSeconds = 5f;
        [SerializeField, Min(100)] private int _voiceFrameSamples = 1024;
        [SerializeField] private bool _emitPushWhenUnfocused = true;

        [Header("Dependencies")]
        [SerializeField] private PartyManager _partyManager;
        [SerializeField] private GuildManager _guildManager;
        [SerializeField] private FriendSystem _friendSystem;

        private readonly Queue<float> _sentMessageTimes = new();
        private readonly List<PlayerReport> _reports = new();
        private readonly Dictionary<string, int> _mentionCounts = new(StringComparer.OrdinalIgnoreCase);

        private ChatClientSystem _chatClientSystem;
        private string _voiceDeviceName;
        private AudioClip _micClip;
        private int _lastVoiceSample;

        public IReadOnlyList<ChatMessage> Messages => _chatClientSystem != null ? _chatClientSystem.Messages : Array.Empty<ChatMessage>();
        public IReadOnlyList<PlayerReport> Reports => _reports;
        public bool IsVoiceActive { get; private set; }
        public string LocalPlayerName => _chatClientSystem != null ? _chatClientSystem.LocalPlayerName : "Player";

        public event Action<ChatMessage> OnMessageReceived;
        public event Action<PushNotificationRequest> OnPushNotificationRequested;
        public event Action<PlayerReport> OnPlayerReported;
        public event Action<string, string> OnGuildMentionDetected;
        public event Action<bool> OnVoiceStateChanged;
        public event Action<VoiceFrame> OnVoiceFrameCaptured;

        private void Awake()
        {
            if (_partyManager == null)
                _partyManager = FindAnyObjectByType<PartyManager>();
            if (_guildManager == null)
                _guildManager = FindAnyObjectByType<GuildManager>();
            if (_friendSystem == null)
                _friendSystem = FindAnyObjectByType<FriendSystem>();

            if (!GameContext.TryGetSystem(out _chatClientSystem) || _chatClientSystem == null)
            {
                _chatClientSystem = new ChatClientSystem();
                GameContext.RegisterSystem(_chatClientSystem);
            }

            if (!_chatClientSystem.HasTransport)
                _chatClientSystem.AttachTransport(new MockChatTransport());

            _chatClientSystem.OnMessageReceived += HandleMessageReceived;
            GameContext.RegisterSystem(this);
        }

        private void OnDestroy()
        {
            if (_chatClientSystem != null)
                _chatClientSystem.OnMessageReceived -= HandleMessageReceived;

            StopPartyVoice();
        }

        private void Update()
        {
            if (!IsVoiceActive || _micClip == null)
                return;

            CaptureVoiceFrame();
        }

        public async Task<bool> SendAsync(ChatSendRequest request, Action<string> onError = null)
        {
            if (_chatClientSystem == null)
            {
                onError?.Invoke("Chat client is not available.");
                return false;
            }

            ChatSendRequest prepared = request;
            prepared.Target = ChatSanitizer.SanitizeName(prepared.Target);
            prepared.Text = FilterBlockedWords(ChatSanitizer.SanitizeText(prepared.Text));

            if (!ValidateSocialChannel(prepared, out string error))
            {
                onError?.Invoke(error);
                return false;
            }

            if (IsSpamBurst())
            {
                onError?.Invoke("Too many messages sent too quickly.");
                return false;
            }

            bool sent = await _chatClientSystem.SendAsync(prepared, onError);
            if (!sent)
                return false;

            RegisterSendTime();

            if (prepared.Channel == ChatChannel.Guild)
                DetectMentions(prepared.Text);

            return true;
        }

        public void Clear()
        {
            _chatClientSystem?.Clear();
        }

        public void ReceiveSystemMessage(string text)
        {
            _chatClientSystem?.ReceiveSystemMessage(text);
        }

        public Task<bool> SendWhisperAsync(string target, string text, Action<string> onError = null)
        {
            return SendAsync(new ChatSendRequest
            {
                Channel = ChatChannel.Private,
                Target = target,
                Text = text
            }, onError);
        }

        public bool ReportPlayer(string target, string reason)
        {
            target = ChatSanitizer.SanitizeName(target);
            reason = ChatSanitizer.SanitizeText(reason);
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(reason))
                return false;

            var report = new PlayerReport
            {
                reporter = LocalPlayerName,
                target = target,
                reason = reason,
                timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _reports.Add(report);
            OnPlayerReported?.Invoke(report);
            ReceiveSystemMessage($"Reported {target} for {reason}.");
            return true;
        }

        public bool StartPartyVoice(string deviceName = null)
        {
            if (IsVoiceActive)
                return true;

            if (_partyManager == null || !_partyManager.HasParty)
                return false;

            if (Microphone.devices == null || Microphone.devices.Length == 0)
                return false;

            _voiceDeviceName = string.IsNullOrWhiteSpace(deviceName) ? Microphone.devices[0] : deviceName;
            _micClip = Microphone.Start(_voiceDeviceName, true, 1, AudioSettings.outputSampleRate);
            _lastVoiceSample = 0;
            IsVoiceActive = _micClip != null;
            OnVoiceStateChanged?.Invoke(IsVoiceActive);
            return IsVoiceActive;
        }

        public void StopPartyVoice()
        {
            if (!IsVoiceActive)
                return;

            if (!string.IsNullOrWhiteSpace(_voiceDeviceName))
                Microphone.End(_voiceDeviceName);

            _micClip = null;
            _voiceDeviceName = null;
            _lastVoiceSample = 0;
            IsVoiceActive = false;
            OnVoiceStateChanged?.Invoke(false);
        }

        private void HandleMessageReceived(ChatMessage message)
        {
            OnMessageReceived?.Invoke(message);

            bool socialChannel = message.Channel == ChatChannel.Party || message.Channel == ChatChannel.Guild || message.Channel == ChatChannel.Private;
            bool fromOtherPlayer = !string.Equals(message.Sender, LocalPlayerName, StringComparison.OrdinalIgnoreCase);
            if (socialChannel && fromOtherPlayer && (!_emitPushWhenUnfocused || !Application.isFocused))
            {
                OnPushNotificationRequested?.Invoke(new PushNotificationRequest
                {
                    title = message.Channel.ToString(),
                    body = $"{message.Sender}: {message.Text}",
                    channel = message.Channel
                });
            }

            if (message.Channel == ChatChannel.Guild)
                DetectMentions(message.Text);
        }

        private bool ValidateSocialChannel(ChatSendRequest request, out string error)
        {
            error = string.Empty;

            if (request.Channel == ChatChannel.Party && (_partyManager == null || !_partyManager.HasParty))
            {
                error = "Party chat requires an active party.";
                return false;
            }

            if (request.Channel == ChatChannel.Guild && (_guildManager == null || !_guildManager.HasGuild))
            {
                error = "Guild chat requires a guild.";
                return false;
            }

            if (request.Channel == ChatChannel.Private && (_friendSystem == null || !_friendSystem.IsFriend(request.Target)))
            {
                error = "Whispers are limited to players on your friend list.";
                return false;
            }

            return true;
        }

        private string FilterBlockedWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _blockedWords == null || _blockedWords.Length == 0)
                return text;

            string result = text;
            for (int i = 0; i < _blockedWords.Length; i++)
            {
                string blocked = ChatSanitizer.SanitizeText(_blockedWords[i]);
                if (string.IsNullOrWhiteSpace(blocked))
                    continue;

                result = Regex.Replace(result, Regex.Escape(blocked), "***", RegexOptions.IgnoreCase);
            }

            return result;
        }

        private bool IsSpamBurst()
        {
            float now = Time.unscaledTime;
            while (_sentMessageTimes.Count > 0 && now - _sentMessageTimes.Peek() > _spamWindowSeconds)
                _sentMessageTimes.Dequeue();

            return _sentMessageTimes.Count >= _maxMessagesPerWindow;
        }

        private void RegisterSendTime()
        {
            _sentMessageTimes.Enqueue(Time.unscaledTime);
        }

        private void DetectMentions(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            MatchCollection matches = Regex.Matches(text, "@([A-Za-z0-9_]{2,24})");
            for (int i = 0; i < matches.Count; i++)
            {
                string name = matches[i].Groups[1].Value;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                _mentionCounts.TryGetValue(name, out int count);
                _mentionCounts[name] = count + 1;
                OnGuildMentionDetected?.Invoke(name, text);
            }
        }

        private void CaptureVoiceFrame()
        {
            int position = Microphone.GetPosition(_voiceDeviceName);
            if (position < 0 || _micClip == null)
                return;

            int available = position - _lastVoiceSample;
            if (available < 0)
                available += _micClip.samples;

            if (available < _voiceFrameSamples)
                return;

            float[] samples = new float[_voiceFrameSamples];
            _micClip.GetData(samples, _lastVoiceSample);
            _lastVoiceSample = (_lastVoiceSample + _voiceFrameSamples) % _micClip.samples;

            OnVoiceFrameCaptured?.Invoke(new VoiceFrame
            {
                sampleRate = _micClip.frequency,
                channelCount = _micClip.channels,
                samples = samples
            });
        }
    }
}