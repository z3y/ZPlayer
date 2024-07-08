
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
    public TextMeshProUGUI _ppText;

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

    VRCUrl _localUrl;

    [SerializeField] TextMeshProUGUI _logText;
    [SerializeField] TextMeshProUGUI _ownerNameText;

    [SerializeField] Material _copyMaterial;

    [SerializeField] Animator _seekTimeAnimator;

    int _retryCount = 0;

    void Start()
    {
        SelectVideoPlayer(_isAvProLocal);

        UpdateCurrentTimeUILoop();

        UpdateOwnerText();
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

    public void Play(VRCUrl url)
    {
        if (videoPlayer != null && videoPlayer.IsPlaying)
        {
            videoPlayer.Stop();
        }

        ShowLoading();
        _localUrl = url;
        _isAvProStarting = _isAvProLocal;
        LogUI(url.ToString());
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


    }

    public void PlayFromInputField()
    {
        if (!TransferOwner())
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

        if (!TransferOwner())
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
        if (seconds <= 0 || seconds == float.NaN || seconds == float.MaxValue)
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
        bool isDefault = _screenObject.layer == 0;
        var children = _screenObject.GetComponentsInChildren<Transform>(true);
        int layer = isDefault ? 19 : 0;
        _screenObject.layer = layer;

        foreach (Transform child in children)
        {
            child.gameObject.layer = layer;
        }

        HighlightText(_ppText, isDefault);
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
        if (!TransferOwner())
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
        if (!TransferOwner())
        {
            return;
        }

        videoPlayer.Pause();
        OnVideoActuallyPause();

        ownerPlaying = false;
        ownerProgress = videoPlayer.GetTime();
        RequestSerialization();
    }

    bool TransferOwner()
    {
        if (IsOwner())
        {
            return true;
        }
        else if (locked)
        {
            return false;
        }

        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        return true;
    }

    bool IsOwner() => Networking.IsOwner(Networking.LocalPlayer, gameObject);

    public void EventResync()
    {
    }

    public void EventAVProToggle()
    {
        if (!TransferOwner())
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
        if (!TransferOwner())
        {
            return;
        }

        locked = true;
        RequestSerialization();
        UpdateLockButtonsState();
    }

    public void Unlock()
    {
        if (!TransferOwner())
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
        PermanentlyShowUI();
        TogglePlayPauseButtons(false);

        Log("Video End");

        LogUI("URL");
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
}
