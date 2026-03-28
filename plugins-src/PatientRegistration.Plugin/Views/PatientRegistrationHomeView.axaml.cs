using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PatientRegistration.Plugin.ViewModels;

namespace PatientRegistration.Plugin.Views;

public partial class PatientRegistrationHomeView : UserControl
{
    public PatientRegistrationHomeView()
        : this(new PatientRegistrationViewModel(new DesignTimeRepository(), new DesignTimePrintService()))
    {
    }

    public PatientRegistrationHomeView(PatientRegistrationViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private PatientRegistrationViewModel? ViewModel => DataContext as PatientRegistrationViewModel;

    private async void OnSaveAndPrintClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.SaveAsync(printRequested: true);
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.SaveAsync(printRequested: false);
    }

    private async void OnRefreshRecordsClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.LoadRecentRegistrationsAsync();
    }

    private async void OnReprintClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReprintSelectedAsync();
    }

    private void OnClearFilterClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClearRegistrationFilter();
    }

    private async void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.LoadRegistrationOptionsAsync();
        await ViewModel.LoadRecentRegistrationsAsync();
    }

    private sealed class DesignTimeRepository : Services.IPatientRegistrationRepository
    {
        public Task<Models.PatientRegistrationSaveResult> SaveAsync(
            Models.PatientRegistrationDraft draft,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Models.PatientRegistrationSaveResult
            {
                PatientId = Guid.NewGuid(),
                RegistrationId = Guid.NewGuid(),
                QrCodeId = Guid.NewGuid(),
                QrCodeContent = "DESIGN-TIME-QR"
            });
        }

        public Task<IReadOnlyList<Models.PatientRegistrationRecord>> GetRecentRegistrationsAsync(int limit = 20, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Models.PatientRegistrationRecord> rows =
            [
                new Models.PatientRegistrationRecord
                {
                    RegistrationId = Guid.NewGuid(),
                    PatientName = "示例患者",
                    Department = "康复科",
                    CreatedAt = DateTimeOffset.Now,
                    QrCodeContent = "DESIGN-TIME-QR",
                    IdType = "id_card",
                    IdNumberMasked = "**************1234",
                    Notes = "设计态备注"
                }
            ];

            return Task.FromResult(rows);
        }

        public Task<Models.PatientRegistrationOptionData> GetRegistrationOptionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Models.PatientRegistrationOptionData
            {
                Departments = ["康复科", "理疗科"],
                Doctors =
                [
                    new Models.RegistrationDoctorOption { Id = "doctor-1", Name = "张医生", Department = "康复科" },
                    new Models.RegistrationDoctorOption { Id = "doctor-2", Name = "李医生", Department = "理疗科" }
                ],
                TreatmentItems =
                [
                    new Models.RegistrationTreatmentItemOption { Id = "item-1", Name = "理疗" },
                    new Models.RegistrationTreatmentItemOption { Id = "item-2", Name = "艾灸" }
                ]
            });
        }
    }

    private sealed class DesignTimePrintService : Services.IQrPrintService
    {
        public Task PrintAsync(Models.PatientRegistrationPrintPayload payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
