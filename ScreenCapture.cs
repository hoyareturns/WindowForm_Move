using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WindowForm_Move;

public static class ScreenCapture
{
    private const int SRCCOPY = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;

    public static Bitmap Capture(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var destination = graphics.GetHdc();
        var source = GetDC(IntPtr.Zero);
        var errorCode = 0;
        try
        {
            if (!BitBlt(destination, 0, 0, bounds.Width, bounds.Height, source, bounds.Left, bounds.Top, SRCCOPY | CAPTUREBLT))
            {
                var lastError = Marshal.GetLastWin32Error();
                errorCode = lastError == 0 ? -1 : lastError;
            }
        }
        finally
        {
            graphics.ReleaseHdc(destination);
            ReleaseDC(IntPtr.Zero, source);
        }

        if (errorCode != 0)
        {
            bitmap.Dispose();
            throw new Win32Exception(errorCode, "화면을 캡처할 수 없습니다.");
        }

        return bitmap;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(
        IntPtr destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        int rasterOperation);
}
