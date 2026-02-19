using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalIntelligenceBridge.Services;

public class SupabaseTodoRepository : ITodoRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SupabaseTodoRepository> _logger;
    private readonly SupabaseConfig _config;
    private readonly ISupabaseService _supabaseService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly List<TodoItem> _fallbackStore = CreateSeedTodos();

    public SupabaseTodoRepository(
        HttpClient httpClient,
        ILogger<SupabaseTodoRepository> logger,
        IOptions<AppSettings> settings,
        ISupabaseService supabaseService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = settings.Value.Supabase;
        _supabaseService = supabaseService;
    }

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_supabaseService.IsConfigured)
        {
            return CloneList(_fallbackStore);
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, "/rest/v1/todos?select=*&order=created_at.desc");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to load todos from Supabase. Status={StatusCode}", response.StatusCode);
                return CloneList(_fallbackStore);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var rows = JsonSerializer.Deserialize<List<SupabaseTodoRow>>(content, _jsonOptions) ?? new List<SupabaseTodoRow>();
            var todos = rows.Select(MapToTodo).ToList();

            // Keep an in-memory fallback snapshot for temporary offline path.
            _fallbackStore.Clear();
            _fallbackStore.AddRange(CloneList(todos));

            return todos;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading todos from Supabase, fallback to local cache.");
            return CloneList(_fallbackStore);
        }
    }

    public async Task<bool> AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        if (!_supabaseService.IsConfigured)
        {
            _fallbackStore.Add(Clone(item));
            return true;
        }

        try
        {
            var row = MapToRow(item);
            var payload = JsonSerializer.Serialize(row, _jsonOptions);

            using var request = CreateRequest(HttpMethod.Post, "/rest/v1/todos");
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _fallbackStore.Add(Clone(item));
                return true;
            }

            _logger.LogWarning("Failed to add todo to Supabase. Status={StatusCode}", response.StatusCode);
            _fallbackStore.Add(Clone(item));
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding todo to Supabase, persisted in fallback store only.");
            _fallbackStore.Add(Clone(item));
            return false;
        }
    }

    public async Task<bool> UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        if (!_supabaseService.IsConfigured)
        {
            UpsertFallback(item);
            return true;
        }

        try
        {
            var row = MapToRow(item);
            var payload = JsonSerializer.Serialize(row, _jsonOptions);

            using var request = CreateRequest(HttpMethod.Patch, $"/rest/v1/todos?id=eq.{item.Id}");
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                UpsertFallback(item);
                return true;
            }

            _logger.LogWarning("Failed to update todo in Supabase. Status={StatusCode}", response.StatusCode);
            UpsertFallback(item);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating todo in Supabase, updated fallback store only.");
            UpsertFallback(item);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_supabaseService.IsConfigured)
        {
            RemoveFallback(id);
            return true;
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Delete, $"/rest/v1/todos?id=eq.{id}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                RemoveFallback(id);
                return true;
            }

            _logger.LogWarning("Failed to delete todo from Supabase. Status={StatusCode}", response.StatusCode);
            RemoveFallback(id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting todo from Supabase, removed from fallback store only.");
            RemoveFallback(id);
            return false;
        }
    }

    public async Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        var count = _fallbackStore.Count(x => x.IsCompleted);

        if (!_supabaseService.IsConfigured)
        {
            _fallbackStore.RemoveAll(x => x.IsCompleted);
            return count;
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Delete, "/rest/v1/todos?is_completed=eq.true");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to clear completed todos from Supabase. Status={StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error clearing completed todos from Supabase.");
        }

        _fallbackStore.RemoveAll(x => x.IsCompleted);
        return count;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var schema = string.IsNullOrWhiteSpace(_config.Schema) ? "public" : _config.Schema;
        var request = new HttpRequestMessage(method, $"{_config.Url.TrimEnd('/')}{relativePath}");

        request.Headers.Add("apikey", _config.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AnonKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Accept-Profile", schema);
        request.Headers.TryAddWithoutValidation("Content-Profile", schema);

        return request;
    }

    private static TodoItem MapToTodo(SupabaseTodoRow row)
    {
        return new TodoItem
        {
            Id = row.Id,
            Title = row.Title ?? string.Empty,
            Description = row.Description ?? string.Empty,
            IsCompleted = row.IsCompleted,
            CreatedAt = row.CreatedAt ?? DateTime.Now,
            CompletedAt = row.CompletedAt,
            Priority = ParsePriority(row.Priority),
            Category = string.IsNullOrWhiteSpace(row.Category) ? "默认" : row.Category,
            Tags = row.Tags ?? new List<string>(),
            DueDate = row.DueDate
        };
    }

    private static SupabaseTodoRow MapToRow(TodoItem item)
    {
        return new SupabaseTodoRow
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            IsCompleted = item.IsCompleted,
            CreatedAt = item.CreatedAt,
            CompletedAt = item.CompletedAt,
            Priority = item.Priority.ToString().ToLowerInvariant(),
            Category = item.Category,
            Tags = item.Tags,
            DueDate = item.DueDate
        };
    }

    private static TodoItem.PriorityLevel ParsePriority(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "high" => TodoItem.PriorityLevel.High,
            "low" => TodoItem.PriorityLevel.Low,
            _ => TodoItem.PriorityLevel.Normal
        };
    }

    private static List<TodoItem> CreateSeedTodos()
    {
        return new List<TodoItem>
        {
            new()
            {
                Title = "学习 Avalonia 基础",
                Description = "了解 XAML 布局和数据绑定",
                Priority = TodoItem.PriorityLevel.High,
                Category = "学习",
                Tags = new List<string> { "技术", "UI" },
                DueDate = DateTime.Now.AddDays(3)
            },
            new()
            {
                Title = "创建第一个 Avalonia 项目",
                Description = "使用 MVVM 模式开发",
                Priority = TodoItem.PriorityLevel.Normal,
                Category = "工作",
                Tags = new List<string> { "项目", "开发" }
            },
            new()
            {
                Title = "购买 groceries",
                Description = "牛奶、面包、鸡蛋",
                Priority = TodoItem.PriorityLevel.Low,
                Category = "购物",
                Tags = new List<string> { "日常" },
                DueDate = DateTime.Now.AddDays(1)
            },
            new()
            {
                Title = "晨跑 5 公里",
                Description = "保持健康",
                Priority = TodoItem.PriorityLevel.Normal,
                Category = "健康",
                Tags = new List<string> { "运动" },
                IsCompleted = true
            },
            new()
            {
                Title = "准备周会报告",
                Description = "总结本周工作进展",
                Priority = TodoItem.PriorityLevel.High,
                Category = "工作",
                Tags = new List<string> { "会议", "汇报" },
                DueDate = DateTime.Now.AddDays(-1)
            }
        };
    }

    private static List<TodoItem> CloneList(IEnumerable<TodoItem> source) => source.Select(Clone).ToList();

    private static TodoItem Clone(TodoItem item)
    {
        return new TodoItem
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            IsCompleted = item.IsCompleted,
            CreatedAt = item.CreatedAt,
            CompletedAt = item.CompletedAt,
            Priority = item.Priority,
            Category = item.Category,
            Tags = item.Tags.ToList(),
            DueDate = item.DueDate,
            IsReminderSet = item.IsReminderSet,
            ReminderTime = item.ReminderTime
        };
    }

    private void UpsertFallback(TodoItem item)
    {
        var index = _fallbackStore.FindIndex(x => x.Id == item.Id);
        if (index >= 0)
        {
            _fallbackStore[index] = Clone(item);
        }
        else
        {
            _fallbackStore.Add(Clone(item));
        }
    }

    private void RemoveFallback(Guid id)
    {
        var index = _fallbackStore.FindIndex(x => x.Id == id);
        if (index >= 0)
        {
            _fallbackStore.RemoveAt(index);
        }
    }

    private sealed class SupabaseTodoRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("is_completed")]
        public bool IsCompleted { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("due_date")]
        public DateTime? DueDate { get; set; }
    }
}
