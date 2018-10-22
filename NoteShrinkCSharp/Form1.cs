using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;
using Accord.Imaging;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private Bitmap WorkingBmp;
        private int num_colors = 8;
        private double value_threshold = 0.25;
        private double sat_threshold = 0.1;
        private Random rng = new Random();

        public Form1()
        {
            InitializeComponent();
            var inFile = @"Pix\notesA1.jpg";
            WorkingBmp = (Bitmap)System.Drawing.Image.FromFile(inFile);
            ImageCleanup(WorkingBmp);
        }

        private void ImageCleanup(Bitmap inBmp)
        {
            //https://github.com/mzucker/noteshrink

            // Test pack_rgb and unpack_rgb
            //var im = new List<byte[]>()
            //{
            //    new byte[] { 230, 230, 226 },
            //    new byte[] { 242, 238, 234 }
            //};
            //var pck = pack_rgb(im);
            //var upck0 = unpack_rgb(pck[0]);
            //var upck1 = unpack_rgb(pck[1]);

            //img = np.array(pil_img)
            var target = new Accord.Imaging.Converters.ImageToMatrix(0, 255);
            byte[][][] npimage;

            target.Convert(inBmp, out npimage);

            var samples = sample_pixels(npimage);
            var palette = get_palette(samples);
            var labels = apply_palette(npimage, palette);
            save("foo.png", labels, palette);
        }

        private List<byte[]> sample_pixels(byte[][][] img)
        {
            //def sample_pixels(img, options):
            //'''Pick a fixed percentage of pixels in the image, returned in random order.'''
            var pixels = img.SelectMany(x => x).ToArray();
            var num_pixels = pixels.Length;
            var num_samples = (int)(num_pixels * 0.05);
            var pxls = new List<byte[]>();

            var idx = Enumerable.Range(0, num_pixels).ToList();
            ShuffleAll(idx);

            for (int i = 0; i < num_samples; i++)
                pxls.Add(pixels[idx[i]]);

            return pxls;
        }

        private List<byte[]> get_palette(List<byte[]> samples1)
        {
            //def get_palette(samples, options, return_mask= False, kmeans_iter= 40):
            //'''Extract the palette for the set of sampled RGB values. The first
            //palette entry is always the background color; the rest are determined
            //from foreground pixels by running K - means clustering.Returns the
            //palette, as well as a mask corresponding to the foreground pixels.
            var bg_color = get_bg_color(samples1, 6);
            var fg_mask = get_fg_mask(bg_color, samples1);

            //var kmeans_iter = 40; // Can't specify this at run time. 44 iterations are run versus 40 in python.
            var kmeans = new Accord.MachineLearning.KMeans(num_colors - 1);

            //Convert List of Byte[] to double[][] for Accord KMeans
            var doubleSamples = new double[samples1.Count][];
            for (int i = 0; i < samples1.Count; i++)
                doubleSamples[i] = new double[] { samples1[i][0], samples1[i][1], samples1[i][2] };

            // Filter samples to only true items
            var countOfTrue = fg_mask.Where(x => x == true).Count();
            var filteredSamples = new double[countOfTrue][];
            var c = 0;
            for (int i = 0; i < fg_mask.Count; i++)
            {
                if (fg_mask[i])
                {
                    filteredSamples[c] = new double[] { doubleSamples[i][0], doubleSamples[i][1], doubleSamples[i][2] };
                    c++;
                }
            }

            // Accord KMeans returns different values than scipy kmeans.
            var clusters = kmeans.Learn(filteredSamples);
            var palette1 = clusters.Centroids.ToList();
            palette1.Insert(0, new double[] { bg_color.R, bg_color.G, bg_color.B });

            var bytePal = new List<byte[]>();
            for (int i = 0; i < palette1.Count; i++)
                bytePal.Add(new byte[] { (byte)palette1[i][0], (byte)palette1[i][1], (byte)palette1[i][2] });

            return bytePal;
        }

        private Color get_bg_color(List<byte[]> image, int bits_per_channel = 0)
        {
            //def get_bg_color(image, bits_per_channel= None):
            //'''Obtains the background color from an image or array of RGB colors
            //by grouping similar colors into bins and finding the most frequent
            //one.

            var quantized = quantize(image, bits_per_channel);
            var packed = pack_rgb(quantized);
            var unique = packed.GroupBy(item => item, (key, elements) => new { key, count = elements.Count() }).OrderByDescending(o => o.count).ToDictionary(x => x.key, x => x.count);
            //var maxvalue = unique.Max(x => x.Value);
            //var maxindex = unique.ToList().FindIndex(x => x.Value == maxvalue);

            // Since they're sorted descending, I can just get index 0
            int packed_mode = unique.ElementAt(0).Key;

            return unpack_rgb(packed_mode);
        }

        private List<byte[]> quantize(List<byte[]> image, int bits_per_channel = 0)
        {
            //def quantize(image, bits_per_channel= None):
            //'''Reduces the number of bits per channel in the given image.'''
            if (bits_per_channel == 0)
                bits_per_channel = 6;
            var shift = 8 - bits_per_channel;
            var halfbin = (1 << shift) >> 1;

            // don't modify the passed in image
            var newImage = new byte[image.Count][];

            // Init array to 0's
            for (int i = 0; i < image.Count; i++)
                newImage[i] = new byte[] { 0, 0, 0 };

            for (int i = 0; i < image.Count; i++)
            {
                newImage[i][0] = (byte)(((image[i][0] >> shift) << shift) + halfbin);
                newImage[i][1] = (byte)(((image[i][1] >> shift) << shift) + halfbin);
                newImage[i][2] = (byte)(((image[i][2] >> shift) << shift) + halfbin);
            }

            return newImage.ToList();
        }

        private int[] pack_rgb(List<byte[]> rgb)
        {
            //def pack_rgb(rgb):
            //'''Packs a 24-bit RGB triples into a single integer,
            //works on both arrays and tuples.'''
            var packed = new int[rgb.Count];
            for (int i = 0; i < rgb.Count; i++)
            {
                //packed = (rgb[:, 0] << 16 | rgb[:, 1] << 8 | rgb[:, 2])
                var r = (int)rgb[i][0];
                var g = (int)rgb[i][1];
                var b = (int)rgb[i][2];
                var p = r << 16 | g << 8 | b;
                //var p2 = ((r & 0xff) << 16) | ((g & 0xff) << 8) | (b & 0xff);
                packed[i] = p;
            }

            return packed;
        }

        private Color unpack_rgb(int packed_mode)
        {
            //def unpack_rgb(packed):
            //'''Unpacks a single integer or array of integers into one or more 24 - bit RGB values.
            var red = (packed_mode >> 16) & 0xff;  // extract red byte (bits 23-17)
            var green = (packed_mode >> 8) & 0xff; // extract green byte (bits 15-8)
            var blue = packed_mode & 0xff;         // extract blue byte (bits 7-0)

            return Color.FromArgb(red, green, blue);
        }

        private List<bool> get_fg_mask(Color bg_color, List<byte[]> samples2)
        {
            //def get_fg_mask(bg_color, samples, options):
            //'''Determine whether each pixel in a set of samples is foreground by
            //comparing it to the background color. A pixel is classified as a
            //foreground pixel if either its value or saturation differs from the
            //background by a threshold.'''

            Tuple<float, double> rgbval = rgb_to_sv(bg_color);
            Tuple<List<float>, List<float>> t = rgb_to_sv_samples(samples2);

            var s_bg = rgbval.Item1;
            var v_bg = rgbval.Item2;
            var s_samples = t.Item1;
            var v_samples = t.Item2;
            var s_diffList = new List<float>();
            var v_diffList = new List<float>();
            for (int i = 0; i < s_samples.Count; i++)
            {
                var curSDiff = Math.Abs(s_bg - s_samples[i]);
                var curVDiff = Math.Abs(v_bg - v_samples[i]);
                s_diffList.Add(curSDiff);
                v_diffList.Add((float)curVDiff);
            }

            var fg_mask = new List<bool>();
            for (int i = 0; i < s_diffList.Count; i++)
            {
                var curMask = v_diffList[i] >= value_threshold | s_diffList[i] >= sat_threshold;
                fg_mask.Add(curMask);
            }

            return fg_mask;
        }

        private Tuple<float, double> rgb_to_sv(Color rgb)
        {
            //def rgb_to_sv(rgb): // bg_color
            //'''Convert an RGB image or array of RGB colors to saturation and
            //value, returning each one as a separate 32 - bit floating point array or
            //value.

            var cmax = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B));
            var cmin = Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));
            var delta = cmax - cmin;
            var saturation = (float)delta / (float)cmax;
            saturation = cmax == 0 ? 0 : saturation;
            var value = cmax / 255.0;

            return Tuple.Create(saturation, value);
        }

        private Tuple<List<float>, List<float>> rgb_to_sv_samples(List<byte[]> samples3)
        {
            //rgb_to_sv samples
            var saturationList = new List<float>();
            var valueList = new List<float>();
            for (int i = 0; i < samples3.Count; i++)
            {
                var curMin = Math.Min(samples3[i][0], Math.Min(samples3[i][1], samples3[i][2]));
                var curMax = Math.Max(samples3[i][0], Math.Max(samples3[i][1], samples3[i][2]));
                var curDelta = curMax - curMin;
                var curSaturation = (float)curDelta / (float)curMax;
                curSaturation = curMax == 0 ? 0 : curSaturation;
                var curValue = curMax / 255.0f;

                saturationList.Add(curSaturation);
                valueList.Add(curValue);
            }

            return Tuple.Create(saturationList, valueList);
        }

        private int[] apply_palette(byte[][][] img1, List<byte[]> palette2)
        {
            //def apply_palette(img, palette, options):
            //'''Apply the pallete to the given image. The first step is to set all
            //background pixels to the background color; then, nearest-neighbor
            //matching is used to map each foreground color to the closest one in
            //the palette.

            var pal0 = palette2[0];
            var bg_color1 = Color.FromArgb(pal0[0], pal0[1], pal0[2]);
            var imgAsSamples = img1.SelectMany(x => x).ToList();
            //get_fg_mask
            var fg_mask1 = get_fg_mask(bg_color1, imgAsSamples);
            var num_pixels = imgAsSamples.Count;
            var pixelLabels = new int[num_pixels];

            // Can't find vq (Vector Quantization) algorithm so let's just find the closest color
            //Convert palette to color array
            var colorMap = new Color[palette2.Count];
            for (int i = 0; i < palette2.Count; i++)
                colorMap[i] = Color.FromArgb(palette2[i][0], palette2[i][1], palette2[i][2]);

            // We should really compare colors using L*a*b and DeltaE but I don't believe we need that precision
            for (int i = 0; i < num_pixels; i++)
            {
                if (fg_mask1[i])
                {
                    var curColor = Color.FromArgb(imgAsSamples[i][0], imgAsSamples[i][1], imgAsSamples[i][2]);
                    pixelLabels[i] = FindNearestColorIndex(curColor, colorMap);
                }
                else
                    pixelLabels[i] = 0;
            }

            return pixelLabels;
        }

        private void save(string fn, int[] labels1, List<byte[]> palette3)
        {
            var saturate = true;
            var white_bg = true;

            if (saturate)
            {
                var floatPal = new float[palette3.Count][];
                for (int i = 0; i < palette3.Count; i++)
                    floatPal[i] = new float[] { palette3[i][0], palette3[i][1], palette3[i][2] };
                var pmin = floatPal.SelectMany(x => x).Min();
                var pmax = floatPal.SelectMany(x => x).Max();
                for (int i = 0; i < floatPal.Length; i++)
                {
                    floatPal[i][0] = 255 * (floatPal[i][0] - pmin) / (pmax - pmin);
                    floatPal[i][1] = 255 * (floatPal[i][1] - pmin) / (pmax - pmin);
                    floatPal[i][2] = 255 * (floatPal[i][2] - pmin) / (pmax - pmin);
                    palette3[i][0] = (byte)floatPal[i][0];
                    palette3[i][1] = (byte)floatPal[i][1];
                    palette3[i][2] = (byte)floatPal[i][2];
                }
            }
            if (white_bg)
                palette3[0] = new byte[] { 255, 255, 255 };

            var newData = new byte[WorkingBmp.Width * WorkingBmp.Height * 4];
            var lblIdx = 0;
            for (int i = 0; i < labels1.Length * 4 - 1; i += 4)
            {
                newData[i] = palette3[labels1[lblIdx]][0];
                newData[i + 1] = palette3[labels1[lblIdx]][1];
                newData[i + 2] = palette3[labels1[lblIdx]][2];
                newData[i + 3] = 255;
                lblIdx++;
            }

            using (var stream = new MemoryStream(newData))
            {
                var bmp = new Bitmap(WorkingBmp.Width, WorkingBmp.Height, PixelFormat.Format32bppRgb);
                var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
                System.Runtime.InteropServices.Marshal.Copy(newData, 0, bmpData.Scan0, newData.Length);
                bmp.UnlockBits(bmpData);
                bmp.Save(fn, ImageFormat.Png);
                imageBox.Image = bmp;
            }
        }

        private Color FindNearestColor(Color current, Color[] map)
        {
            int shortestDistance;
            int index;

            index = -1;
            shortestDistance = int.MaxValue;

            for (int i = 0; i < map.Length; i++)
            {
                Color match;
                int distance;

                match = map[i];
                distance = GetDistance(current, match);

                if (distance < shortestDistance)
                {
                    index = i;
                    shortestDistance = distance;
                }
            }

            return map[index];
        }

        public int FindNearestColorIndex(Color current, Color[] map)
        {
            int shortestDistance;
            int index;

            index = -1;
            shortestDistance = int.MaxValue;

            for (int i = 0; i < map.Length; i++)
            {
                Color match;
                int distance;

                match = map[i];
                distance = GetDistance(current, match, false);

                if (distance < shortestDistance)
                {
                    index = i;
                    shortestDistance = distance;
                }
            }

            return index;
        }

        public int GetDistance(Color current, Color match, bool OnlySquared = true)
        {
            if (OnlySquared)
                return GetEuclideanDistanceSquared(current, match);
            else
                return GetEuclideanDistanceSquareRoot(current, match);
        }

        public int GetEuclideanDistanceSquared(Color current, Color match)
        {
            int redDifference;
            int greenDifference;
            int blueDifference;

            redDifference = current.R - match.R;
            greenDifference = current.G - match.G;
            blueDifference = current.B - match.B;

            return (redDifference * redDifference) + (greenDifference * greenDifference) + (blueDifference * blueDifference);
        }

        public int GetEuclideanDistanceSquareRoot(Color current, Color match)
        {
            int redDifference;
            int greenDifference;
            int blueDifference;

            redDifference = current.R - match.R;
            greenDifference = current.G - match.G;
            blueDifference = current.B - match.B;

            return (int)Math.Sqrt((redDifference * redDifference) + (greenDifference * greenDifference) + (blueDifference * blueDifference));
        }

        public void ShuffleAll<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
