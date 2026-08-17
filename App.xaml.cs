using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoreSearch;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EnsureIconExists();
        base.OnStartup(e);
    }

    public static void EnsureIconExists()
    {
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (File.Exists(iconPath)) return;

        try
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Fundo escuro arredondado
                dc.DrawRoundedRectangle(
                    new SolidColorBrush(Color.FromRgb(24, 24, 27)),
                    null,
                    new Rect(0, 0, 64, 64), 12, 12);

                // Lupa branca
                var pen = new Pen(Brushes.White, 5);
                dc.DrawEllipse(null, pen, new Point(27, 27), 12, 12);
                dc.DrawLine(pen, new Point(36, 36), new Point(48, 48));
            }

            var rtb = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            byte[] pngBytes = ms.ToArray();

            // Escreve como ICO com cabeçalho PNG encapsulado
            using var fs = File.Create(iconPath);
            using var writer = new BinaryWriter(fs);

            // ICO Header (6 bytes)
            writer.Write((ushort)0); // Reserved
            writer.Write((ushort)1); // Type ICO
            writer.Write((ushort)1); // Image count

            // ICO Directory Entry (16 bytes)
            writer.Write((byte)64); // Width
            writer.Write((byte)64); // Height
            writer.Write((byte)0);  // Color palette
            writer.Write((byte)0);  // Reserved
            writer.Write((ushort)1); // Color planes
            writer.Write((ushort)32); // Bits per pixel
            writer.Write((uint)pngBytes.Length); // Image size
            writer.Write((uint)22); // Image offset (6 + 16 = 22)

            // PNG bytes
            writer.Write(pngBytes);
        }
        catch
        {
            // Ignora se não puder salvar no diretório corrente
        }
    }
}
