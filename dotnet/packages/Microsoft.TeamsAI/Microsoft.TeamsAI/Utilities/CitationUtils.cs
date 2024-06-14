using Microsoft.Teams.AI.AI.Action;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Teams.AI.Utilities
{
    public class CitationUtils
    {
        /// <summary>
        /// Clips the text to a maximum length in case it exceeds the limit.
        /// Replaces the last 3 characters with "..."
        /// </summary>
        /// <param name="text">The text to clip.</param>
        /// <param name="maxLength">The max text length. Must be at least 4 characters long</param>
        /// <returns>The clipped text.</returns>
        public static string Snippet(string text, int maxLength)
        {
            if (text.Length <= maxLength)
            {
                return text;
            }

            string snippet = text.Substring(0, maxLength - 3).Trim();
            snippet += "...";
            return snippet;
        }

        /// <summary>
        /// Convert citation tags `[doc(s)n]` to `[n]` where n is a number.
        /// </summary>
        /// <param name="text">The text to format</param>
        /// <returns>The formatted text.</returns>
        public static string FormatCitationsResponse(string text)
        {
            return Regex.Replace(text, @"【\d+:(\d+)†source】", "[$1]", RegexOptions.IgnoreCase);
            // return Regex.Replace(text, @"\[docs?(\d+)\]", "[$1]", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Filters out citations that are not referenced in the `text` as `[n]` tags (ex. `[1]` or `[2]`)
        /// </summary>
        /// <param name="text">Text that has citation tags.</param>
        /// <param name="citations">List of citations</param>
        /// <returns></returns>
        public static List<ClientCitation>? GetUsedCitations(string text, List<ClientCitation> citations)
        {
            Regex regex = new(@"\[(\d+)\]");
            //  Regex regex2 = new(@"【\d+:(\d+)†source】");
            //Regex regex = new(@"【\d+:(\d+)†source】");
            MatchCollection matches = regex.Matches(text);

            if (matches.Count == 0)
            {
                return null;
            }
            else
            {
                List<ClientCitation> usedCitations = new();
                foreach (Match match in matches)
                {
                    if (!usedCitations.Any(a => $"[{a.Position}]" == match.Value))
                    {
                        citations.Find((citation) =>
                            {
                                //  var isMatch = regex2.Match(citation.Appearance.Name)

                                //  if (FormatCitationsResponse(citation.Appearance.Text) == match.Value)
                                //if (citation.Appearance.Text == match.Value)
                                //     if (citation.Position == match.Value)
                                if ($"[{citation.Position}]" == match.Value)
                                //    if (match.Value.EndsWith($"{citation.Position}]"))
                                {
                                    // citation.Position = Regex.Replace(match.Value, @"\[(\d+)\]", "$1", RegexOptions.IgnoreCase);
                                    usedCitations.Add(citation);
                                    return true;
                                }
                                return false;
                            });
                    }

                }
                return usedCitations;
            }
        }
    }
}