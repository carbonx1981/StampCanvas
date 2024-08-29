using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Stampify
{
    public partial class MainWindow : Window
    {
        private Image? _currentStamp; // The current image being worked on.
        private Image? _selectedImage; // The currently selected image on the canvas.
        private BitmapSource? _originalImageSource; // Store the original image source
        private Point _startPoint; // Starting point for drag/move.
        private bool _isDragging; // Flag to check if dragging is happening.
        private double _brightness = 0; // Current brightness level
        private double _contrast = 0; // Current contrast level
        private double _hue = 0; // Current hue level
        private double _saturation = 0; // Current saturation level

        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += MainWindow_KeyDown; // Add key down event for Ctrl+S, Delete, + and -
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                SaveComposition(); // Trigger the save functionality
                e.Handled = true; // Mark the event as handled
            }
            else if (e.Key == Key.Delete)
            {
                DeleteSelectedImage(); // Delete the selected image
                e.Handled = true; // Mark the event as handled
            }
            else if (e.Key == Key.OemPlus || e.Key == Key.Add) // Check for + key (OemPlus for main keyboard, Add for numpad)
            {
                MoveImageToFront();
                e.Handled = true; // Mark the event as handled
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) // Check for - key (OemMinus for main keyboard, Subtract for numpad)
            {
                MoveImageToBack();
                e.Handled = true; // Mark the event as handled
            }
        }

        private void MoveImageToFront()
        {
            if (_selectedImage != null)
            {
                // Increase ZIndex to bring the selected image to the front
                int currentZIndex = Canvas.GetZIndex(_selectedImage);
                Canvas.SetZIndex(_selectedImage, currentZIndex + 1);
                UpdateObjectInfo(); // Update the object info
            }
        }

        private void MoveImageToBack()
        {
            if (_selectedImage != null)
            {
                // Decrease ZIndex to send the selected image to the back
                int currentZIndex = Canvas.GetZIndex(_selectedImage);
                Canvas.SetZIndex(_selectedImage, currentZIndex - 1);
                UpdateObjectInfo(); // Update the object info
            }
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image image)
            {
                _selectedImage = image;
                _startPoint = e.GetPosition(MainCanvas);
                _isDragging = true;

                // Capture the mouse to track events even if they leave the image
                _selectedImage.CaptureMouse();
                UpdateObjectInfo(); // Update object info when an image is selected
            }
        }

        private void UpdateObjectInfo()
        {
            if (_selectedImage != null)
            {
                // Update Z-Order
                int zIndex = Canvas.GetZIndex(_selectedImage);
                ZOrderTextBox.Text = zIndex.ToString();

                // Update Position
                double left = Canvas.GetLeft(_selectedImage);
                double top = Canvas.GetTop(_selectedImage);
                PositionTextBox.Text = $"X: {left}, Y: {top}";

                // Update Size
                double width = _selectedImage.Width;
                double height = _selectedImage.Height;
                SizeTextBox.Text = $"Width: {width}, Height: {height}";
            }
            else
            {
                ZOrderTextBox.Text = "";
                PositionTextBox.Text = "";
                SizeTextBox.Text = "";
            }
        }

        private void LoadImagesButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                foreach (string filePath in openFileDialog.FileNames)
                {
                    LoadImage(filePath);
                }
            }
        }

        private void LoadImage(string imagePath)
        {
            Image image = new Image();
            image.Source = new BitmapImage(new Uri(imagePath));
            image.Width = 100; // Thumbnail size
            ImageLibrary.Items.Add(image);
        }

        private void SaveComposition_Click(object sender, RoutedEventArgs e)
        {
            SaveComposition(); // Call the save logic method
        }

        private void SaveComposition()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG Files|*.png|JPEG Files|*.jpg|Bitmap Files|*.bmp",
                DefaultExt = "png",
                AddExtension = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    (int)MainCanvas.ActualWidth, (int)MainCanvas.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);
                MainCanvas.Measure(new Size((int)MainCanvas.ActualWidth, (int)MainCanvas.ActualHeight));
                MainCanvas.Arrange(new Rect(new Size((int)MainCanvas.ActualWidth, (int)MainCanvas.ActualHeight)));
                renderBitmap.Render(MainCanvas);

                BitmapEncoder encoder = null;
                if (filePath.EndsWith(".png"))
                    encoder = new PngBitmapEncoder();
                else if (filePath.EndsWith(".jpg"))
                    encoder = new JpegBitmapEncoder();
                else if (filePath.EndsWith(".bmp"))
                    encoder = new BmpBitmapEncoder();

                if (encoder != null)
                {
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }
                }
            }
        }

        private void ImageLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageLibrary.SelectedItem is Image selectedImage)
            {
                _currentStamp = selectedImage;
                _selectedImage = selectedImage;
                _originalImageSource = _selectedImage.Source as BitmapSource; // Store the original image
            }
        }

        private void ImageLibrary_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is Image selectedImage)
            {
                DragDrop.DoDragDrop(listBox, selectedImage, DragDropEffects.Copy);
            }
        }

        private void MainCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void MainCanvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Image)))
            {
                Image draggedImage = e.Data.GetData(typeof(Image)) as Image;

                if (draggedImage != null)
                {
                    // Create a new image to place on the canvas
                    Image newImage = new Image
                    {
                        Source = draggedImage.Source,
                        Width = draggedImage.Width,
                        Height = draggedImage.Height,
                        RenderTransformOrigin = new Point(0.5, 0.5), // Center transformations
                        RenderTransform = new TransformGroup
                        {
                            Children = new TransformCollection
                            {
                                new RotateTransform(0), // Start with no rotation
                                new ScaleTransform(1, 1), // Start with no scaling
                                new SkewTransform(0, 0) // Start with no skew
                            }
                        }
                    };

                    // Attach event handlers for moving, resizing, rotating, and deleting
                    newImage.MouseLeftButtonDown += Image_MouseLeftButtonDown;
                    newImage.MouseRightButtonDown += Image_MouseRightButtonDown;
                    newImage.MouseMove += Image_MouseMove;
                    newImage.MouseLeftButtonUp += Image_MouseLeftButtonUp;
                    newImage.MouseRightButtonUp += Image_MouseRightButtonUp;

                    Point dropPosition = e.GetPosition(MainCanvas);
                    Canvas.SetLeft(newImage, dropPosition.X);
                    Canvas.SetTop(newImage, dropPosition.Y);

                    MainCanvas.Children.Add(newImage);
                    _selectedImage = newImage; // Select the new image
                    _originalImageSource = newImage.Source as BitmapSource; // Store the original image
                }
            }
        }

        private void Image_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image image)
            {
                _selectedImage = image;
                _startPoint = e.GetPosition(MainCanvas);
                _isDragging = true;

                // Capture the mouse to track events even if they leave the image
                _selectedImage.CaptureMouse();
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (_selectedImage == null || !_isDragging)
                return;

            Point currentPoint = e.GetPosition(MainCanvas);
            double deltaX = currentPoint.X - _startPoint.X;
            double deltaY = currentPoint.Y - _startPoint.Y;

            TransformGroup transformGroup = _selectedImage.RenderTransform as TransformGroup;
            RotateTransform rotateTransform = transformGroup.Children[0] as RotateTransform;
            ScaleTransform scaleTransform = transformGroup.Children[1] as ScaleTransform;
            SkewTransform skewTransform = transformGroup.Children[2] as SkewTransform;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    // Rotate Mode: Rotate the image
                    rotateTransform.Angle += deltaX / 5; // Rotate based on mouse movement
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    // Resize Mode: Uniform resize (equal zoom)
                    double scaleChange = Math.Max(0.1, 1 + deltaX / 200);
                    scaleTransform.ScaleX *= scaleChange;
                    scaleTransform.ScaleY *= scaleChange;
                }
                else
                {
                    // Move Mode: Move the image
                    double left = Canvas.GetLeft(_selectedImage) + deltaX;
                    double top = Canvas.GetTop(_selectedImage) + deltaY;

                    Canvas.SetLeft(_selectedImage, left);
                    Canvas.SetTop(_selectedImage, top);
                }
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    // Rotate Mode with Right Click: Rotate the image
                    rotateTransform.Angle += deltaX / 5; // Rotate based on mouse movement
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    // Skew Mode with Right Click: Skew the image
                    skewTransform.AngleX += deltaX / 5; // Skew along the X-axis
                    skewTransform.AngleY += deltaY / 5; // Skew along the Y-axis
                }
                else if (Keyboard.IsKeyDown(Key.LeftAlt))
                {
                    // Mirror Mode with Right Click: Flip the image horizontally
                    scaleTransform.ScaleX *= -1; // Flip the image horizontally
                    _isDragging = false; // Stop dragging after flipping
                }
                else
                {
                    // Uneven Resize with Right Click: Resize width and height independently
                    scaleTransform.ScaleX = Math.Max(0.1, scaleTransform.ScaleX + deltaX / 200); // Resize width
                    scaleTransform.ScaleY = Math.Max(0.1, scaleTransform.ScaleY + deltaY / 200); // Resize height
                }
            }

            _startPoint = currentPoint; // Update start point for next move
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _selectedImage?.ReleaseMouseCapture(); // Release mouse capture
        }

        private void Image_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _selectedImage?.ReleaseMouseCapture(); // Release mouse capture
        }

        // Adjust Brightness
        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _brightness = e.NewValue / 10.0; // Smaller scaling for smoother changes
            ApplyAdjustments();
        }

        // Adjust Contrast
        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _contrast = e.NewValue / 10.0; // Smaller scaling for smoother changes
            ApplyAdjustments();
        }

        // Adjust Hue
        private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _hue = e.NewValue; // Hue adjustment in degrees
            ApplyAdjustments();
        }

        // Adjust Saturation
        private void SaturationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _saturation = e.NewValue; // Saturation adjustment from -1 to 1
            ApplyAdjustments();
        }

        // Function to Apply Adjustments Based on Original Data
        private void ApplyAdjustments()
        {
            if (_selectedImage == null || _originalImageSource == null)
                return;

            // Start with a fresh copy of the original image data
            var width = _originalImageSource.PixelWidth;
            var height = _originalImageSource.PixelHeight;
            var stride = width * 4;
            byte[] pixelData = new byte[height * stride];
            _originalImageSource.CopyPixels(pixelData, stride, 0);

            // Ensure pixel format is correct
            PixelFormat format = _originalImageSource.Format;
            if (format != PixelFormats.Bgra32 && format != PixelFormats.Pbgra32)
            {
                // Convert to a compatible format
                FormatConvertedBitmap converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = _originalImageSource;
                converted.DestinationFormat = PixelFormats.Bgra32;
                converted.EndInit();
                _originalImageSource = converted;
                _originalImageSource.CopyPixels(pixelData, stride, 0);
            }

            for (int i = 0; i < pixelData.Length; i += 4)
            {
                // Get original color values
                double red = pixelData[i + 2] / 255.0;
                double green = pixelData[i + 1] / 255.0;
                double blue = pixelData[i] / 255.0;

                // Apply brightness
                red = ApplyBrightness(red, _brightness);
                green = ApplyBrightness(green, _brightness);
                blue = ApplyBrightness(blue, _brightness);

                // Apply contrast
                red = ApplyContrast(red, _contrast);
                green = ApplyContrast(green, _contrast);
                blue = ApplyContrast(blue, _contrast);

                // Convert RGB to HSL
                double hue, saturation, lightness;
                RGBtoHSL(red, green, blue, out hue, out saturation, out lightness);

                // Apply hue and saturation adjustments
                hue = (hue + _hue / 360.0) % 1.0; // Adjust hue (normalize to 0-1 range)
                saturation = Math.Max(0, Math.Min(1, saturation * (1 + _saturation))); // Adjust saturation

                // Convert HSL back to RGB
                HSLtoRGB(hue, saturation, lightness, out red, out green, out blue);

                // Clamp values
                red = Math.Max(0, Math.Min(1, red));
                green = Math.Max(0, Math.Min(1, green));
                blue = Math.Max(0, Math.Min(1, blue));

                // Set adjusted values back
                pixelData[i + 2] = (byte)(red * 255);
                pixelData[i + 1] = (byte)(green * 255);
                pixelData[i] = (byte)(blue * 255);
            }

            // Create new BitmapSource from adjusted pixel data
            var newBitmap = BitmapSource.Create(width, height, _originalImageSource.DpiX, _originalImageSource.DpiY, PixelFormats.Bgra32, null, pixelData, stride);
            _selectedImage.Source = newBitmap;
        }

        // Apply brightness adjustment
        private double ApplyBrightness(double color, double brightness)
        {
            return Math.Max(0, Math.Min(1, color + brightness));
        }

        // Apply contrast adjustment
        private double ApplyContrast(double color, double contrast)
        {
            return ((color - 0.5) * Math.Max(contrast + 1, 0)) + 0.5;
        }

        // Convert RGB to HSL
        private void RGBtoHSL(double r, double g, double b, out double h, out double s, out double l)
        {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0; // achromatic
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
                if (max == r)
                {
                    h = (g - b) / d + (g < b ? 6 : 0);
                }
                else if (max == g)
                {
                    h = (b - r) / d + 2;
                }
                else
                {
                    h = (r - g) / d + 4;
                }
                h /= 6;
            }
        }

        // Convert HSL back to RGB
        private void HSLtoRGB(double h, double s, double l, out double r, out double g, out double b)
        {
            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                Func<double, double, double, double> hue2rgb = (p, q, t) =>
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1 / 6.0) return p + (q - p) * 6 * t;
                    if (t < 1 / 2.0) return q;
                    if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6;
                    return p;
                };

                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = hue2rgb(p, q, h + 1 / 3.0);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1 / 3.0);
            }
        }

        // Delete the selected image from the canvas
        private void DeleteSelectedImage()
        {
            if (_selectedImage != null)
            {
                MainCanvas.Children.Remove(_selectedImage); // Remove the image from the canvas
                _selectedImage = null; // Clear the selected image reference
                UpdateObjectInfo(); // Clear the object info when image is deleted
            }
        }

        // File Menu Handlers
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Export functionality is not yet implemented.");
        }

        // Edit Menu Handlers
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Undo functionality is not yet implemented.");
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Redo functionality is not yet implemented.");
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("History functionality is not yet implemented.");
        }

        // View Menu Handlers
        private void ToggleGrids_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Toggle grids functionality is not yet implemented.");
        }

        private void ToggleGuides_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Toggle guides functionality is not yet implemented.");
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Zoom in functionality is not yet implemented.");
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Zoom out functionality is not yet implemented.");
        }

        // Image Menu Handlers
        private void OpenColorAdjustments_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open color adjustments functionality is not yet implemented.");
        }

        private void OpenFilters_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open filters functionality is not yet implemented.");
        }

        private void OpenEffects_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open effects functionality is not yet implemented.");
        }

        // Layers Menu Handlers
        private void OpenLayerManagement_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open layer management functionality is not yet implemented.");
        }

        private void OpenBlendModes_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open blend modes functionality is not yet implemented.");
        }

        private void OpenClipping_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open clipping functionality is not yet implemented.");
        }

        private void OpenMasking_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open masking functionality is not yet implemented.");
        }

        // Text Menu Handlers
        private void AddText_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Add text functionality is not yet implemented.");
        }

        private void OpenFontSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open font settings functionality is not yet implemented.");
        }

        // Help Menu Handlers
        private void OpenUserGuide_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open user guide functionality is not yet implemented.");
        }

        private void OpenAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("About functionality is not yet implemented.");
        }

        // Layers Tab Handlers
        private void AddLayer_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Add layer functionality is not yet implemented.");
        }

        private void DeleteLayer_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Delete layer functionality is not yet implemented.");
        }

        // Filters and Effects Tab Handlers
        private void ApplyBlur_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Apply blur functionality is not yet implemented.");
        }

        private void ApplySharpen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Apply sharpen functionality is not yet implemented.");
        }
    } // End of class MainWindow
} // End of namespace Stampify
