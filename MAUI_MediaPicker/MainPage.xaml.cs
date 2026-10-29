using System.Collections.ObjectModel;
using MAUI_MediaPicker.Services;
using Plugin.Maui.Audio;

namespace MAUI_MediaPicker
{
    public partial class MainPage : ContentPage
    {
        private readonly IAudioRecorder _recorder = AudioManager.Current.CreateRecorder();
        private readonly SpeechToTextService _speech = new();
        private readonly AiService _ai = new();
        private readonly ObservableCollection<RecordingItem> _recordings = new();

        private IAudioPlayer? _player;
        private FileStream? _playerStream;
        private string? _currentFile;
        private DateTime _recordingStarted;
        private IDispatcherTimer? _timer;

        public MainPage()
        {
            InitializeComponent();

            RecordingsView.ItemsSource = _recordings;
            LoadRecordings();
        }

        private async void OnRecordClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_recorder.IsRecording)
                    await StopRecordingAsync();
                else
                    await StartRecordingAsync();
            }
            catch (Exception ex)
            {
                ResetRecordingUi();
                await DisplayAlertAsync("Помилка запису", ex.Message, "OK");
            }
        }

        private async Task StartRecordingAsync()
        {
#if !WINDOWS
            var status = await Permissions.RequestAsync<Permissions.Microphone>();

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlertAsync("Мікрофон", "Доступ до мікрофона заборонено", "OK");
                return;
            }
#endif

            StopPlayback();

            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            _currentFile = Path.Combine(FileSystem.AppDataDirectory, $"record_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
            await _recorder.StartAsync();

            _recordingStarted = DateTime.Now;
            RecordButton.Text = "Зупинити запис";
            StatusLabel.Text = "Йде запис...";
            StartTimer();
        }

        private async Task StopRecordingAsync()
        {
            var audioSource = await _recorder.StopAsync();
            ResetRecordingUi();

            if (_currentFile != null)
            {
                await using (var audioStream = audioSource.GetAudioStream())
                await using (var file = File.Create(_currentFile))
                    await audioStream.CopyToAsync(file);

                var item = new RecordingItem { FilePath = _currentFile };
                _recordings.Insert(0, item);
                RecordingsView.SelectedItem = item;
                StatusLabel.Text = "Запис збережено";
            }

            RefreshEmptyState();
        }

        private void ResetRecordingUi()
        {
            _timer?.Stop();
            RecordButton.Text = "Почати запис";
            StatusLabel.Text = string.Empty;
        }

        private void StartTimer()
        {
            _timer ??= Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick -= OnTimerTick;
            _timer.Tick += OnTimerTick;
            TimerLabel.Text = "00:00";
            _timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            TimerLabel.Text = (DateTime.Now - _recordingStarted).ToString(@"mm\:ss");
        }

        private void LoadRecordings()
        {
            var files = new DirectoryInfo(FileSystem.AppDataDirectory)
                .GetFiles("record_*.wav")
                .OrderByDescending(f => f.CreationTime);

            foreach (var file in files)
                _recordings.Add(new RecordingItem { FilePath = file.FullName });

            RefreshEmptyState();
        }

        private void RefreshEmptyState()
        {
            EmptyLabel.IsVisible = _recordings.Count == 0;
            SummarizeButton.IsEnabled = RecordingsView.SelectedItem != null;
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            SummarizeButton.IsEnabled = RecordingsView.SelectedItem != null;
        }

        private void OnPlayClicked(object? sender, EventArgs e)
        {
            if (_recorder.IsRecording || (sender as Button)?.BindingContext is not RecordingItem item)
                return;

            StopPlayback();

            _playerStream = File.OpenRead(item.FilePath);
            _player = AudioManager.Current.CreatePlayer(_playerStream);
            _player.PlaybackEnded += OnPlaybackEnded;
            _player.Play();
            StatusLabel.Text = $"Відтворення: {item.Name}";
        }

        private void OnPlaybackEnded(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (ReferenceEquals(sender, _player))
                    StopPlayback();
            });
        }

        private void StopPlayback()
        {
            var player = _player;
            var stream = _playerStream;
            _player = null;
            _playerStream = null;

            if (player != null)
            {
                player.PlaybackEnded -= OnPlaybackEnded;
                player.Stop();
                player.Dispose();
            }

            stream?.Dispose();

            if (!_recorder.IsRecording)
                StatusLabel.Text = string.Empty;
        }

        private async void OnDeleteClicked(object? sender, EventArgs e)
        {
            if (_recorder.IsRecording || (sender as Button)?.BindingContext is not RecordingItem item)
                return;

            if (!await DisplayAlertAsync("Видалити запис", item.Name, "Видалити", "Скасувати"))
                return;

            StopPlayback();
            File.Delete(item.FilePath);
            _recordings.Remove(item);
            RefreshEmptyState();
        }

        private async void OnSummarizeClicked(object? sender, EventArgs e)
        {
            if (RecordingsView.SelectedItem is not RecordingItem item)
                return;

            var isUkrainian = LanguagePicker.SelectedIndex == 0;

            SetBusy(true);
            TranscriptLabel.Text = string.Empty;
            SummaryLabel.Text = string.Empty;

            try
            {
                StatusLabel.Text = "Розпізнаю мовлення...";
                var text = await _speech.TranscribeAsync(item.FilePath, isUkrainian ? "uk" : "ru");

                if (string.IsNullOrWhiteSpace(text))
                {
                    await DisplayAlertAsync("Голос", "Не вдалося розпізнати мовлення.", "OK");
                    return;
                }

                TranscriptLabel.Text = text;

                StatusLabel.Text = "Виділяю тези...";
                SummaryLabel.Text = await _ai.SummarizeAsync(text, isUkrainian ? "українська" : "російська");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Помилка", ex.Message, "OK");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            Spinner.IsRunning = busy;
            SummarizeButton.IsEnabled = !busy && RecordingsView.SelectedItem != null;
            RecordButton.IsEnabled = !busy;

            if (!busy)
                StatusLabel.Text = string.Empty;
        }
    }
}
