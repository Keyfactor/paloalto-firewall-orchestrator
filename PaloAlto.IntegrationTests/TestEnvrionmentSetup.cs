// Copyright 2025 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
