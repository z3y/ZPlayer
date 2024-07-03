
using System;
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
        _urlField.textComponent.text = currentUrl.ToString();
    }

    public override void OnVideoStart()
    {
        UpdateCurrentTimeUINow();
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
    }

    public static float LinearToLogVolume(float linearVolume, float scale = 1f)
    {
        return (Mathf.Pow(10, linearVolume * scale) - 1) / (Mathf.Pow(10, scale) - 1);
    }
}
