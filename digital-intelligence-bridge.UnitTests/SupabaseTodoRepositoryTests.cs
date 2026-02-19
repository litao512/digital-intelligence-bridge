using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SupabaseTodoRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ShouldReturnFallbackSeed_WhenSupabaseNotConfigured()
    {
        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = string.Empty,
                    AnonKey = string.Empty,
                    Schema = "dib"
                }
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            configured: false);

        var todos = await repository.GetAllAsync();

        Assert.NotEmpty(todos);
    }

    [Fact]
    public async Task GetAllAsync_ShouldMapSupabaseRows_WhenRequestSucceeds()
    {
        HttpRequestMessage? captured = null;
        var payload = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = Guid.NewGuid(),
                title = "from-supabase",
                description = "desc",
                is_completed = false,
                created_at = DateTime.UtcNow,
                completed_at = (DateTime?)null,
                priority = "high",
                category = "工作",
                tags = new[] { "t1", "t2" },
                due_date = (DateTime?)null
            }
        });

        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = "http://localhost:54321",
                    AnonKey = "anon-key",
                    Schema = "dib"
                }
            },
            req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
            },
            configured: true);

        var todos = await repository.GetAllAsync();

        Assert.Single(todos);
        Assert.Equal("from-supabase", todos[0].Title);
        Assert.Equal(TodoItem.PriorityLevel.High, todos[0].Priority);
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.Contains("Accept-Profile"));
    }

    [Fact]
    public async Task GetAllAsync_ShouldAttachAuthAndSchemaHeaders_WhenConfigured()
    {
        HttpRequestMessage? captured = null;
        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = "http://localhost:54321",
                    AnonKey = "anon-key",
                    Schema = "dib"
                }
            },
            req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                };
            },
            configured: true);

        _ = await repository.GetAllAsync();

        Assert.NotNull(captured);
        Assert.NotNull(captured!.RequestUri);
        Assert.Contains("/rest/v1/todos", captured.RequestUri!.ToString());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("anon-key", captured.Headers.Authorization?.Parameter);
        Assert.True(captured.Headers.Contains("apikey"));
        Assert.True(captured.Headers.Contains("Accept-Profile"));
        Assert.True(captured.Headers.Contains("Content-Profile"));
        Assert.Equal("dib", captured.Headers.GetValues("Accept-Profile").Single());
        Assert.Equal("dib", captured.Headers.GetValues("Content-Profile").Single());
        Assert.Contains(captured.Headers.Accept, x => x.MediaType == "application/json");
    }

    [Fact]
    public async Task GetAllAsync_ShouldUsePublicSchema_WhenSchemaIsEmpty()
    {
        HttpRequestMessage? captured = null;
        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = "http://localhost:54321",
                    AnonKey = "anon-key",
                    Schema = string.Empty
                }
            },
            req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                };
            },
            configured: true);

        _ = await repository.GetAllAsync();

        Assert.NotNull(captured);
        Assert.Equal("public", captured!.Headers.GetValues("Accept-Profile").Single());
        Assert.Equal("public", captured.Headers.GetValues("Content-Profile").Single());
    }

    [Fact]
    public async Task AddAsync_ShouldKeepDataInFallback_WhenPostFails()
    {
        var item = new TodoItem
        {
            Title = "local-fallback",
            Description = "d",
            Category = "默认",
            Priority = TodoItem.PriorityLevel.Normal
        };

        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = "http://localhost:54321",
                    AnonKey = "anon-key",
                    Schema = "dib"
                }
            },
            req =>
            {
                return req.Method switch
                {
                    var m when m == HttpMethod.Post => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    var m when m == HttpMethod.Get => throw new HttpRequestException("offline"),
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                };
            },
            configured: true);

        var addResult = await repository.AddAsync(item);
        var todos = await repository.GetAllAsync();

        Assert.False(addResult);
        Assert.Contains(todos, x => x.Title == "local-fallback");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFallback_WhenPatchFails()
    {
        var id = Guid.NewGuid();
        var item = new TodoItem
        {
            Id = id,
            Title = "before-update",
            Description = "d",
            Category = "默认",
            Priority = TodoItem.PriorityLevel.Normal
        };

        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = "http://localhost:54321",
                    AnonKey = "anon-key",
                    Schema = "dib"
                }
            },
            req =>
            {
                return req.Method switch
                {
                    var m when m == HttpMethod.Post => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    var m when m == HttpMethod.Patch => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    var m when m == HttpMethod.Get => throw new HttpRequestException("offline"),
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                };
            },
            configured: true);

        await repository.AddAsync(item);
        item.Title = "after-update";

        var updateResult = await repository.UpdateAsync(item);
        var todos = await repository.GetAllAsync();

        Assert.False(updateResult);
        Assert.Contains(todos, x => x.Id == id && x.Title == "after-update");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveFromFallback_WhenDeleteFails()
    {
        var id = Guid.NewGuid();
        var item = new TodoItem
        {
            Id = id,
            Title = "to-delete",
            Description = "d",
            Category = "默认",
            Priority = TodoItem.PriorityLevel.Normal
        };

        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = "http://localhost:54321",
                    AnonKey = "anon-key",
                    Schema = "dib"
                }
            },
            req =>
            {
                return req.Method switch
                {
                    var m when m == HttpMethod.Post => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    var m when m == HttpMethod.Delete => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    var m when m == HttpMethod.Get => throw new HttpRequestException("offline"),
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                };
            },
            configured: true);

        await repository.AddAsync(item);
        var deleteResult = await repository.DeleteAsync(id);
        var todos = await repository.GetAllAsync();

        Assert.False(deleteResult);
        Assert.DoesNotContain(todos, x => x.Id == id);
    }

    [Fact]
    public async Task ClearCompletedAsync_ShouldClearFallback_WhenRemoteDeleteFails()
    {
        var completedItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "completed-local",
            IsCompleted = true,
            Category = "默认",
            Priority = TodoItem.PriorityLevel.Normal
        };

        var repository = CreateRepository(
            new AppSettings
            {
                Supabase = new SupabaseConfig
                {
                    Url = "http://localhost:54321",
                    AnonKey = "anon-key",
                    Schema = "dib"
                }
            },
            req =>
            {
                return req.Method switch
                {
                    var m when m == HttpMethod.Post => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    var m when m == HttpMethod.Delete => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    var m when m == HttpMethod.Get => throw new HttpRequestException("offline"),
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                };
            },
            configured: true);

        await repository.AddAsync(completedItem);

        var removedCount = await repository.ClearCompletedAsync();
        var todos = await repository.GetAllAsync();

        Assert.True(removedCount >= 1);
        Assert.DoesNotContain(todos, x => x.IsCompleted);
    }

    private static SupabaseTodoRepository CreateRepository(
        AppSettings settings,
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        bool configured)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responder));
        return new SupabaseTodoRepository(
            httpClient,
            NullLogger<SupabaseTodoRepository>.Instance,
            Options.Create(settings),
            new StubSupabaseService(configured));
    }

    private sealed class StubSupabaseService : ISupabaseService
    {
        public StubSupabaseService(bool configured)
        {
            IsConfigured = configured;
        }

        public bool IsConfigured { get; }

        public Task<SupabaseConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SupabaseConnectionResult(true, true, HttpStatusCode.OK, "ok"));
        }

        public Task<SupabaseConnectionResult> CheckTableAccessAsync(string tableName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SupabaseConnectionResult(true, true, HttpStatusCode.OK, "ok"));
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
