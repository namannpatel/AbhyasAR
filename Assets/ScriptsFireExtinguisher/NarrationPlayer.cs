using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays pre-generated Sarvam AI narration clips (see Assets/Audio/Narration/manifest.json)
/// keyed by a stable narration id. Each clip's own asset name IS its id (e.g.
/// "pass_pull_pin.wav" -> "pass_pull_pin"), so wiring this component is just dragging every
/// clip from that folder into narrationClips -- no per-id inspector fields to keep in sync
/// as lines get added or renamed.
///
/// Generated entirely offline, ahead of time, by a one-off dev-time script (not shipped in
/// the app) that hit the Sarvam AI REST API once per line and saved the returned audio --
/// playback here never touches the network, matching the app's offline-first design. Callers
/// (TrainingInstructionsUI, FireAlertIntro, FireResultsModal) look this up the same way they
/// look up every other always-present ARRig component: FindFirstObjectByType, cached once.
/// </summary>
public class NarrationPlayer : MonoBehaviour
{
    [Tooltip("Drag every clip from Assets/Audio/Narration/ here -- looked up by clip.name, which matches each line's narration id (see manifest.json).")]
    [SerializeField] private AudioClip[] narrationClips;

    private AudioSource audioSource;
    private Dictionary<string, AudioClip> clipsById;
    private string currentId;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // narration is a voiceover, not positional world audio

        clipsById = new Dictionary<string, AudioClip>();
        if (narrationClips == null)
        {
            return;
        }
        foreach (var clip in narrationClips)
        {
            if (clip != null && !clipsById.ContainsKey(clip.name))
            {
                clipsById.Add(clip.name, clip);
            }
        }
    }

    /// <summary>
    /// Plays the clip for this narration id, replacing whatever's currently playing (lines
    /// never overlap/stack). Silently no-ops for a null/empty id or one with no recorded
    /// clip yet, so callers can pass through narration ids for branches that don't have a
    /// line without needing their own per-call null checks.
    /// </summary>
    public void Play(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }
        if (id == currentId && audioSource.isPlaying)
        {
            return;
        }
        if (!clipsById.TryGetValue(id, out AudioClip clip) || clip == null)
        {
            return;
        }

        currentId = id;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>Stops narration immediately and clears the "already playing" guard.</summary>
    public void Stop()
    {
        currentId = null;
        audioSource.Stop();
    }
}
