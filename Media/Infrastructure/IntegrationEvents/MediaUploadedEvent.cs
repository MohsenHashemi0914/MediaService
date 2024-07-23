namespace Media.Infrastructure.IntegrationEvents;

public sealed record MediaUploadedEvent(string FileName, string Url, string ObjectId, DateTime OccurredOn);