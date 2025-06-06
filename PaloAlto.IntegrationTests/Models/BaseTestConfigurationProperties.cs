namespace PaloAlto.IntegrationTests.Models;

public abstract class BaseTestConfigurationProperties
{
    public string ServerUsername { get; set; }
    public string ServerPassword { get; set; }
    public string MachineName { get; set; }
    
    public void AddFirewallCredentials()
    {
        ServerUsername = Environment.GetEnvironmentVariable("PALOALTO_FIREWALL_USER");
        ServerPassword = Environment.GetEnvironmentVariable("PALOALTO_FIREWALL_PASSWORD");
        MachineName = Environment.GetEnvironmentVariable("PALOALTO_FIREWALL_HOST");
    }

    public void AddPanoramaCredentials()
    {
        ServerUsername = Environment.GetEnvironmentVariable("PALOALTO_PANORAMA_USER");
        ServerPassword = Environment.GetEnvironmentVariable("PALOALTO_PANORAMA_PASSWORD");
        MachineName = Environment.GetEnvironmentVariable("PALOALTO_PANORAMA_HOST");
    }
}
