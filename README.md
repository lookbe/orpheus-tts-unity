# Orpheus TTS for Unity

A Unity 6 integration for [Orpheus-TTS](https://github.com/canopyai/Orpheus-TTS). This package provides a high-performance, local neural text-to-speech solution leveraging ONNX Runtime and Llama.cpp.

## ⚠️ Hardware & Platform Support
To run this on consumer GPUs, please note the current technical constraints:
* **API Support:** Currently supports **Vulkan** only.
* **Platform:** **Windows** (Vulkan backend).

---

## Installation

Follow these steps exactly to ensure all native dependencies are resolved.

### Configure manifest.json
Open your project's `Packages/manifest.json` and update it to include the scoped registry and the Git dependencies.


```json
{
  "scopedRegistries": [
    {
      "name": "npm",
      "url": "[https://registry.npmjs.com](https://registry.npmjs.com)",
      "scopes": [
        "com.github.asus4"
      ]
    }
  ],
  "dependencies": {
    "com.github.asus4.onnxruntime": "0.4.2",
    "com.github.asus4.onnxruntime.unity": "0.4.2",
    "ai.lookbe.llamacpp": "[https://github.com/lookbe/llama-cpp-unity.git](https://github.com/lookbe/llama-cpp-unity.git)",
    "ai.lookbe.orpheustts": "[https://github.com/lookbe/orpheus-tts-unity.git](https://github.com/lookbe/orpheus-tts-unity.git)",

    ...
    "other dependencies"
    ...
  }
}
```

---

## Requirements: Models

You must download the following two models separately:

1.  **Orpheus TTS (GGUF format):** e.g., [orpheus-3b-0.1-ft-Q4_K_M-GGUF](https://huggingface.co/isaiahbjork/orpheus-3b-0.1-ft-Q4_K_M-GGUF).
2.  **SNAC Decoder (ONNX format):** You must use the exact `decoder_model.onnx` file from [snac_24khz-ONNX](https://huggingface.co/onnx-community/snac_24khz-ONNX/tree/main/onnx).

---

## Testing

1.  **Import Samples:** Go to the Package Manager, select **Orpheus TTS Unity**, and import the **BasicTTS** sample.
2.  **Configure Paths:**
    * Select the `OrpheusTTS` object in the Hierarchy.
    * In the Inspector, locate the **Orpheus Model Path** and **SNAC Model Path** fields.
    * **Important:** Paste the **absolute path** (e.g., `C:\Models\orpheus.gguf`) for both files.
3.  **Run:** Press Play. The system will initialize the Vulkan backend on your GPU.

> **Note:** You can extend the component script to use `Application.streamingAssetsPath` if you wish to bundle models with your build, but the core component requires absolute paths for the initial backend load.
