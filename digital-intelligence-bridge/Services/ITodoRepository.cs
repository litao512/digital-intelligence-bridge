using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Services;

public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> AddAsync(TodoItem item, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(TodoItem item, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default);
}
