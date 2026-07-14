using ImageArchive;
using ImageArchive.Abstractions;
using ImageArchive.Geometry;
using ImageArchive.Integrity;
using ImageArchive.Internal;
using ImageArchive.Manifest;
using ImageArchive.Qr;

namespace ImageArchive.UnitTests;

public class FooterQrLayoutTests
{
    [Fact]
    [Trait("AC", "AC-FR-FTR-001-3")]
    [Trait("AC", "AC-FR-FTR-002-1")]
    [Trait("AC", "AC-FR-FTR-002-2")]
    public void Footer_has_text_ink_and_left_qr_matches_data_region_sha()
    {
        var data = PixelPacker.PackDataRegion(new byte[] { 1, 2, 3, 4, 5 });
        var sha = Sha256Hex.Compute(data);
        var qr = new DefaultQrCodeService();
        var frame = FrameRenderer.RenderFrame(
            frameIndex: 0,
            frameCount: 3,
            dataRegionRgb: data,
            frameShaHex: sha,
            header: new HeaderManifestSection { Type = HeaderContentType.Text, Text = "H", QrCode = new QrCodeManifestSection { Enabled = false } },
            toolCommitUrl: "https://ex.com/t",
            qr: qr,
            workingDirectory: null);

        Assert.True(FrameRenderer.FooterHasTextInk(frame), "Footer center should contain text ink for 'Frame N of M' + SHA line.");
        Assert.True(FrameRenderer.ValidateQrCellMargins(frame, leftFooter: true), "Left footer QR cell must use 50x50 with margins T=2,R=2,B=1,L=1.");
        Assert.True(FrameRenderer.ValidateQrCellMargins(frame, leftFooter: false), "Right footer QR cell must use same margin scheme.");

        // Extract left QR cell and decode — must equal data-region SHA (Line2 content)
        var bpp = frame.BytesPerPixel;
        var cell = new byte[FrameGeometry.QrCellSize * FrameGeometry.QrCellSize * bpp];
        var di = 0;
        for (var y = 0; y < FrameGeometry.QrCellSize; y++)
        for (var x = 0; x < FrameGeometry.QrCellSize; x++)
        {
            var si = ((FrameGeometry.FooterFirstRow + y) * FrameGeometry.Width + x) * bpp;
            for (var b = 0; b < bpp; b++) cell[di++] = frame.Pixels[si + b];
        }
        var decoded = qr.DecodeFromPixels(cell, FrameGeometry.QrCellSize, FrameGeometry.QrCellSize, frame.Format);
        Assert.Equal(sha, decoded, ignoreCase: true);
    }

    [Fact]
    [Trait("AC", "AC-FR-HDR-002-1")]
    public void Header_qr_cell_is_50x50_top_right_with_margins()
    {
        var data = PixelPacker.PackDataRegion(new byte[] { 9 });
        var sha = Sha256Hex.Compute(data);
        var frame = FrameRenderer.RenderFrame(0, 1, data, sha,
            new HeaderManifestSection
            {
                Type = HeaderContentType.Text,
                Text = "x",
                QrCode = new QrCodeManifestSection { Content = "https://ex.com/h", Enabled = true }
            },
            "https://ex.com/t", new DefaultQrCodeService(), null);

        // Top-right 50x50: top-left corner of cell is white (margin)
        var i = ((0) * FrameGeometry.Width + (FrameGeometry.Width - FrameGeometry.QrCellSize)) * 3;
        Assert.True(frame.Pixels[i] > 250 && frame.Pixels[i + 1] > 250);
    }
}
