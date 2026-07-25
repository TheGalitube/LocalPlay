namespace LocalPlay.Services;

public static class EngineLocator
{
    public static string? Find()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("LOCALPLAY_UXPLAY");
        var candidates = new[]
        {
            fromEnvironment,
            Path.Combine(AppContext.BaseDirectory, "engine", "uxplay.exe"),
            @"C:\msys64\ucrt64\bin\uxplay.exe"
        };

        return candidates.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate));
    }
}

