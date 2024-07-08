
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ZPlayer : UdonSharpBehaviour
{

    public VRCUrl defaultUrl;
    [Range(0.0f, 1.0f)] public float volume = 0.7f;
    public bool defaultLocked = false;

    public ZPlayerInternals internals;


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
    }
}
