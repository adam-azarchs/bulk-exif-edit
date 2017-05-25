using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BulkPhotoEdit {
    class Program {
        class CmdLineOptions {
            [Option('o', "fix-orientation",
                HelpText = "Rotate all of the images according to their orientation exif data.")]
            public bool FixOrientation {
                get; set;
            }

            [Option('s', "shift-time",
                HelpText = "Adjust date taken exif data by the given amount of time.")]
            public string ShiftTime {
                get; set;
            }

            [Option('g', "geotag",
                HelpText = "Tag the photos with the given latitude,longitude.")]
            public string LatLon {
                get; set;
            }

            [Option('r', "resolution",
                HelpText = "Set the image resolution to the given DPI.")]
            public float Resolution {
                get; set;
            }

            [ValueList(typeof(List<string>))]
            public List<string> FileNames {
                get; set;
            }

            [HelpOption]
            public string GetUsage() {
                // this without using CommandLine.Text
                //  or using HelpText.AutoBuild
                var usage = new StringBuilder();
                usage.AppendLine("Bulk photo edit");
                usage.AppendLine("Performs bulk edits on sets of JPEG images.");
                return usage.ToString();
            }
        }

        static void Main(string[] args) {
            var options = new CmdLineOptions();
            if (Parser.Default.ParseArguments(args, options) &&
                options.FileNames.Count > 0) {
                string[] filenames = expandWildcards(options.FileNames).ToArray();
                TimeSpan shift = TimeSpan.Zero;
                if (options.ShiftTime != null && options.ShiftTime.Length > 0) {
                    if (!TimeSpan.TryParse(options.ShiftTime, out shift)) {
                        Console.Error.WriteLine("Invalid timespan string {0}", options.ShiftTime);
                    }
                }
                Coordinates? coords = null;
                if (options.LatLon != null && options.LatLon.Length > 0) {
                    coords = Coordinates.TryParse(options.LatLon);
                    if (coords == null) {
                        Console.Error.WriteLine("Invalid coordinates string {0}", options.LatLon);
                    }
                }
                fixOrientation(filenames, options.FixOrientation,
                    options.Resolution, shift, coords);
            } else {
                // Display the default usage information
                Console.WriteLine(options.GetUsage());
            }
        }

        private static IEnumerable<string> expandWildcards(List<string> list) {
            foreach (string glob in list) {
                if (Path.IsPathRooted(glob)) {
                    foreach (string filename in Directory.EnumerateFiles(Path.GetPathRoot(glob), glob)) {
                        yield return filename;
                    }
                } else {
                    foreach (string filename in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), glob)) {
                        yield return filename;
                    }
                }
            }
        }

        private static void fixOrientation(
            IEnumerable<string> filenames, bool fixOrientation,
            float resolution, TimeSpan shift, Coordinates? coords) {
            ImageManipulation manip = new ImageManipulation();
            foreach (string filename in filenames) {
                var rot = manip.AdjustImage(filename, fixOrientation, resolution, shift, coords);
                if (rot.HasValue) {
                    Console.WriteLine("Rotated {0} by {1}", filename, rot.Value);
                } else {
                    Console.WriteLine("Processed {0}", filename);
                }
            }
        }
    }
}
