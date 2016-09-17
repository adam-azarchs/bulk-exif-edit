using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;

namespace BulkPhotoEdit
{
    class ImageManipulation
    {
        private ImageCodecInfo jpegCodecInfo = GetJpegCodec();
        private static ImageCodecInfo GetJpegCodec()
        {
            return ImageCodecInfo.GetImageEncoders()
                .First(enc => enc.MimeType == "image/jpeg");
        }

        public DateTime ReadDate(PropertyItem prop)
        {
            string dateString = Encoding.ASCII.GetString(prop.Value);
            return DateTime.ParseExact(dateString,
                exifDateFormat, CultureInfo.InvariantCulture);
        }

        public void WriteDate(PropertyItem prop, DateTime date)
        {
            prop.Value = Encoding.ASCII.GetBytes(date.ToString(
                exifDateFormat, CultureInfo.InvariantCulture));
        }

        const string exifDateFormat = "yyyy:MM:dd HH:mm:ss\0";
        const int DateTakenID = 0x9003;
        const int OrientationPropID = 0x0112;

        public EncoderValue? AdjustImage(string filename, bool fixOrientation, TimeSpan shift)
        {
            string newFileName = null;
            EncoderValue? transform = null;
            using (Image image = Bitmap.FromFile(filename))
            {
                var orientationProp = image.GetPropertyItem(OrientationPropID);
                if (fixOrientation)
                {
                    transform = Orientation(orientationProp);
                }
                if (transform.HasValue || shift.Duration() > TimeSpan.Zero)
                {
                    orientationProp.Value = BitConverter.GetBytes((Int16)1);
                    image.SetPropertyItem(orientationProp);
                    if (shift.Duration() > TimeSpan.Zero)
                    {
                        var dateTakeProp = image.GetPropertyItem(DateTakenID);
                        WriteDate(dateTakeProp, ReadDate(dateTakeProp) + shift);
                        image.SetPropertyItem(dateTakeProp);
                    }
                    newFileName = Path.GetTempFileName();
                    if (transform.HasValue)
                    {
                        var encParams = new EncoderParameters(1);
                        encParams.Param[0] = new EncoderParameter(
                            System.Drawing.Imaging.Encoder.Transformation,
                            (long)transform.Value);
                        image.Save(newFileName, jpegCodecInfo, encParams);
                    }
                    else
                    {
                        image.Save(newFileName);
                    }
                }
            }
            if (newFileName != null)
            {
                File.Delete(filename);
                File.Move(newFileName, filename);
            }
            return transform;
        }

        private EncoderValue? Orientation(PropertyItem prop)
        {
            switch (BitConverter.ToInt16(prop.Value, 0))
            {
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
