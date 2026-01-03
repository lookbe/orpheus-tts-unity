using LlamaCpp;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OrpheusTTS
{
    public class OrpheusTTS : MonoBehaviour
    {
        [SerializeField]
        private string orpheusModelPath = string.Empty;

        [SerializeField]
        private string snacModelPath = string.Empty;

        private OrpheusModel orpheus;
        private SnacDecoder decoder;
        private AudioSource audioSource;

        List<float[]> audioQueue = new List<float[]>();
        Coroutine audioCoroutine = null;

        public delegate void StatusChangedDelegate(ModelStatus status);
        public event StatusChangedDelegate OnStatusChanged;

        private ModelStatus _status = ModelStatus.Init;

        // Public getter, no public setter
        public ModelStatus status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    _status = value;
                    OnStatusChanged?.Invoke(_status);
                }
            }
        }

        public void InitModel()
        {
            if (string.IsNullOrEmpty(orpheusModelPath))
            {
                return;
            }

            if (string.IsNullOrEmpty(snacModelPath))
            {
                return;
            }

            if (_status != ModelStatus.Init)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Loading;
            StartCoroutine(RunInitModel());
        }

        IEnumerator RunInitModel()
        {
            Debug.Log($"Load orpheus tts model");

            orpheus.modelPath = orpheusModelPath;
            orpheus.InitModel();

            decoder.modelPath = snacModelPath;
            decoder.InitModel();

            yield return new WaitWhile(() => orpheus.status != ModelStatus.Ready);
            yield return new WaitWhile(() => decoder.status != ModelStatus.Ready);

            Debug.Log("Load model done");

            status = ModelStatus.Ready;
        }

        public void Prompt(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return;
            }

            if (status != ModelStatus.Ready)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Generate;
            orpheus.Prompt(prompt);
        }

        // harcoded value from snac decoder
        private const int SampleRate = 24000;
        private const int Channels = 1;

        private void Awake()
        {
            orpheus = GetComponentInChildren<OrpheusModel>();
            decoder = GetComponentInChildren<SnacDecoder>();
            audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            orpheus.OnStatusChanged += OnModelStatusChanged;

            decoder.OnResponseGenerated += OnResponseGenerated;
            decoder.OnStatusChanged += OnModelStatusChanged;
        }

        private void OnDisable()
        {
            orpheus.OnStatusChanged -= OnModelStatusChanged;

            decoder.OnResponseGenerated -= OnResponseGenerated;
            decoder.OnStatusChanged -= OnModelStatusChanged;
        }

        void OnModelStatusChanged(ModelStatus status)
        {
            if (status == ModelStatus.Error)
            {
                StopAllCoroutines();
                status = ModelStatus.Error;
            }
        }

        void OnResponseGenerated(float[] audioChunk)
        {
            audioQueue.Add(audioChunk);
            
            if (audioCoroutine == null)
            {
                audioCoroutine = StartCoroutine(PlayAudio());
            }
        }

        IEnumerator PlayAudio()
        {
            while (audioQueue.Count() > 0)
            {
                float[] samples = audioQueue[0];
                AudioClip clip = AudioClip.Create("RawClip", samples.Length / Channels, Channels, SampleRate, false);
                clip.SetData(samples, 0);

                audioSource.clip = clip;
                audioSource.Play();

                yield return new WaitWhile(() => audioSource.isPlaying);
                audioQueue.RemoveAt(0);
            }

            audioCoroutine = null;
            status = ModelStatus.Ready;
        }
    }
}
