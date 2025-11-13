using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace UVMapConverter
{
    public class ImageGenerator
    {
        public Bitmap GenerateImage(
            UVMapData uvData,
            int outputSize,
            SKColor backgroundColor,
            SKColor lineColor,
            int lineThickness,
            bool drawVertices = true)
        {
            // Create Skia surface
            using var surface = SKSurface.Create(new SKImageInfo(outputSize, outputSize));
            var canvas = surface.Canvas;

            // Clear with background color
            canvas.Clear(backgroundColor);

            // Set up paint for drawing lines (edges)
            using var linePaint = new SKPaint
            {
                Color = lineColor,
                StrokeWidth = lineThickness,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            // Set up paint for drawing vertices (points)
            using var pointPaint = new SKPaint
            {
                Color = lineColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var points = uvData.Points;
            
            
            // Every 3 consecutive points form ONE triangle face
            // We draw each triangle independently
            int triangleCount = points.Count / 3;

            for (int i = 0; i < triangleCount; i++)
            {
                int idx0 = i * 3;
                int idx1 = i * 3 + 1;
                int idx2 = i * 3 + 2;

                if (idx2 < points.Count)
                {
                    // Get the three vertices of this triangle
                    var p0 = UVToPixel(points[idx0], outputSize);
                    var p1 = UVToPixel(points[idx1], outputSize);
                    var p2 = UVToPixel(points[idx2], outputSize);

                    // Draw the three edges of this triangle ONLY
                    // Edge 0->1
                    canvas.DrawLine(p0, p1, linePaint);
                    // Edge 1->2
                    canvas.DrawLine(p1, p2, linePaint);
                    // Edge 2->0
                    canvas.DrawLine(p2, p0, linePaint);
                }
            }

            if (drawVertices)
            {
                // Draw vertices on top for visibility
                float pointSize = Math.Max(lineThickness * 1.2f, 2.5f);
                foreach (var point in points)
                {
                    var pixel = UVToPixel(point, outputSize);
                    canvas.DrawCircle(pixel.X, pixel.Y, pointSize, pointPaint);
                }
            }
            
            // Convert to Avalonia Bitmap
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;

            return new Bitmap(stream);
        }

        private SKPoint UVToPixel(UVPoint uv, int size)
        {
            // Clamp UV coordinates to 0-1 range
            float u = Math.Clamp(uv.U, 0f, 1f);
            float v = Math.Clamp(uv.V, 0f, 1f);

            // Convert to pixel coordinates
            float x = u * size;
            float y = v * size; 
            return new SKPoint(x, y);
        }
    }
}