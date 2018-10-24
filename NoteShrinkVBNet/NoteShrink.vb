Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms
Imports System.Drawing.Imaging
Imports System.IO
Imports Accord.Imaging

Public Class NoteShrink
    Private WorkingBmp As Bitmap
    Private HWBytes As Byte()()() '  H * W  * (B,G,R)
    Private FULLBytes As Byte()() ' (H * W) * (B,G,R)
    Private DISTBytes As Byte()() ' (H * W) * (B,G,R)
    Private SAMPBytes As List(Of Byte())

    Private num_colors As Integer = 8
    Private value_threshold As Double = 0.25
    Private sat_threshold As Double = 0.2
    Private b_saturate As Boolean = True
    Private white_bg As Boolean = True
    Private MaxIteration As Integer = 6

    Sub New(ByVal Bitmap As Bitmap, ByVal FullSamples As Boolean)
        WorkingBmp = Bitmap

        Dim target As Accord.Imaging.Converters.ImageToMatrix = New Accord.Imaging.Converters.ImageToMatrix(0, 255)
        target.Convert(WorkingBmp, HWBytes) '                       H * W  * (B,G,R)
        FULLBytes = HWBytes.SelectMany(Function(x) x).ToArray() '  (H * W) * (B,G,R)

        If FullSamples = False Then SAMPBytes = sample_pixels(FULLBytes) '  (H * W) * INT(B,G,R).Distinct
        If FullSamples = True Then SAMPBytes = sample_pixels2(FULLBytes) '  (H * W) * INT(B,G,R).Distinct
    End Sub
    Sub New(ByVal Filename As String, ByVal FullSamples As Boolean)
        Me.New(Bitmap.FromFile(Filename), FullSamples)
    End Sub

    Public Function Cleanup( _
             Optional ByVal NumColors As Integer = 8,
             Optional ByVal ValueThreshold As Double = 0.25,
             Optional ByVal SaturationThreshold As Double = 0.2,
             Optional ByVal Saturate As Boolean = True,
             Optional ByVal BackGroundWhite As Boolean = True,
             Optional ByVal FullSamples As Boolean = False) As Bitmap
        'https://github.com/mzucker/noteshrink

        num_colors = NumColors
        value_threshold = ValueThreshold
        sat_threshold = SaturationThreshold
        b_saturate = Saturate
        white_bg = BackGroundWhite

        Dim palette As List(Of Byte()) = get_palette(SAMPBytes)
        Dim labels As Integer() = apply_palette(HWBytes, FULLBytes, palette)
        Return applychanges(labels, palette)
    End Function

    Private Function sample_pixels2(ByVal ImageBytes As Byte()()) As List(Of Byte())
        Dim ColorDict As New Dictionary(Of Integer, Byte())
        For i As Integer = 0 To ImageBytes.Count - 1
            Dim Val As Integer = Pack_BGR(ImageBytes(i))
            If ColorDict.ContainsKey(Val) Then Continue For
            ColorDict.Add(Val, ImageBytes(i))
        Next

        ' This is for BGDetection! (Seeding of a few Different Colors)
        For i As Integer = 0 To ImageBytes.Count - 1 Step CInt(ImageBytes.Count / 500)
            ColorDict.Add(i, ImageBytes(i))
        Next
        Return ColorDict.Values.ToList
    End Function
    Private Function sample_pixels(ByVal ImageBytes As Byte()()) As List(Of Byte())
        Dim pxls As New List(Of Byte())()
        For i As Integer = 0 To ImageBytes.Count - 1 Step 50
            pxls.Add(ImageBytes(i))
        Next
        Return pxls
    End Function

    Private Function get_palette(ByVal samples1 As List(Of Byte())) As List(Of Byte())
        Dim bg_color As Byte() = get_bg_color(samples1, 6)
        Dim fg_mask As List(Of Boolean) = get_fg_mask(bg_color, samples1)

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
        If filteredSamples.Count = 0 Then Return Nothing

        Dim kmeans As Accord.MachineLearning.KMeans = New Accord.MachineLearning.KMeans(num_colors - 1)
        kmeans.ComputeCovariances = False : kmeans.ComputeError = False : kmeans.MaxIterations = MaxIteration
        Dim clusters As Accord.MachineLearning.KMeansClusterCollection = kmeans.Learn(filteredSamples)

        Dim palette1 As List(Of Double()) = clusters.Centroids.ToList()
        palette1.Insert(0, New Double() {bg_color(0), bg_color(1), bg_color(2)})

        Dim bytePal As List(Of Byte()) = New List(Of Byte())()
        For i As Integer = 0 To palette1.Count - 1
            bytePal.Add(New Byte() {CByte(Math.Truncate(palette1(i)(0))),
                                    CByte(Math.Truncate(palette1(i)(1))),
                                    CByte(Math.Truncate(palette1(i)(2)))})
        Next

        Return bytePal
    End Function

    Private Function get_bg_color(ByVal image As List(Of Byte()), Optional ByVal bits_per_channel As Integer = 0) As Byte()
        Dim quantized As List(Of Byte()) = quantize(image, bits_per_channel)
        Dim packed As Integer() = Pack_BGR(quantized)

        Dim unique As New Dictionary(Of Integer, Integer)
        For Each Entry As Integer In packed
            If unique.ContainsKey(Entry) Then
                unique(Entry) += 1
            Else
                unique.Add(Entry, 1)
            End If
        Next
        unique = unique.OrderBy(Function(x) x.Value).ToDictionary(Function(x) x.Key, Function(x) x.Value)
        Dim packed_mode As Integer = unique.ElementAt(unique.Count - 1).Key

        Return Unpack_BGR(packed_mode)
    End Function

    Private Function quantize(ByVal image As List(Of Byte()), Optional ByVal bits_per_channel As Integer = 0) As List(Of Byte())
        If bits_per_channel = 0 Then bits_per_channel = 6
        Dim shift As Integer = 8 - bits_per_channel
        Dim halfbin As Integer = (1 << shift) >> 1

        Dim newImage As Byte()() = New Byte(image.Count - 1)() {} : image.CopyTo(newImage)

        For i As Integer = 0 To image.Count - 1
            newImage(i)(0) = CByte(((image(i)(0) >> shift) << shift) + halfbin)
            newImage(i)(1) = CByte(((image(i)(1) >> shift) << shift) + halfbin)
            newImage(i)(2) = CByte(((image(i)(2) >> shift) << shift) + halfbin)
        Next

        Return newImage.ToList()
    End Function

#Region "Pack, Unpack, Convert"
    Private Function ToColor(ByVal Bytes As Byte()) As Color
        ' Warning! RGB From BGR!
        Return Color.FromArgb(Bytes(2), Bytes(1), Bytes(0))
    End Function
    Private Function Pack_BGR(ByVal RGB As Byte()) As Integer
        Dim b As Integer = CInt(RGB(0))
        Dim g As Integer = CInt(RGB(1))
        Dim r As Integer = CInt(RGB(2))
        Dim p As Integer = r << 16 Or g << 8 Or b
        Return p
    End Function
    Private Function Pack_BGR(ByVal BGR As List(Of Byte())) As Integer()
        Dim packed(BGR.Count - 1) As Integer
        For i As Integer = 0 To BGR.Count - 1
            packed(i) = Pack_BGR(BGR(i))
        Next
        Return packed
    End Function
    Private Function Unpack_BGR(ByVal BGR As Integer()) As List(Of Byte())
        Dim packed As New List(Of Byte())
        For i As Integer = 0 To BGR.Count - 1
            packed.Add(Unpack_BGR(BGR(i)))
        Next
        Return packed
    End Function
    Private Function Unpack_BGR(ByVal packed_mode As Integer) As Byte()
        Dim R1 As Integer = ((packed_mode >> 16 And &HFF))
        Dim G1 As Integer = ((packed_mode >> 8 And &HFF))
        Dim B1 As Integer = ((packed_mode And &HFF))
        Return New Byte() {B1, G1, R1}
    End Function
#End Region

    Private Function get_fg_mask(ByVal bg_color As Byte(), ByVal samples2 As List(Of Byte())) As List(Of Boolean)

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

    Private Function rgb_to_sv(ByVal rgb As Byte()) As Tuple(Of Single, Double)
        Dim cmax As Integer = rgb.Max
        Dim cmin As Integer = rgb.Min
        Dim delta As Integer = cmax - cmin
        Dim saturation As Single = CSng(delta) / CSng(cmax)
        saturation = If(cmax = 0, 0, saturation)
        Dim value As Double = cmax / 255.0
        Return Tuple.Create(saturation, value)
    End Function

    Private Function rgb_to_sv_samples(ByVal samples3 As List(Of Byte())) As Tuple(Of List(Of Single), List(Of Double))

        Dim saturationList As List(Of Single) = New List(Of Single)()
        Dim valueList As List(Of Double) = New List(Of Double)()
        For i As Integer = 0 To samples3.Count - 1
            Dim curMin As Integer = samples3(i).Min
            Dim curMax As Integer = samples3(i).Max
            Dim curDelta As Integer = curMax - curMin
            Dim curSaturation As Single = CSng(curDelta) / CSng(curMax)
            curSaturation = If(curMax = 0, 0, curSaturation)
            Dim curValue As Double = curMax / 255.0

            saturationList.Add(curSaturation)
            valueList.Add(curValue)
        Next

        Return Tuple.Create(saturationList, valueList)
    End Function

    Private Function apply_palette(ByVal img1 As Byte()()(),
                                   ByVal FullBytes As Byte()(),
                                   ByVal palette2 As List(Of Byte())
                                   ) As Integer()

        Dim bg_color1 As Byte() = palette2(0)
        Dim imgAsSamples As List(Of Byte()) = FullBytes.ToList()

        Dim fg_mask1 As List(Of Boolean) = get_fg_mask(bg_color1, imgAsSamples)
        Dim num_pixels As Integer = imgAsSamples.Count
        Dim pixelLabels(num_pixels - 1) As Integer

        Dim colorMap(palette2.Count - 1) As Color
        For i As Integer = 0 To palette2.Count - 1
            colorMap(i) = ToColor(palette2(i))
        Next

        For i As Integer = 0 To num_pixels - 1
            If fg_mask1(i) Then
                Dim curColor As Color = ToColor(imgAsSamples(i))
                pixelLabels(i) = FindNearestColorIndex(curColor, colorMap)
            Else
                pixelLabels(i) = 0
            End If
        Next
        Distances.Clear()

        Return pixelLabels
    End Function

    Private Function applychanges(ByVal labels1 As Integer(), ByVal palette3 As List(Of Byte())) As Bitmap

        If b_saturate Then
            Dim floatPal(palette3.Count - 1)() As Single
            For i As Integer = 0 To palette3.Count - 1
                floatPal(i) = New Single() {palette3(i)(0), palette3(i)(1), palette3(i)(2)}
            Next
            Dim pmin As Single = floatPal.SelectMany(Function(x) x).Min()
            Dim pmax As Single = floatPal.SelectMany(Function(x) x).Max()
            For i As Integer = 0 To floatPal.Length - 1
                palette3(i)(0) = CByte(Math.Truncate(255 * (floatPal(i)(0) - pmin) / (pmax - pmin)))
                palette3(i)(1) = CByte(Math.Truncate(255 * (floatPal(i)(1) - pmin) / (pmax - pmin)))
                palette3(i)(2) = CByte(Math.Truncate(255 * (floatPal(i)(2) - pmin) / (pmax - pmin)))
            Next
        End If

        If white_bg = True Then palette3(0) = New Byte() {255, 255, 255}
        Dim NewData2((WorkingBmp.Width * WorkingBmp.Height) - 1)() As Byte
        For i As Integer = 0 To (WorkingBmp.Width * WorkingBmp.Height) - 1
            NewData2(i) = {255,
                           palette3(labels1(i))(2),
                           palette3(labels1(i))(1),
                           palette3(labels1(i))(0)}
        Next

        Dim CONV As New Accord.Imaging.Converters.ArrayToImage(WorkingBmp.Width, WorkingBmp.Height, 0, 255)
        Dim RESBMP As Bitmap = Nothing : CONV.Convert(NewData2, RESBMP) : Return RESBMP

    End Function

#Region "Distances"
    Private Distances As New Dictionary(Of Color, Integer)
    Public Function FindNearestColorIndex(ByVal current As Color, ByVal map As Color()) As Integer
        If Distances.ContainsKey(current) Then
            Return Distances(current) ' Caching of Distances
        Else
            Dim MinDst As Integer = Integer.MaxValue
            Dim Index As Integer = -1
            For i As Integer = 0 To map.Length - 1
                Dim distance As Integer = GetDistance(current, map(i), False)
                If distance < MinDst Then Index = i : MinDst = distance
            Next
            Distances.Add(current, Index)
            Return Index
        End If
    End Function
    Private Function GetDistance(ByVal current As Color, ByVal match As Color, Optional ByVal OnlySquared As Boolean = True) As Integer
        If OnlySquared = True Then
            Return GetEuclideanDistanceSquared(current, match)
        Else
            Return GetEuclideanDistanceSquared(current, match)
        End If
    End Function
    Private Function GetEuclideanDistanceSquared(ByVal current As Color, ByVal match As Color) As Integer
        Dim redDifference As Integer = Math.Abs(CDbl(current.R) - match.R)
        Dim greenDifference As Integer = Math.Abs(CDbl(current.G) - match.G)
        Dim blueDifference As Integer = Math.Abs(CDbl(current.B) - match.B)
        Return (redDifference * redDifference) + (greenDifference * greenDifference) + (blueDifference * blueDifference)
    End Function
    Private Function GetEuclideanDistanceSquareRoot(ByVal current As Color, ByVal match As Color) As Integer
        Dim redDifference As Integer = CDbl(current.R) - match.R
        Dim greenDifference As Integer = CDbl(current.G) - match.G
        Dim blueDifference As Double = CDbl(current.B) - match.B
        Return CInt(Math.Truncate(Math.Sqrt((redDifference * redDifference) + (greenDifference * greenDifference) + (blueDifference * blueDifference))))
    End Function
#End Region

End Class
