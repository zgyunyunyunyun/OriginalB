using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BoxPlacementSfxPlayer : MonoBehaviour
{
    [Header("Placement Clip")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip defaultPlacementClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool spatializeByAnchor;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;

    [Header("Pitch")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField, Range(0.5f, 2f)] private float minPitch = 0.96f;
    [SerializeField, Range(0.5f, 2f)] private float maxPitch = 1.05f;

    private Coroutine loopingFadeRoutine;

    public void PlayPlacementSfx(Transform anchor, AudioClip overrideClip = null)
    {
        PlayPlacementSfxScaled(anchor, overrideClip, 1f);
    }

    public void PlayPlacementSfxScaled(Transform anchor, AudioClip overrideClip, float volumeScale)
    {
        var clip = overrideClip != null ? overrideClip : defaultPlacementClip;
        if (clip == null)
        {
            return;
        }

        EnsureAudioSource();
        if (audioSource == null)
        {
            return;
        }

        if (anchor != null)
        {
            transform.position = anchor.position;
        }

        audioSource.spatialBlend = spatializeByAnchor ? spatialBlend : 0f;
        var originalPitch = audioSource.pitch;
        audioSource.pitch = ResolvePitch();
        var finalVolume = Mathf.Clamp01(volume) * Mathf.Clamp01(volumeScale);
        audioSource.PlayOneShot(clip, finalVolume);
        audioSource.pitch = originalPitch;
    }

    public void PlayPlacementLoopingSfxWithFade(Transform anchor, AudioClip overrideClip, float volumeScale, float fadeDuration)
    {
        var clip = overrideClip != null ? overrideClip : defaultPlacementClip;
        if (clip == null)
        {
            return;
        }

        EnsureAudioSource();
        if (audioSource == null)
        {
            return;
        }

        if (anchor != null)
        {
            transform.position = anchor.position;
        }

        if (loopingFadeRoutine != null)
        {
            StopCoroutine(loopingFadeRoutine);
            loopingFadeRoutine = null;
        }

        loopingFadeRoutine = StartCoroutine(PlayLoopingFadeRoutine(clip, volumeScale, Mathf.Max(0.05f, fadeDuration)));
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = spatializeByAnchor ? spatialBlend : 0f;
    }

    private IEnumerator PlayLoopingFadeRoutine(AudioClip clip, float volumeScale, float fadeDuration)
    {
        var originalClip = audioSource.clip;
        var originalLoop = audioSource.loop;
        var originalVolume = audioSource.volume;
        var originalPitch = audioSource.pitch;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.pitch = 1f;

        var startVolume = Mathf.Clamp01(volume) * Mathf.Clamp01(volumeScale);
        audioSource.volume = startVolume;
        audioSource.Play();

        var elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / fadeDuration);
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        audioSource.Stop();
        audioSource.clip = originalClip;
        audioSource.loop = originalLoop;
        audioSource.volume = originalVolume;
        audioSource.pitch = originalPitch;
        loopingFadeRoutine = null;
    }

    private float ResolvePitch()
    {
        var clampedMin = Mathf.Clamp(minPitch, 0.5f, 2f);
        var clampedMax = Mathf.Clamp(maxPitch, 0.5f, 2f);
        if (clampedMax < clampedMin)
        {
            var temp = clampedMin;
            clampedMin = clampedMax;
            clampedMax = temp;
        }

        if (!randomizePitch)
        {
            return clampedMin;
        }

        return Random.Range(clampedMin, clampedMax);
    }
}