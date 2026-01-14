# Voice Cloning for Orpheus TTS

> [!WARNING]
> This is an experimental feature and is not official. The model still has bugs, and cloning is inconsistent (roughly 60% success rate).

## Requirements

1.  **Orpheus Pretrained Model**: A GGUF format model is required. 
    > [!IMPORTANT]
    > Do not mistake this with a fine-tuned model. You must use the **pretrained** (pt) version (e.g., `Orpheus-3B-pt`) for cloning to work correctly.
    *   Example: [Orpheus-3B-pt-GGUF](https://huggingface.co/mradermacher/Orpheus-3B-pt-GGUF)
2.  **Reference Sample**: A high-quality WAV speech sample.
3.  **Transcript**: A plain `.txt` file containing the exact transcript of the reference sample.

## Setup

1.  Open the **OrpheusCloneTTS** sample/scene.
2.  Set the **Model Path** to your GGUF model.
3.  Set the **Reference Audio Path** and **Transcript Path** (use absolute paths).

## Two-Step Process

Voice cloning works best when you "resample" the voice first to clean it up.

### Step 1: Sound Resampling (Cleanup)

This step cleans up the original audio to increase cloning success probability.

1.  **Use a High-Quality Model**: Use the highest quantization level your system can run (ideally **Q5** or higher). Avoid going below Q5 for this step.
2.  **Enable WAV Writer**: 
    *   Set a valid folder path for the **WavWriter**.
    *   Enable the **WavWriter** toggle in the inspector.
3.  **Generate Match**:
    *   Copy the exact text from your reference transcript into the input field.
    *   Run the generation.
4.  **Evaluate**:
    *   If the generated sound is good and captures the voice well, use this new generated file as your **Reference Audio** for future clones.
    *   *Note: Highly stylized voices (e.g., Genshin Impact Paimon) may fail to capture high notes. If it doesn't sound right after a few tries, the voice or reference sample may not be compatible.*

### Step 2: Running the Clone

Once you have a "clean" resampled WAV file, you can use it for actual cloning.

1.  **Reference Audio**: Use the resampled WAV from Step 1.
2.  **Optimization**: You can now use a lower quality/quantized model (like **Q4**) if needed for faster inference.
