using System;
using System.Linq;

namespace SpaceDilemma.Hud
{
    // Plain game data: mission code can update this without referencing UGUI or TMP.
    public sealed class ObjectiveModel
    {
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string KoreanTitle { get; private set; }
        public string KoreanDescription { get; private set; }
        public int Current { get; private set; }
        public int Total { get; private set; }
        public bool IsExample { get; private set; }
        public event Action Changed;

        public void Set(string title, string description, int current, int total, bool isExample = false)
            => SetLocalized(title, description, title, description, current, total, isExample);

        public void SetLocalized(string englishTitle, string englishDescription, string koreanTitle,
            string koreanDescription, int current, int total, bool isExample = false)
        {
            Title = EnglishText(englishTitle, "Current Objective");
            Description = EnglishText(englishDescription, "English description unavailable.");
            KoreanTitle = koreanTitle ?? Title;
            KoreanDescription = koreanDescription ?? Description;
            Current = current; Total = total; IsExample = isExample;
            Changed?.Invoke();
        }

        static string EnglishText(string value, string fallback)
        {
            if (value == null) return "";
            const string punctuation = "’‘“”–—…\u00A0";
            return value.All(c => c <= '\u007f' || punctuation.Contains(c)) ? value : fallback;
        }
    }
}
