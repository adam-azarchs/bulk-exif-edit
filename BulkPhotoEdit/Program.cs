using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace BulkPhotoEdit
{
    class Program
    {
        class CmdLineOptions
        {
            [Option('o', "fix-orientation",
                HelpText = "Rotate all of the images according to their orientation exif data.")]
            public bool FixOrientation { get; set; }

            [Option('s', "shift-time",
                HelpText = "Adjust date taken exif data by the given amount of time.")]
            public string ShiftTime { get; set; }

            [ValueList(typeof(List<string>))]
            public List<string> FileNames { get; set; }

            [HelpOption]
            public string GetUsage()
            {
                // this without using CommandLine.Text
                //  or using HelpText.AutoBuild
                var usage = new StringBuilder();
                usage.AppendLine("Bulk photo edit");
                usage.AppendLine("Performs bulk edits on sets of JPEG images.");
                return usage.ToString();
            }
        }

        static void Main(string[] args)
        {
            var options = new CmdLineOptions();
            if (CommandLine.Parser.Default.ParseArguments(args, options) &&
                options.FileNames.Count > 0)
            {
                string[] filenames = ExpandWildcards(options.FileNames).ToArray();
                TimeSpan shift = TimeSpan.Zero;
                if (options.ShiftTime != null && options.ShiftTime.Length > 0)
                {
                    if (!TimeSpan.TryParse(options.ShiftTime, out shift))
                    {
                        Console.Error.WriteLine("Invalid timespan string {0}", options.ShiftTime);
                    }
                }
                FixOrientation(filenames, options.FixOrientation, shift);
            }
            else
            {
                // Display the default usage information
                Console.WriteLine(options.GetUsage());
            }
        }

        private static IEnumerable<string> ExpandWildcards(List<string> list)
        {
            foreach (string glob in list)
            {
                if (Path.IsPathRooted(glob))
                {
                    foreach (string filename in Directory.EnumerateFiles(Path.GetPathRoot(glob), glob))
                    {
                        yield return filename;
                    }
                }
                else
                {
                    foreach (string filename in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), glob))
                    {
                        yield return filename;
                    }
                }
            }
        }

        private static void FixOrientation(
            IEnumerable<string> filenames, bool fixOrientation, TimeSpan shift)
        {
            ImageManipulation manip = new ImageManipulation();
            foreach (string filename in filenames)
            {
                var rot = manip.AdjustImage(filename, fixOrientation, shift);
                if (rot.HasValue)
                {
                    Console.WriteLine("Rotated {0} by {1}", filename, rot.Value);
                }
                else
                {
                    Console.WriteLine("Processed {0}", filename);
                }
            }
        }
    }
}
