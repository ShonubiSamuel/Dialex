using System.Collections.Generic;
using UnityEngine;

public class SignMatcher
{
    private Dictionary<string, string> signMap;

    public SignMatcher(Dictionary<string, string> mapping)
    {
        signMap = mapping;
    }

    public List<string> GetAnimationSequence(List<string> gloss)
    {
        List<string> result = new List<string>();

        for (int i = 0; i < gloss.Count;)
        {
            string current = gloss[i].ToLower();

            // Try single word
            if (signMap.TryGetValue(current, out string wordAnim))
            {
                result.Add(wordAnim);
            }
            else
            {
                // Fallback: spell it
                foreach (char c in current)
                {
                    if (char.IsLetter(c))
                        result.Add($"Spell:{char.ToLower(c)}");
                }
            }

            i++;
        }

        return result;
    }

}