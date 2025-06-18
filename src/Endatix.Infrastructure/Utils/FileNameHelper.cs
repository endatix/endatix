namespace Endatix.Infrastructure.Utils;

public static class FileNameHelper
{
    public static string SanitizeFileName(string fileName)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
} 