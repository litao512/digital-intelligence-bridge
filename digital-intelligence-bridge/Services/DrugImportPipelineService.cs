using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 医保药品导入流水线服务
/// </summary>
public class DrugImportPipelineService : IDrugImportPipelineService
{
    private const int InsertBatchSize = 500;
    private readonly IDrugExcelImportService _excelImportService;
    private readonly IDrugImportRepository _repository;

    public DrugImportPipelineService(
        IDrugExcelImportService excelImportService,
        IDrugImportRepository repository)
    {
        _excelImportService = excelImportService;
        _repository = repository;
    }

    public async Task<DrugImportBatch> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var preview = await _excelImportService.ValidateStructureAsync(filePath, cancellationToken);
        if (!preview.IsValid)
        {
            throw new InvalidOperationException(string.Join("；", preview.Errors));
        }

        var batch = await _repository.CreateBatchAsync(filePath, cancellationToken);
        batch.Stage = "Import";
        batch.Result = "Running";
        batch.ValidatedAt = DateTimeOffset.Now;

        await _repository.ExecuteInImportSessionAsync(async ct =>
        {
            var rawRows = new List<DrugImportRow>(InsertBatchSize);
            var cleanRows = new List<DrugImportRow>(InsertBatchSize);
            var errorRows = new List<DrugImportRow>(InsertBatchSize);

            await foreach (var row in _excelImportService.ReadRowsAsync(filePath, ct))
            {
                row.BatchId = batch.BatchId;
                rawRows.Add(row);
                batch.RawCount++;

                if (IsValidRow(row))
                {
                    cleanRows.Add(row);
                    batch.CleanCount++;
                }
                else
                {
                    errorRows.Add(row);
                    batch.ErrorCount++;
                    batch.LastError = row.ErrorMessage;
                }

                await FlushIfNeededAsync(rawRows, cleanRows, errorRows, ct);
            }

            await FlushAsync(rawRows, cleanRows, errorRows, ct);
            await _repository.MergeBatchAsync(batch.BatchId, ct);
        }, cancellationToken);
        batch.ImportedAt = DateTimeOffset.Now;
        batch.Result = "Succeeded";
        return batch;
    }

    private static bool IsValidRow(DrugImportRow row)
    {
        return !string.IsNullOrWhiteSpace(row.BusinessKey)
               && string.IsNullOrWhiteSpace(row.ErrorCode)
               && string.IsNullOrWhiteSpace(row.ErrorMessage);
    }

    private async Task FlushIfNeededAsync(
        List<DrugImportRow> rawRows,
        List<DrugImportRow> cleanRows,
        List<DrugImportRow> errorRows,
        CancellationToken cancellationToken)
    {
        if (rawRows.Count < InsertBatchSize &&
            cleanRows.Count < InsertBatchSize &&
            errorRows.Count < InsertBatchSize)
        {
            return;
        }

        await FlushAsync(rawRows, cleanRows, errorRows, cancellationToken);
    }

    private async Task FlushAsync(
        List<DrugImportRow> rawRows,
        List<DrugImportRow> cleanRows,
        List<DrugImportRow> errorRows,
        CancellationToken cancellationToken)
    {
        if (rawRows.Count > 0)
        {
            await _repository.InsertRawBatchAsync(rawRows, cancellationToken);
            rawRows.Clear();
        }

        if (cleanRows.Count > 0)
        {
            await _repository.InsertCleanBatchAsync(cleanRows, cancellationToken);
            cleanRows.Clear();
        }

        if (errorRows.Count > 0)
        {
            await _repository.InsertErrorBatchAsync(errorRows, cancellationToken);
            errorRows.Clear();
        }
    }
}
