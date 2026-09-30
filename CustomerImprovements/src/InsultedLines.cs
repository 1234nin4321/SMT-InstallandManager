using UnityEngine;

namespace SMTCustomerImprovements
{
    // What an honest customer says when they're accused of paying with fake cash or a stolen card
    static class InsultedLines
    {
        static readonly string[] Any =
        {
            "Excuse me?! I've never been so insulted!",
            "Do I look like a criminal to you?",
            "I'm taking my business elsewhere!",
            "I want to speak to your manager!",
            "Keep your groceries. I'll shop across the street.",
            "How dare you! I've shopped here for years!",
            "One star. I'm leaving one star.",
            "Wow. Just wow.",
            "You'll be hearing from my lawyer!",
            "Fine! I didn't want those snacks anyway.",
            "I was going to recommend this place to my friends!",
            "Accusing your own customers? Unbelievable!",
        };

        static readonly string[] Cash =
        {
            "Fake?! That's my grandma's birthday money!",
            "I got that cash from the bank this morning!",
            "My money is as real as it gets!",
            "Hold it up to the light then, go on!",
        };

        static readonly string[] Card =
        {
            "Stolen? It has my name on it! Look!",
            "That's my card! I've had it for ten years!",
            "Want to see my ID too? Unbelievable!",
            "My bank will hear about this!",
        };

        public static string Pick(bool card)
        {
            var own = card ? Card : Cash;
            int i = Random.Range(0, Any.Length + own.Length);
            return i < Any.Length ? Any[i] : own[i - Any.Length];
        }
    }
}
