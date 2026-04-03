using PatientRegistration.Plugin.Models;
using PatientRegistration.Plugin.Services;
using PatientRegistration.Plugin.ViewModels;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PatientRegistrationViewModelTests
{
    [Fact]
    public async Task SaveAsync_ShouldBlock_WhenRequiredFieldsMissing()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = new PatientRegistrationViewModel(repository, printer);

        var result = await viewModel.SaveAsync(printRequested: true);

        Assert.False(result);
        Assert.Contains("基础信息", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("姓名不能为空", viewModel.PatientNameError);
        Assert.Equal("证件号不能为空", viewModel.IdNumberError);
        Assert.Equal("出生日期不能为空", viewModel.BirthDateError);
        Assert.False(repository.SaveCalled);
    }

    [Fact]
    public void CanSubmit_ShouldRemainTrue_WhenNotSavingEvenIfRequiredFieldsMissing()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = new PatientRegistrationViewModel(repository, printer);

        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void IdCardInput_ShouldAutoFillBirthDateAndGender_WhenValid()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = new PatientRegistrationViewModel(repository, printer)
        {
            IdType = "id_card"
        };

        viewModel.IdNumber = "110105199002142428";

        Assert.Equal("female", viewModel.Gender);
        Assert.NotNull(viewModel.BirthDate);
        Assert.Equal(new DateTime(1990, 2, 14), viewModel.BirthDate!.Value.DateTime.Date);
    }

    [Fact]
    public async Task SaveAsync_ShouldAllow_WhenDiagnosticInfoEmptyWithoutConfirmation()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);

        viewModel.Department = string.Empty;
        viewModel.DoctorName = string.Empty;
        viewModel.VisitTimeRange = string.Empty;
        viewModel.SelectedTreatmentItems = string.Empty;
        viewModel.ConfirmEmptyDiagnosticInfo = false;

        var result = await viewModel.SaveAsync(printRequested: false);

        Assert.True(result);
        Assert.True(repository.SaveCalled);
        Assert.Contains("可在扫码时现场新增诊疗项目", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_ShouldBlock_WhenIdCardNumberInvalid()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);
        viewModel.IdType = "id_card";
        viewModel.IdNumber = "123456";

        var result = await viewModel.SaveAsync(printRequested: false);

        Assert.False(result);
        Assert.Contains("身份证号格式不正确", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(repository.SaveCalled);
    }

    [Fact]
    public async Task SaveAsync_ShouldBlock_WhenPassportNumberInvalid()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);
        viewModel.IdType = "passport";
        viewModel.IdNumber = "A12";

        var result = await viewModel.SaveAsync(printRequested: false);

        Assert.False(result);
        Assert.Contains("护照号格式不正确", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(repository.SaveCalled);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistAndPrint_WhenPrintRequested()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);

        var result = await viewModel.SaveAsync(printRequested: true);

        Assert.True(result);
        Assert.True(repository.SaveCalled);
        Assert.True(printer.PrintCalled);
        Assert.Equal("测试备注", printer.LastPayload?.Notes);
        Assert.Contains("已触发二维码打印", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.LastSavedRegistrationCode));
    }

    [Fact]
    public async Task LoadRecentRegistrationsAsync_ShouldPopulateRecords()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);

        await viewModel.LoadRecentRegistrationsAsync();

        Assert.NotEmpty(viewModel.RecentRegistrations);
        Assert.Equal("测试患者", viewModel.RecentRegistrations[0].PatientName);
    }

    [Fact]
    public async Task ReprintSelectedAsync_ShouldPrintSelectedRegistration()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);
        await viewModel.LoadRecentRegistrationsAsync();
        viewModel.SelectedRegistrationId = viewModel.RecentRegistrations[0].RegistrationId;

        var result = await viewModel.ReprintSelectedAsync();

        Assert.True(result);
        Assert.True(printer.PrintCalled);
        Assert.Equal("补打备注", printer.LastPayload?.Notes);
        Assert.Contains("已补打", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRegistrationOptionsAsync_ShouldPopulateDropdownOptions()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);

        await viewModel.LoadRegistrationOptionsAsync();

        Assert.NotEmpty(viewModel.AvailableDepartments);
        Assert.NotEmpty(viewModel.AvailableDoctors);
        Assert.NotEmpty(viewModel.AvailableTreatmentItems);
    }

    [Fact]
    public async Task SaveAsync_ShouldUseSelectedOptions_WhenDropdownSelectionsProvided()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);

        await viewModel.LoadRegistrationOptionsAsync();
        viewModel.SelectedDepartment = "康复科";
        viewModel.SelectedDoctorId = "doctor-1";
        viewModel.AvailableTreatmentItems[0].IsSelected = true;
        viewModel.AvailableTreatmentItems[1].IsSelected = true;

        var result = await viewModel.SaveAsync(printRequested: false);

        Assert.True(result);
        Assert.Equal("康复科", repository.LastSavedDraft?.Department);
        Assert.Equal("张医生", repository.LastSavedDraft?.DoctorName);
        Assert.Contains("理疗", repository.LastSavedDraft?.PlannedTreatmentItemNames ?? []);
        Assert.Contains("艾灸", repository.LastSavedDraft?.PlannedTreatmentItemNames ?? []);
    }

    [Fact]
    public async Task SaveForNextAsync_ShouldResetIdentityFields_AndKeepDiagnosticDefaults()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);

        await viewModel.LoadRegistrationOptionsAsync();
        viewModel.SelectedDepartment = "康复科";
        viewModel.SelectedDoctorId = "doctor-1";
        viewModel.VisitTimeRange = "上午";
        viewModel.Notes = "登记备注";

        var result = await viewModel.SaveForNextAsync(printRequested: false);

        Assert.True(result);
        Assert.Equal(string.Empty, viewModel.PatientName);
        Assert.Equal(string.Empty, viewModel.IdNumber);
        Assert.Null(viewModel.BirthDate);
        Assert.Equal(string.Empty, viewModel.ContactPhone);
        Assert.Equal(string.Empty, viewModel.Notes);
        Assert.Equal("康复科", viewModel.SelectedDepartment);
        Assert.Equal("doctor-1", viewModel.SelectedDoctorId);
        Assert.Equal("上午", viewModel.VisitTimeRange);
        Assert.True(viewModel.ConfirmEmptyDiagnosticInfo);
    }

    [Fact]
    public async Task LoadRegistrationOptionsAsync_ShouldFilterDoctorsBySelectedDepartment()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);

        await viewModel.LoadRegistrationOptionsAsync();
        viewModel.SelectedDepartment = "理疗科";

        Assert.Single(viewModel.FilteredDoctors);
        Assert.Equal("doctor-2", viewModel.FilteredDoctors[0].Id);
    }

    [Fact]
    public async Task ApplyRegistrationFilter_ShouldFilterByPatientAndIdSuffix()
    {
        var repository = new FakeRepository();
        var printer = new FakePrinter();
        var viewModel = BuildValidViewModel(repository, printer);
        await viewModel.LoadRecentRegistrationsAsync();

        viewModel.SearchPatientKeyword = "测试";
        Assert.Single(viewModel.FilteredRecentRegistrations);

        viewModel.SearchPatientKeyword = string.Empty;
        viewModel.SearchIdSuffix = "5678";
        Assert.Single(viewModel.FilteredRecentRegistrations);
        Assert.Equal("另一个患者", viewModel.FilteredRecentRegistrations[0].PatientName);
    }

    private static PatientRegistrationViewModel BuildValidViewModel(
        IPatientRegistrationRepository repository,
        IQrPrintService printer)
    {
        var viewModel = new PatientRegistrationViewModel(repository, printer)
        {
            PatientName = "测试患者",
            Gender = "female",
            BirthDate = new DateTimeOffset(new DateTime(1990, 1, 1)),
            IdType = "id_card",
            IdNumber = "510101199001011234",
            ContactPhone = string.Empty,
            Department = "康复科",
            DoctorName = "张医生",
            VisitTimeRange = "2026-03-27 09:00-10:00",
            SelectedTreatmentItems = "理疗,艾灸",
            Notes = "测试备注",
            ConfirmEmptyDiagnosticInfo = true
        };

        return viewModel;
    }

    private sealed class FakeRepository : IPatientRegistrationRepository
    {
        public bool SaveCalled { get; private set; }
        public PatientRegistrationDraft? LastSavedDraft { get; private set; }

        public Task<PatientRegistrationSaveResult> SaveAsync(PatientRegistrationDraft draft, CancellationToken cancellationToken = default)
        {
            SaveCalled = true;
            LastSavedDraft = draft;
            return Task.FromResult(new PatientRegistrationSaveResult
            {
                PatientId = Guid.NewGuid(),
                RegistrationId = Guid.NewGuid(),
                QrCodeId = Guid.NewGuid(),
                QrCodeContent = "REG-TEST-001"
            });
        }

        public Task<IReadOnlyList<PatientRegistrationRecord>> GetRecentRegistrationsAsync(int limit = 20, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PatientRegistrationRecord> rows =
            [
                new PatientRegistrationRecord
                {
                    RegistrationId = Guid.NewGuid(),
                    PatientName = "测试患者",
                    Department = "康复科",
                    CreatedAt = DateTimeOffset.Now,
                    QrCodeContent = "REG-TEST-001",
                    IdType = "id_card",
                    IdNumberMasked = "**************1234",
                    Notes = "补打备注"
                },
                new PatientRegistrationRecord
                {
                    RegistrationId = Guid.NewGuid(),
                    PatientName = "另一个患者",
                    Department = "理疗科",
                    CreatedAt = DateTimeOffset.Now.AddMinutes(-10),
                    QrCodeContent = "REG-TEST-002",
                    IdType = "id_card",
                    IdNumberMasked = "**************5678",
                    Notes = "次要备注"
                }
            ];
            return Task.FromResult(rows);
        }

        public Task<PatientRegistrationOptionData> GetRegistrationOptionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PatientRegistrationOptionData
            {
                Departments = ["康复科", "理疗科"],
                Doctors =
                [
                    new RegistrationDoctorOption { Id = "doctor-1", Name = "张医生", Department = "康复科" },
                    new RegistrationDoctorOption { Id = "doctor-2", Name = "李医生", Department = "理疗科" }
                ],
                TreatmentItems =
                [
                    new RegistrationTreatmentItemOption { Id = "item-1", Name = "理疗" },
                    new RegistrationTreatmentItemOption { Id = "item-2", Name = "艾灸" }
                ]
            });
        }
    }

    private sealed class FakePrinter : IQrPrintService
    {
        public bool PrintCalled { get; private set; }
        public PatientRegistrationPrintPayload? LastPayload { get; private set; }

        public Task PrintAsync(PatientRegistrationPrintPayload payload, CancellationToken cancellationToken = default)
        {
            PrintCalled = true;
            LastPayload = payload;
            return Task.CompletedTask;
        }
    }
}
