using LlamaCpp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OrpheusTTS
{
    public class WavWriter : MonoBehaviour
    {
        public string wavFileFolder;

        OrpheusCloneTTS cloneTTS;
        SnacDecoder decoder;

        private void Awake()
        {
            cloneTTS = GetComponent<OrpheusCloneTTS>();
            decoder = GetComponentInChildren<SnacDecoder>();            
        }

        private void OnEnable()
        {
            cloneTTS.OnStatusChanged += OnStatusChanged;
            decoder.OnResponseGenerated += OnResponseGenerated;
        }

        private void OnDisable()
        {
            cloneTTS.OnStatusChanged -= OnStatusChanged;
            decoder.OnResponseGenerated += OnResponseGenerated;
        }

        List<float> _response = new List<float>();

        void OnStatusChanged(ModelStatus status)
        {
            if (status == ModelStatus.Ready)
            {
                if (_response.Count > 0)
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = $"generated_{timestamp}.wav";
                    float[] buffer = _response.ToArray();
                    Task.Run(() =>
                    {
                        string fullpath = Path.Join(wavFileFolder, fileName);
                        SaveWav(fullpath, buffer, 24000);
                        Debug.Log($"{fullpath} saved");
                    });

                }

                _response.Clear();
            }
        }

        void OnResponseGenerated(float[] samples)
        {
            _response.AddRange(samples);
        }

        public static void SaveWav(string filePath, float[] samples, int sampleRate)
        {
            using (var stream = File.Create(filePath))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Length * 4);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)3); // IEEE Float
                writer.Write((short)1); // Mono
                writer.Write(sampleRate);
                writer.Write(sampleRate * 4); // byte rate
                writer.Write((short)4); // block align
                writer.Write((short)32); // bits per sample

                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 4);
                foreach (var sample in samples)
                {
                    writer.Write(sample);
                }
            }
        }
    }
}
