
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ZPlayer : UdonSharpBehaviour
{

    public VRCUrl[] defaultPlaylistUrls;
    [Range(0.0f, 1.0f)] public float volume = 0.7f;
    public bool unlocked = true;

    public ZPlayerInternals internals;


    void Start()
    {
        if (Networking.IsOwner(Networking.LocalPlayer, internals.gameObject))
        {
            SendCustomEventDelayedFrames(nameof(LoadPlaylist), 1);
        }
        internals.SetVolume(volume);
    }

    public void LoadPlaylist()
    {
        for (int i = 0; i < defaultPlaylistUrls.Length; i++)
        {
            var url = defaultPlaylistUrls[i];
            internals.Play(url);
        }
    }
}
