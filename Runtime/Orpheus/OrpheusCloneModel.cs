using LlamaCpp;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace OrpheusTTS
{
    public class OrpheusCloneModel : LlamaCpp.Completion
    {
        protected int[] orpheusPrefix = new int[] { 128259 };
        protected int[] orpheusSuffix = new int[] { 128009, 128260, 128261, 128257 };
        protected int[] orpheusFinalTokens = new int[] { 128258, 128262 };

        protected class ClonePayload : CompletionPayload
        {
            public string Prompt;
            public string Transcript;
            public int[] AudioTokens;
        }

        public void PromptWithClone(string prompt, string transcript, int[] audioTokens)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                return;
            }

            if (_llamaContext == IntPtr.Zero)
            {
                Debug.LogError("invalid context");
                return;
            }

            if (_llamaModel == IntPtr.Zero)
            {
                Debug.LogError("model not loaded");
                return;
            }

            if (status != LlamaCpp.ModelStatus.Ready)
            {
                Debug.LogError("invalid status");
                return;
            }

            status = LlamaCpp.ModelStatus.Generate;
            var payload = new ClonePayload()
            {
                Prompt = prompt,
                Transcript = transcript,
                AudioTokens = audioTokens,

                Temp = this.temperature,
                TopK = this.topK,
                TopP = this.topP,
                MinP = this.minP,
                RepeatPenalty = this.repeatPenalty,

            };

            RunBackground(payload, RunPromptWithClone);
        }

        void RunPromptWithClone(ClonePayload inputPayload, CancellationToken cts)
        {
            List<int> token_list = new List<int>();

            int[] prompt_token = base.Tokenize(inputPayload.Prompt);

            if (inputPayload.AudioTokens != null && inputPayload.AudioTokens.Length != 0)
            {
                int[] transcript_token = base.Tokenize(inputPayload.Transcript);

                token_list.AddRange(prompt_token);
                token_list.AddRange(orpheusPrefix);
                token_list.AddRange(transcript_token);
                token_list.AddRange(orpheusSuffix);
                token_list.AddRange(inputPayload.AudioTokens);
                token_list.AddRange(orpheusFinalTokens);
            }

            token_list.AddRange(orpheusPrefix);
            token_list.AddRange(prompt_token);
            token_list.AddRange(orpheusSuffix);

            var payload = new GenerationPayload()
            {
                Tokens = token_list.ToArray(),
                Temp = inputPayload.Temp,
                TopK = inputPayload.TopK,
                TopP = inputPayload.TopP,
                MinP = inputPayload.MinP,
                RepeatPenalty = inputPayload.RepeatPenalty,

            };

            RunGenerate(payload, cts);
        }

        protected override bool EndGeneration(int token, int generated_token_count)
        {
            return Native.llama_vocab_is_eog(_llamaVocab, token) || token == 128258;
        }
    }
}
