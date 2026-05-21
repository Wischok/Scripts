using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    //singleton
    private static AudioManager m_instance;
    public static AudioManager Instance { get { return m_instance; } }

    private void Awake()
    {
        m_instance = this;
    }

    //play audio clip at position
    public void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1.0f)
    {
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    //play audio clip through audio source
    public void PlayClip(AudioClip clip, float volume = 1.0f)
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.PlayOneShot(clip, volume);
    }
}
