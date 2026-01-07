using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OrpheusTTS
{
    public class SnacTokenizer : MonoBehaviour
    {
        public OrpheusModel model;
        public SnacDecoder snac;

        private List<int> streamTokenBuffer = new List<int>();
        private int streamTokenCount = 0;

        private const string CustomTokenPrefix = "<custom_token_";

        // --- SNAC/Audio Constants ---
        private const int SNAC_CHUNK_SIZE = 28;
        private const int SNAC_INCREMENT = 7;

        private void OnEnable()
        {
            if (model != null)
            {
                model.OnResponseStreamed += OnBotResponseStreamed;
                model.OnResponseGenerated += OnBotResponseGenerated;
            }
        }

        private void OnDisable()
        {
            if (model != null)
            {
                model.OnResponseGenerated -= OnBotResponseGenerated;
                model.OnResponseStreamed -= OnBotResponseStreamed;
            }
        }

        void OnBotResponseStreamed(string response)
        {
            int tokenId = GetTokenId(response, streamTokenCount);

            if (tokenId > 0)
            {
                streamTokenBuffer.Add(tokenId);
                streamTokenCount++;

                // Decode a 28-token chunk every 7 new tokens (SNAC logic)
                if (streamTokenCount % SNAC_INCREMENT == 0 && streamTokenCount >= SNAC_CHUNK_SIZE)
                {
                    List<int> bufferToProc = streamTokenBuffer.GetRange(streamTokenBuffer.Count - SNAC_CHUNK_SIZE, SNAC_CHUNK_SIZE);
                    snac?.Decode(bufferToProc);
                }
            }
        }

        void OnBotResponseGenerated(string response)
        {
            streamTokenBuffer.Clear();
            streamTokenCount = 0;
        }

        int GetTokenId(string tokenString, int index)
        {
            tokenString = tokenString.Trim();
            int lastTokenStart = tokenString.LastIndexOf(CustomTokenPrefix);

            if (lastTokenStart == -1) return 0;

            string lastToken = tokenString.Substring(lastTokenStart);

            if (lastToken.StartsWith(CustomTokenPrefix) && lastToken.EndsWith(">"))
            {
                try
                {
                    string numberStr = lastToken.Substring(CustomTokenPrefix.Length, lastToken.Length - CustomTokenPrefix.Length - 1);

                    if (int.TryParse(numberStr, out int number))
                    {
                        int tokenId = number - 10 - ((index % SNAC_INCREMENT) * 4096);
                        return tokenId;
                    }
                    else
                    {
                        return 0;
                    }
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            return 0;
        }
    }
}
