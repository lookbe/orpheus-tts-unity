using LlamaCpp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace OrpheusTTS
{
    public class SnacEncoder : BackgroundRunner
    {

        [Header("Model")]
        [Tooltip("SNAC encoder ONNX model absolute path")]
        public string modelPath = string.Empty;

        // Define a delegate (or use Action<T>)
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

        protected void PostStatus(ModelStatus newStatus)
        {
            unityContext?.Post(_ => status = newStatus, null);
        }

        void OnDestroy()
        {
            BackgroundStopSync();
            FreeModel();
        }

        private int[] FlattenCodes(long[][] codes)
        {
            var allCodes = new List<int>();
            int L = codes[0].Length;
            for (int i = 0; i < L; i++)
            {
                allCodes.Add((int)codes[0][i] + 128266);
                allCodes.Add((int)codes[1][2 * i] + 128266 + 4096);
                allCodes.Add((int)codes[2][4 * i] + 128266 + (2 * 4096));
                allCodes.Add((int)codes[2][(4 * i) + 1] + 128266 + (3 * 4096));
                allCodes.Add((int)codes[1][(2 * i) + 1] + 128266 + (4 * 4096));
                allCodes.Add((int)codes[2][(4 * i) + 2] + 128266 + (5 * 4096));
                allCodes.Add((int)codes[2][(4 * i) + 3] + 128266 + (6 * 4096));
            }
            return allCodes.ToArray();
        }

        // Define a delegate (or use Action<T>)
        public delegate void ResponseGeneratedDelegate(int[] response);
        public event ResponseGeneratedDelegate OnResponseGenerated;

        InferenceSession _session;

        public void InitModel()
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogError("model path not set");
                return;
            }

            if (_status != ModelStatus.Init)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Loading;
            RunBackground(RunInitModel);
        }

        void RunInitModel(CancellationToken cts)
        {
            try
            {
                Debug.Log($"Load model at {modelPath}");

                var options = new SessionOptions();
                _session = new InferenceSession(modelPath, options);
                if (_session == null)
                {
                    throw new System.Exception("unable to load model");
                }

                Debug.Log("Load model done");

                // Warmup
                Debug.Log("Warmup model...");
                var dummyFrames = Enumerable.Repeat(0f, 28).ToList();
                EncodeInternal(dummyFrames.ToArray(), cts);
                Debug.Log("Warmup done");

                PostStatus(ModelStatus.Ready);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"An unexpected error occurred: {ex.Message}");

                FreeModel();
                PostStatus(ModelStatus.Init);
            }
        }

        void FreeModel()
        {
            _session?.Dispose();
        }

        private class EncodePayload : IBackgroundPayload
        {
            public float[] Waveform;
        }

        public void Encode(float[] waveform)
        {
            if (waveform.Length == 0)
            {
                Debug.LogError("encoded waveform has wrong length");
                return;
            }

            if (_session == null)
            {
                Debug.LogError("model not loaded");
                return;
            }

            if (status != ModelStatus.Ready && status != ModelStatus.Generate)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Generate;
            RunBackground(new EncodePayload() { Waveform = waveform }, RunEncode);
        }

        void RunEncode(EncodePayload payload, CancellationToken cts)
        {
            try
            {
                int[] tokens = EncodeInternal(payload.Waveform, cts);
                PostResponse(tokens);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"An unexpected error occurred during RunDecode: {ex.Message}");
                PostResponse(new int[0]);
            }
            finally
            {
                PostStatus(ModelStatus.Ready);
            }
        }

        public int[] EncodeInternal(float[] waveform, CancellationToken cts)
        {
            // waveform: [T]
            var inputTensor = new DenseTensor<float>(waveform, new[] { 1, 1, waveform.Length });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_values", inputTensor)
            };

            using var results = _session.Run(inputs);
            // Expected 3 outputs: audio_codes.0 [1, L], audio_codes.1 [1, 2L], audio_codes.2 [1, 4L]
            var codes = new long[3][];
            for (int i = 0; i < 3; i++)
            {
                var tensor = results.ElementAt(i).AsTensor<long>();
                codes[i] = tensor.ToArray();
            }

            return FlattenCodes(codes);
        }

        void PostResponse(int[] response)
        {
            unityContext?.Post(_ => OnResponseGenerated?.Invoke(response), null);
        }
    }
}
