#region License
/*
Copyright © Joan Charmant 2010.
jcharmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// A static class with hepler functions related to Images, conversions, etc.
    /// </summary>
    public static class ImageHelper
    {
        public static void Save(string filename, Bitmap image)
        {
            string directory = Path.GetDirectoryName(filename);
            if(!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
                
            string filenameToLower = filename.ToLower();
            
            if (filenameToLower.EndsWith("jpg") || filenameToLower.EndsWith("jpeg"))
            {
                Bitmap jpgImage = ImageHelper.ConvertToJPG(image, 100);
                jpgImage.Save(filename, ImageFormat.Jpeg);
                jpgImage.Dispose();
            }
            else if (filenameToLower.EndsWith("bmp"))
            {
                image.Save(filename, ImageFormat.Bmp);
            }
            else if (filenameToLower.EndsWith("png"))
            {
                image.Save(filename, ImageFormat.Png);
            }
            else
            {
                // the user may have put a filename in the form : "filename.ext"
                // where ext is unsupported. Or he misunderstood and put ".00.00"
                // We force format to jpg and we change back the extension to ".jpg".
                string newFilename = Path.GetDirectoryName(filename) + "\\" + Path.GetFileNameWithoutExtension(filename) + ".jpg";

                Bitmap jpgImage = ImageHelper.ConvertToJPG(image, 100);
                jpgImage.Save(newFilename, ImageFormat.Jpeg);
                jpgImage.Dispose();
            }
        }
        public static Bitmap ConvertToJPG(Bitmap image, long quality)
        {
            MemoryStream memStr = new MemoryStream();
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            ImageCodecInfo ici = null;
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == "image/jpeg")
                {
                    ici = codec;
                    break;
                }
            }

            if (ici != null)
            {
                //Create a collection of encoder parameters (we only need one in the collection)
                EncoderParameters ep = new EncoderParameters();
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);

                image.Save(memStr, ici, ep);
            }
            else
            {
                // No JPG encoder found (is that common ?) Use default system.
                image.Save(memStr, ImageFormat.Jpeg);
            }

            return new Bitmap(memStr);
        }
        public static Bitmap GetSideBySideComposite(Bitmap leftImage, Bitmap rightImage, bool video, bool horizontal)
        {
            IList<Bitmap> images = new Bitmap[] { leftImage, rightImage };
            IList<int> slotIndices = new int[] { 0, 1 };
            return GetComposite(images, slotIndices, video, horizontal ? 2 : 1, horizontal ? 1 : 2);
        }

        public static Bitmap GetComposite(IList<Bitmap> images, int screenCount, bool video)
        {
            if (images == null)
                throw new ArgumentNullException("images");

            int[] slotIndices = new int[images.Count];
            for (int i = 0; i < slotIndices.Length; i++)
                slotIndices[i] = i;

            return GetComposite(images, slotIndices, screenCount, video);
        }

        public static Bitmap GetComposite(IList<Bitmap> images, IList<int> slotIndices, int screenCount, bool video)
        {
            int columns;
            int rows;
            ScreenLayoutSpec.GetDefaultGrid(screenCount, out columns, out rows);
            return GetComposite(images, slotIndices, screenCount, columns, rows, video);
        }

        public static Bitmap GetComposite(IList<Bitmap> images, IList<int> slotIndices, int screenCount, int columns, int rows, bool video)
        {
            ValidateCompositeArguments(images, slotIndices, screenCount);

            if (columns <= 0 || rows <= 0 || columns * rows != screenCount)
                ScreenLayoutSpec.GetDefaultGrid(screenCount, out columns, out rows);

            return GetComposite(images, slotIndices, video, columns, rows);
        }

        private static Bitmap GetComposite(IList<Bitmap> images, IList<int> slotIndices, bool video, int columns, int rows)
        {
            int cellWidth = 0;
            int cellHeight = 0;

            for (int i = 0; i < images.Count; i++)
            {
                Bitmap image = images[i];
                if (image == null)
                    continue;

                cellWidth = Math.Max(cellWidth, image.Width);
                cellHeight = Math.Max(cellHeight, image.Height);
            }

            if (cellWidth == 0 || cellHeight == 0)
            {
                cellWidth = 1;
                cellHeight = 1;
            }

            int width = cellWidth * columns;
            int height = cellHeight * rows;
            if (video)
            {
                if (height % 2 != 0)
                    height++;
                if (width % 4 != 0)
                    width += 4 - width % 4;
            }

            Bitmap composite = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(composite))
            {
                graphics.Clear(Color.Black);

                for (int i = 0; i < images.Count; i++)
                {
                    Bitmap image = images[i];
                    if (image == null)
                        continue;

                    int slotIndex = slotIndices[i];
                    int column = slotIndex % columns;
                    int row = slotIndex / columns;
                    int left = column * cellWidth + (cellWidth - image.Width) / 2;
                    int top = row * cellHeight + (cellHeight - image.Height) / 2;
                    graphics.DrawImageUnscaled(image, left, top);
                }
            }

            return composite;
        }

        public static Bitmap GetOverlayComposite(IList<Bitmap> images)
        {
            if (images == null || images.Count == 0)
                return null;

            int width = 0;
            int height = 0;
            foreach (Bitmap image in images)
            {
                if (image == null)
                    continue;
                width = Math.Max(width, image.Width);
                height = Math.Max(height, image.Height);
            }

            if (width == 0 || height == 0)
                return null;

            Bitmap composite = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(composite))
            {
                graphics.Clear(Color.Black);
                int validCount = 0;
                foreach (Bitmap image in images)
                {
                    if (image != null)
                        validCount++;
                }

                float alpha = validCount > 0 ? 1.0f / validCount : 1.0f;
                foreach (Bitmap image in images)
                {
                    if (image == null)
                        continue;

                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        ColorMatrix matrix = new ColorMatrix();
                        matrix.Matrix33 = alpha;
                        attributes.SetColorMatrix(matrix);
                        graphics.DrawImage(image, new Rectangle(0, 0, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }
                }
            }

            return composite;
        }

        private static void ValidateCompositeArguments(IList<Bitmap> images, IList<int> slotIndices, int screenCount)
        {
            if (images == null)
                throw new ArgumentNullException("images");
            if (slotIndices == null)
                throw new ArgumentNullException("slotIndices");
            if (images.Count != slotIndices.Count)
                throw new ArgumentException("Images and slot indices must have the same count.");
            if (screenCount < 1 || screenCount > 4)
                throw new ArgumentOutOfRangeException("screenCount", "Screen count must be between 1 and 4.");

            bool[] usedSlots = new bool[screenCount];
            for (int i = 0; i < slotIndices.Count; i++)
            {
                int slotIndex = slotIndices[i];
                if (slotIndex < 0 || slotIndex >= screenCount)
                    throw new ArgumentOutOfRangeException("slotIndices", "A slot index is outside the layout.");
                if (usedSlots[slotIndex])
                    throw new ArgumentException("Slot indices must be unique.", "slotIndices");

                usedSlots[slotIndex] = true;
            }
        }
    }
}
