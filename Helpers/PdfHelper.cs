using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
namespace SitioSubicIMS.Web.Helpers
{
    public class PdfHelper
    {
        public static PdfPTable CreateHeader(string wwwRootPath)
        {
            // Define column widths
            float[] widths = new float[] { 20f, 60f, 20f };
            PdfPTable headerTable = new PdfPTable(3)
            {
                WidthPercentage = 100
            };
            headerTable.SetWidths(widths);

            // 1. Logo cell
            string logoPath = Path.Combine(wwwRootPath, "images", "logo.png");
            Image logo = null;
            if (File.Exists(logoPath))
            {
                logo = Image.GetInstance(logoPath);
                logo.ScaleToFit(100f, 100f);
            }
            PdfPCell logoCell;
            if (logo != null)
            {
                logoCell = new PdfPCell(logo);
            }
            else
            {
                logoCell = new PdfPCell(new Phrase(""));
            }
            logoCell.Border = Rectangle.NO_BORDER;
            logoCell.HorizontalAlignment = Element.ALIGN_LEFT;
            logoCell.VerticalAlignment = Element.ALIGN_MIDDLE;
            headerTable.AddCell(logoCell);

            // 2. Title cell
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8);

            // Use a Phrase to contain multiple lines with line breaks and control spacing
            var titlePhrase = new Phrase {
                new Chunk("Sitio Subic Waterworks\n", titleFont),
                new Chunk("Integrated Management System\n", subtitleFont),
                new Chunk("Sitio Subic, Brgy. Balele, Tanauan City, Batangas", smallFont)
            };

            PdfPCell titleCell = new PdfPCell(titlePhrase)
            {
                Border = Rectangle.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };
            headerTable.AddCell(titleCell);

            // 3. Empty cell to balance
            PdfPCell spacerCell = new PdfPCell(new Phrase(""))
            {
                Border = Rectangle.NO_BORDER
            };
            headerTable.AddCell(spacerCell);

            // 4. Bottom border row (1 cell spanning all 3 columns)
            PdfPCell borderCell = new PdfPCell(new Phrase(new Chunk("\n", smallFont)))
            {
                Border = Rectangle.BOTTOM_BORDER,
                Colspan = 3,
                BorderWidthBottom = 1f,
                PaddingTop = 2f
            };
            headerTable.AddCell(borderCell);
            return headerTable;
        }
    }
}
