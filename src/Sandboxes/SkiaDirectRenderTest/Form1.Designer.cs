namespace SkiaDirectRenderTest;

partial class Form1
{
	/// <summary>
	///  Required designer variable.
	/// </summary>
	private System.ComponentModel.IContainer components = null;

	/// <summary>
	///  Clean up any resources being used.
	/// </summary>
	/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing && (components != null))
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	#region Windows Form Designer generated code

	/// <summary>
	///  Required method for Designer support - do not modify
	///  the contents of this method with the code editor.
	/// </summary>
	private void InitializeComponent()
	{
		skglControl1 = new SkiaSharp.Views.Desktop.SKGLControl();
		SuspendLayout();
		// 
		// skglControl1
		// 
		skglControl1.BackColor = Color.Black;
		skglControl1.Dock = DockStyle.Fill;
		skglControl1.Location = new Point(0, 0);
		skglControl1.Margin = new Padding(4, 3, 4, 3);
		skglControl1.Name = "skglControl1";
		skglControl1.Size = new Size(800, 450);
		skglControl1.TabIndex = 0;
		skglControl1.VSync = true;
		skglControl1.PaintSurface += skglControl1_PaintSurface;
		// 
		// Form1
		// 
		AutoScaleDimensions = new SizeF(7F, 15F);
		AutoScaleMode = AutoScaleMode.Font;
		ClientSize = new Size(800, 450);
		Controls.Add(skglControl1);
		Name = "Form1";
		Text = "Form1";
		ResumeLayout(false);
	}

	#endregion

	private SkiaSharp.Views.Desktop.SKGLControl skglControl1;
}
