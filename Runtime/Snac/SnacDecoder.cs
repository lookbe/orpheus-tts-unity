using LlamaCpp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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

            // Wait for all remaining background tasks to complete
            List<Task> remainingTasks = _backgroundTasks.Values.ToList();
            
            if (remainingTasks.Count > 0)
            {
                Debug.Log($"Waiting for {remainingTasks.Count} remaining background tasks to finish...");
                await Task.WhenAll(remainingTasks);
            }

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

        InferenceSession _session;
        string[] _inputNames;

        private ulong _inputIndex = 0;
        private ulong _nextExpectedIndex = 0;
        private Dictionary<ulong, byte[]> _responseCache = new Dictionary<ulong, byte[]>();
        private Dictionary<ulong, Task> _backgroundTasks = new Dictionary<ulong, Task>();

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

                _inputNames = _session.InputMetadata.Keys.ToArray();

                Debug.Log("Load model done");

                // Warmup
                Debug.Log("Warmup model...");
                var dummyFrames = Enumerable.Repeat(0, 28).ToList();
                DecodeInternal(dummyFrames, cts);
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

        private class DecodePayload : IBackgroundPayload
        {
            public List<int> Frames;
            public ulong Index;
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

            if (status != ModelStatus.Ready && status != ModelStatus.Generate)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = ModelStatus.Generate;
            RunBackgroundDecode(frames);
        }

        protected void RunBackgroundDecode(List<int> frames)
        {
            ulong index = _inputIndex++;
            _responseCache[index] = null;

            CancellationTokenSource cts = new CancellationTokenSource();
            DecodePayload payload = new DecodePayload() { Frames = new List<int>(frames), Index = index };

            var task = Task.Run(() =>
            {
                try
                {
                    RunDecode(payload, cts.Token);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in background task: {ex.Message}");
                }
            }, cts.Token);

            _backgroundTasks[index] = task;
        }

        void RunDecode(DecodePayload payload, CancellationToken cts)
        {
            try
            {
                byte[] byteArray = DecodeInternal(payload.Frames, cts);
                PostResponse(payload.Index, byteArray);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"An unexpected error occurred during RunDecode: {ex.Message}");
                PostResponse(payload.Index, new byte[0]);
            }
        }

        byte[] DecodeInternal(List<int> multiframe, CancellationToken cts)
        {
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

                bool invalid = codes0Array.Any(v => v < 0 || v > 4096) || codes1Array.Any(v => v < 0 || v > 4096) || codes2Array.Any(v => v < 0 || v > 4096);
                if (invalid)
                {
                    return new byte[0];
                }

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

                    return byteArray;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Inference failed in DecodeInternal: {ex.Message}");
                return new byte[0];
            }
        }

        void PostResponse(ulong index, byte[] response)
        {
            unityContext?.Post(_ => ProcessResponse(index, response), null);
        }

        // process this on main unity thread
        void ProcessResponse(ulong index, byte[] response)
        {
            _backgroundTasks.Remove(index);
            _responseCache[index] = response;

            while (true)
            {
                _responseCache.TryGetValue(_nextExpectedIndex, out byte[] nextResponse);
                if (nextResponse == null)
                {
                    break;
                }

                float[] audioData = ConvertToAudioData(nextResponse);
                OnResponseGenerated?.Invoke(audioData);

                _responseCache.Remove(_nextExpectedIndex);
                _nextExpectedIndex++;
            }

            if (_responseCache.Count == 0)
            {
                status = ModelStatus.Ready;
            }
        }
    }

}
