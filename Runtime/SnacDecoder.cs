using UnityEngine;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlamaCpp;

namespace OrpheusTTS
{
    public class SnacDecoder : BackgroundRunner
    {

        [Header("Model")]
        [Tooltip("SNAC decoder ONNX model absolute path")]
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

        async void OnDestroy()
        {
            await BackgroundStop();
            FreeModel();
        }

        // Define a delegate (or use Action<T>)
        public delegate void ResponseGeneratedDelegate(float[] response);
        public event ResponseGeneratedDelegate OnResponseGenerated;

        private float[] ConvertToAudioData(byte[] rawAudioBytes)
        {
            if (rawAudioBytes == null || rawAudioBytes.Length == 0) return new float[0];

            int numSamples = rawAudioBytes.Length / 2;
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                // Read 2 bytes and convert them into a 16-bit signed integer (short)
                short sampleValue = BitConverter.ToInt16(rawAudioBytes, i * 2);

                // Normalize the short to a float
                samples[i] = sampleValue / 32767.0f;
            }

            return samples;
        }

        private void PostResponse(byte[] response)
        {
            float[] audioData = ConvertToAudioData(response);
            unityContext?.Post(_ => OnResponseGenerated?.Invoke(audioData), null);
        }

        InferenceSession _session;
        string[] _inputNames;

        public void InitModel()
        {
            if (string.IsNullOrEmpty(modelPath))
            {
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
                _inputNames = _session.InputMetadata.Keys.ToArray();

                Debug.Log("Load model done");

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

        private class DecodePayload : IBackgroundPayload
        {
            public List<int> Frames;
        }

        public void Decode(List<int> frames)
        {
            // harcoded value from onnx
            if (frames.Count != 28)
            {
                Debug.LogError("decoded frames has wrong length");
                return;
            }

            if (_session == null)
            {
                Debug.LogError("model not loaded");
                return;
            }

            if (status != ModelStatus.Ready)
            {
                Debug.LogError("invalid status");
                return;
            }

            RunBackgroundUnchecked(new DecodePayload() { Frames = frames }, RunPrompt);
        }

        protected void RunBackgroundUnchecked<T>(T payload, Action<T, CancellationToken> work) where T : IBackgroundPayload
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            Task.Run(() =>
            {
                try
                {
                    work(payload, cts.Token);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in background task: {ex.Message}");
                }
            }, cts.Token);
        }

        void RunPrompt(DecodePayload payload, CancellationToken cts)
        {
            List<int> multiframe = payload.Frames;

            try
            {
                int numFrames = multiframe.Count / 7;
                // Take only full frames (multiframe[: num_frames * 7])
                var frame = multiframe.Take(numFrames * 7).ToList();

                // Initialize lists to collect token codes as **LONG (Int64)**
                var codes0List = new List<long>();
                var codes1List = new List<long>();
                var codes2List = new List<long>();

                for (int j = 0; j < numFrames; j++)
                {
                    int i = 7 * j;

                    // Convert int token to long before adding
                    codes0List.Add((long)frame[i]);

                    codes1List.Add((long)frame[i + 1]);
                    codes1List.Add((long)frame[i + 4]);

                    codes2List.Add((long)frame[i + 2]);
                    codes2List.Add((long)frame[i + 3]);
                    codes2List.Add((long)frame[i + 5]);
                    codes2List.Add((long)frame[i + 6]);
                }

                // --- 1. Create Data Arrays ---
                var codes0Array = codes0List.ToArray();
                var codes1Array = codes1List.ToArray();
                var codes2Array = codes2List.ToArray();

                // --- 2. Define Shapes ---
                // Shape is [Batch Size, Sequence Length] -> [1, array.Length]
                int[] shape0 = { 1, codes0Array.Length };
                int[] shape1 = { 1, codes1Array.Length };
                int[] shape2 = { 1, codes2Array.Length };

                // --- 3. Create DenseTensor Inputs (No Tensor.Create) ---
                // Using the DenseTensor<T> constructor that accepts data and dimensions
                var codes0Tensor = new DenseTensor<long>(codes0Array, shape0);
                var codes1Tensor = new DenseTensor<long>(codes1Array, shape1);
                var codes2Tensor = new DenseTensor<long>(codes2Array, shape2);

                // --- 4. Prepare Inputs for Session ---
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputNames[0], codes0Tensor),
                    NamedOnnxValue.CreateFromTensor(_inputNames[1], codes1Tensor),
                    NamedOnnxValue.CreateFromTensor(_inputNames[2], codes2Tensor)
                };

                // --- 5. Run ONNX Model ---
                using (var results = _session.Run(inputs))
                {
                    // Assuming the output is a float tensor (audio_hat)
                    var audioHatTensor = results.First().AsTensor<float>();

                    // --- 6. Postprocessing (Slicing and Scaling) ---
                    if (audioHatTensor.Dimensions.Length != 3 || audioHatTensor.Dimensions[2] < 4096)
                    {
                        throw new Exception("Unexpected audio_hat tensor dimensions.");
                    }

                    // Extract the slice (equivalent to audio_hat[:, :, 2048:4096])
                    int sliceStart = 2048;
                    int sliceEnd = 4096;
                    int sliceLength = sliceEnd - sliceStart;

                    var audioInt16 = new short[sliceLength];

                    // Manual slicing and scaling (equivalent to (audio_np * 32767).astype(np.int16))
                    for (int k = 0; k < sliceLength; k++)
                    {
                        // Accessing the element at [BatchIndex: 0, ChannelIndex: 0, SampleIndex: sliceStart + k]
                        float sample = audioHatTensor[0, 0, sliceStart + k];
                        audioInt16[k] = (short)(sample * 32767.0f);
                    }

                    // Convert short[] array to byte[] (equivalent to .tobytes())
                    var byteArray = new byte[sliceLength * sizeof(short)];
                    Buffer.BlockCopy(audioInt16, 0, byteArray, 0, byteArray.Length);

                    PostResponse(byteArray);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"An unexpected error occurred: {ex.Message}");
            }
        }
    }

}
