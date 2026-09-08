
using System.Text;

namespace ChongReanProject.Domains;


public class KhmerTimeParser
{

    public List<string> ParsingTitle(string title)
    {
        List<string> titleafparing = [];

        var text = new StringBuilder();
        for (int t = 0; t < title.Length; t++)
        {
            char letter = title[t];


            if (letter is '(' or '[')
                continue;

            text.Append(letter);

            if (letter is ']' or ')')
            {
                text.Remove(text.Length - 1, 1);
                titleafparing.Add(text.ToString());
                text.Clear();
            }


        }
        if (text.Length > 0)
        {
            titleafparing.Add(text.ToString());
        }

        return titleafparing;
    }

    public string? ParsingUniqueId(string url)
    {
        Uri.TryCreate(url, UriKind.Absolute, out var uri);

        if (uri is null)
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0) return null;

        if (!int.TryParse(segments[0], out int uniqueId)) return null;

        return uniqueId.ToString();





    }
}


/*
[China, US pledge to work with Cambodia on cultural heritage preservation](https://www.khmertimeskh.com/502019706/china-us-pledge-to-work-with-cambodia-on-cultural-heritage-preservation/)
*/