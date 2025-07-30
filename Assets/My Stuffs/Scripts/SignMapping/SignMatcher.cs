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

            //bool matchedPhrase = false;

            // Check if any multi-word entry in the map starts with the current word
            // foreach (var entry in signMap)
            // {
            //     //string[] entryWords = entry.Key.ToLower().Split(' ');
            //
            //     // if (entryWords.Length == 2 && entryWords[0] == current)
            //     // {
            //     //     // Check if next word in gloss matches second word in mapping
            //     //     if (i + 1 < gloss.Count && gloss[i + 1].ToLower() == entryWords[1])
            //     //     {
            //     //         result.Add(entry.Value);
            //     //         i += 2;
            //     //         matchedPhrase = true;
            //     //         break;
            //     //     }
            //     // }
            // }

            //if (matchedPhrase) continue;

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