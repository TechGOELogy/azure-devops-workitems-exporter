using System;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace AzureDevOpsWorkItemExporter.Templates;

public sealed class PuppeteerPdfRenderer : IHtmlToPdfRenderer
{
    private const int TimeoutMs = 60000;

    private static readonly string[] ChromiumArgs =
    {
        "--no-sandbox",
        "--disable-setuid-sandbox",
        "--disable-dev-shm-usage",
        "--disable-gpu",
        "--no-zygote",
        "--disable-extensions",
        "--disable-background-networking",
        "--disable-sync",
        "--metrics-recording-only",
        "--mute-audio",
        "--no-first-run",
        "--safebrowsing-disable-auto-update"
    };

    public byte[] Render(string html)
    {
        return RenderAsync(html).GetAwaiter().GetResult();
    }

    private static async Task<byte[]> RenderAsync(string html)
    {
        var safeHtml = html ?? string.Empty;

        var fetcher = new BrowserFetcher();
        var browserInfo = await fetcher.DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = browserInfo.GetExecutablePath(),
            Args = ChromiumArgs,
            Timeout = TimeoutMs,
            ProtocolTimeout = TimeoutMs
        });

        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(safeHtml, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Load },
            Timeout = TimeoutMs
        });

        var options = new PdfOptions
        {
            PrintBackground = true
        };

        return await page.PdfDataAsync(options);
    }
}
