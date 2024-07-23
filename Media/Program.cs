using MassTransit;
using Media;
using Media.Infrastructure.Context;
using Media.Infrastructure.Extensions;
using Media.Infrastructure.IntegrationEvents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMinio(config =>
{
    var endpoint = builder.Configuration["MinioConfiguration:EndpointAddress"];
    var accessKey = builder.Configuration["MinioConfiguration:AccessKey"];
    var secretKey = builder.Configuration["MinioConfiguration:SecretKey"];

    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

    config.WithEndpoint(endpoint)
          .WithCredentials(accessKey, secretKey)
          .WithSSL(secure: true)
          .Build();
});

builder.Services.AddDbContext<MediaDbContext>(options =>
{
    options.UseInMemoryDatabase("MediaDb");
});

builder.Services.Configure<AppSettings>(builder.Configuration);

builder.ConfigureBroker();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/{bucket_name}/{object_id}/", async (
    [FromRoute(Name = "bucket_name")] string bucketName,
    [FromRoute(Name = "object_id")] string objectId,
    IFormFile file,
    IPublishEndpoint publisher,
    IConfiguration configuration,
    MediaDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var minioOptions = configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>();

    var client = new MinioClient()
                 .WithEndpoint(minioOptions!.EndpointAddress)
                 .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
                 .Build();

    var putObjectArgs = new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(file.FileName)
                        .WithStreamData(file.OpenReadStream())
                        .WithObjectSize(file.Length);

    try
    {
        _ = await client.PutObjectAsync(putObjectArgs, cancellationToken);
        var token = new UrlToken
        {
            Id = Guid.NewGuid(),
            BucketName = bucketName,
            ObjectName = file.FileName,
            ContentType = file.ContentType
        };
        await dbContext.AddAsync(token);
        await dbContext.SaveChangesAsync();
        var accessUrl = $"http://localhost:5284/{token.Id}";
        var eventData = new MediaUploadedEvent(file.FileName, accessUrl, objectId, DateTime.UtcNow);
        await publisher.Publish(eventData, cancellationToken);
    }
    catch (Exception)
    {
        throw;
    }
}).DisableAntiforgery();

app.MapGet("/{token_id:guid:required}", async ([FromRoute(Name = "token_id")] Guid tokenId,
    IMinioClient client,
    IConfiguration configuration,
    MediaDbContext dbContext) =>
{
    var token = await dbContext.Tokens.FindAsync(tokenId);

    using var memoryStream = new MemoryStream();
    var getObjectArgs = new GetObjectArgs()
                        .WithBucket(token!.BucketName)
                        .WithObject(token.ObjectName)
                        .WithCallbackStream(stream =>
                        {
                            stream.CopyTo(memoryStream);
                        });

    try
    {
        _ = await client.GetObjectAsync(getObjectArgs);
        return Results.File(memoryStream, contentType: token.ContentType);
    }
    catch (Exception)
    {
        throw;
    }
    
}).DisableAntiforgery();

app.Run();