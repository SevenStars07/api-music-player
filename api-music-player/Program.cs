using api_music_player.Models;
using api_music_player.Output;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Dapper;
using Npgsql;

namespace api_music_player;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddCors();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseCors(builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
        );

        app.UseAuthorization();

        app.MapGet("/greet/{name}", (HttpContext httpContext, IConfiguration configuration, string name) =>
        {
            return new
            {
                Greet = $"Hello {name}",
                Db = configuration.GetConnectionString("Database"),
                AzureBlobStorage = configuration.GetConnectionString("AzureBlobStorage")
            };
        }).WithName("HelloWorld").WithOpenApi();

        app.MapGet("/get-songs", async (HttpContext httpContext, IConfiguration configuration) =>
            {
                await using var connection = new NpgsqlConnection(configuration.GetConnectionString("Database"));

                var songs = (await connection.QueryAsync<Song>("select * from songs")).ToList();

                foreach (var song in songs)
                {
                    song.Url = await GetSongUrl(song, configuration);
                }

                return new SongsOutput
                {
                    Songs = songs.Select(x => new SongOutput
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Artist = x.Artist,
                        Album = x.Album,
                        Duration = x.Duration,
                        Url = x.Url
                    })
                };
            })
            .WithName("GetSongs")
            .WithOpenApi();

        app.Run();
    }

    private static async Task<string> GetSongUrl(Song song, IConfiguration configuration)
    {
        var blobClient = GetBlobClient(configuration, song.FileName);

        var sas = await CreateServiceSASBlob(blobClient);

        return sas.AbsoluteUri;
    }

    private static BlobClient GetBlobClient(IConfiguration configuration, string filename)
    {
        var blobServiceClient = new BlobServiceClient(configuration.GetConnectionString("AzureBlobStorage"));

        var blobContainerClient = blobServiceClient.GetBlobContainerClient("music");

        var blobClient = blobContainerClient.GetBlobClient(filename);

        return blobClient;
    }

    private static async Task<Uri> CreateServiceSASBlob(
        BlobClient blobClient,
        string storedPolicyName = null)
    {
        // Check if BlobContainerClient object has been authorized with Shared Key
        BlobSasBuilder sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
            BlobName = blobClient.Name,
            Resource = "b"
        };
        if (blobClient.CanGenerateSasUri)
        {
            // Create a SAS token that's valid for one day

            if (storedPolicyName == null)
            {
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddMonths(1);
                sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
            }
            else
            {
                sasBuilder.Identifier = storedPolicyName;
            }

            var sasURI = blobClient.GenerateSasUri(sasBuilder);

            return sasURI;
        }
        else
        {
            // Client object is not authorized via Shared Key
            return null;
        }
    }
}