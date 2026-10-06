namespace ReType.Core.Converters;

/// <summary>
/// IConverter の集合から (source, target) に対応するエンジンを選ぶ。
/// 対応するエンジンがない組み合わせ（例: Audio→Video）は null を返す。
/// </summary>
public sealed class ConverterRegistry
{
    private readonly List<IConverter> converters = [];

    public void Register(IConverter converter) => converters.Add(converter);

    public IReadOnlyList<IConverter> Converters => converters;

    /// <summary>指定組み合わせを変換できるエンジンを 1 つ返す（見つかなければ null）。</summary>
    public IConverter? Resolve(FileFamily source, FileFamily target) =>
        converters.FirstOrDefault(c => c.CanConvert(source, target));
}
