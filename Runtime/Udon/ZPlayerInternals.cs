
using System;
using System.Runtime.InteropServices;
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

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ZPlayerInternals : UdonSharpBehaviour
{

    BaseVRCVideoPlayer _videoPlayer;
    [SerializeField] VRCUnityVideoPlayer _unityPlayer;
    [SerializeField] VRCAVProVideoPlayer _avproPlayer;

    [SerializeField] VRCUrlInputField _urlField;
    public VRCUrl currentUrl;

    [SerializeField] TextMeshProUGUI _currentTimeText;
    [SerializeField] TextMeshProUGUI _durationText;
    [SerializeField] Slider _seekSlider;
    [SerializeField] Slider _volumeSlider;

    [SerializeField] AudioSource _audioSource;

    [SerializeField] GameObject _screenObject;
    [SerializeField] Animator _uiAnimator;
    [SerializeField] Animator _loadingAnimator;

    [SerializeField] GameObject _playButton;
    [SerializeField] GameObject _pauseButton;

    public TextMeshProUGUI _avpText;
    public TextMeshProUGUI _ppText;

    [SerializeField] CustomRenderTexture _crt;
    [SerializeField] MeshRenderer _copyScreen;

    bool _isAvPro;

    Color _defaultColor = new Color(0.7254901960784313f, 0.7254901960784313f, 0.7254901960784313f);
    Color _highlightColor = new Color(0.44313725490196076f, 0.5686274509803921f, 0.7254901960784313f);


    void Start()
    {
        SelectVideoPlayer(_isAvPro);

        UpdateCurrentTimeUILoop();
    }

    void Log(string text) => Debug.Log($"[ZPlayer] {text}");

    void SelectVideoPlayer(bool avpro)
    {
        _isAvPro = avpro;

        if (_videoPlayer != null)
        {
            if (_videoPlayer.IsPlaying)
            {
                _videoPlayer.Stop();
            }
        }

        if (_isAvPro)
        {
            _videoPlayer = _avproPlayer;
        }
        else
        {
            _videoPlayer = _unityPlayer;
        }

        HighlightText(_avpText, _isAvPro);
    }

    void HighlightText(TextMeshProUGUI text, bool highlight)
    {
        text.color = highlight ? _highlightColor : _defaultColor;
    }

    public void Play(VRCUrl url)
    {
        ShowLoading();
        currentUrl = url;
        _videoPlayer.PlayURL(url);
    }


    public void PlayFromInputField()
    {
        Play(_urlField.GetUrl());
    }


    bool _isSeeking = false;
    public void StartSeeking()
    {
        _isSeeking = true;
    }

    void EndSeeking()
    {
        float time = _videoPlayer.GetDuration() * _seekSlider.value;
        _videoPlayer.SetTime(time);
        _isSeeking = false;
        SendCustomEventDelayedFrames(nameof(UpdateCurrentTimeUINow), 1);
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

    void UpdateCurrentTimeUINow()
    {
        if (!_videoPlayer.IsReady)
        {
            return;
        }

        UpdateCopyTexture();

        float time = _videoPlayer.GetTime();
        _currentTimeText.text = GetFormattedTime(time);

        if (!_isSeeking)
        {
            _seekSlider.SetValueWithoutNotify(time / _videoPlayer.GetDuration());
        }
    }

    string GetFormattedTime(float seconds)
    {
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

        HighlightText(_ppText, !isDefault);
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
        _videoPlayer.Play();
    }

    public void EventPause()
    {
        _videoPlayer.Pause();
        OnVideoActuallyPause();
    }

    public void EventResync()
    {
    }

    public void EventAVProToggle()
    {
        SelectVideoPlayer(!_isAvPro);
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


    #region Player Callbacks

    /// <summary>
    /// only called once when the video is loaded
    /// </summary>
    public override void OnVideoReady()
    {
        HideLoading();
        AllowHideUI();
        TogglePlayPauseButtons(true);
        float duration = _videoPlayer.GetDuration();
        _durationText.text = GetFormattedTime(duration);
        _urlField.textComponent.text = currentUrl.ToString();

        Log($"Ready: {currentUrl}");
    }

    /// <summary>
    /// Called when the video is starting and after unpausing
    /// </summary>
    public override void OnVideoStart()
    {
        Log("Start");

        UpdateCurrentTimeUINow();

        TogglePlayPauseButtons(true);
        AllowHideUI();
    }

    /// <summary>
    /// Called after the video ends, but not when it loops
    /// </summary>
    public override void OnVideoEnd()
    {
        PermanentlyShowUI();
        TogglePlayPauseButtons(false);

        Log("End");

    }

    public override void OnVideoError(VRC.SDK3.Components.Video.VideoError videoError)
    {
        HideLoading();

        Log("Error" + videoError.ToString());
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
