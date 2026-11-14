using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceDilemma.CustomInput
{
    public sealed class InputContextStack
    {
        public sealed class Entry
        {
            public int token;
            public InputContextProfile profile;
        }
        readonly List<Entry> entries = new List<Entry>();
        int nextToken;
        public IReadOnlyList<Entry> Entries => entries;
        public InputContextProfile Current => entries.OrderByDescending(e => e.profile.priority).ThenByDescending(e => e.token).FirstOrDefault()?.profile;
        public event Action<InputContextProfile> Changed;
        public InputContextStack(InputContextProfile gameplay) { entries.Add(new Entry { profile = gameplay }); }
        public int Push(InputContextProfile profile)
        {
            if (!profile) throw new ArgumentNullException(nameof(profile));
            var before = Current;
            int token = ++nextToken; entries.Add(new Entry { token = token, profile = profile });
            if (before != Current) Changed?.Invoke(Current);
            return token;
        }
        public void Pop(int token)
        {
            if (token == 0) return;
            var before = Current;
            entries.RemoveAll(e => e.token == token);
            if (before != Current) Changed?.Invoke(Current);
        }
    }
}
