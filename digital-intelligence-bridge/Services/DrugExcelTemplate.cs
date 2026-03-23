using System.Collections.Generic;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 医保药品固定模板定义
/// </summary>
public sealed class DrugExcelTemplate
{
    public static DrugExcelTemplate Default { get; } = new(
        new[]
        {
            new DrugExcelSheetTemplate(
                "总表（270419）",
                new[]
                {
                    "药品代码", "数据来源", "注册名称", "商品名称", "注册剂型", "剂型", "注册规格", "规格", "包装材质",
                    "最小包装数量", "最小制剂单位", "最小包装单位", "药品企业", "分包装企业名称", "生产企业",
                    "批准文号", "原批准文号", "药品本位码", "上市药品持有人", "市场状态", "医保药品名称",
                    "2025版甲乙类", "医保剂型", "编号", "备注", "曾用码"
                }),
            new DrugExcelSheetTemplate(
                "新增（559）",
                new[]
                {
                    "药品代码", "注册名称", "商品名称", "注册剂型", "剂型", "注册规格", "规格", "包装材质",
                    "最小包装数量", "最小制剂单位", "最小包装单位", "药品企业", "分包装企业名称", "生产企业",
                    "批准文号", "原批准文号", "药品本位码", "上市药品持有人", "市场状态", "医保药品名称",
                    "2025版甲乙类", "医保剂型", "编号", "备注"
                }),
            new DrugExcelSheetTemplate(
                "变更（449）",
                new[]
                {
                    "药品代码", "数据来源", "本期变更", "状态", "注册名称", "商品名称", "注册剂型", "剂型",
                    "注册规格", "规格", "包装材质", "最小包装数量", "最小制剂单位", "最小包装单位", "药品企业",
                    "分包装企业名称", "生产企业", "批准文号", "原批准文号", "药品本位码", "上市药品持有人",
                    "市场状态", "医保药品名称", "2025版甲乙类", "医保剂型", "编号", "备注", "曾用码"
                }),
            new DrugExcelSheetTemplate(
                "关联关系表",
                new[]
                {
                    "药品代码", "goods_id", "曾用码", "曾用goods_id"
                })
        });

    public DrugExcelTemplate(IReadOnlyList<DrugExcelSheetTemplate> sheets)
    {
        Sheets = sheets;
    }

    public IReadOnlyList<DrugExcelSheetTemplate> Sheets { get; }
}

public sealed class DrugExcelSheetTemplate
{
    public DrugExcelSheetTemplate(string name, IReadOnlyList<string> columns)
    {
        Name = name;
        Columns = columns;
    }

    public string Name { get; }

    public IReadOnlyList<string> Columns { get; }
}
