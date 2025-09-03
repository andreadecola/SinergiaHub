using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web;

namespace Sinergia.App_Helpers
{
    public static class FotoProfiloHelper
    {
        public static string ElaboraEFaiUpload(HttpPostedFileBase fileFoto, string sottoCartella)
        {
            if (fileFoto == null || fileFoto.ContentLength == 0)
                throw new Exception("Nessun file ricevuto.");

            // ✅ Elabora l'immagine (crop centrato e resize)
            using (var imgOriginale = Image.FromStream(fileFoto.InputStream))
            using (var bmp = new Bitmap(imgOriginale))
            using (var ritagliata = RitagliaFotoCentro(bmp))
            {
                string nomeFile = $"foto_{Guid.NewGuid():N}.jpg";
                string relativePath = $"/Content/img/{sottoCartella}/{nomeFile}";
                string absolutePath = HttpContext.Current.Server.MapPath(relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

                ritagliata.Save(absolutePath, ImageFormat.Jpeg);
                return relativePath;
            }
        }

        public static Bitmap RitagliaFotoCentro(Bitmap original)
        {
            int targetWidth = 413;
            int targetHeight = 531;

            // 🔁 Aumenta margini viso → crop più grande
            double scaleFactor = 1.3; // Aumenta per vedere più viso (zoom-out)

            int cropWidth = (int)(targetWidth * scaleFactor);
            int cropHeight = (int)(targetHeight * scaleFactor);

            // 🔝 Sposta verso alto per includere fronte
            int x = (original.Width - cropWidth) / 2;
            int y = (original.Height - cropHeight) / 4;

            Rectangle cropArea = new Rectangle(
                Math.Max(0, x),
                Math.Max(0, y),
                Math.Min(cropWidth, original.Width - x),
                Math.Min(cropHeight, original.Height - y)
            );

            var cropped = original.Clone(cropArea, original.PixelFormat);

            // 🔁 Resize finale alla dimensione esatta 413x531
            Bitmap final = new Bitmap(targetWidth, targetHeight);
            using (Graphics g = Graphics.FromImage(final))
            {
                g.DrawImage(cropped, new Rectangle(0, 0, targetWidth, targetHeight));
            }

            return final;
        }

    }
}
