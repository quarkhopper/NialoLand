// KaleidoscopeRenderer.cs — the math heart of the kaleidoscope effect.
//
// How kaleidoscopes work (the short version):
//   A real kaleidoscope uses mirrors arranged in a wedge shape. The image
//   seen through one wedge is reflected across each mirror, filling a circle
//   with symmetric copies. We simulate this digitally by:
//     1. Converting each output pixel to polar coordinates (r = distance from
//        center, θ = angle).
//     2. Adding a rotation offset to θ so the image appears to spin.
//     3. Folding θ into a single "wedge" and mirroring alternate wedges so
//        the seams look seamless.
//     4. Converting back to Cartesian coords to sample the source image.
//
// This class has no UI dependencies — it just takes a Bitmap in and returns
// a rendered Bitmap out. That makes it easy to reuse (e.g. for screenshots).

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Kaleidoscope;

public static class KaleidoscopeRenderer
{
    /// <summary>
    /// Renders one frame of the kaleidoscope effect.
    /// </summary>
    /// <param name="source">The source photo to use as the pattern tile.</param>
    /// <param name="segments">Number of mirror slices (must be ≥ 2). Even numbers look best.</param>
    /// <param name="rotationRadians">Current rotation angle — increment this each frame to animate.</param>
    /// <param name="outputSize">Pixel dimensions of the output bitmap.</param>
    /// <returns>A new Bitmap with the kaleidoscope frame rendered into it.</returns>
    public static Bitmap RenderFrame(Bitmap source, int segments, double rotationRadians, Size outputSize)
    {
        // Clamp segments to a safe minimum so we don't divide by zero.
        if (segments < 2) segments = 2;

        int outW = outputSize.Width;
        int outH = outputSize.Height;

        // The wedge angle is the slice of the circle each mirror section occupies.
        // e.g. 8 segments → each wedge is 2π/8 = 45 degrees.
        double wedgeAngle = (2.0 * Math.PI) / segments;

        int srcW = source.Width;
        int srcH = source.Height;

        // The radius of the "sampling circle" inside the source image.
        // We use half the shorter side so we always stay within the image bounds.
        double sampleRadius = Math.Min(srcW, srcH) * 0.45;

        // Source image center — this is the origin point we orbit around.
        double srcCx = srcW / 2.0;
        double srcCy = srcH / 2.0;

        // Output image center.
        double outCx = outW / 2.0;
        double outCy = outH / 2.0;

        // Lock the source bitmap for fast pixel reads.
        // BitmapData gives us a direct pointer to the pixel array in memory —
        // much faster than calling GetPixel() for every pixel in a big image.
        var srcData = source.LockBits(
            new Rectangle(0, 0, srcW, srcH),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        // Create and lock the output bitmap.
        var output = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
        var outData = output.LockBits(
            new Rectangle(0, 0, outW, outH),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        // Copy source pixels into a managed byte array so we can index without
        // unsafe pointer arithmetic. 4 bytes per pixel (B, G, R, A order in memory).
        int srcStride = srcData.Stride;
        int outStride = outData.Stride;
        byte[] srcPixels = new byte[Math.Abs(srcStride) * srcH];
        byte[] outPixels = new byte[Math.Abs(outStride) * outH];
        Marshal.Copy(srcData.Scan0, srcPixels, 0, srcPixels.Length);

        // --- Main render loop ---
        // For every output pixel, figure out which source pixel it maps to.
        for (int y = 0; y < outH; y++)
        {
            for (int x = 0; x < outW; x++)
            {
                // Step 1: Translate output pixel to be relative to the center.
                double dx = x - outCx;
                double dy = y - outCy;

                // Step 2: Convert to polar coordinates.
                double r = Math.Sqrt(dx * dx + dy * dy);
                double theta = Math.Atan2(dy, dx); // range: -π to +π

                // Step 3: Apply the rotation animation offset.
                theta += rotationRadians;

                // Step 4: Normalize theta to [0, 2π) before folding into a wedge.
                theta = theta % (2.0 * Math.PI);
                if (theta < 0) theta += 2.0 * Math.PI;

                // Step 5: Fold theta into [0, wedgeAngle).
                double thetaInWedge = theta % wedgeAngle;

                // Step 6: Mirror alternate wedges so the pattern tiles seamlessly.
                // Without this, you'd see a visible seam at every wedge boundary.
                int wedgeIndex = (int)(theta / wedgeAngle);
                if (wedgeIndex % 2 == 1)
                    thetaInWedge = wedgeAngle - thetaInWedge;

                // Step 7: Convert back to Cartesian to get our source sample point.
                // We scale by sampleRadius so the full output circle maps neatly
                // to a circle inside the source image.
                double scale = sampleRadius / Math.Max(outCx, outCy);
                double srcX = srcCx + r * scale * Math.Cos(thetaInWedge);
                double srcY = srcCy + r * scale * Math.Sin(thetaInWedge);

                // Step 8: Clamp to source image bounds (in case of edge rounding).
                int sx = Math.Clamp((int)srcX, 0, srcW - 1);
                int sy = Math.Clamp((int)srcY, 0, srcH - 1);

                // Step 9: Read the source pixel (BGRA format — note B comes first).
                int srcOffset = sy * srcStride + sx * 4;
                byte b = srcPixels[srcOffset];
                byte g = srcPixels[srcOffset + 1];
                byte r2 = srcPixels[srcOffset + 2];
                byte a = srcPixels[srcOffset + 3];

                // Step 10: Write to the output pixel array.
                int outOffset = y * outStride + x * 4;
                outPixels[outOffset] = b;
                outPixels[outOffset + 1] = g;
                outPixels[outOffset + 2] = r2;
                outPixels[outOffset + 3] = a;
            }
        }

        // Write the filled pixel array back into the output bitmap and unlock both.
        Marshal.Copy(outPixels, 0, outData.Scan0, outPixels.Length);
        source.UnlockBits(srcData);
        output.UnlockBits(outData);

        return output;
    }
}
