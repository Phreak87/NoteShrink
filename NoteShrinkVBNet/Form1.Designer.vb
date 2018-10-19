Namespace WindowsFormsApp1
	Partial Class Form1
		''' <summary>
		''' Erforderliche Designervariable.
		''' </summary>
		Private components As System.ComponentModel.IContainer = Nothing

		''' <summary>
		''' Verwendete Ressourcen bereinigen.
		''' </summary>
		''' <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
		Protected Overrides Sub Dispose(disposing As Boolean)
			If disposing AndAlso (components IsNot Nothing) Then
				components.Dispose()
			End If
			MyBase.Dispose(disposing)
		End Sub

		#Region "Vom Windows Form-Designer generierter Code"

		''' <summary>
		''' Erforderliche Methode für die Designerunterstützung.
		''' Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
		''' </summary>
		Private Sub InitializeComponent()
			Dim resources As New System.ComponentModel.ComponentResourceManager(GetType(Form1))
			Me.imageBox = New System.Windows.Forms.PictureBox()
			DirectCast(Me.imageBox, System.ComponentModel.ISupportInitialize).BeginInit()
			Me.SuspendLayout()
			' 
			' imageBox
			' 
			Me.imageBox.Dock = System.Windows.Forms.DockStyle.Fill
			Me.imageBox.Image = DirectCast(resources.GetObject("imageBox.Image"), System.Drawing.Image)
			Me.imageBox.Location = New System.Drawing.Point(0, 0)
			Me.imageBox.Name = "imageBox"
			Me.imageBox.Size = New System.Drawing.Size(755, 601)
			Me.imageBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
			Me.imageBox.TabIndex = 0
			Me.imageBox.TabStop = False
			' 
			' Form1
			' 
			Me.AutoScaleDimensions = New System.Drawing.SizeF(6F, 13F)
			Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
			Me.ClientSize = New System.Drawing.Size(755, 601)
			Me.Controls.Add(Me.imageBox)
			Me.Name = "Form1"
			Me.Text = "Form1"
			AddHandler Me.Load, New System.EventHandler(AddressOf Me.Form1_Load)
			DirectCast(Me.imageBox, System.ComponentModel.ISupportInitialize).EndInit()
			Me.ResumeLayout(False)

		End Sub

		#End Region

		Public imageBox As System.Windows.Forms.PictureBox
	End Class
End Namespace

