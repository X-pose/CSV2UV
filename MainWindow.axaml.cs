using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.PanAndZoom;

namespace UVMapConverter
{
    public partial class MainWindow : Window
    {
        private string? _currentCsvPath;
        private UVMapData? _currentUVData;
        private Bitmap? _currentBitmap;

        private string? fileName;

        public MainWindow()
        {
            InitializeComponent();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            var dropZone = this.FindControl<Border>("DropZone");
            var browseButton = this.FindControl<Button>("BrowseButton");
            var saveButton = this.FindControl<Button>("SaveButton");
            var clearButton = this.FindControl<Button>("ClearButton");
            var thicknessSlider = this.FindControl<Slider>("ThicknessSlider");
            var sizeCombo = this.FindControl<ComboBox>("SizeComboBox");
            var bgCombo = this.FindControl<ComboBox>("BackgroundComboBox");
            var lineCombo = this.FindControl<ComboBox>("LineColorComboBox");

            if (dropZone != null)
            {
                dropZone.AddHandler(DragDrop.DropEvent, Drop);
                dropZone.AddHandler(DragDrop.DragOverEvent, DragOver);
            }

            if (browseButton != null)
                browseButton.Click += BrowseButton_Click;

            if (saveButton != null)
                saveButton.Click += SaveButton_Click;

            if (clearButton != null)
                clearButton.Click += ClearButton_Click;

            if (thicknessSlider != null)
                thicknessSlider.PropertyChanged += async(s, e) =>
                {
                    if (e.Property.Name == "Value")
                    {
                        var text = this.FindControl<TextBlock>("ThicknessText");
                        if (text != null)
                            text.Text = $"{(int)thicknessSlider.Value} px";
                        if (_currentUVData != null)
                           await RegeneratePreview();
                    }
                };

            if (sizeCombo != null)
                sizeCombo.SelectionChanged += async (s, e) => { if (_currentUVData != null) await RegeneratePreview(); };

            if (bgCombo != null)
                bgCombo.SelectionChanged += async (s, e) => { if (_currentUVData != null) await RegeneratePreview(); };

            if (lineCombo != null)
                lineCombo.SelectionChanged += async (s, e) => { if (_currentUVData != null) await RegeneratePreview(); };
        }

        private void DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private async void Drop(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                var file = files?.FirstOrDefault();

                if (file != null)
                {
                    var path = file.Path.LocalPath;
                    if (Path.GetExtension(path).ToLower() == ".csv")
                    {
                        await ProcessCsvFile(path);
                    }
                    else
                    {
                        UpdateStatus("Please drop a CSV file", true);
                    }
                }
            }
        }

        private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("Browse button clicked");
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select UV Map CSV File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                fileName = Path.GetFileName(files[0].Path.LocalPath);
                await ProcessCsvFile(files[0].Path.LocalPath);
            }
        }

        private async Task ProcessCsvFile(string path)
        {
            try
            {
                UpdateStatus("Processing CSV file...");
                _currentCsvPath = path;

                _currentUVData = UVMapParser.ParseCsv(path);
                
               ShowActionPanel(true);
                await GenerateAndShowPreview();
                UpdateStatus($" Convertion is done");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", true);
            }
        }

        private async Task GenerateAndShowPreview()
        {
            if (_currentUVData == null) return;

            var size = GetSelectedSize();
            var bgColor = GetSelectedBackgroundColor();
            var lineColor = GetSelectedLineColor();
            var thickness = (int)(this.FindControl<Slider>("ThicknessSlider")?.Value ?? 2);
        
            var generator = new ImageGenerator();
            _currentBitmap = await Task.Run(() => 
                generator.GenerateImage(_currentUVData, size, bgColor, lineColor, thickness));

            var previewImage = this.FindControl<Image>("PreviewImage");
            if (previewImage != null)
                previewImage.Source = _currentBitmap;

            var dropZoneContent = this.FindControl<StackPanel>("DropZoneContent");
            var zoomBorder = this.FindControl<ZoomBorder>("ZoomBorder");
            var previewScroller = this.FindControl<ScrollViewer>("PreviewScroller");

            if (dropZoneContent != null)
                dropZoneContent.IsVisible = false;
            
            if (zoomBorder != null)
                zoomBorder.IsVisible = true;

            if (previewScroller != null)
                previewScroller.IsVisible = true;

           ShowActionPanel(true);
        }

        private async Task RegeneratePreview()
        {
            if (_currentUVData != null)
            {
                try
                {
                    UpdateStatus("Regenerating preview...");
                    await GenerateAndShowPreview();
                    UpdateStatus("Preview regenerated");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}", true);
                }
            }
        }

        private async void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_currentBitmap == null) return;

            var format = GetSelectedFormat();
            var extension = format.ToLower();
            
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save UV Map Image",
                SuggestedFileName = $"{fileName}.{extension}",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(format) { Patterns = new[] { $"*.{extension}" } }
                }
            });

            if (file != null)
            {
                try
                {
                    UpdateStatus("Saving image...");
                    await using var stream = await file.OpenWriteAsync();
                    
                    switch (extension)
                    {
                        case "png":
                            _currentBitmap.Save(stream);
                            break;
                        case "jpg":
                        case "jpeg":
                            _currentBitmap.Save(stream);
                            break;
                        case "bmp":
                            _currentBitmap.Save(stream);
                            break;
                        case "webp":
                            _currentBitmap.Save(stream);
                            break;
                    }

                    UpdateStatus($"Image saved successfully to {Path.GetFileName(file.Path.LocalPath)}");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error saving: {ex.Message}", true);
                }
            }
        }

        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentCsvPath = null;
            _currentUVData = null;
            _currentBitmap?.Dispose();
            _currentBitmap = null;

            var dropZoneContent = this.FindControl<StackPanel>("DropZoneContent");
            var previewScroller = this.FindControl<ScrollViewer>("PreviewScroller");
            var previewImage = this.FindControl<Image>("PreviewImage");
            var zoomBorder = this.FindControl<ZoomBorder>("ZoomBorder");

            if (dropZoneContent != null)
                dropZoneContent.IsVisible = true;

            if (previewScroller != null)
                previewScroller.IsVisible = false;

            if (previewImage != null)
                previewImage.Source = null;
                
            if (zoomBorder != null)
                zoomBorder.IsVisible = false;

           ShowActionPanel(false);
            UpdateStatus("Ready");
        }

        private void ShowActionPanel(bool show)
        {
           
            var actionPanel = this.FindControl<StackPanel>("ActionPanel");
            if (actionPanel != null) actionPanel.IsVisible = show;
           
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = message;
                statusText.Foreground = isError 
                    ? Avalonia.Media.Brushes.Red 
                    : Avalonia.Media.Brushes.White;
            }
        }

        private int GetSelectedSize()
        {
            var combo = this.FindControl<ComboBox>("SizeComboBox");
            return combo?.SelectedIndex switch
            {
                0 => 512,
                1 => 1024,
                2 => 2048,
                3 => 4096,
                _ => 2048
            };
        }

        private SkiaSharp.SKColor GetSelectedBackgroundColor()
        {
            var combo = this.FindControl<ComboBox>("BackgroundComboBox");
            return combo?.SelectedIndex switch
            {
                0 => SkiaSharp.SKColors.Transparent,
                1 => SkiaSharp.SKColors.White,
                2 => SkiaSharp.SKColors.Black,
                3 => SkiaSharp.SKColors.Gray,
                _ => SkiaSharp.SKColors.White
            };
        }

        private SkiaSharp.SKColor GetSelectedLineColor()
        {
            var combo = this.FindControl<ComboBox>("LineColorComboBox");
            return combo?.SelectedIndex switch
            {
                0 => SkiaSharp.SKColors.White,
                1 => SkiaSharp.SKColors.Black,
                2 => SkiaSharp.SKColors.Red,
                3 => SkiaSharp.SKColors.Blue,
                4 => SkiaSharp.SKColors.Green,
                _ => SkiaSharp.SKColors.Black
            };
        }

        private string GetSelectedFormat()
        {
            var combo = this.FindControl<ComboBox>("FormatComboBox");
            return combo?.SelectedIndex switch
            {
                0 => "png",
                1 => "jpg",
                2 => "bmp",
                3 => "webp",
                _ => "png"
            };
        }
    }
}