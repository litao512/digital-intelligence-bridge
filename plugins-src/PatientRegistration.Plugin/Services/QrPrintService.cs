using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using QRCoder;
using PatientRegistration.Plugin.Configuration;
using PatientRegistration.Plugin.Models;
using PatientRegistration.Plugin.Utils;

namespace PatientRegistration.Plugin.Services;

public class QrPrintService : IQrPrintService
{
    private readonly PrintTemplateSettings _template;
    private readonly string _pluginDirectory;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;

    public QrPrintService(
        PrintTemplateSettings? template = null,
        string? pluginDirectory = null,
        Func<ProcessStartInfo, Process?>? processStarter = null)
    {
        _template = template ?? new PrintTemplateSettings();
        _pluginDirectory = string.IsNullOrWhiteSpace(pluginDirectory) ? AppContext.BaseDirectory : pluginDirectory;
        _processStarter = processStarter ?? Process.Start;
    }

    public Task PrintAsync(PatientRegistrationPrintPayload payload, CancellationToken cancellationToken = default)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "dib-patient-registration-print");
        Directory.CreateDirectory(outputDirectory);

        var qrContent = BuildQrContent(payload);
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(20);

        var htmlPath = Path.Combine(outputDirectory, $"registration-{payload.RegistrationId:N}.html");
        var logoDataUri = BuildLogoDataUri(_template.LogoPath, _pluginDirectory);
        var htmlContent = BuildPrintHtml(payload, pngBytes, _template, logoDataUri);
        File.WriteAllText(htmlPath, htmlContent, Encoding.UTF8);

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Arguments = $"/c start \"\" \"{htmlPath}\"",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        _ = _processStarter(startInfo);
        return Task.CompletedTask;
    }

    private static string BuildQrContent(PatientRegistrationPrintPayload payload)
    {
        return payload.QrCodeContent;
    }

    private static string BuildPrintHtml(
        PatientRegistrationPrintPayload payload,
        byte[] qrPngBytes,
        PrintTemplateSettings template,
        string? logoDataUri)
    {
        var qrBase64 = Convert.ToBase64String(qrPngBytes);
        var registrationCode = Encode(RegistrationCodeFormatter.Format(payload.RegistrationId));
        var patientName = Encode(payload.PatientName);
        var idType = Encode(payload.IdType);
        var idNumberMasked = Encode(payload.IdNumberMasked);
        var notes = Encode(payload.Notes);
        var hospitalName = Encode(template.HospitalName);
        var ticketTitle = Encode(template.TicketTitle);
        var ticketSubtitle = Encode(template.TicketSubtitle);
        var footerTip = Encode(template.FooterTip);
        var diagnosticHint = Encode(template.DiagnosticHint);
        var paperWidth = Normalize(template.PaperWidthMm, 58, 100, 80);
        var qrSize = Normalize(template.QrSizeMm, 24, 60, 46);
        var titleFontSize = Normalize(template.TitleFontSizePx, 12, 30, 18);
        var bodyFontSize = Normalize(template.BodyFontSizePx, 10, 20, 12);
        var contentWidth = Math.Max(50, paperWidth - 8);
        var idTypeRow = template.ShowIdType
            ? $"""<div class="row"><span class="label">证件类型：</span>{idType}</div>"""
            : string.Empty;
        var notesRow = template.ShowNotes && !string.IsNullOrWhiteSpace(notes)
            ? $"""<div class="row"><span class="label">登记备注：</span>{notes}</div>"""
            : string.Empty;
        var logoHtml = string.IsNullOrWhiteSpace(logoDataUri)
            ? string.Empty
            : $"""<div class="logo-wrap"><img src="{logoDataUri}" alt="医院标识" /></div>""";
        var printedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return $$"""
                 <!doctype html>
                 <html lang="zh-CN">
                 <head>
                   <meta charset="utf-8" />
                   <title>就诊身份码</title>
                   <style>
                     @page { size: {{paperWidth}}mm auto; margin: 4mm; }
                     body { margin: 0; font-family: "Microsoft YaHei", sans-serif; color: #222; }
                     .ticket { width: {{contentWidth}}mm; margin: 0 auto; }
                     .logo-wrap { text-align: center; margin: 0 0 6px; }
                     .logo-wrap img { max-width: 26mm; max-height: 12mm; object-fit: contain; }
                     .title { text-align: center; font-size: {{titleFontSize}}px; font-weight: 700; margin-bottom: 6px; }
                     .subtitle { text-align: center; font-size: {{bodyFontSize}}px; color: #666; margin-bottom: 10px; }
                     .line { border-top: 1px dashed #999; margin: 8px 0; }
                     .row { font-size: {{bodyFontSize}}px; line-height: 1.6; word-break: break-all; }
                     .label { color: #555; }
                     .qr-wrap { text-align: center; margin: 10px 0; }
                     .qr-wrap img { width: {{qrSize}}mm; height: {{qrSize}}mm; }
                     .reg-code { text-align: center; font-size: {{bodyFontSize}}px; letter-spacing: 0.5px; }
                     .tips { font-size: {{Math.Max(10, bodyFontSize - 1)}}px; color: #444; line-height: 1.5; }
                     .footer { text-align: center; font-size: 10px; color: #777; margin-top: 8px; }
                   </style>
                 </head>
                 <body>
                   <div class="ticket">
                     {{logoHtml}}
                     <div class="title">{{ticketTitle}}</div>
                     <div class="subtitle">{{hospitalName}}｜{{ticketSubtitle}}</div>
                     <div class="line"></div>
                     <div class="row"><span class="label">患者姓名：</span>{{patientName}}</div>
                     {{idTypeRow}}
                     <div class="row"><span class="label">证件号码：</span>{{idNumberMasked}}</div>
                     {{notesRow}}
                     <div class="row"><span class="label">打印时间：</span>{{printedAt}}</div>
                     <div class="line"></div>
                     <div class="qr-wrap">
                       <img src="data:image/png;base64,{{qrBase64}}" alt="就诊身份二维码" />
                     </div>
                     <div class="reg-code">登记号：{{registrationCode}}</div>
                     <div class="line"></div>
                     <div class="tips">{{diagnosticHint}}</div>
                     <div class="footer">{{footerTip}}</div>
                   </div>
                 </body>
                 </html>
                 """;
    }

    private static string Encode(string? text)
    {
        return WebUtility.HtmlEncode(text ?? string.Empty);
    }

    private static string? BuildLogoDataUri(string? logoPath, string pluginDirectory)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return null;
        }

        var resolvedPath = Path.IsPathRooted(logoPath)
            ? logoPath
            : Path.GetFullPath(Path.Combine(pluginDirectory, logoPath));

        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        var ext = Path.GetExtension(resolvedPath).ToLowerInvariant();
        var mime = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => null
        };

        if (mime is null)
        {
            return null;
        }

        var bytes = File.ReadAllBytes(resolvedPath);
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    private static int Normalize(int value, int min, int max, int fallback)
    {
        if (value <= 0)
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

}
