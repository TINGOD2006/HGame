using System.Collections.Generic;
using HGame.Core;

namespace HGame.Unity
{
    public sealed class GallerySession
    {
        private readonly List<string> unlocked;
        public string CurrentId { get; private set; }
        public bool IsPlaying { get; private set; }
        public GallerySession(ProfileState profile) { unlocked = new List<string>(profile.UnlockedGalleryIds); }
        public IReadOnlyList<string> Items { get { return unlocked.AsReadOnly(); } }
        public bool Play(string id)
        {
            if (!unlocked.Contains(id)) return false;
            CurrentId = id; IsPlaying = true; return true;
        }
        public void Skip() { IsPlaying = false; }
        public void Close() { IsPlaying = false; CurrentId = null; }
    }
}
