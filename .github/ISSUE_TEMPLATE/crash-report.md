---
name: Crash report
about: Create a report to help us improve
title: ''
labels: ''
assignees: ''

---

## 🛑 Stop! Before you submit
Please perform these two checks to ensure the issue is with the library and not a corrupted model file. **Issues that do not complete these steps may be closed.**

### 1. Verify Model Integrity (Hash Check)
Check the SHA-256 hash of your local model file and compare it with the official hash on Hugging Face (found under the "Files and versions" tab).
- **Windows (PowerShell):** `Get-FileHash .\your_model_file.gguf`
- **Linux/Mac:** `sha256sum your_model_file.gguf`

- [ ] I have verified that my local model hash matches the Hugging Face hash.

### 2. Test with standalone Model Tester
Our library uses the same logic as the standard llama.cpp tools. Please verify if the crash persists in the standalone tester:
1. Download the [llama-cpp-model-tester](https://github.com/lookbe/llama-cpp-model-tester/releases/tag/model-tester).
2. Run your model through it.

- [ ] The model works fine in the **llama-cpp-model-tester**, but crashes in this library.

---

## Bug Description
A clear and concise description of what the bug is.

## Reproduction Steps
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

## Environment Info
- **Model Name:**
- **Quantization:** (e.g., Q4_K_M)
- **OS:** (e.g., Windows 11, Ubuntu 22.04)
- **Hardware:** (e.g., 16GB RAM, RTX 3060)

## Logs / Screenshots
If applicable, add screenshots or paste the terminal error log here.
