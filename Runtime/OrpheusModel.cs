using System.Collections.Generic;

namespace OrpheusTTS
{
    public class OrpheusModel : LlamaCpp.Completion
    {
        // special tokens for orpheus tts
        int[] orpheusPrefix = new int[] { 128259 };
        int[] orpheusSuffix = new int[] { 128009, 128260, 128261, 128257 };

        protected override int[] Tokenize(string prompt)
        {
            var tokens = base.Tokenize(prompt);

            List<int> tokensBuffer = new List<int>();
            tokensBuffer.AddRange(orpheusPrefix);
            tokensBuffer.AddRange(tokens);
            tokensBuffer.AddRange(orpheusSuffix);

            return tokensBuffer.ToArray();
        }
    }
}
