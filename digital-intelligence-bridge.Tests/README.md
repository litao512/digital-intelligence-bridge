# digital-intelligence-bridge.Tests

该目录是历史过渡测试方案（控制台断言 Runner）。

- 建议优先使用标准 xUnit 工程：`digital-intelligence-bridge.UnitTests`
- 本目录保留用于某些环境下的快速自检与兜底回归

运行方式：

```bash
dotnet build digital-intelligence-bridge.Tests/digital-intelligence-bridge.Tests.csproj -c Debug
dotnet digital-intelligence-bridge.Tests/bin/Debug/net10.0/digital-intelligence-bridge.Tests.dll
```
