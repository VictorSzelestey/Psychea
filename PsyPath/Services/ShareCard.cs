namespace PsyPath.Services;

// «Поделиться результатом»: снимок карточки с экрана в PNG и системное окно «Поделиться»
public static class ShareCard
{
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
