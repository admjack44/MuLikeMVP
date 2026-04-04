using System.Text;

namespace MuLike.Systems
{
    public static class ChatSanitizer
    {
        public const int MaxMessageLength = 256;

        public static string SanitizeText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var builder = new StringBuilder(raw.Length);
            bool lastWasSpace = false;

            for (int i = 0; i < raw.Length && builder.Length < MaxMessageLength; i++)
            {
                char c = raw[i];

                if (char.IsControl(c))
                {
                    if (!lastWasSpace)
                    {
                        builder.Append(' ');
                        lastWasSpace = true;
                    }
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        builder.Append(' ');
                        lastWasSpace = true;
                    }
                    continue;
                }

                builder.Append(c);
                lastWasSpace = false;
            }

            return builder.ToString().Trim();
        }

        public static string SanitizeName(string raw, int maxLength = 24)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string text = SanitizeText(raw);
            return text.Length > maxLength ? text.Substring(0, maxLength) : text;
        }
    }
}
