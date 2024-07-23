namespace Media;

public sealed class AppSettings
{
    public required BrokerOptions BrokerConfiguration { get; init; }
    public required MinioOptions MinioConfiguration { get; init; }
}

public sealed class MinioOptions
{
    public const string SectionName = "MinioConfiguration";

    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string EndpointAddress { get; init; }
}

public sealed class BrokerOptions
{
    public const string SectionName = "BrokerConfiguration";

    public required string Host { get; init; }
    public required string UserName { get; init; }
    public required string Password { get; init; }
}