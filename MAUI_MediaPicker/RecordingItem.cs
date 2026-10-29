namespace MAUI_MediaPicker;

public class RecordingItem
{
    public required string FilePath { get; init; }

    public string Name => Path.GetFileNameWithoutExtension(FilePath);

    public string Details
    {
        get
        {
            var info = new FileInfo(FilePath);
            return $"{info.CreationTime:dd.MM.yyyy HH:mm:ss} · {info.Length / 1024.0:0} КБ";
        }
    }
}
