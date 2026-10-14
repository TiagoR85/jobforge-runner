namespace JobForge.Runner.Configuration;

public static class RequiredConfig
{
    public static void Ensure(IConfiguration configuration, params string[] keys)
    {
        var missing = keys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Missing required configuration: {string.Join(", ", missing)}. See .env.example.");
        }
    }
}
