
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


    void Start()
    {
        _videoPlayer = _unityPlayer;

        UpdateCurrentTimeUILoop();
    }


    public void Play(VRCUrl url)
    {
        currentUrl = url;
        _videoPlayer.PlayURL(url);
    }


    public void PlayFromInputField()
    {
        Play(_urlField.GetUrl());
    }

    public override void OnVideoEnd()
    {
        Debug.Log("video end");
    }

    public override void OnVideoError(VRC.SDK3.Components.Video.VideoError videoError)
    {
    }

    public override void OnVideoLoop()
    {
    }

    public override void OnVideoPause()
    {
    }

    public override void OnVideoPlay()
    {
    }

    public override void OnVideoReady()
    {
        Debug.Log("READY");
        float duration = _videoPlayer.GetDuration();
        _durationText.text = GetFormattedTime(duration);
    }

    public override void OnVideoStart()
    {
        Debug.Log("video start");
    }

    bool _isSeeking = false;
    public void StartSeeking() => _isSeeking = true;
    void EndSeeking()
    {
        _videoPlayer.SetTime(_videoPlayer.GetDuration() * _seekSlider.value);
        _isSeeking = false;
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
        SendCustomEventDelayedSeconds(nameof(UpdateCurrentTimeUILoop), 1.0f);
        if (!_videoPlayer.IsReady)
        {
            return;
        }

        float time = _videoPlayer.GetTime();
        _currentTimeText.text = GetFormattedTime(time);

        if (!_isSeeking)
        {
            _seekSlider.value = time / _videoPlayer.GetDuration();
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
}
