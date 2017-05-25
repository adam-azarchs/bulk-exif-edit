using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BulkPhotoEdit {
    public struct Coordinates {
        public double Latitude;
        public double Longitude;

        public static Coordinates? TryParse(string from) {
            string[] strings = from.Split(',');
            if (strings.Length != 2) {
                return null;
            }
            Coordinates parsed;
            if (!double.TryParse(strings[0], out parsed.Latitude)) {
                return null;
            }
            if (!double.TryParse(strings[1], out parsed.Longitude)) {
                return null;
            }
            return parsed;
        }

        public override string ToString() {
            return String.Format("{0:0.000000},{1:0.000000}",
                Latitude, Longitude);
        }
    }

    public class ImageManipulation {
        private ImageCodecInfo jpegCodecInfo = getJpegCodec();
        private static ImageCodecInfo getJpegCodec() {
            return ImageCodecInfo.GetImageEncoders()
                .First(enc => enc.MimeType == "image/jpeg");
        }

        public DateTime ReadDate(PropertyItem prop) {
            string dateString = Encoding.ASCII.GetString(prop.Value);
            return DateTime.ParseExact(dateString,
                ExifDateFormat, CultureInfo.InvariantCulture);
        }

        public void WriteDate(PropertyItem prop, DateTime date) {
            prop.Value = Encoding.ASCII.GetBytes(date.ToString(
                ExifDateFormat, CultureInfo.InvariantCulture));
        }

        static readonly byte[] North = { (byte)'N', 0 };
        static readonly byte[] South = { (byte)'S', 0 };
        static readonly byte[] East = { (byte)'E', 0 };
        static readonly byte[] West = { (byte)'W', 0 };

        private byte[] degreesToRational(double degrees) {
            degrees = Math.Abs(degrees);
            UInt32 deg = (UInt32)Math.Floor(degrees);
            double minutes = (degrees - deg) * 60.0;
            UInt32 min = (UInt32)Math.Floor(minutes);
            double seconds = (minutes - min) * 60.0;
            const UInt32 degreesFraction = 1;
            const UInt32 secondsFraction = 1 << 20;
            UInt32 sec = (UInt32)Math.Round(seconds * secondsFraction);
            return BitConverter.GetBytes(deg)
                .Concat(BitConverter.GetBytes(degreesFraction))
                .Concat(BitConverter.GetBytes(min))
                .Concat(BitConverter.GetBytes(degreesFraction))
                .Concat(BitConverter.GetBytes(sec))
                .Concat(BitConverter.GetBytes(secondsFraction)).ToArray();
        }

        private byte[] toRational(float number) {
            UInt32 denominator = 10000;
            UInt32 numerator = (UInt32)Math.Round(number * denominator);
            return BitConverter.GetBytes(numerator).Concat(
                BitConverter.GetBytes(denominator)).ToArray();
        }

        private void setProp(Image image, int propId, short type, byte[] value) {
            var prop = image.PropertyItems.First();
            prop.Id = propId;
            prop.Type = type;
            prop.Value = value;
            prop.Len = value.Length;
            image.SetPropertyItem(prop);
        }

        const short ExifTypeAscii = 2;
        const short ExifTypeShort = 3;
        const short ExifTypeRational = 5;

        public void WriteLatLon(Image image, Coordinates coordinates) {
            const int latRefId = 0x0001;
            setProp(image, latRefId, ExifTypeAscii,
                coordinates.Latitude >= 0 ? North : South);

            const int latId = 0x0002;
            setProp(image, latId, ExifTypeRational,
                degreesToRational(coordinates.Latitude));

            const int lonRefId = 0x0003;
            setProp(image, lonRefId, ExifTypeAscii,
                coordinates.Longitude >= 0 ? East : West);

            const int lonId = 0x0004;
            setProp(image, lonId, ExifTypeRational,
                degreesToRational(coordinates.Longitude));
        }

        const string ExifDateFormat = "yyyy:MM:dd HH:mm:ss\0";
        const int DateTakenID = 0x9003;
        const int OrientationPropID = 0x0112;
        const int HorizontalResPropID = 0x011A;
        const int VerticalResPropID = 0x011B;
        const int ResolutionUnitPropID = 0x0128;
        const short ResolutionUnitInches = 2;

        public EncoderValue? AdjustImage(string filename, bool fixOrientation,
            float resolution, TimeSpan shift, Coordinates? coordinates) {
            string newFileName = null;
            EncoderValue? transform = null;
            using (Bitmap image = new Bitmap(filename)) {
                PropertyItem orientationProp = null;
                if (fixOrientation) {
                    try {
                        orientationProp = image.GetPropertyItem(OrientationPropID);
                    } catch (ArgumentException) {
                        orientationProp = null;
                    }
                    if (orientationProp != null) {
                        transform = orientation(orientationProp);
                    }
                }
                if (transform.HasValue ||
                    shift.Duration() > TimeSpan.Zero ||
                    coordinates.HasValue ||
                    image.HorizontalResolution != resolution ||
                    image.VerticalResolution != resolution) {
                    if (orientationProp != null) {
                        orientationProp.Value = BitConverter.GetBytes((Int16)1);
                        image.SetPropertyItem(orientationProp);
                    }
                    if (shift.Duration() > TimeSpan.Zero) {
                        var dateTakeProp = image.GetPropertyItem(DateTakenID);
                        WriteDate(dateTakeProp, ReadDate(dateTakeProp) + shift);
                        image.SetPropertyItem(dateTakeProp);
                    }
                    if (coordinates.HasValue) {
                        WriteLatLon(image, coordinates.Value);
                    }
                    if (resolution > 0) {
                        setProp(image, HorizontalResPropID, ExifTypeRational, toRational(resolution));
                        setProp(image, VerticalResPropID, ExifTypeRational, toRational(resolution));
                        setProp(image, ResolutionUnitPropID, ExifTypeShort, BitConverter.GetBytes(ResolutionUnitInches));
                    }
                    newFileName = Path.GetTempFileName();
                    if (transform.HasValue) {
                        var encParams = new EncoderParameters(1);
                        encParams.Param[0] = new EncoderParameter(
                            System.Drawing.Imaging.Encoder.Transformation,
                            (long)transform.Value);
                        image.Save(newFileName, jpegCodecInfo, encParams);
                    } else {
                        image.Save(newFileName);
                    }
                }
            }
            if (newFileName != null) {
                File.Delete(filename);
                File.Move(newFileName, filename);
            }
            return transform;
        }

        private EncoderValue? orientation(PropertyItem prop) {
            switch (BitConverter.ToInt16(prop.Value, 0)) {
                case 1:
                    return null;
                case 2:
                    return EncoderValue.TransformFlipHorizontal;
                case 3:
                    return EncoderValue.TransformRotate180;
                case 4:
                    return EncoderValue.TransformFlipVertical;
                case 6:
                    return EncoderValue.TransformRotate90;
                case 8:
                    return EncoderValue.TransformRotate270;
                default:
                    throw new NotImplementedException("Can't rotate and flip...");
            }
        }
    }
}
