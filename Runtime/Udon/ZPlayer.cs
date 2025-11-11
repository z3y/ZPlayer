
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ZPlayer : UdonSharpBehaviour
{

    public VRCUrl defaultUrl;
    public float startTime = 0;
    [Range(0.0f, 1.0f)] public float volume = 0.7f;
    public bool defaultLocked = false;

    public ZPlayerInternals internals;

    //internal const string volumeKey = "video-volume";
    void Start()
    {
        if (Networking.IsOwner(Networking.LocalPlayer, internals.gameObject))
        {
            SendCustomEventDelayedFrames(nameof(PlayDefaultUrl), 1);
        }
        internals.SetVolume(volume);
        internals.locked = defaultLocked;
        internals.UpdateLockButtonsState();
    }

    public void PlayDefaultUrl()
    {
        internals.Play(defaultUrl);
        if (startTime > 0)
        {
            internals._startTime = startTime;
        }
    }

    //public override void OnPlayerDataUpdated(VRCPlayerApi player, PlayerData.Info[] infos)
    //{
    //    if (player.isLocal)
    //    {
    //        var linearVolume = PlayerData.GetFloat(Networking.LocalPlayer, volumeKey);
    //        internals.SetVolume(linearVolume);
    //    }
    //}
}
