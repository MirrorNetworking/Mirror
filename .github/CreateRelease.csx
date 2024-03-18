using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Modify PreprocessorDefine.cs
string path = "Assets/Mirror/CompilerSymbols/PreprocessorDefine.cs";
string text = File.ReadAllText(path);

// Find the whole line of the first define ending with "MIRROR_n_OR_NEWER,"
string pattern = @"\s+\""(MIRROR_(\d+)_OR_NEWER)\""\,\n";
Match match = Regex.Matches(text, pattern).First();

// Remove the first define
text = text.Replace(match.Value, "");

// Find the highest version number entry, not having a comma at the end
pattern = @"\""(MIRROR_(\d+)_OR_NEWER)\""\n";
MatchCollection matches = Regex.Matches(text, pattern);
int maxVersion = matches.Max(m => int.Parse(m.Groups[2].Value));

// Find the last define ending with "MIRROR_n_OR_NEWER"
pattern = @"(\s+)\""(MIRROR_(\d+)_OR_NEWER)\""";
matches = Regex.Matches(text, pattern);
Match lastMatch = matches.Last();

// Add a new define for the next full version, used here and in ProjectSettings and version.txt
string newDefine = $"MIRROR_{maxVersion + 1}_OR_NEWER";

// Add the new define to the end of the hashset entries, with a comma after the previous entry and properly indented
text = text.Insert(lastMatch.Index + lastMatch.Length, $",\n{match.Groups[1].Value}\"{newDefine}\"");

File.WriteAllText(path, text);

// Modify ProjectSettings.asset
path = "ProjectSettings/ProjectSettings.asset";
text = File.ReadAllText(path);

// Define a regular expression pattern for finding the sections
pattern = @"(Server|Standalone|WebGL):(.+?)(?=(Server|Standalone|WebGL)|$)";
MatchCollection sectionMatches = Regex.Matches(text, pattern, RegexOptions.Singleline);

if (sectionMatches.Count > 0)
{
    foreach (Match sectionMatch in sectionMatches)
    {
        string sectionName = sectionMatch.Groups[1].Value.Trim();
        string sectionContent = sectionMatch.Groups[2].Value.Trim();

        // Now, you can work with sectionName and sectionContent
        // to locate and update the defines within each section.
        // For example, you can use Regex to modify defines within sectionContent.

        // For simplicity, let's assume you want to add the newDefine to the end of each section.
        pattern = @"(MIRROR_(\d+)_OR_NEWER);";
        MatchCollection defineMatches = Regex.Matches(sectionContent, pattern);

        if (defineMatches.Count > 0)
        {
            Match lastDefineMatch = defineMatches[defineMatches.Count - 1];
            int lastIndex = lastDefineMatch.Index + lastDefineMatch.Length;
            sectionContent = sectionContent.Insert(lastIndex, $";{newDefine}");
        }

        // Replace the section in the original text with the modified section content
        text = text.Remove(sectionMatch.Index, sectionMatch.Length);
        text = text.Insert(sectionMatch.Index, $"{sectionName}:{sectionContent}");
    }
}

File.WriteAllText(path, text);

// Update version.txt with newDefine, e.g. MIRROR_84_OR_NEWER, replacing _ with .
File.WriteAllText("Assets/Mirror/version.txt", newDefine.Replace("_", "."));
