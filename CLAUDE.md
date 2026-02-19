# CLAUDE.md

�o��-?件为 Claude Code (claude.ai/code) 提�>�o�此代码�"中工�o�s"�O?导�?,

## 项�>��,述

�T�~��?个面�' WinForms �?�'�?.�s" Avalonia UI �.��-��.T�<项�>� - �?个�.�Sz�<项管�?�"�"��O�"示�? MVVM 模式�?��.�据�'�s�'O XAML �f�?�?,使�"� Semi.Avalonia 主�~�'O CommunityToolkit.Mvvm 提�> MVVM �"��O��?,

## �z"建�'O运�O�'�令

```bash
# �>�.�项�>��>��.
cd digital-intelligence-bridge

# �z"建项�>�
dotnet build

# 运�O�"�"�
dotnet run

# Release 模式运�O
dotnet run --configuration Release

# �?�'�f��?�载
dotnet watch run --hot-reload
```

## �z��z"�,述

### MVVM 模式
项�>�遵循严格�s" MVVM �z��z"�s
- **Models**�s�.�据�z�"�^�, `TodoItem.cs`�?- �z�Z� `ObservableObject` 并使�"� `[ObservableProperty]` �?��?��?��S��"Y�^��z�?��~�>��?s�Y�
- **ViewModels**�s�s�S��?��'�'O�S��?��^�, `MainWindowViewModel.cs`�?- 继�?��?� `ViewModelBase`�O�?O `ViewModelBase` 继�?��?� `ObservableObject`
- **Views**�sXAML UI �s�?�^�, `MainWindow.axaml`�?- 代码�Z置�-?件�O.含�o?�'�?��'

### ViewLocator 模式
`ViewLocator.cs` �z�Z� `IDataTemplate` �Z�口�O�?s�?�'�名约�s�?��S��~��" ViewModel �^� View�s
- ViewModel: `MainWindowViewModel` �?' View: `MainWindow`
- 使�"�反�"�s位对�"�s"�?�>�类�z<
- �o� `App.axaml` 中注�?O为�"�"�级�.�据模板

### 核�f�?�z�
- **Avalonia UI 11.3.12**�s跨平台 UI �?�z�
- **CommunityToolkit.Mvvm 8.2.1**�sMVVM 样板代码源�"Y�^��T��^`[ObservableProperty]`�?�`[RelayCommand]`�?
- **Semi.Avalonia 11.3.7**�s�Z�代 UI 主�~�^�o� `App.axaml` 中�.�置�?

### �.�据�'�s约�s
- �?记 `[ObservableProperty]` �s"�?��?��s�?��S��"Y�^�可�,�Y�z�?�
- �'�令使�"� `[RelayCommand]` �?��?� - �-��.名�, `AddTodo` �s�"Y�^� `AddTodoCommand`
- �>?�^使�"� `ObservableCollection<T>` �z�Z��?��S� UI �>��-�
- �o��?�>��S设置 `x:DataType` �"��Z�-�'�'�s�^�o� `.csproj` 中�~认启�"��?

### 系�Y�?~�>~�z�Z�
�"�"��"��O��o?小�O-�^��?~�>~�s
- �o� `App.axaml` 中�?s�? `<TrayIcon.Icons>` �.�置
- �o� `App.axaml.cs` 中�?s�? `ShutdownMode.OnExplicitShutdown` �z�Z��?��'
- �<��^��-口�.��-��<件使�.��s��-��?O�z�??�?�
- �??�?��-�式�s�?~�>~�>��?"�??�?�"�o�.�^-�~�式�f�"� `ExitApplication()` �-��.

## �?�要�z�Z��?�S,

- **�O证**�s�o� `App.axaml.cs` 中禁�"� Avalonia �s" DataAnnotations �O证�O以避�.��Z CommunityToolkit �O证�?�突
- **主�~**�s使�"� `SemiTheme`�^�~认�.�?�模式�?�O�o� `App.axaml` 样式中注�?O
- **�-�"**�s�o� `Program.cs` 中�?s�? `.WithInterFont()` �.�置 Inter �-�"
- **�-�'�'�s**�s�o�项�>��-?件中�~认启�"��>�o?要�o��?�>��S设置 `x:DataType`

