using System.Runtime.CompilerServices;

namespace PaloAlto.IntegrationTests;

public static class TestEnvrionmentSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        LoadDotEnvFile();
    }
    
    private static void LoadDotEnvFile()
    {
        var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env.test");
        if (File.Exists(envFile))
        {
            foreach (var line in File.ReadAllLines(envFile))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                    
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
        }
        else
        {
            throw new Exception(".env.test not found. Copy the .env.test.example file to .env.test and fill in the required values. Make sure the .env.test has the CopyAlways property enabled.");
        }
    }
}
