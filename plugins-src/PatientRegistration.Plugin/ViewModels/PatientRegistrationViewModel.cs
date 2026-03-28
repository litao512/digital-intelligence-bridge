using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PatientRegistration.Plugin.Models;
using PatientRegistration.Plugin.Services;

namespace PatientRegistration.Plugin.ViewModels;

public class PatientRegistrationViewModel : INotifyPropertyChanged
{
    private readonly IPatientRegistrationRepository _repository;
    private readonly IQrPrintService _printService;
    private string _patientName = string.Empty;
    private string _gender = "unknown";
    private DateTime? _birthDate;
    private string _idType = "id_card";
    private string _idNumber = string.Empty;
    private string _contactPhone = string.Empty;
    private string _department = string.Empty;
    private string _doctorName = string.Empty;
    private string _visitTimeRange = string.Empty;
    private string _selectedTreatmentItems = string.Empty;
    private string _notes = string.Empty;
    private string _statusMessage = "请填写患者信息后保存并打印身份二维码";
    private bool _confirmEmptyDiagnosticInfo;
    private bool _isSaving;
    private Guid? _selectedRegistrationId;
    private PatientRegistrationRecord? _selectedRegistration;
    private string _selectedDepartment = string.Empty;
    private string _selectedDoctorId = string.Empty;
    private RegistrationDoctorOption? _selectedDoctor;
    private string _searchPatientKeyword = string.Empty;
    private string _searchIdSuffix = string.Empty;

    public PatientRegistrationViewModel(
        IPatientRegistrationRepository repository,
        IQrPrintService printService)
    {
        _repository = repository;
        _printService = printService;
    }

    public ObservableCollection<PatientRegistrationRecord> RecentRegistrations { get; } = [];
    public ObservableCollection<PatientRegistrationRecord> FilteredRecentRegistrations { get; } = [];
    public ObservableCollection<string> AvailableDepartments { get; } = [];
    public ObservableCollection<RegistrationDoctorOption> AvailableDoctors { get; } = [];
    public ObservableCollection<RegistrationTreatmentItemOption> AvailableTreatmentItems { get; } = [];

    public string SelectedDepartment
    {
        get => _selectedDepartment;
        set
        {
            if (_selectedDepartment == value)
            {
                return;
            }

            _selectedDepartment = value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                Department = value;
            }

            OnPropertyChanged();
        }
    }

    public string SelectedDoctorId
    {
        get => _selectedDoctorId;
        set
        {
            if (_selectedDoctorId == value)
            {
                return;
            }

            _selectedDoctorId = value;
            var doctor = AvailableDoctors.FirstOrDefault(item => item.Id == value);
            if (doctor is not null)
            {
                _selectedDoctor = doctor;
                DoctorName = doctor.Name;
                if (!string.IsNullOrWhiteSpace(doctor.Department))
                {
                    SelectedDepartment = doctor.Department;
                }
            }
            else
            {
                _selectedDoctor = null;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDoctor));
        }
    }

    public RegistrationDoctorOption? SelectedDoctor
    {
        get => _selectedDoctor;
        set
        {
            if (_selectedDoctor == value)
            {
                return;
            }

            _selectedDoctor = value;
            _selectedDoctorId = value?.Id ?? string.Empty;
            if (value is not null)
            {
                DoctorName = value.Name;
                if (!string.IsNullOrWhiteSpace(value.Department))
                {
                    SelectedDepartment = value.Department;
                }
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDoctorId));
        }
    }

    public string SearchPatientKeyword
    {
        get => _searchPatientKeyword;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_searchPatientKeyword == normalized)
            {
                return;
            }

            _searchPatientKeyword = normalized;
            OnPropertyChanged();
            ApplyRegistrationFilter();
        }
    }

    public string SearchIdSuffix
    {
        get => _searchIdSuffix;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_searchIdSuffix == normalized)
            {
                return;
            }

            _searchIdSuffix = normalized;
            OnPropertyChanged();
            ApplyRegistrationFilter();
        }
    }

    public Guid? SelectedRegistrationId
    {
        get => _selectedRegistrationId;
        set
        {
            if (_selectedRegistrationId == value)
            {
                return;
            }

            _selectedRegistrationId = value;
            if (value.HasValue)
            {
                _selectedRegistration = FilteredRecentRegistrations.FirstOrDefault(item => item.RegistrationId == value.Value)
                    ?? RecentRegistrations.FirstOrDefault(item => item.RegistrationId == value.Value);
                OnPropertyChanged(nameof(SelectedRegistration));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanReprintSelected));
        }
    }

    public PatientRegistrationRecord? SelectedRegistration
    {
        get => _selectedRegistration;
        set
        {
            if (_selectedRegistration == value)
            {
                return;
            }

            _selectedRegistration = value;
            _selectedRegistrationId = value?.RegistrationId;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedRegistrationId));
            OnPropertyChanged(nameof(CanReprintSelected));
        }
    }

    public string PatientName
    {
        get => _patientName;
        set
        {
            if (_patientName == value)
            {
                return;
            }

            _patientName = value;
            OnPropertyChanged();
        }
    }

    public string Gender
    {
        get => _gender;
        set
        {
            if (_gender == value)
            {
                return;
            }

            _gender = value;
            OnPropertyChanged();
        }
    }

    public DateTime? BirthDate
    {
        get => _birthDate;
        set
        {
            if (_birthDate == value)
            {
                return;
            }

            _birthDate = value;
            OnPropertyChanged();
        }
    }

    public string IdType
    {
        get => _idType;
        set
        {
            if (_idType == value)
            {
                return;
            }

            _idType = value;
            OnPropertyChanged();
        }
    }

    public string IdNumber
    {
        get => _idNumber;
        set
        {
            if (_idNumber == value)
            {
                return;
            }

            _idNumber = value;
            OnPropertyChanged();
        }
    }

    public string ContactPhone
    {
        get => _contactPhone;
        set
        {
            if (_contactPhone == value)
            {
                return;
            }

            _contactPhone = value;
            OnPropertyChanged();
        }
    }

    public string Department
    {
        get => _department;
        set
        {
            if (_department == value)
            {
                return;
            }

            _department = value;
            OnPropertyChanged();
        }
    }

    public string DoctorName
    {
        get => _doctorName;
        set
        {
            if (_doctorName == value)
            {
                return;
            }

            _doctorName = value;
            OnPropertyChanged();
        }
    }

    public string VisitTimeRange
    {
        get => _visitTimeRange;
        set
        {
            if (_visitTimeRange == value)
            {
                return;
            }

            _visitTimeRange = value;
            OnPropertyChanged();
        }
    }

    public string SelectedTreatmentItems
    {
        get => _selectedTreatmentItems;
        set
        {
            if (_selectedTreatmentItems == value)
            {
                return;
            }

            _selectedTreatmentItems = value;
            OnPropertyChanged();
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes == value)
            {
                return;
            }

            _notes = value;
            OnPropertyChanged();
        }
    }

    public bool ConfirmEmptyDiagnosticInfo
    {
        get => _confirmEmptyDiagnosticInfo;
        set
        {
            if (_confirmEmptyDiagnosticInfo == value)
            {
                return;
            }

            _confirmEmptyDiagnosticInfo = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (_isSaving == value)
            {
                return;
            }

            _isSaving = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanReprintSelected));
        }
    }

    public bool CanSave =>
        !string.IsNullOrWhiteSpace(PatientName)
        && !string.IsNullOrWhiteSpace(IdType)
        && !string.IsNullOrWhiteSpace(IdNumber)
        && BirthDate.HasValue;

    public bool HasDiagnosticInfo =>
        !string.IsNullOrWhiteSpace(Department)
        || !string.IsNullOrWhiteSpace(DoctorName)
        || !string.IsNullOrWhiteSpace(VisitTimeRange)
        || !string.IsNullOrWhiteSpace(SelectedTreatmentItems)
        || AvailableTreatmentItems.Any(item => item.IsSelected);

    public bool CanSubmit => CanSave && !IsSaving;

    public bool CanReprintSelected => SelectedRegistration is not null && !IsSaving;

    public async Task<bool> SaveAsync(bool printRequested, CancellationToken cancellationToken = default)
    {
        if (!CanSave)
        {
            StatusMessage = "患者基础信息未填写完整，请补全后再保存";
            return false;
        }

        if (!HasDiagnosticInfo && !ConfirmEmptyDiagnosticInfo)
        {
            StatusMessage = "当前未填写诊疗信息，请勾选确认后继续保存";
            return false;
        }

        IsSaving = true;
        try
        {
            var draft = BuildDraft();
            var saveResult = await _repository.SaveAsync(draft, cancellationToken);

            if (printRequested)
            {
                var payload = new PatientRegistrationPrintPayload
                {
                    RegistrationId = saveResult.RegistrationId,
                    PatientName = draft.PatientName,
                    IdType = draft.IdType,
                    IdNumberMasked = MaskIdNumber(draft.IdNumber),
                    QrCodeContent = saveResult.QrCodeContent,
                    Notes = draft.Notes ?? string.Empty
                };

                await _printService.PrintAsync(payload, cancellationToken);
                StatusMessage = "登记已保存，已触发二维码打印流程";
            }
            else
            {
                StatusMessage = "登记已保存，可在记录列表补打二维码";
            }

            await LoadRecentRegistrationsAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task LoadRecentRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetRecentRegistrationsAsync(cancellationToken: cancellationToken);

        RecentRegistrations.Clear();
        foreach (var row in rows)
        {
            RecentRegistrations.Add(row);
        }

        ApplyRegistrationFilter();
    }

    public async Task<bool> ReprintSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (!SelectedRegistrationId.HasValue)
        {
            StatusMessage = "请先选择要补打的登记记录";
            return false;
        }

        var selected = SelectedRegistration ?? RecentRegistrations.FirstOrDefault(item => item.RegistrationId == SelectedRegistrationId.Value);
        if (selected is null)
        {
            StatusMessage = "未找到选中的登记记录，请先刷新列表";
            return false;
        }

        IsSaving = true;
        try
        {
            await _printService.PrintAsync(new PatientRegistrationPrintPayload
            {
                RegistrationId = selected.RegistrationId,
                PatientName = selected.PatientName,
                IdType = selected.IdType,
                IdNumberMasked = selected.IdNumberMasked,
                QrCodeContent = selected.QrCodeContent,
                Notes = selected.Notes
            }, cancellationToken);

            StatusMessage = "二维码已补打";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"补打失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task LoadRegistrationOptionsAsync(CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetRegistrationOptionsAsync(cancellationToken);

        AvailableDepartments.Clear();
        foreach (var department in data.Departments)
        {
            AvailableDepartments.Add(department);
        }

        AvailableDoctors.Clear();
        foreach (var doctor in data.Doctors)
        {
            AvailableDoctors.Add(doctor);
        }

        AvailableTreatmentItems.Clear();
        foreach (var item in data.TreatmentItems)
        {
            AvailableTreatmentItems.Add(new RegistrationTreatmentItemOption
            {
                Id = item.Id,
                Name = item.Name,
                IsSelected = false
            });
        }
    }

    public void ClearRegistrationFilter()
    {
        SearchPatientKeyword = string.Empty;
        SearchIdSuffix = string.Empty;
        ApplyRegistrationFilter();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private PatientRegistrationDraft BuildDraft()
    {
        var selectedOptionItems = AvailableTreatmentItems
            .Where(item => item.IsSelected)
            .Select(item => item.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var textItems = SelectedTreatmentItems
            .Split(',', '，', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = selectedOptionItems.Length > 0 ? selectedOptionItems : textItems;

        return new PatientRegistrationDraft
        {
            PatientName = PatientName.Trim(),
            Gender = Gender.Trim(),
            BirthDate = BirthDate!.Value,
            IdType = IdType.Trim(),
            IdNumber = IdNumber.Trim(),
            ContactPhone = ContactPhone.Trim(),
            Department = Department.Trim(),
            DoctorName = DoctorName.Trim(),
            VisitTimeRange = VisitTimeRange.Trim(),
            PlannedTreatmentItemNames = items,
            Notes = Notes.Trim()
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is nameof(PatientName)
            or nameof(IdType)
            or nameof(IdNumber)
            or nameof(BirthDate)
            or nameof(Department)
            or nameof(DoctorName)
            or nameof(VisitTimeRange)
            or nameof(SelectedTreatmentItems))
        {
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(HasDiagnosticInfo));
            OnPropertyChanged(nameof(CanSubmit));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string MaskIdNumber(string idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber) || idNumber.Length <= 4)
        {
            return idNumber;
        }

        return new string('*', idNumber.Length - 4) + idNumber[^4..];
    }

    private void ApplyRegistrationFilter()
    {
        IEnumerable<PatientRegistrationRecord> query = RecentRegistrations;

        if (!string.IsNullOrWhiteSpace(SearchPatientKeyword))
        {
            query = query.Where(item =>
                !string.IsNullOrWhiteSpace(item.PatientName)
                && item.PatientName.Contains(SearchPatientKeyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchIdSuffix))
        {
            query = query.Where(item =>
                !string.IsNullOrWhiteSpace(item.IdNumberMasked)
                && item.IdNumberMasked.EndsWith(SearchIdSuffix, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.ToList();

        FilteredRecentRegistrations.Clear();
        foreach (var row in filtered)
        {
            FilteredRecentRegistrations.Add(row);
        }

        if (FilteredRecentRegistrations.Count == 0)
        {
            SelectedRegistration = null;
            return;
        }

        if (SelectedRegistration is not null
            && FilteredRecentRegistrations.Any(item => item.RegistrationId == SelectedRegistration.RegistrationId))
        {
            SelectedRegistration = FilteredRecentRegistrations.First(item => item.RegistrationId == SelectedRegistration.RegistrationId);
            return;
        }

        if (SelectedRegistrationId.HasValue)
        {
            var selectedById = FilteredRecentRegistrations.FirstOrDefault(item => item.RegistrationId == SelectedRegistrationId.Value);
            if (selectedById is not null)
            {
                SelectedRegistration = selectedById;
                return;
            }
        }

        SelectedRegistration = FilteredRecentRegistrations[0];
    }
}
