using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace UVMapConverter
{
    public class UVPoint
    {
        public float U { get; set; }
        public float V { get; set; }
        public int Lod { get; set; }

        public UVPoint(float u, float v, int lod = 0)
        {
            U = u;
            V = v;
            Lod = lod;
        }
    }

    public class UVMapData
    {
        public int TextureWidth { get; set; }
        public int TextureHeight { get; set; }
        public List<UVPoint> Points { get; set; } = new List<UVPoint>();
    }

    public static class UVMapParser
    {
        public static UVMapData ParseCsv(string filePath)
        {
            var result = new UVMapData();

            try
            {
                var lines = File.ReadAllLines(filePath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
                
                if (lines.Count < 3)
                {
                    throw new Exception("CSV file must have at least 3 lines (header, size, data)");
                }

                // Line 0: Header (u, v, lod) - skip it
                // Line 1: Texture size (width, height)
                var sizeLine = lines[1].Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();

                if (sizeLine.Length >= 2)
                {
                    result.TextureWidth = int.Parse(sizeLine[0], CultureInfo.InvariantCulture);
                    result.TextureHeight = int.Parse(sizeLine[1], CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new Exception("Invalid texture size format in line 2");
                }

                // Lines 2+: UV coordinate data (u, v, lod)
                for (int i = 2; i < lines.Count; i++)
                {
                    var line = lines[i].Trim();
                    
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .ToArray();

                    if (parts.Length >= 2)
                    {
                        try
                        {
                            float u = ParseFloat(parts[0]);
                            float v = ParseFloat(parts[1]);
                            int lod = parts.Length >= 3 ? int.Parse(parts[2]) : 0;

                            result.Points.Add(new UVPoint(u, v, lod));
                        }
                        catch
                        {
                            // Skip lines that can't be parsed
                            continue;
                        }
                    }
                }

                if (result.Points.Count == 0)
                {
                    throw new Exception("No valid UV coordinates found in CSV file");
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing CSV: {ex.Message}");
            }
        }

        private static float ParseFloat(string value)
        {
            value = value.Trim();

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
                return result;

            value = value.Replace(',', '.');
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return result;

            throw new FormatException($"Cannot parse '{value}' as a float");
        }
    }
}