using System.Runtime.InteropServices;

namespace Gatherly.Windows.Services;

/// <summary>
/// Windows Shell 视频缩略图提取器 — 使用 IShellItemImageFactory COM 接口
/// 从系统缩略图缓存中获取视频首帧预览
/// </summary>
public static class VideoThumbnailProvider
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("bcc18b79-c7cf-4200-ad76-522817f0e730")]
    private interface IShellItemImageFactory
    {
        void GetImage(
            [MarshalAs(UnmanagedType.Struct)] SIZE size,
            uint flags,
            out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IntPtr ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const uint SIIGBF_THUMBNAILONLY = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    /// <summary>
    /// 尝试从视频文件提取缩略图。失败返回 null。
    /// </summary>
    public static Avalonia.Media.Imaging.Bitmap? TryGetThumbnail(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero,
                typeof(IShellItemImageFactory).GUID, out var shellItemPtr);
            if (hr != 0 || shellItemPtr == IntPtr.Zero) return null;

            try
            {
                var shellItem = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(shellItemPtr);
                var size = new SIZE { cx = 400, cy = 280 };
                shellItem.GetImage(size, SIIGBF_THUMBNAILONLY, out var hBitmap);

                if (hBitmap == IntPtr.Zero) return null;

                var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero,
                    new System.Windows.Int32Rect(0, 0, 0, 0),
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(hBitmap);

                using var ms = new MemoryStream();
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                encoder.Save(ms);
                ms.Position = 0;

                return new Avalonia.Media.Imaging.Bitmap(ms);
            }
            finally
            {
                Marshal.Release(shellItemPtr);
            }
        }
        catch
        {
            return null;
        }
    }
}
