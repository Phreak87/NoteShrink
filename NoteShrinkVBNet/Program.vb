Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows.Forms

NotInheritable Class Program
	Private Sub New()
	End Sub
	''' <summary>
	''' Der Haupteinstiegspunkt für die Anwendung.
	''' </summary>
	<STAThread> _
	Friend Shared Sub Main()
		Application.EnableVisualStyles()
		Application.SetCompatibleTextRenderingDefault(False)
		Application.Run(New WindowsFormsApp1.Form1())
	End Sub
End Class
