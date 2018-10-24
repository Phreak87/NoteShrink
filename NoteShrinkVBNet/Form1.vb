Public Class Form1

    Dim Shrink As NoteShrink
    Dim Setting As New Settings

    Private Sub PropertyGrid1_PropertyValueChanged(ByVal s As Object, ByVal e As System.Windows.Forms.PropertyValueChangedEventArgs) Handles PropertyGrid1.PropertyValueChanged
        UpdatePBox()
    End Sub
    Private Sub Form1_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        PropertyGrid1.SelectedObject = Setting
    End Sub

    Class Settings
        Property num_colors As Integer = 8
        Property value_threshold As Double = 0.25
        Property sat_threshold As Double = 0.2
        Property b_saturate As Boolean = True
        Property white_bg As Boolean = True
        Property MaxIteration As Integer = 1
    End Class

    Sub UpdatePBox()
        If IsNothing(Shrink) Then Exit Sub
        Me.PictureBox1.Image = Nothing
        Me.PictureBox1.Image = Shrink.Cleanup(Setting.num_colors, Setting.value_threshold, Setting.sat_threshold, Setting.b_saturate, Setting.white_bg)
        Me.PictureBox1.Update()
    End Sub

    Private Sub DateiAuswählenToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles DateiAuswählenToolStripMenuItem.Click
        Dim n As New OpenFileDialog: n.ShowDialog()
        Shrink = New NoteShrink(n.FileName, True) : UpdatePBox()
    End Sub
End Class
