using ReType.Core;

namespace ReType.App;

/// <summary>Surfaces conversion status as balloon tips on the tray icon.</summary>
public sealed class BalloonReporter(NotifyIcon notifyIcon, Func<string> idleText) : IConversionReporter
{
    public void ConversionStarted(RenameDecision decision)
    {
        // Only long-running categories get a heads-up balloon; images finish
        // before the balloon would even be readable.
        if (ConversionMatrix.CategoryOf(decision.FromExtension) is ConversionCategory.Video or ConversionCategory.Audio)
        {
            notifyIcon.ShowBalloonTip(
                4000,
                "ReType: 変換中",
                $"{Path.GetFileName(decision.NewPath)} の変換を開始しました（時間がかかることがあります）",
                ToolTipIcon.Info);
        }
    }

    public void ConversionProgress(RenameDecision decision, double? percent)
    {
        var name = Path.GetFileName(decision.NewPath);
        var text = percent is null
            ? $"ReType: 変換中 {name}"
            : $"ReType: 変換中 {percent.Value:F0}% {name}";
        SetIconText(text);
    }

    public void ConversionSucceeded(RenameDecision decision, string outputPath)
    {
        SetIconText(idleText());
        notifyIcon.ShowBalloonTip(
            4000,
            "ReType: 変換完了",
            $"{Path.GetFileName(outputPath)} に変換しました",
            ToolTipIcon.Info);
    }

    public void ConversionFailed(RenameDecision decision, string reason, bool rolledBack)
    {
        SetIconText(idleText());
        notifyIcon.ShowBalloonTip(
            6000,
            "ReType: 変換に失敗",
            $"{Path.GetFileName(decision.NewPath)}: {reason}" +
            (rolledBack ? "（拡張子をもとに戻しました）" : string.Empty),
            ToolTipIcon.Warning);
    }

    public void ConversionUnsupported(RenameDecision decision)
    {
        SetIconText(idleText());
        notifyIcon.ShowBalloonTip(
            6000,
            "ReType: 未対応の変換",
            $"{decision.FromExtension} → {decision.ToExtension} には対応していません" +
            "（拡張子をもとに戻しました）",
            ToolTipIcon.Warning);
    }

    /// <summary>NotifyIcon.Text has a hard length cap; keep well inside it.</summary>
    private void SetIconText(string text) =>
        notifyIcon.Text = text.Length <= 60 ? text : text[..60];
}
