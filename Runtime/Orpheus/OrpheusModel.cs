using System.Collections.Generic;

namespace OrpheusTTS
{
    public class OrpheusModel : LlamaCpp.Completion
    {
        // special tokens for orpheus tts
        protected int[] orpheusPrefix = new int[] { 128259 };
        protected int[] orpheusSuffix = new int[] { 128009, 128260, 128261, 128257 };

        public override int[] Tokenize(string prompt)
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
