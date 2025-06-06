namespace PaloAlto.IntegrationTests.Generators;

public static class AliasGenerator
{
    public static string Generate()
    {
        return Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10).ToLowerInvariant();
    }
}
