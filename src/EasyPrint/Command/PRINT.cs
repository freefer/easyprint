using Microsoft.Extensions.Logging;
using PDFtoImage;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SkiaSharp;
using SuperSocket.WebSocket.Server;
using System.Diagnostics;
using System.Drawing.Printing;

namespace EasyPrint.Command
{
    class PRINT : JsonCommandBase<PrintJob>
    {
        Stopwatch sw = new Stopwatch();
        protected override async ValueTask ExecuteJsonCommandAsync(EasyPrintSession session, PrintJob content, CancellationToken cancellationToken)
        {
            try
            {
                string printerName = content.PrinterName;
                if (printerName == "")
                {
                    printerName = PrinterHelper.GetDefaultPrinterName() ?? "";
                    content.PrinterName = printerName == "" ? "默认打印机" : printerName;
                }
                session.AppServer.WorkForm.AppendLog($"接收到打印任务: {content.Id} 「{content.PrinterName}」 「{string.Join("", content.Context.Take(10))}...」", LogLevel.Information);
                session.AppServer.WorkForm.AddPrintJob(content);
                var pdf = await CreatePDF(session.AppServer.Browser, content);
                session.AppServer.WorkForm.UpdatePrintJob(content.Id, PrintJobStatus.Printing);
                PrintPDF(pdfData: pdf, printerName);

                await session.SendMessage(new PrintResponseMessage { status = 200, message = "打印成功", data = content.Id, command = "PRINT" });

            }
            catch (Exception)
            {
                session.AppServer.WorkForm.UpdatePrintJob(content.Id, PrintJobStatus.Failed);
                throw;
            }
        }


        private async Task<Stream> CreatePDF(IBrowser browser, PrintJob job)
        {


            if (job.WidthMm == 0 || job.HeightMm == 0)
            {
                var (w, h) = PrinterHelper.GetDefaultPaperSizeMm();
                job.WidthMm = (double)w;
                job.HeightMm = (double)h;
            }


            await using var page = await browser.NewPageAsync();

            await page.SetContentAsync($"{job.Context}");



            // 设置PDF选项，使用打印机的纸张大小（mm单位，精度高于英寸换算）
            var data = await page.PdfStreamAsync(new PdfOptions
            {
                Width = $"{job.WidthMm}mm",      // 宽度（毫米）
                Height = $"{job.HeightMm}mm",    // 高度（毫米）
                PrintBackground = true,    // 打印背景图和颜色
                MarginOptions = new MarginOptions  // 设置边距
                {
                    Top = $"{job.PaddingMm[0]}mm",
                    Right = $"{job.PaddingMm[1]}mm",
                    Bottom = $"{job.PaddingMm[2]}mm",
                    Left = $"{job.PaddingMm[3]}mm"
                },
                PreferCSSPageSize = false  // 不使用CSS中的@page尺寸
            });


            await page.CloseAsync();

            //await page.PdfAsync("test.pdf", new PdfOptions
            //{
            //    Width = $"{width}in",      // 宽度（英寸）
            //    Height = $"{height}in",    // 高度（英寸）
            //    PrintBackground = true,    // 打印背景图和颜色
            //    MarginOptions = new MarginOptions  // 设置边距
            //    {
            //        Top = "0",
            //        Right = "0",
            //        Bottom = "0",
            //        Left = "0"
            //    },
            //    PreferCSSPageSize = false  // 不使用CSS中的@page尺寸
            //});


            return data;

        }



        private static void PrintPDF(Stream pdfData, string? printerName = null)
        {
            try
            {
                Console.WriteLine("正在加载PDF...");

                // 将PDF转换为图像列表（每页一个图像），设置高DPI提升清晰度
                var renderOptions = new RenderOptions
                {
                    AntiAliasing = PdfAntiAliasing.All,  // 全抗锯齿

                };

                var skImages = Conversion.ToImages(pdfData, options: renderOptions).ToList();


                // 将SKBitmap转换为System.Drawing.Bitmap（使用高质量编码）
                var bitmaps = new List<Bitmap>();
                foreach (var skBitmap in skImages)
                {
                    using (var image = SKImage.FromBitmap(skBitmap))
                    {
                        // 使用PNG格式并设置100%质量，确保条码清晰
                        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                        using (var stream = data.AsStream())
                        {
                            var bitmap = new Bitmap(stream);

                            bitmaps.Add(bitmap);

                        }
                    }
                    skBitmap.Dispose();
                }

                // 创建打印文档
                using (var printDoc = new PrintDocument())
                {

                    // 设置打印机
                    if (!string.IsNullOrEmpty(printerName))
                    {
                        printDoc.PrinterSettings.PrinterName = printerName;
                    }



                    // 设置打印选项
                    printDoc.PrinterSettings.Copies = 1;
                    printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                    int currentPage = 0;

                    // 打印每一页
                    printDoc.PrintPage += (sender, e) =>
                    {
                        if (currentPage < bitmaps.Count && e.Graphics != null)
                        {
                            var bitmap = bitmaps[currentPage];
                            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                            // 设置高质量渲染模式（条码打印最好不使用插值）
                            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;  // 最近邻插值，保持锐利边缘

                            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                            // 计算实际打印尺寸（基于DPI，避免缩放）
                            // bitmap的像素尺寸 / DPI = 实际英寸尺寸
                            // 实际英寸尺寸 * 打印机DPI(通常100) = 打印像素尺寸
                            float bitmapDpi = bitmap.HorizontalResolution;
                            float printerDpi = e.Graphics.DpiX;  // 获取打印机DPI

                            // 计算缩放比例：打印机DPI / 图像DPI
                            float scale = printerDpi / bitmapDpi;

                            // 计算打印尺寸（保持原始比例，基于DPI计算）
                            int printWidth = (int)(bitmap.Width * scale);
                            int printHeight = (int)(bitmap.Height * scale);

                            // 如果打印尺寸超过页面，则缩小以适应
                            var pageWidth = e.PageBounds.Width;
                            var pageHeight = e.PageBounds.Height;

                            if (printWidth > pageWidth || printHeight > pageHeight)
                            {
                                float pageScale = Math.Min(
                                    (float)pageWidth / printWidth,
                                    (float)pageHeight / printHeight
                                );
                                printWidth = (int)(printWidth * pageScale);
                                printHeight = (int)(printHeight * pageScale);
                            }

                            // 居中打印
                            int x = (pageWidth - printWidth) / 2;
                            int y = (pageHeight - printHeight) / 2;

                            // 使用Rectangle结构获得更精确的绘制
                            Rectangle destRect = new Rectangle(x, y, printWidth, printHeight);
                            e.Graphics.DrawImage(bitmap, destRect,
                                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                GraphicsUnit.Pixel);

                            currentPage++;
                            e.HasMorePages = currentPage < bitmaps.Count;

                            Console.WriteLine($"正在打印第 {currentPage}/{bitmaps.Count} 页 (图像DPI:{bitmapDpi}, 打印机DPI:{printerDpi})");
                        }
                        else
                        {
                            e.HasMorePages = false;
                        }
                    };

                    // 开始打印
                    printDoc.Print();
                    Console.WriteLine("PDF已发送到打印机");
                }

                // 释放图像资源
                foreach (var bitmap in bitmaps)
                {
                    bitmap?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打印失败: {ex.Message}");
                Console.WriteLine($"详细错误: {ex}");
            }
        }

    }
}
