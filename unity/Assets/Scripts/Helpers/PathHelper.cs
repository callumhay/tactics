using System;

public static class PathHelper {

  public static string GetRelativePath(string fullPath, string basePath) {
    // Require trailing backslash for path
    if (!basePath.EndsWith("\\")) { basePath += "\\"; }

    Uri baseUri = new Uri(basePath);
    Uri fullUri = new Uri(fullPath);

    Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

    // Uri's use forward slashes so convert back to backward slashes
    return relativeUri.ToString().Replace("/", "\\");
  }
}