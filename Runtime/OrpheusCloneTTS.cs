using LlamaCpp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace OrpheusTTS
{
    public class OrpheusCloneTTS : MonoBehaviour
    {
        [Header("Models")]
        public string orpheusCloneModelPath = string.Empty;
        public string snacEncoderModelPath = string.Empty;
        public string snacDecoderModelPath = string.Empty;

        [Header("Reference Audio")]
        public string refAudioPath;
        public string refTranscriptPath;

        protected OrpheusCloneModel orpheus;
        protected SnacEncoder encoder;
        protected SnacDecoder decoder;
        protected AudioSource audioSource;

        private Queue<float> audioQueue = new Queue<float>();

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

        // harcoded value from snac decoder
        private const int SampleRate = 24000;
        private const int Channels = 1;

        private void Awake()
        {
            orpheus = GetComponentInChildren<OrpheusCloneModel>();
            encoder = GetComponentInChildren<SnacEncoder>();
            decoder = GetComponentInChildren<SnacDecoder>();
            audioSource = GetComponent<AudioSource>();

            audioSource.clip = AudioClip.Create("StreamingClip", SampleRate * 60, Channels, SampleRate, true, OnAudioRead);
            audioSource.loop = true;
            audioSource.Play();
        }

        private void OnEnable()
        {
            orpheus.OnStatusChanged += OnModelStatusChanged;
            decoder.OnStatusChanged += OnModelStatusChanged;
            encoder.OnStatusChanged += OnModelStatusChanged;

            decoder.OnResponseGenerated += OnResponseGenerated;
            encoder.OnResponseGenerated += OnEncodedAudio;
        }

        private void OnDisable()
        {
            orpheus.OnStatusChanged -= OnModelStatusChanged;
            decoder.OnStatusChanged -= OnModelStatusChanged;
            encoder.OnStatusChanged -= OnModelStatusChanged;

            decoder.OnResponseGenerated -= OnResponseGenerated;
            encoder.OnResponseGenerated -= OnEncodedAudio;
        }

        void OnModelStatusChanged(ModelStatus status)
        {
            if (status == ModelStatus.Error)
            {
                StopAllCoroutines();
                this.status = ModelStatus.Error;
            }
        }

        void OnResponseGenerated(float[] audioChunk)
        {
            foreach (var s in audioChunk)
                audioQueue.Enqueue(s);
        }

        private void OnAudioRead(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (audioQueue.Count > 0)
                    data[i] = audioQueue.Dequeue();
                else
                    data[i] = 0f;
            }
        }

        public void InitModel()
        {
            if (string.IsNullOrEmpty(orpheusCloneModelPath))
            {
                return;
            }

            if (string.IsNullOrEmpty(snacEncoderModelPath))
            {
                return;
            }

            if (string.IsNullOrEmpty(snacDecoderModelPath))
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

            orpheus.modelPath = orpheusCloneModelPath;
            orpheus.InitModel();

            encoder.modelPath = snacEncoderModelPath;
            encoder.InitModel();

            decoder.modelPath = snacDecoderModelPath;
            decoder.InitModel();

            yield return new WaitWhile(() => orpheus.status != ModelStatus.Ready);
            yield return new WaitWhile(() => encoder.status != ModelStatus.Ready);
            yield return new WaitWhile(() => decoder.status != ModelStatus.Ready);

            // temporary encoding
            yield return StartCoroutine(EncodeRef());

            Debug.Log("Load model done");

            status = ModelStatus.Ready;
        }

        string transcriptText;
        int[] audioTokens;

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
            StartCoroutine(WaitForGenerationAndPlaybackDone(prompt));
        }

        IEnumerator WaitForGenerationAndPlaybackDone(string prompt)
        {
            orpheus.PromptWithClone(prompt, transcriptText, audioTokens);

            yield return new WaitUntil(() => orpheus.status == ModelStatus.Ready);
            yield return new WaitUntil(() => decoder.status == ModelStatus.Ready);

            // Wait for all audio samples to be played
            yield return new WaitUntil(() => audioQueue.Count == 0);

            status = ModelStatus.Ready;
        }

        public void Stop()
        {
            if (status != ModelStatus.Generate)
            {
                Debug.Log("already stopped");
                return;
            }

            orpheus.Stop();
        }

        private IEnumerator EncodeRef()
        {
            float[] audioSamples = null;
            var loadTask = Task.Run(() =>
            {
                audioSamples = WavReader.LoadWav(refAudioPath);
                transcriptText = File.ReadAllText(refTranscriptPath);
            });
            yield return new WaitUntil(() => loadTask.IsCompleted);

            encoder.Encode(audioSamples);
            yield return new WaitUntil(() => decoder.status == ModelStatus.Ready);
        }

        void OnEncodedAudio(int[] tokens)
        {
            audioTokens = tokens;
        }

    }
}
