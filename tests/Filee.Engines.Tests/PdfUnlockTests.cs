// Unlock PDF: a PDF → PDF preset with the password a source opens with writes the same PDF without it, or locked with
// the preset's own password instead.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using PdfSharp.Pdf.IO;

namespace Filee.Engines.Tests;

public class PdfUnlockTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static string LockedPdf(string folder)
    {
        var pdf = new PdfBuilder().NewPage();
        pdf.Line("Secret", 12, pdf.Font(), 50, 80);
        var path = pdf.Save(Path.Combine(folder, "locked.pdf"));
        PdfBuilder.Protect(path, "1234");
        return path;
    }

    [Fact]
    public async Task The_open_password_is_removed()
    {
        var pdf = LockedPdf(fx.NewFolder());

        var job = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "pdf", Pdf = { OpenPassword = "1234" } });

        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        using var opened = PdfReader.Open(job.Outputs.Single(), PdfDocumentOpenMode.Modify); // no password, and changes allowed
        Assert.Equal(1, opened.PageCount);
        Assert.Throws<PdfReaderException>(() => PdfReader.Open(pdf, PdfDocumentOpenMode.Import).Dispose()); // the source stays locked
    }

    [Fact]
    public async Task A_wrong_open_password_is_refused()
    {
        var pdf = LockedPdf(fx.NewFolder());

        var job = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "pdf", Pdf = { OpenPassword = "wrong" } });

        Assert.Equal(JobState.Failed, job.State);
        Assert.Equal("error.pdf_open_password_wrong", job.Files.Single().ErrorKey);
    }

    [Fact]
    public async Task The_preset_password_replaces_the_old_one()
    {
        var pdf = LockedPdf(fx.NewFolder());

        var job = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "pdf", Pdf = { OpenPassword = "1234", Password = "new-5678" } });

        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        var output = job.Outputs.Single();
        Assert.ThrowsAny<Exception>(() => PdfReader.Open(output, "1234", PdfDocumentOpenMode.Import).Dispose());
        using var opened = PdfReader.Open(output, "new-5678", PdfDocumentOpenMode.Import);
        Assert.Equal(1, opened.PageCount);
    }
}
