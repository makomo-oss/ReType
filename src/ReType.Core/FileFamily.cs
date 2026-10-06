namespace ReType.Core;

/// <summary>
/// ファミリー種別。変換は原則として同一ファミリー内のみ、
/// 一部のクロスファミリー組み合わせのみ ConverterRegistry で許可される。
/// </summary>
public enum FileFamily
{
    Unknown = 0,
    Image,
    Video,
    Audio,
    Document,
}
