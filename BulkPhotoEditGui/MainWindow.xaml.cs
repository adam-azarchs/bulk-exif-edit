using BulkPhotoEdit;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace BulkPhotoEditGui {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private async void editImages(object sender, RoutedEventArgs e) {
            var dialog = new OpenFileDialog {
                Multiselect = true,
                CheckFileExists = true,
                ValidateNames = true,
                Filter = "Jpeg images|*.jpg;*.jpeg",
                Title = "Choose files to modify..."
            };
            if ((dialog.ShowDialog(this) ?? false) &&
                dialog.FileNames.Length >= 1) {
                string[] filenames = dialog.FileNames;
                TimeSpan shift = TimeSpan.Zero;
                if (AdjustTimesCheckbox.IsChecked ?? false) {
                    if (!TimeSpan.TryParse(AdjustByText.Text, out shift)) {
                        MessageBox.Show("Can't parse timespan.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                Coordinates? coords = null;
                if (GeotagCheckbox.IsChecked ?? true) {
                    if (GeotagText.Text == null || GeotagText.Text.Length == 0) {
                        MessageBox.Show("Invalid geotag.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    coords = Coordinates.TryParse(GeotagText.Text);
                    if (coords == null) {
                        MessageBox.Show("Can't parse geotag.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                bool rotate = FixOrientationCheckbox.IsChecked ?? true;
                float resolution = -1;
                if (SetResolutionCheckbox.IsChecked ?? true) {
                    if (string.IsNullOrWhiteSpace(ResolutionText.Text)) {
                        MessageBox.Show("Invalid resolution.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (!float.TryParse(ResolutionText.Text, out resolution)) {
                        MessageBox.Show("Can't parse resolution.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                await processImages(filenames, rotate, resolution, shift,
                    coords);
            }
        }

        private async Task processImages(string[] filenames, bool rotate,
            float resolution, TimeSpan shift, Coordinates? coords) {
            EditProgress.Value = 0;
            EditProgress.Maximum = filenames.Length;
            ImageManipulation manip = new ImageManipulation();
            for (int i = 0; i < filenames.Length; ++i) {
                this.ProcessingStatus.Content = String.Format(
                    "Processing {0}...", filenames[i]);
                await Task.Factory.StartNew(() =>
                    manip.AdjustImage(filenames[i], rotate, resolution,
                                      shift, coords));
                EditProgress.Value = i + 1;
                this.ProcessingStatus.Content = String.Format(
                    "Processing {0}... done.", filenames[i]);
            }
        }
    }
}
