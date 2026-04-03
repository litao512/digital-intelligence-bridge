using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PatientRegistration.Plugin.Utils;
using PatientRegistration.Plugin.Models;
using PatientRegistration.Plugin.Services;

namespace PatientRegistration.Plugin.ViewModels;

public class PatientRegistrationViewModel : INotifyPropertyChanged
{
    private readonly IPatientRegistrationRepository _repository;
    private readonly IQrPrintService _printService;
    private string _patientName = string.Empty;
    private string _gender = "unknown";
    private DateTimeOffset? _birthDate;
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
    private string _lastSavedRegistrationCode = string.Empty;
    private string _patientNameError = string.Empty;
    private string _idTypeError = string.Empty;
    private string _idNumberError = string.Empty;
    private string _birthDateError = string.Empty;
    private bool _isAutoFillingFromIdCard;
    private bool _isGenderManuallyEdited;
    private bool _isBirthDateManuallyEdited;

    public PatientRegistrationViewModel(
        IPatientRegistrationRepository repository,
        IQrPrintService printService)
    {
        _repository = repository;
        _printService = printService;
        InitializeBasicOptions();
    }

    public ObservableCollection<PatientRegistrationRecord> RecentRegistrations { get; } = [];
    public ObservableCollection<PatientRegistrationRecord> FilteredRecentRegistrations { get; } = [];
    public ObservableCollection<string> AvailableDepartments { get; } = [];
    public ObservableCollection<RegistrationDoctorOption> AvailableDoctors { get; } = [];
    public ObservableCollection<RegistrationDoctorOption> FilteredDoctors { get; } = [];
    public ObservableCollection<RegistrationTreatmentItemOption> AvailableTreatmentItems { get; } = [];
    public ObservableCollection<RegistrationBasicOption> GenderOptions { get; } = [];
    public ObservableCollection<RegistrationBasicOption> IdTypeOptions { get; } = [];

    public string PatientNameError
    {
        get => _patientNameError;
        private set
        {
            if (_patientNameError == value)
            {
                return;
            }

            _patientNameError = value;
            OnPropertyChanged();
        }
    }

    public string IdTypeError
    {
        get => _idTypeError;
        private set
        {
            if (_idTypeError == value)
            {
                return;
            }

            _idTypeError = value;
            OnPropertyChanged();
        }
    }

    public string IdNumberError
    {
        get => _idNumberError;
        private set
        {
            if (_idNumberError == value)
            {
                return;
            }

            _idNumberError = value;
            OnPropertyChanged();
        }
    }

    public string BirthDateError
    {
        get => _birthDateError;
        private set
        {
            if (_birthDateError == value)
            {
                return;
            }

            _birthDateError = value;
            OnPropertyChanged();
        }
    }

    public string LastSavedRegistrationCode
    {
        get => _lastSavedRegistrationCode;
        private set
        {
            if (_lastSavedRegistrationCode == value)
            {
                return;
            }

            _lastSavedRegistrationCode = value;
            OnPropertyChanged();
        }
    }

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

            ApplyDoctorFilter();
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
            var doctor = FilteredDoctors.FirstOrDefault(item => item.Id == value)
                ?? AvailableDoctors.FirstOrDefault(item => item.Id == value);
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
            PatientNameError = string.Empty;
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
            if (!_isAutoFillingFromIdCard)
            {
                _isGenderManuallyEdited = true;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedGenderOption));
        }
    }

    public DateTimeOffset? BirthDate
    {
        get => _birthDate;
        set
        {
            if (_birthDate == value)
            {
                return;
            }

            _birthDate = value;
            if (!_isAutoFillingFromIdCard)
            {
                _isBirthDateManuallyEdited = true;
            }
            BirthDateError = string.Empty;
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
            IdTypeError = string.Empty;
            if (!string.Equals(_idType, "id_card", StringComparison.OrdinalIgnoreCase))
            {
                _isGenderManuallyEdited = false;
                _isBirthDateManuallyEdited = false;
            }
            else
            {
                TryAutofillIdentityFromIdCard();
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedIdTypeOption));
        }
    }

    public string IdNumber
    {
        get => _idNumber;
        set
        {
            var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
            if (_idNumber == normalized)
            {
                return;
            }

            _idNumber = normalized;
            IdNumberError = string.Empty;
            TryAutofillIdentityFromIdCard();
            OnPropertyChanged();
        }
    }

    public RegistrationBasicOption? SelectedGenderOption
    {
        get => GenderOptions.FirstOrDefault(item => item.Value == Gender);
        set
        {
            if (value is null || Gender == value.Value)
            {
                return;
            }

            Gender = value.Value;
            OnPropertyChanged();
        }
    }

    public RegistrationBasicOption? SelectedIdTypeOption
    {
        get => IdTypeOptions.FirstOrDefault(item => item.Value == IdType);
        set
        {
            if (value is null || IdType == value.Value)
            {
                return;
            }

            IdType = value.Value;
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

    public bool CanSubmit => !IsSaving;

    public bool CanReprintSelected => SelectedRegistration is not null && !IsSaving;

    public async Task<bool> SaveAsync(bool printRequested, CancellationToken cancellationToken = default)
    {
        if (!ValidateRequiredFields())
        {
            StatusMessage = "患者基础信息未填写完整，请补全后再保存";
            return false;
        }

        if (!ValidateIdNumber())
        {
            return false;
        }

        IsSaving = true;
        try
        {
            var draft = BuildDraft();
            var saveResult = await _repository.SaveAsync(draft, cancellationToken);
            LastSavedRegistrationCode = RegistrationCodeFormatter.Format(saveResult.RegistrationId);

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

                try
                {
                    await _printService.PrintAsync(payload, cancellationToken);
                    StatusMessage = "登记已保存，已触发二维码打印流程";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"登记已保存，但打印失败：{ex.Message}。可在记录列表补打二维码。";
                }
            }
            else
            {
                StatusMessage = "登记已保存，可在记录列表补打二维码";
            }

            if (!HasDiagnosticInfo)
            {
                StatusMessage += "；当前未填写诊疗信息，医生可在扫码时现场新增诊疗项目";
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

    public async Task<bool> SaveForNextAsync(bool printRequested, CancellationToken cancellationToken = default)
    {
        var success = await SaveAsync(printRequested, cancellationToken);
        if (!success)
        {
            return false;
        }

        ResetForNextRegistration();
        StatusMessage = "登记已保存，可继续登记下一位患者";
        return true;
    }

    public async Task LoadRecentRegistrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _repository.GetRecentRegistrationsAsync(cancellationToken: cancellationToken);

            RecentRegistrations.Clear();
            foreach (var row in rows)
            {
                RecentRegistrations.Add(row);
            }

            ApplyRegistrationFilter();
        }
        catch (Exception ex)
        {
            RecentRegistrations.Clear();
            FilteredRecentRegistrations.Clear();
            SelectedRegistration = null;
            StatusMessage = $"登记记录加载失败：{ex.Message}";
        }
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
        try
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
            ApplyDoctorFilter();

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
        catch (Exception ex)
        {
            AvailableDepartments.Clear();
            AvailableDoctors.Clear();
            AvailableTreatmentItems.Clear();
            StatusMessage = $"登记选项加载失败：{ex.Message}。可先手工录入后保存。";
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
            BirthDate = BirthDate!.Value.Date,
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
            or nameof(Gender)
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

    private void InitializeBasicOptions()
    {
        GenderOptions.Clear();
        GenderOptions.Add(new RegistrationBasicOption { Value = "male", Label = "男" });
        GenderOptions.Add(new RegistrationBasicOption { Value = "female", Label = "女" });
        GenderOptions.Add(new RegistrationBasicOption { Value = "unknown", Label = "未知" });

        IdTypeOptions.Clear();
        IdTypeOptions.Add(new RegistrationBasicOption { Value = "id_card", Label = "身份证" });
        IdTypeOptions.Add(new RegistrationBasicOption { Value = "passport", Label = "护照" });
        IdTypeOptions.Add(new RegistrationBasicOption { Value = "other", Label = "其他证件" });
    }

    private bool ValidateRequiredFields()
    {
        PatientNameError = string.Empty;
        IdTypeError = string.Empty;
        IdNumberError = string.Empty;
        BirthDateError = string.Empty;

        var valid = true;
        if (string.IsNullOrWhiteSpace(PatientName))
        {
            PatientNameError = "姓名不能为空";
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(IdType))
        {
            IdTypeError = "请选择证件类型";
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(IdNumber))
        {
            IdNumberError = "证件号不能为空";
            valid = false;
        }

        if (!BirthDate.HasValue)
        {
            BirthDateError = "出生日期不能为空";
            valid = false;
        }

        return valid;
    }

    private void TryAutofillIdentityFromIdCard()
    {
        if (!string.Equals(IdType, "id_card", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var idNumber = IdNumber.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(idNumber, @"^\d{17}[\dX]$", RegexOptions.CultureInvariant))
        {
            return;
        }

        if (!DateTime.TryParseExact(
                idNumber.Substring(6, 8),
                "yyyyMMdd",
                null,
                System.Globalization.DateTimeStyles.None,
                out var birthDate))
        {
            return;
        }

        _isAutoFillingFromIdCard = true;
        try
        {
            if (!_isBirthDateManuallyEdited)
            {
                BirthDate = new DateTimeOffset(birthDate);
            }

            if (!_isGenderManuallyEdited)
            {
                var sequenceCode = idNumber[16] - '0';
                Gender = sequenceCode % 2 == 0 ? "female" : "male";
            }
        }
        finally
        {
            _isAutoFillingFromIdCard = false;
        }
    }

    private void ResetForNextRegistration()
    {
        PatientName = string.Empty;
        Gender = "unknown";
        BirthDate = null;
        IdType = "id_card";
        IdNumber = string.Empty;
        ContactPhone = string.Empty;
        Notes = string.Empty;
        _isGenderManuallyEdited = false;
        _isBirthDateManuallyEdited = false;
        PatientNameError = string.Empty;
        IdTypeError = string.Empty;
        IdNumberError = string.Empty;
        BirthDateError = string.Empty;
    }

    private void ApplyDoctorFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SelectedDepartment)
            ? AvailableDoctors
            : new ObservableCollection<RegistrationDoctorOption>(
                AvailableDoctors.Where(item =>
                    string.Equals(item.Department, SelectedDepartment, StringComparison.OrdinalIgnoreCase)));

        FilteredDoctors.Clear();
        foreach (var doctor in filtered)
        {
            FilteredDoctors.Add(doctor);
        }

        if (SelectedDoctor is null)
        {
            return;
        }

        if (FilteredDoctors.Any(item => item.Id == SelectedDoctor.Id))
        {
            return;
        }

        SelectedDoctor = null;
        DoctorName = string.Empty;
    }

    private bool ValidateIdNumber()
    {
        var idNumber = IdNumber.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(idNumber))
        {
            StatusMessage = "证件号不能为空，请检查后再保存";
            IdNumberError = "证件号不能为空";
            return false;
        }

        return IdType switch
        {
            "id_card" => ValidateByRegex(idNumber, @"^(?:\d{15}|\d{17}[\dX])$", "身份证号格式不正确，请输入15位或18位身份证号"),
            "passport" => ValidateByRegex(idNumber, @"^[A-Z0-9]{5,20}$", "护照号格式不正确，请输入5-20位字母或数字"),
            _ => ValidateByLength(idNumber, 4, 64, "证件号长度需在4-64位之间")
        };
    }

    private bool ValidateByRegex(string value, string pattern, string errorMessage)
    {
        if (Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant))
        {
            return true;
        }

        StatusMessage = errorMessage;
        IdNumberError = errorMessage;
        return false;
    }

    private bool ValidateByLength(string value, int minLength, int maxLength, string errorMessage)
    {
        if (value.Length >= minLength && value.Length <= maxLength)
        {
            return true;
        }

        StatusMessage = errorMessage;
        IdNumberError = errorMessage;
        return false;
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
