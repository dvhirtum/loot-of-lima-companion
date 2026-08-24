using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LootOfLima.ScenarioImporter;

/// <summary>Thin wrapper over the external <c>pdftotext</c> binary (poppler).</summary>
internal static partial class PdfText
{
    /// <summary>
    /// Removes the "PAGE n" footer, which pdftotext often places on the same line as the last row
    /// of data. Left in, it tokenizes as three bare letters plus a spurious code (E3 for "PAGE 3")
    /// and pushes that row to 33 codes, so the row is silently dropped.
    /// </summary>
    public static string RemovePageFooter(string line) => PageFooterPattern().Replace(line, " ");

    [GeneratedRegex(@"PAGE\s*\d+", RegexOptions.IgnoreCase)]
    private static partial Regex PageFooterPattern();

    /// <summary>Extracts one page. <paramref name="linePrinter"/> selects the rigid fixed-pitch
    /// grid needed for the multi-column Scenario Tokens Chart; the default layout mode mangles it.</summary>
    public static string ExtractPage(string pdfPath, int page, bool linePrinter = false)
    {
        var mode = linePrinter ? "-lineprinter -fixed 5" : "-layout";
        var startInfo = new ProcessStartInfo("pdftotext")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in mode.Split(' '))
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(page.ToString());
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add(page.ToString());
        startInfo.ArgumentList.Add(pdfPath);
        startInfo.ArgumentList.Add("-");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start pdftotext. Is poppler on PATH?");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"pdftotext failed on page {page} of '{pdfPath}' (exit {process.ExitCode}): {stderr}");
        }

        return stdout.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
