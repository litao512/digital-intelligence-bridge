namespace PatientRegistration.Plugin.Configuration;

public class PluginSettings
{
    public PostgresSettings Postgres { get; set; } = new();

    public RegistrationSettings Registration { get; set; } = new();
}

public class PostgresSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class RegistrationSettings
{
    public bool EnableDirectPrint { get; set; } = true;

    public bool RequireEmptyDiagnosticConfirmation { get; set; } = true;

    public PrintTemplateSettings PrintTemplate { get; set; } = new();
}

public class PrintTemplateSettings
{
    public string HospitalName { get; set; } = "门诊";

    public string LogoPath { get; set; } = string.Empty;

    public int PaperWidthMm { get; set; } = 80;

    public int QrSizeMm { get; set; } = 46;

    public int TitleFontSizePx { get; set; } = 18;

    public int BodyFontSizePx { get; set; } = 12;

    public bool ShowIdType { get; set; } = true;

    public bool ShowNotes { get; set; } = true;

    public string TicketTitle { get; set; } = "门诊就诊身份码";

    public string TicketSubtitle { get; set; } = "用于接诊、巡视、完成等身份确认";

    public string FooterTip { get; set; } = "请妥善保管本单据";

    public string DiagnosticHint { get; set; } = "若系统未预填诊疗信息，医生扫码后可现场新增治疗项目。";
}
