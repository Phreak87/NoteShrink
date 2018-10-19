Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms
Imports System.Drawing.Imaging
Imports System.IO
Imports Accord.Imaging

Namespace WindowsFormsApp1
	Public Partial Class Form1
        Inherits Form

		Private WorkingBmp As Bitmap
		Private num_colors As Integer = 8
		Private value_threshold As Double = 0.25
        Private sat_threshold As Double = 0.2
        Private saturate As Boolean = True
        Private white_bg As Boolean = True


		Public Sub New()
			InitializeComponent()
            Dim inFile As String = "Pix\graph-paper-ink-only.jpg"
			WorkingBmp = DirectCast(System.Drawing.Image.FromFile(inFile), Bitmap)
			ImageCleanup(WorkingBmp)
		End Sub

		Private Sub ImageCleanup(inBmp As Bitmap)
			'https://github.com/mzucker/noteshrink

			' Test pack_rgb and unpack_rgb
			'var im = new List<byte[]>()
			'{
			'    new byte[] { 230, 230, 226 },
			'    new byte[] { 242, 238, 234 }
			'};
			'var pck = pack_rgb(im);
			'var upck0 = unpack_rgb(pck[0]);
			'var upck1 = unpack_rgb(pck[1]);

			'img = np.array(pil_img)
            Dim target As Accord.Imaging.Converters.ImageToMatrix = New Accord.Imaging.Converters.ImageToMatrix(0, 255)
            Dim npimage As Byte()()() = Nothing : target.Convert(inBmp, npimage)

            Dim samples As List(Of Byte()) = sample_pixels(npimage)
            Dim palette As List(Of Byte()) = get_palette(samples)
            Dim labels As Integer() = apply_palette(npimage, palette)
			save("foo.png", labels, palette)
		End Sub

		Private Function sample_pixels(img As Byte()()()) As List(Of Byte())
			'def sample_pixels(img, options):
			''''Pick a fixed percentage of pixels in the image, returned in random order.'''
            Dim num_pixels As Byte()() = img.SelectMany(Function(x) x).ToArray()
            Dim num_samples As Integer = CInt(Math.Truncate(num_pixels.Length * 0.05))
            Dim random As New Random()
			random.[Next](0, num_samples)
            Dim pxls As New List(Of Byte())()

			For i As Integer = 0 To num_samples - 1
                Dim idx As Integer = random.[Next](0, num_samples)
				' swap Blue & Red
				pxls.Add(New Byte() {num_pixels(i)(2), num_pixels(i)(1), num_pixels(i)(0)})
			Next
			'var clr = new List<Color>();
			'for (int i = 0; i < num_samples; i++)
			'{
			'    //swap Blue & Red
			'    pxls.Add(new byte[] { num_pixels[i][2], num_pixels[i][1], num_pixels[i][0] });
			'    clr.Add(Color.FromArgb(pxls[i][0], pxls[i][1], pxls[i][2]));
			'}

			Return pxls
		End Function
		Private Function get_palette(samples1 As List(Of Byte())) As List(Of Byte())
			'def get_palette(samples, options, return_mask= False, kmeans_iter= 40):
			''''Extract the palette for the set of sampled RGB values. The first
			'palette entry is always the background color; the rest are determined
			'from foreground pixels by running K - means clustering.Returns the
			'palette, as well as a mask corresponding to the foreground pixels.
            Dim bg_color As System.Drawing.Color = get_bg_color(samples1, 6)
            Dim fg_mask As List(Of Boolean) = get_fg_mask(bg_color, samples1)

			'var kmeans_iter = 40; // Can't specify this at run time. 44 iterations are run versus 40 in python.
            Dim kmeans As Accord.MachineLearning.KMeans = New Accord.MachineLearning.KMeans(num_colors - 1)

			'Convert List of Byte[] to double[][] for Accord KMeans
            Dim doubleSamples()() As Double = New Double(samples1.Count - 1)() {}
			For i As Integer = 0 To samples1.Count - 1
				doubleSamples(i) = New Double() {samples1(i)(0), samples1(i)(1), samples1(i)(2)}
			Next

			' Filter samples to only true items
            Dim countOfTrue As Integer = fg_mask.Where(Function(x) x = True).Count()
            Dim filteredSamples(countOfTrue - 1)() As Double
            Dim c As Integer = 0
			For i As Integer = 0 To fg_mask.Count - 1
				If fg_mask(i) Then
					filteredSamples(c) = New Double() {doubleSamples(i)(0), doubleSamples(i)(1), doubleSamples(i)(2)}
					c += 1
				End If
			Next
			' Accord KMeans returns different values than scipy kmeans.
            Dim clusters As Accord.MachineLearning.KMeansClusterCollection = kmeans.Learn(filteredSamples)
            Dim palette1 As List(Of Double()) = clusters.Centroids.ToList()
			palette1.Insert(0, New Double() {bg_color.R, bg_color.G, bg_color.B})

            Dim bytePal As List(Of Byte()) = New List(Of Byte())()
			For i As Integer = 0 To palette1.Count - 1
				bytePal.Add(New Byte() {CByte(Math.Truncate(palette1(i)(0))), CByte(Math.Truncate(palette1(i)(1))), CByte(Math.Truncate(palette1(i)(2)))})
			Next

			Return bytePal
		End Function

		Private Function get_bg_color(image As List(Of Byte()), Optional bits_per_channel As Integer = 0) As Color
			'def get_bg_color(image, bits_per_channel= None):
			''''Obtains the background color from an image or array of RGB colors
			'by grouping similar colors into bins and finding the most frequent
			'one.

            Dim quantized As List(Of Byte()) = quantize(image, bits_per_channel)
            Dim packed As Integer() = pack_rgb(quantized)

            Dim unique As New Dictionary(Of Integer, Integer)
            For Each Entry As Integer In packed.Distinct.ToList
                unique.Add(Entry, packed.ToList.FindAll(Function(s) s = Entry).Count)
            Next
            unique = unique.OrderBy(Function(x) x.Value).ToDictionary(Function(x) x.Key, Function(x) x.Value)
            ' var unique = packed.GroupBy(item => item, (key, elements) => new { key, count = elements.Count() }).ToDictionary(x => x.key, x => x.count);
            'Dim unique = packed.GroupBy(Function(item) item, Function(key_ As Integer, elements As IEnumerable(Of Integer)) New With {key_, elements.Count})
            'unique = unique.ToDictionary(Function(x) x.key, Function(x) x.Count)
            Dim maxvalue As Integer = unique.Max(Function(x) x.Value)
            Dim maxindex As Integer = unique.ToList().FindIndex(Function(x) x.Value = maxvalue)
            Dim packed_mode As Integer = unique.ElementAt(maxindex).Key

            Return unpack_rgb(packed_mode)
        End Function

		Private Function quantize(image As List(Of Byte()), Optional bits_per_channel As Integer = 0) As List(Of Byte())
			'def quantize(image, bits_per_channel= None):
			''''Reduces the number of bits per channel in the given image.'''
			If bits_per_channel = 0 Then
				bits_per_channel = 6
			End If
            Dim shift As Integer = 8 - bits_per_channel
            Dim halfbin As Integer = (1 << shift) >> 1

			' don't modify the passed in image
            Dim newImage As Byte()() = New Byte(image.Count - 1)() {}

			' Init array to 0's
			For i As Integer = 0 To image.Count - 1
				newImage(i) = New Byte() {0, 0, 0}
			Next

			For i As Integer = 0 To image.Count - 1
				newImage(i)(0) = CByte(((image(i)(0) >> shift) << shift) + halfbin)
				newImage(i)(1) = CByte(((image(i)(1) >> shift) << shift) + halfbin)
				newImage(i)(2) = CByte(((image(i)(2) >> shift) << shift) + halfbin)
			Next

			Return newImage.ToList()
		End Function

		Private Function pack_rgb(rgb As List(Of Byte())) As Integer()
			'def pack_rgb(rgb):
			''''Packs a 24-bit RGB triples into a single integer,
			'works on both arrays and tuples.'''
            Dim packed(rgb.Count - 1) As Integer
			For i As Integer = 0 To rgb.Count - 1
				'packed = (rgb[:, 0] << 16 | rgb[:, 1] << 8 | rgb[:, 2])
                Dim r As Integer = CInt(rgb(i)(0))
                Dim g As Integer = CInt(rgb(i)(1))
                Dim b As Integer = CInt(rgb(i)(2))
                Dim p As Integer = r << 16 Or g << 8 Or b
				packed(i) = p
			Next

			Return packed
		End Function

		Private Function unpack_rgb(packed_mode As Integer) As Color
		'(Dictionary<int,int> packed)
			'def unpack_rgb(packed):
			''''Unpacks a single integer or array of integers into one or more 24 - bit RGB values.
			'var maxvalue = packed.Max(x => x.Value);
			'var maxindex = packed.ToList().FindIndex(x => x.Value == maxvalue);
			'int packed_mode = packed.ElementAt(maxindex).Key;
            Dim r1 As Integer = ((packed_mode >> 16) And &HFF)
            Dim g1 As Integer = ((packed_mode >> 8) And &HFF)
            Dim b1 As Integer = ((packed_mode) And &HFF)

			Return Color.FromArgb(r1, g1, b1)
		End Function

		Private Function get_fg_mask(bg_color As Color, samples2 As List(Of Byte())) As List(Of Boolean)
			'def get_fg_mask(bg_color, samples, options):
			''''Determine whether each pixel in a set of samples is foreground by
			'comparing it to the background color. A pixel is classified as a
			'foreground pixel if either its value or saturation differs from the
			'background by a threshold.'''

			Dim rgbval As Tuple(Of Single, Double) = rgb_to_sv(bg_color)
			Dim t As Tuple(Of List(Of Single), List(Of Double)) = rgb_to_sv_samples(samples2)

            Dim s_bg As Single = rgbval.Item1
            Dim v_bg As Double = rgbval.Item2
            Dim s_samples As List(Of Single) = t.Item1
            Dim v_samples As List(Of Double) = t.Item2
            Dim s_diffList As New List(Of Single)()
            Dim v_diffList As New List(Of Double)()
			For i As Integer = 0 To s_samples.Count - 1
                Dim curSDiff As Single = Math.Abs(s_bg - s_samples(i))
                Dim curVDiff As Double = Math.Abs(v_bg - v_samples(i))
				s_diffList.Add(curSDiff)
				v_diffList.Add(curVDiff)
			Next

            Dim fg_mask As New List(Of Boolean)()
			For i As Integer = 0 To s_diffList.Count - 1
                Dim curMask As Boolean = v_diffList(i) >= value_threshold Or s_diffList(i) >= sat_threshold
				fg_mask.Add(curMask)
			Next

			Return fg_mask
		End Function

		Private Function rgb_to_sv(rgb As Color) As Tuple(Of Single, Double)
			'def rgb_to_sv(rgb): // bg_color
			''''Convert an RGB image or array of RGB colors to saturation and
			'value, returning each one as a separate 32 - bit floating point array or
			'value.
            Dim cmax As Integer = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B))
            Dim cmin As Integer = Math.Min(rgb.R, Math.Min(rgb.G, rgb.B))
            Dim delta As Integer = cmax - cmin
            Dim saturation As Single = CSng(delta) / CSng(cmax)
			saturation = If(cmax = 0, 0, saturation)
            Dim value As Double = cmax / 255.0

			Return Tuple.Create(saturation, value)
		End Function

		Private Function rgb_to_sv_samples(samples3 As List(Of Byte())) As Tuple(Of List(Of Single), List(Of Double))
			'rgb_to_sv samples
            Dim saturationList As List(Of Single) = New List(Of Single)()
            Dim valueList As List(Of Double) = New List(Of Double)()
			For i As Integer = 0 To samples3.Count - 1
                Dim curMin As Integer = Math.Min(samples3(i)(0), Math.Min(samples3(i)(1), samples3(i)(2)))
                Dim curMax As Integer = Math.Max(samples3(i)(0), Math.Max(samples3(i)(1), samples3(i)(2)))
                Dim curDelta As Integer = curMax - curMin
                Dim curSaturation As Single = CSng(curDelta) / CSng(curMax)
				curSaturation = If(curMax = 0, 0, curSaturation)
                Dim curValue As Double = curMax / 255.0

				saturationList.Add(curSaturation)
				valueList.Add(curValue)
			Next

			Return Tuple.Create(saturationList, valueList)
		End Function

		Private Function apply_palette(img1 As Byte()()(), palette2 As List(Of Byte())) As Integer()
			'def apply_palette(img, palette, options):
			''''Apply the pallete to the given image. The first step is to set all
			'background pixels to the background color; then, nearest-neighbor
			'matching is used to map each foreground color to the closest one in
			'the palette.

            Dim pal0 As Byte() = palette2(0)
            Dim bg_color1 As Color = Color.FromArgb(pal0(0), pal0(1), pal0(2))
            Dim imgAsSamples As List(Of Byte()) = img1.SelectMany(Function(x) x).ToList()
			'get_fg_mask
            Dim fg_mask1 As List(Of Boolean) = get_fg_mask(bg_color1, imgAsSamples)
            Dim num_pixels As Integer = imgAsSamples.Count
            Dim pixelLabels(num_pixels - 1) As Integer

			' Can't find vq (Vector Quantization) algorithm so let's just find the closest color
			'Convert palette to color array
            Dim colorMap(palette2.Count - 1) As Color
			For i As Integer = 0 To palette2.Count - 1
				colorMap(i) = Color.FromArgb(palette2(i)(0), palette2(i)(1), palette2(i)(2))
			Next

			' We should really compare colors using L*a*b and DeltaE but I don't believe we need that precision
			For i As Integer = 0 To num_pixels - 1
				If fg_mask1(i) Then
                    Dim curColor As Color = Color.FromArgb(imgAsSamples(i)(0), imgAsSamples(i)(1), imgAsSamples(i)(2))
					pixelLabels(i) = FindNearestColorIndex(curColor, colorMap)
				Else
					pixelLabels(i) = 0
				End If
			Next

			Return pixelLabels
		End Function

		Private Sub save(fn As String, labels1 As Integer(), palette3 As List(Of Byte()))

			If saturate Then
                Dim floatPal(palette3.Count - 1)() As Single
				For i As Integer = 0 To palette3.Count - 1
					floatPal(i) = New Single() {palette3(i)(0), palette3(i)(1), palette3(i)(2)}
				Next
                Dim pmin As Single = floatPal.SelectMany(Function(x) x).Min()
                Dim pmax As Single = floatPal.SelectMany(Function(x) x).Max()
				For i As Integer = 0 To floatPal.Length - 1
					floatPal(i)(0) = 255 * (floatPal(i)(0) - pmin) / (pmax - pmin)
					floatPal(i)(1) = 255 * (floatPal(i)(1) - pmin) / (pmax - pmin)
					floatPal(i)(2) = 255 * (floatPal(i)(2) - pmin) / (pmax - pmin)
					palette3(i)(0) = CByte(Math.Truncate(floatPal(i)(0)))
					palette3(i)(1) = CByte(Math.Truncate(floatPal(i)(1)))
					palette3(i)(2) = CByte(Math.Truncate(floatPal(i)(2)))
				Next
			End If
			If white_bg Then
				palette3(0) = New Byte() {255, 255, 255}
			End If

            Dim newData As Byte() = New Byte(WorkingBmp.Width * WorkingBmp.Height * 4 - 1) {}
            Dim lblIdx As Integer = 0
			For i As Integer = 0 To labels1.Length * 4 - 2 Step 4
				newData(i) = palette3(labels1(lblIdx))(0)
				newData(i + 1) = palette3(labels1(lblIdx))(1)
				newData(i + 2) = palette3(labels1(lblIdx))(2)
				newData(i + 3) = 255
				lblIdx += 1
			Next

            Using stream As New System.IO.MemoryStream(newData, False)
                'using (var bmp = new Bitmap(imageBox.Image.Width, imageBox.Image.Height, PixelFormat.Format32bppRgb))
                Dim bmp As New Bitmap(WorkingBmp.Width, WorkingBmp.Height, PixelFormat.Format32bppRgb)
                Dim bmpData As BitmapData = bmp.LockBits(New Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.[WriteOnly], bmp.PixelFormat)
                System.Runtime.InteropServices.Marshal.Copy(newData, 0, bmpData.Scan0, newData.Length)
                bmp.UnlockBits(bmpData)
                bmp.Save(fn, ImageFormat.Png)
                imageBox.Image = bmp
            End Using
		End Sub

		Private Function FindNearestColor(current As Color, map As Color()) As Color
			Dim shortestDistance As Integer
			Dim index As Integer

			index = -1
			shortestDistance = Integer.MaxValue

			For i As Integer = 0 To map.Length - 1
				Dim match As Color
				Dim distance As Integer

				match = map(i)
				distance = GetDistance(current, match)

				If distance < shortestDistance Then
					index = i
					shortestDistance = distance
				End If
			Next

			Return map(index)
		End Function

		Public Function FindNearestColorIndex(current As Color, map As Color()) As Integer
			Dim shortestDistance As Integer
			Dim index As Integer

			index = -1
			shortestDistance = Integer.MaxValue

			For i As Integer = 0 To map.Length - 1
				Dim match As Color
				Dim distance As Integer

				match = map(i)
				distance = GetDistance(current, match, False)

				If distance < shortestDistance Then
					index = i
					shortestDistance = distance
				End If
			Next

			Return index
		End Function

		Public Function GetDistance(current As Color, match As Color, Optional OnlySquared As Boolean = True) As Integer
			If OnlySquared Then
				Return GetEuclideanDistanceSquared(current, match)
			Else
				Return GetEuclideanDistanceSquareRoot(current, match)
			End If
		End Function

		Public Function GetEuclideanDistanceSquared(current As Color, match As Color) As Integer
			Dim redDifference As Integer
			Dim greenDifference As Integer
			Dim blueDifference As Integer

			redDifference = current.R - match.R
			greenDifference = current.G - match.G
			blueDifference = current.B - match.B

			Return (redDifference * redDifference) + (greenDifference * greenDifference) + (blueDifference * blueDifference)
		End Function

		Public Function GetEuclideanDistanceSquareRoot(current As Color, match As Color) As Integer
            Dim redDifference As Integer
            Dim greenDifference As Integer
            Dim blueDifference As Double

            redDifference = CDbl(current.R) - match.R
            greenDifference = CDbl(current.G) - match.G
            blueDifference = CDbl(current.B) - match.B

			Return CInt(Math.Truncate(Math.Sqrt((redDifference * redDifference) + (greenDifference * greenDifference) + (blueDifference * blueDifference))))
		End Function

		Private Sub Form1_Load(sender As Object, e As EventArgs)

		End Sub
	End Class
End Namespace
