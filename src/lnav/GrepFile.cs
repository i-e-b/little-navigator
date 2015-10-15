namespace lnav
{
    using System.Drawing;
    using System.IO;
    using System.Text.RegularExpressions;

    public static class Grep
    {
        public static Point? FileContainsPattern(string filePath, Regex pattern)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                using (var reader = File.OpenText(filePath))
                {
                    string line;
                    int row = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        row++;
                        var match = pattern.Match(line);
                        if (!match.Success) continue;
                        return new Point(match.Index, row);
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        public static bool IsValid(string pattern)
        {
            try {
                Regex.Match("dgfjkdflgj", pattern);
                return true;
            } catch {
                return false;
            }
        }
    }
}