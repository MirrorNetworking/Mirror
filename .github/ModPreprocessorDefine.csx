#r "System.IO"
#r "System.Text.RegularExpressions"

void Main()
{
    // Define the path to the PreprocessorDefine.cs file
    string filePath = "Assets/Mirror/CompilerSymbols/PreprocessorDefine.cs";

    // Read the contents of the file
    string fileContents = File.ReadAllText(filePath);

    // Find and remove the first entry ending with "_OR_NEWER"
    fileContents = RemoveFirstOrNewerEntry(fileContents);

    // Find the last entry and capture the version number
    string versionNumber = GetLastVersionNumber(fileContents);

    // Append a new entry with the correct indentation and next version number
    fileContents = AppendNewEntry(fileContents, versionNumber);

    // Write the modified contents back to the file
    File.WriteAllText(filePath, fileContents);
}

string RemoveFirstOrNewerEntry(string input)
{
    // Regex pattern to match the first entry ending with "_OR_NEWER"
    string pattern = @"^\s*""[^""]*_OR_NEWER""\s*,\s*$";

    // Find the first match
    Match match = Regex.Match(input, pattern, RegexOptions.Multiline);

    // If a match is found, remove the entire line
    if (match.Success)
    {
        input = input.Remove(match.Index, match.Length);
    }

    return input;
}

string GetLastVersionNumber(string input)
{
    // Regex pattern to match the last entry and capture the version number
    string pattern = @"^\s*""([^""]*)_OR_NEWER""\s*,\s*$";

    // Find all matches
    MatchCollection matches = Regex.Matches(input, pattern, RegexOptions.Multiline);

    // Capture the version number from the last match
    string versionNumber = matches.Count > 0 ? matches[matches.Count - 1].Groups[1].Value : "";

    return versionNumber;
}

string AppendNewEntry(string input, string versionNumber)
{
    // Calculate the next version number (increment by 1)
    int nextVersion = int.TryParse(versionNumber, out int currentVersion) ? currentVersion + 1 : 1;

    // Get the indentation of the "HashSet<string> defines = new HashSet<string>" line
    string indentation = GetHashSetIndentation(input);

    // Create the new entry with the correct indentation and next version number
    string newEntry = indentation + $"    \"MIRROR_{nextVersion}_OR_NEWER\"";

    // Find the position of the "defines" HashSet and insert the new entry into it
    int definesStartIndex = input.IndexOf("HashSet<string> defines = new HashSet<string>");
    int definesEndIndex = input.IndexOf("};", definesStartIndex) + 1;

    // Insert the comma and new entry into the "defines" HashSet
    input = input.Remove(definesEndIndex - 2, 2); // Remove the trailing "};"
    input = input.Insert(definesEndIndex - 2, $",\n{newEntry}\n{indentation}};");

    return input;
}

string GetHashSetIndentation(string input)
{
    // Regex pattern to match the indentation of "HashSet<string> defines = new HashSet<string>"
    string pattern = @"^\s*HashSet<string> defines = new HashSet<string>";

    // Find the first match
    Match match = Regex.Match(input, pattern, RegexOptions.Multiline);

    // If a match is found, capture the indentation and add 4 spaces
    string indentation = match.Success ? Regex.Match(match.Value, @"^\s*").Value : "";

    return indentation;
}
