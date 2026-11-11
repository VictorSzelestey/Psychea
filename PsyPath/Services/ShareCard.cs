namespace PsyPath.Services;

// «Поделиться результатом»: снимок карточки с экрана в PNG и системное окно «Поделиться»
public static class ShareCard
{
    static readonly SemaphoreSlim LessonGate = new(1, 1);

    public static async Task<bool> ShareLesson(LessonShareResult result, string title)
    {
        // Повторное нажатие во время подготовки уже обрабатывается первым запросом.
        if (!await LessonGate.WaitAsync(0)) return true;
        try
        {
            using var mascot = await FileSystem.OpenAppPackageFileAsync("share_mascot.png");
            using var regular = await FileSystem.OpenAppPackageFileAsync("share_regular.ttf");
            using var bold = await FileSystem.OpenAppPackageFileAsync("share_bold.ttf");
            byte[] png = await Task.Run(() => LessonShareCardRenderer.Render(result, mascot, regular, bold));
            string path = Path.Combine(FileSystem.CacheDirectory, $"psychea_lesson_{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(path, png);
            await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(path, "image/png")
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            LessonGate.Release();
        }
    }

    public static async Task<bool> Share(VisualElement card, string title)
    {
        try
        {
            var shot = await card.CaptureAsync();
            if (shot == null) return false;
            string path = Path.Combine(FileSystem.CacheDirectory, "psypath_result.png");
            await using (var src = await shot.OpenReadAsync(ScreenshotFormat.Png))
            await using (var dst = File.Create(path))
                await src.CopyToAsync(dst);
            await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(path, "image/png")
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
