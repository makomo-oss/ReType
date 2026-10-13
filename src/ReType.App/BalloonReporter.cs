using ReType.Core;

namespace ReType.App;

/// <summary>Surfaces conversion status as balloon tips on the tray icon.</summary>
public sealed class BalloonReporter(NotifyIcon notifyIcon) : IConversionReporter
{
    public void ConversionStarted(RenameDecision decision)
    {
    }

    public void ConversionSucceeded(RenameDecision decision, string outputPath) =>
        notifyIcon.ShowBalloonTip(
            4000,
            "ReType: 変換完了",
            $"{Path.GetFileName(outputPath)} に変換しました",
            ToolTipIcon.Info);

    public void ConversionFailed(RenameDecision decision, string reason, bool rolledBack) =>
        notifyIcon.ShowBalloonTip(
            6000,
            "ReType: 変換に失敗",
            $"{Path.GetFileName(decision.NewPath)}: {reason}" +
            (rolledBack ? "（拡張子をもとに戻しました）" : string.Empty),
            ToolTipIcon.Warning);

    public void ConversionUnsupported(RenameDecision decision) =>
        notifyIcon.ShowBalloonTip(
            6000,
            "ReType: 未対応の変換",
            $"{decision.FromExtension} → {decision.ToExtension} には対応していません" +
            "（拡張子をもとに戻しました）",
            ToolTipIcon.Warning);
}
