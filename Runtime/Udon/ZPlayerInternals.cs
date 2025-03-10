﻿
using System;
using System.Globalization;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;


public enum ZplayerStatus : byte
{
    StartVideo,
    Sync,
    EndVideo,
}


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ZPlayerInternals : UdonSharpBehaviour
{

    [HideInInspector] public BaseVRCVideoPlayer videoPlayer;
    [SerializeField] VRCUnityVideoPlayer _unityPlayer;
    [SerializeField] VRCAVProVideoPlayer _avproPlayer;

    [SerializeField] VRCUrlInputField _urlField;

    [SerializeField] TextMeshProUGUI _currentTimeText;
    [SerializeField] TextMeshProUGUI _durationText;
    [SerializeField] Slider _seekSlider;
    [SerializeField] Slider _volumeSlider;
    [SerializeField] TextMeshProUGUI _volumeText;

    [SerializeField] AudioSource _audioSource;

    [SerializeField] GameObject _screenObject;
    [SerializeField] Animator _uiAnimator;
    [SerializeField] Animator _loadingAnimator;

    [SerializeField] GameObject _playButton;
    [SerializeField] GameObject _pauseButton;

    [SerializeField] GameObject _lockButton;
    [SerializeField] GameObject _unlockButton;

    public TextMeshProUGUI _avpText;
    public Image _ppImage;

    [SerializeField] CustomRenderTexture _crt;
    [SerializeField] MeshRenderer _copyScreen;

    bool _isAvProLocal;
    bool _isAvProStarting;


    Color _defaultColor = new Color(0.7254901960784313f, 0.7254901960784313f, 0.7254901960784313f);
    Color _highlightColor = new Color(0.44313725490196076f, 0.5686274509803921f, 0.7254901960784313f);

    [UdonSynced, HideInInspector] public bool ownerPlaying = false;
    [UdonSynced, HideInInspector] public VRCUrl currentUrl;
    [UdonSynced, HideInInspector] public float ownerProgress;
    [UdonSynced, HideInInspector] public double lastSyncTime;
    [UdonSynced, HideInInspector] public bool locked;
    [UdonSynced, HideInInspector] public bool isAvPro;

    [SerializeField] bool isDefaultAVPro = true;

    VRCUrl _localUrl;

    [SerializeField] TextMeshProUGUI _logText;
    [SerializeField] TextMeshProUGUI _ownerNameText;

    [SerializeField] Material _copyMaterial;

    [SerializeField] Animator _seekTimeAnimator;

    int _retryCount = 0;


    void Start()
    {

#if !UNITY_EDITOR
        _isAvProLocal = isDefaultAVPro;
#endif
        SelectVideoPlayer(_isAvProLocal);

        UpdateCurrentTimeUILoop();

        UpdateOwnerText();

        SendCustomEventDelayedFrames(nameof(_InitLtcToggle), 1);
    }

    void Log(string text) => Debug.Log($"[ZPlayer] {text}");

    void SelectVideoPlayer(bool avpro)
    {
        _isAvProLocal = avpro;
        isAvPro = avpro;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (avpro)
        {
            videoPlayer = _avproPlayer;
        }
        else
        {
            videoPlayer = _unityPlayer;
        }

        UpdateSharedMaterial();
        HideLoading();

        HighlightText(_avpText, _isAvProLocal);
    }

    void HighlightText(TextMeshProUGUI text, bool highlight)
    {
        text.color = highlight ? _highlightColor : _defaultColor;
    }
    void HighlightImage(Image image, bool highlight)
    {
        image.color = highlight ? _highlightColor : _defaultColor;
    }

    public void Play(VRCUrl url)
    {
        if (url == null)
        {
            return;
        }

        string urlStr = url.ToString();
        if (string.IsNullOrEmpty(urlStr))
        {
            return;
        }

        if (videoPlayer.IsPlaying)
        {
            videoPlayer.Stop();
        }

        ShowLoading();
        _localUrl = url;
        _isAvProStarting = _isAvProLocal;
        _copyMaterial.SetFloat("_IsAVProInput", _isAvProLocal ? 1 : 0);
        LogUI(urlStr);
        videoPlayer.PlayURL(url);
        _urlField.SetUrl(url);


        if (IsOwner())
        {
            ownerPlaying = true;
            currentUrl = url;
            ownerProgress = 0;
            RequestSerialization();
        }

    }

    public override void OnDeserialization()
    {
        bool remotePlaying = ownerPlaying;

        if (isAvPro != _isAvProLocal)
        {
            if (videoPlayer.IsPlaying)
            {
                currentUrl = null;
                _localUrl = null;
            }
            SelectVideoPlayer(isAvPro);
        }

        if (currentUrl == null)
        {
            videoPlayer.Stop();
        }

        bool justStarted = false;
        if (currentUrl != null && (_localUrl == null || (_localUrl.ToString() != currentUrl.ToString())))
        {
            //Log($"Url changed from {_localUrl} to {currentUrl}");
            _retryCount = 0;
            Play(currentUrl);
            justStarted = true;
        }

        if (remotePlaying != videoPlayer.IsPlaying)
        {
            if (!remotePlaying)
            {
                videoPlayer.Pause();
                OnVideoActuallyPause();
            }
            else
            {
                videoPlayer.Play();
            }
        }

        if (videoPlayer.IsPlaying)
        {
            const float Threshold = 5.0f;
            float time = OwnerTimeOffset();
            if (Mathf.Abs(time - videoPlayer.GetTime()) > Threshold)
            {
                Seek(time);
                _skipNextUIUpdate = false;
                UpdateCurrentTimeUINow(time);
                _skipNextUIUpdate = true;
            }
        }
        else
        {
            Seek(ownerProgress);

            if (!justStarted)
            {
                _skipNextUIUpdate = false;
                UpdateCurrentTimeUINow(ownerProgress);
                _skipNextUIUpdate = true;
            }

        }

        UpdateLockButtonsState();
        UpdateOwnerText();

        if (_requestedResync)
        {
            HideLoading();
            _requestedResync = false;
        }

    }

    public void PlayFromInputField()
    {
        if (!TryTransferOwnernership())
        {
            LogUI($"Player Locked");
            return;
        }

        var url = _urlField.GetUrl();
        string urlStr = url.ToString();

        if (string.IsNullOrEmpty(urlStr))
        {
            return;
        }

        if (urlStr.Length > 1000)
        {
            Log($"URL too long: {urlStr.Length}");
            return;
        }

        Play(url);
    }


    bool _isSeeking = false;
    bool _skipNextUIUpdate = false;
    float _seekDurationTempMultiplier = 1.0f;
    [SerializeField] TextMeshProUGUI _seekTimeDurationPreview;
    public void StartSeeking()
    {
        if (!_isSeeking)
        {
            _isSeeking = true;
            _seekTimeAnimator.SetTrigger("Show");
            if (videoPlayer.IsPlaying)
            {
                _seekDurationTempMultiplier = videoPlayer.GetDuration();
            }
        }

        _seekTimeDurationPreview.text = GetFormattedTime(_seekDurationTempMultiplier * _seekSlider.value);
    }

    void EndSeeking()
    {
        _isSeeking = false;
        _seekTimeAnimator.SetTrigger("Hide");

        if (!TryTransferOwnernership())
        {
            return;
        }

        float time = videoPlayer.GetDuration() * _seekSlider.value;
        videoPlayer.SetTime(time);
        _skipNextUIUpdate = false;
        UpdateCurrentTimeUINow(time);
        _skipNextUIUpdate = true;

        ownerProgress = time;
        RequestSerialization();
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (_isSeeking && !value)
        {
            EndSeeking();
        }
    }

    public void UpdateCurrentTimeUILoop()
    {
        UpdateCurrentTimeUINow();

        SendCustomEventDelayedSeconds(nameof(UpdateCurrentTimeUILoop), 1.0f);
    }

    void UpdateCurrentTimeUINow(float overrideTime = -1)
    {
        if (!videoPlayer.IsPlaying && overrideTime < 0)
        {
            return;
        }

        if (_skipNextUIUpdate)
        {
            _skipNextUIUpdate = false;
            return;
        }

        float time = overrideTime >= 0 ? overrideTime : videoPlayer.GetTime();
        _currentTimeText.text = GetFormattedTime(time);

        if (videoPlayer.IsPlaying)
        {
            UpdateSharedMaterial();
        }

        if (!_isSeeking)
        {
            float duration = videoPlayer.GetDuration();
            duration = Mathf.Max(duration, 0.001f);
            float sliderValue = time / duration;
            sliderValue = Mathf.Clamp01(sliderValue);
            _seekSlider.SetValueWithoutNotify(sliderValue);
        }
    }

    string GetFormattedTime(float seconds)
    {
        if (seconds <= 0 || seconds == float.NaN || seconds == float.MaxValue || float.IsInfinity(seconds))
        {
            return "0:00";
        }

        var time = TimeSpan.FromSeconds(seconds);
        var result = ((int)time.TotalHours).ToString("D2") + time.ToString(@"\:mm\:ss");
        if (result.StartsWith("00:"))
        {
            result = result.Substring(3, result.Length-3);
        }
        return result;
    }

    float GetLogVolume()
    {
        float linear = _volumeSlider.value;
        float log = LinearToLogVolume(linear);
        return log;
    }

    public void SetVolume(float linearVolume)
    {
        _volumeSlider.value = linearVolume;
    }
    public void UpdateVolume()
    {
        float volume = GetLogVolume();
        _audioSource.volume = volume;

        _volumeText.text = ((int)(_volumeSlider.value * 100.0f)).ToString();
    }

    public void DisablePostProcess()
    {
#if !UNITY_EDITOR
        bool isDefault = _screenObject.layer == 0;
        var children = _screenObject.GetComponentsInChildren<Transform>(true);
        int layer = isDefault ? 19 : 0;
        _screenObject.layer = layer;

        foreach (Transform child in children)
        {
            child.gameObject.layer = layer;
        }

        HighlightImage(_ppImage, isDefault);
#endif

    }

    public static float LinearToLogVolume(float linearVolume, float scale = 2.0f)
    {
        return (Mathf.Pow(10, linearVolume * scale) - 1) / (Mathf.Pow(10, scale) - 1);
    }

    void PermanentlyShowUI()
    {
        _uiAnimator.SetBool("ForceShow", true);
        _uiAnimator.SetTrigger("Show");
    }
    void AllowHideUI()
    {
        _uiAnimator.SetBool("ForceShow", false);
    }


    public void EventPlay()
    {
        if (!TryTransferOwnernership())
        {
            return;
        }

        videoPlayer.Play();

        ownerPlaying = true;
        ownerProgress = videoPlayer.GetTime();
        RequestSerialization();
    }

    public override void OnPreSerialization()
    {
        lastSyncTime = Networking.GetServerTimeInSeconds();
    }

    public void EventPause()
    {
        if (!TryTransferOwnernership())
        {
            return;
        }

        videoPlayer.Pause();
        OnVideoActuallyPause();

        ownerPlaying = false;
        ownerProgress = videoPlayer.GetTime();
        RequestSerialization();
    }

    bool TryTransferOwnernership()
    {
        if (IsOwner())
        {
            return true;
        }
        else if (locked && !Networking.IsMaster)
        {
            return false;
        }

        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        return true;
    }

    bool IsOwner() => Networking.IsOwner(Networking.LocalPlayer, gameObject);

    public void EventResync()
    {
        if (!IsOwner())
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestResync));
            _requestedResync = true;
            ShowLoading();
        }
        else
        {
            RequestResync();
        }
    }

    bool _requestedResync = false;
    public void RequestResync()
    {
        if (videoPlayer.IsPlaying)
        {
            ownerProgress = videoPlayer.GetTime();
        }
        Log("Requested Resync");
        RequestSerialization();
    }

    public void EventAVProToggle()
    {
        if (!TryTransferOwnernership())
        {
            return;
        }

        SelectVideoPlayer(!_isAvProLocal);
        currentUrl = null;
        RequestSerialization();
    }

    void TogglePlayPauseButtons(bool isPlaying)
    {
        _playButton.SetActive(!isPlaying);
        _pauseButton.SetActive(isPlaying);
    }


    void UpdateCopyTexture()
    {
        if (_copyScreen.HasPropertyBlock())
        {
            var mpb = new MaterialPropertyBlock();
            _copyScreen.GetPropertyBlock(mpb);
            var texture = mpb.GetTexture("_MainTex");
            if (texture != null)
            {
                _copyScreen.sharedMaterial.SetTexture("_MainTex", texture);
                if (_crt.height != texture.height || _crt.width != texture.width)
                {
                    /*_crt.Release();
                    _crt.height = texture.height;
                    _crt.width = texture.width;
                    _crt.Create();*/
                }

            }
        }

    }

    void ShowLoading()
    {
        _loadingAnimator.SetTrigger("Show");
    }

    void HideLoading()
    {
        _loadingAnimator.SetTrigger("Hide");
    }

    void UpdateSharedMaterial()
    {
        _copyMaterial.SetFloat("_IsAVProInput", _isAvProLocal ? 1 : 0);
        _copyMaterial.SetVector("_Resolution", new Vector2(videoPlayer.VideoWidth, videoPlayer.VideoHeight));
    }


    float OwnerTimeOffset()
    {
        if (!ownerPlaying)
        {
            return ownerProgress;
        }
        float elapsed = (float)(Networking.GetServerTimeInSeconds() - lastSyncTime);
        float offsetTime = ownerProgress + elapsed;
        return offsetTime;
    }

    void Seek(float time)
    {
        Log($"Seeking to {GetFormattedTime(time)}");
        videoPlayer.SetTime(time);
    }

    public void Lock()
    {
        if (!TryTransferOwnernership())
        {
            return;
        }

        locked = true;
        RequestSerialization();
        UpdateLockButtonsState();
    }

    public void Unlock()
    {
        if (!TryTransferOwnernership())
        {
            return;
        }

        locked = false;
        RequestSerialization();
        UpdateLockButtonsState();
    }

    internal void UpdateLockButtonsState()
    {
        _lockButton.SetActive(locked);
        _unlockButton.SetActive(!locked);
    }

    void LogUI(string text)
    {
        _logText.text = text;
    }

    void UpdateOwnerText()
    {
        var player = Networking.GetOwner(gameObject);
        if (player == null)
        {
            return;
        }
        _ownerNameText.text = player.displayName;
    }

    bool IsRTSPStream(string urlStr, bool isAvPro)
    {
        return isAvPro && videoPlayer.GetDuration() == 0f && IsRTSPURL(urlStr);
    }

    bool IsRTSPURL(string urlStr)
    {
        return urlStr.StartsWith("rtsp://", System.StringComparison.OrdinalIgnoreCase) ||
               urlStr.StartsWith("rtmp://", System.StringComparison.OrdinalIgnoreCase) || // RTMP isn't really supported in VRC's context and it's probably never going to be, but we'll just be safe here
               urlStr.StartsWith("rtspt://", System.StringComparison.OrdinalIgnoreCase) || // rtsp over TCP
               urlStr.StartsWith("rtspu://", System.StringComparison.OrdinalIgnoreCase); // rtsp over UDP
    }

    #region Player Callbacks

    /// <summary>
    /// only called once when the video is loaded, but not when avpro is ready :skull:
    /// </summary>
    public override void OnVideoReady()
    {
        HideLoading();
        AllowHideUI();
        TogglePlayPauseButtons(true);
        float duration = videoPlayer.GetDuration();
        _durationText.text = GetFormattedTime(duration);
        _urlField.textComponent.text = currentUrl.ToString();

        Log($"Ready: {currentUrl}");

        SendCustomEventDelayedFrames(nameof(UpdateSharedMaterial), 1);

        if (ownerProgress > 0)
        {
            float time = OwnerTimeOffset();
            Seek(time);
            _skipNextUIUpdate = false;
            UpdateCurrentTimeUINow(time);
            _skipNextUIUpdate = true;
        }

        if (!ownerPlaying)
        {
            videoPlayer.Pause();
            OnVideoActuallyPause();
        }

    }

    /// <summary>
    /// Called when the video is starting and after unpausing
    /// </summary>
    public override void OnVideoStart()
    {
        Log("Start");
        UpdateCopyTexture();
        UpdateSharedMaterial();
        AllowHideUI();
        TogglePlayPauseButtons(true);
        _retryCount = 0;

        _seekDurationTempMultiplier = videoPlayer.GetDuration();

        if (_isAvProLocal && _isAvProStarting)
        {
            _isAvProStarting = false;
            OnVideoReady();
        }

        SendCustomEventDelayedFrames(nameof(UpdateCurrentTimeUINow), 1);
    }

    /// <summary>
    /// Called after the video ends, but not when it loops
    /// </summary>
    public override void OnVideoEnd()
    {
        if (currentUrl != null)
        {
            if (IsRTSPStream(currentUrl.ToString(), isAvPro))
            {
                return;
            }
        }


        PermanentlyShowUI();
        //TogglePlayPauseButtons(false);

        Log("Video End");

        LogUI("URL");
        UpdateCurrentTimeUINow(0);
    }

    public override void OnVideoError(VRC.SDK3.Components.Video.VideoError videoError)
    {
        HideLoading();

        string err = "VideoError: " + videoError.ToString();
        Log(err);

        if (videoError == VRC.SDK3.Components.Video.VideoError.AccessDenied && _localUrl != null && _localUrl.ToString().StartsWith("https://"))
        {
            LogUI(err + $" - Enable Untrusted URLs");
            return;
        }

        if (_retryCount < 3 && _localUrl != null)
        {
            _retryCount++;
            LogUI(err + $" - Retrying {_retryCount}");
            SendCustomEventDelayedSeconds(nameof(RetryCurrentUrl), 3);
        }
        else
        {
            _retryCount = 0;
            _localUrl = null;
            LogUI(err);
        }
    }

    public void RetryCurrentUrl()
    {
        if (_localUrl != null && !videoPlayer.IsPlaying)
        {
            Play(currentUrl);
        }
    }

    /// <summary>
    /// Never seems to get called
    /// </summary>
    public override void OnVideoPlay() {}

    /// <summary>
    /// Never seems to get called
    /// </summary>
    public override void OnVideoPause() {}

    /// <summary>
    /// Manually called when the player pauses
    /// </summary>
    public void OnVideoActuallyPause()
    {
        Log("Pause");

        TogglePlayPauseButtons(false);
        PermanentlyShowUI();
        ownerPlaying = false;
    }

    /// <summary>
    /// Called when the video ends but when loop is enabled
    /// </summary>
    public override void OnVideoLoop()
    {
        AllowHideUI();
        TogglePlayPauseButtons(true);
    }

    #endregion


    [SerializeField] TextMeshProUGUI _ltcText;
#if LTCGI_INCLUDED
    [SerializeField] LTCGI_UdonAdapter _optionalLtcgiAdapter;
#endif

#if LTCGI_INCLUDED && !UNITY_ANDROID
    public void ToggleLtc()
    {
        bool ltcgiEnabled = !_optionalLtcgiAdapter._GetGlobalState();
        _optionalLtcgiAdapter._SetGlobalState(ltcgiEnabled);
        HighlightText(_ltcText, ltcgiEnabled);
    }
    public void _InitLtcToggle()
    {
        if (_optionalLtcgiAdapter != null)
        {
            _ltcText.gameObject.SetActive(true);
            HighlightText(_ltcText, _optionalLtcgiAdapter._GetGlobalState());
        }
        else
        {
            _ltcText.gameObject.SetActive(false);
        }
    }
#else
    public void ToggleLtc()
    {
    }
    public void _InitLtcToggle()
    {
        _ltcText.gameObject.SetActive(false);
    }
#endif
}

// a lot taken from udonsharp video player in order to not solve already solved problems

/*MIT License

Copyright (c) 2020 Merlin

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/