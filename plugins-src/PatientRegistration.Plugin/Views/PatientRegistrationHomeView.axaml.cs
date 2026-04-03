using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PatientRegistration.Plugin.ViewModels;

namespace PatientRegistration.Plugin.Views;

public partial class PatientRegistrationHomeView : UserControl
{
    private static readonly string[] EnterFocusOrder =
    [
        "PatientNameBox",
        "IdTypeBox",
        "IdNumberBox",
        "GenderBox",
        "BirthDatePicker",
        "ContactPhoneBox",
        "DepartmentBox",
        "DoctorBox",
        "VisitTimeRangeBox"
    ];

    public PatientRegistrationHomeView()
        : this(new PatientRegistrationViewModel(new DesignTimeRepository(), new DesignTimePrintService()))
    {
    }

    public PatientRegistrationHomeView(PatientRegistrationViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        KeyDown += OnViewKeyDown;
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

    private async void OnSaveAndNextClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.SaveForNextAsync(printRequested: true);
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

        Focus();
        await ViewModel.LoadRegistrationOptionsAsync();
        await ViewModel.LoadRecentRegistrationsAsync();
    }

    private async void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            if (e.Source is TextBox textBox && textBox.AcceptsReturn)
            {
                return;
            }

            var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focusedElement is not Control focusedControl)
            {
                return;
            }

            if (MoveFocusToNextControl(focusedControl))
            {
                e.Handled = true;
            }
            return;
        }

        if (e.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        if (e.Key == Key.S)
        {
            await ViewModel.SaveAsync(printRequested: false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.P)
        {
            await ViewModel.SaveAsync(printRequested: true);
            e.Handled = true;
        }
    }

    private bool MoveFocusToNextControl(Control current)
    {
        var currentIndex = Array.FindIndex(EnterFocusOrder, name =>
        {
            var control = this.FindControl<Control>(name);
            return ReferenceEquals(control, current);
        });

        if (currentIndex < 0)
        {
            return false;
        }

        for (var i = currentIndex + 1; i < EnterFocusOrder.Length; i++)
        {
            var next = this.FindControl<Control>(EnterFocusOrder[i]);
            if (next is null || !next.IsEnabled || !next.IsVisible)
            {
                continue;
            }

            return next.Focus();
        }

        return false;
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
