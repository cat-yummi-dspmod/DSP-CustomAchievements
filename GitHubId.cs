using System;

namespace CustomAchievements
{
    /// <summary>把各种写法的 GitHub 仓库地址统一成 "github.com/owner/repo"（小写）。
    /// Normalizes any spelling of a GitHub repository URL to "github.com/owner/repo" (lower case).</summary>
    internal static class GitHubId
    {
        internal static bool TryNormalize(string url, out string id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }
            string s = url.Trim();
            int cut = s.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0)
            {
                s = s.Substring(0, cut);
            }
            int scheme = s.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                s = s.Substring(scheme + 3);
            }
            if (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(4);
            }
            string[] parts = s.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !parts[0].Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string owner = parts[1];
            string repo = parts[2];
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                repo = repo.Substring(0, repo.Length - 4);
            }
            if (!IsName(owner) || !IsName(repo))
            {
                return false;
            }
            id = ("github.com/" + owner + "/" + repo).ToLowerInvariant();
            return true;
        }

        internal static bool IsKey(string key)
        {
            return IsName(key);
        }

        static bool IsName(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 100)
            {
                return false;
            }
            foreach (char c in s)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
